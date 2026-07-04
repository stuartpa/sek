using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Modeling;
using SMB2.Adapter;

/// This is a SIMPLIFIED version of a model for SMB2 which captures some aspects
/// of the protocol. This model is not intended to solve the SMB2 test suite problem,
/// but only for demonstrating certain concepts of modeling and adaptering.

namespace SMB2.Model
{

    #region Parameters

    public static class Parameters
    {
        /// <summary>
        /// Credit goal the client should try to maintain. This is also the
        /// maximal size of the sequence window.
        /// </summary>
        public static int creditGoal = 1;
          
         /// <summary>
        /// If set, the client will always use the first ID in the window.
        /// Otherwise, it will try any ID (which makes the state space much larger).
        /// </summary>
        public static bool useSequentialIds = true;

        /// <summary>
        /// The maximal number of trees to connect to.
        /// </summary>
        public static int maxNoOfTrees = 1;

        /// <summary>
        /// The maximal number of files to create.
        /// </summary>
        public static int maxNoOfFiles = 1;
        /// <summary>
        ///  Test file names to use.
        /// </summary>
        public static string[] testFileNames = new string[] {"smb2test" };

        /// <summary>
        /// Test data to use.
        /// </summary>
        public static byte[][] testData = new byte[][] { new byte[] { 99, 156, 2 } };

        /// <summary>
        /// If true, injects an error into the model, namely that
        /// the model will expect ReadResponse instead of ErrorResponse when reading 
        /// an empty file. Thus if this flag is true, a conformance error
        /// will be reported when testing against the SMB2 implementation.
        /// </summary>
        public static bool allowReadEmptyFile = false;
              
    }

    #endregion

    #region Request Types

    abstract class Request : CompoundValue
    {
        internal Command_Values command;
        internal int sequenceId;

        internal Request(Command_Values command, int sequenceId)
        {
            this.command = command;
            this.sequenceId = sequenceId;
        }
    }

    class TreeConnectRequest : Request
    {
        internal int shareId;

        internal TreeConnectRequest(int sequenceId, int shareId)
            : base(Command_Values.TREE_CONNECT, sequenceId)
        {
            this.shareId = shareId;
        }
    }

    class CreateRequest : Request
    {
        internal int treeId;
        internal CreateType disposition;
        internal string fileName;

        internal CreateRequest(int sequenceId, int treeId, CreateType disposition, string fileName)
            : base(Command_Values.CREATE, sequenceId)
        {
            this.treeId = treeId;
            this.disposition = disposition;
            this.fileName = fileName;
        }
    }

    class WriteRequest : Request
    {
        internal int treeId;
        internal int fileId;
        internal Sequence<byte> data;

        internal WriteRequest(int sequenceId, int treeId, int fileId, Sequence<byte> data)
            : base(Command_Values.WRITE, sequenceId)
        {
            this.treeId = treeId;
            this.fileId = fileId;
            this.data = data;
        }
    }

    class ReadRequest : Request
    {
        internal int treeId;
        internal int fileId;

        internal ReadRequest(int sequenceId, int treeId, int fileId)
            : base(Command_Values.READ, sequenceId)
        {
            this.treeId = treeId;
            this.fileId = fileId;
        }
    }

    class CloseRequest : Request
    {
        internal int treeId;
        internal int fileId;

        internal CloseRequest(int sequenceId, int treeId, int fileId)
            : base(Command_Values.CLOSE, sequenceId)
        {
            this.treeId = treeId;
            this.fileId = fileId;
        }
    }
    
    #endregion

    #region Share and File Type

    class Share : CompoundValue
    {
        internal ShareType type;
        internal MapContainer<string, Sequence<byte>> files = new MapContainer<string,Sequence<byte>>();

        internal Share(ShareType type)
        {
            this.type = type;
        }
    }

    class File : CompoundValue
    {
        internal Share share;
        internal string fileName;

        internal File(Share share, string fileName)
        {
            this.share = share;
            this.fileName = fileName;
        }
    }

    #endregion


    public static class Model
    {

        #region Model State
        
        /// <summary>
        /// A flag indicating whether a connection has been established.
        /// </summary>
        static bool connected;

        /// <summary>
        /// The pool of relative sequence ids used by the model.
        /// Initially set to {1..Parameters.creditGoal}. Elements 
        /// will be removed and put into the sequence window as credit
        /// is acquired, and put back into the pool when they have been used. 
        /// </summary>
        static SetContainer<int> sequenceIdPool;

        /// <summary>
        /// Relative message IDs that are available to be used by the client. 
        /// In order to finitize the model state space, the model uses relative
        /// id's in the range {1..Parameters.creditGoal}. If a response is received
        /// for such a relative message id, it will be put back to the pool.
        /// If the useSequentialIds parameter flag is set, identifiers will
        /// be allocated sequentially from this window.
        /// </summary>
        static SetContainer<int> sequenceIdWindow;

        /// <summary>
        /// Logical tree ids available.
        /// </summary>
        static SetContainer<int> treeIds = new SetContainer<int>(FromTo(1, Parameters.maxNoOfTrees));

        /// <summary>
        /// Tree ids which have been requested but not yet responded.
        /// </summary>
        static int treeIdsInFlight = 0;

        /// <summary>
        /// Logical file ids available.
        /// </summary>
        static SetContainer<int> fileIds = new SetContainer<int>(FromTo(1, Parameters.maxNoOfFiles));

        /// <summary>
        /// File ids which have been requested but not yet responded.
        /// </summary>
        static int fileIdsInFlight = 0;

        /// <summary>
        /// The shares which are configured during server setup.
        /// </summary>
        static MapContainer<int, Share> shares = new MapContainer<int,Share>();

        /// <summary>
        /// The received requests which do not have a response yet.
        /// </summary>
        static SequenceContainer<Request> inflight = new SequenceContainer<Request>();

        /// <summary>
        /// The open trees.
        /// </summary>
        static MapContainer<int, int> openTrees = new MapContainer<int, int>();

        /// <summary>
        /// The open files.
        /// </summary>
        static MapContainer<int, File> openFiles = new MapContainer<int, File>();

        #endregion      

        #region Helpers

        /// <summary>
        /// Builds a requirement identifier.
        /// </summary>
        static string MakeReqId(int id, string description)
        {
            return "SMB2_R" + id;
        }

        /// <summary>
        /// A short cut for Condition.IsTrue using a requirement id and description. 
        /// Every requires which captures a requirement should use this.
        /// </summary>
        static void Requires(bool cond, int id, string description)
        {
            Condition.IsTrue(cond, MakeReqId(id, description));
        }

        /// <summary>
        /// A short cut for Requirements.Capture using a requirement id and description.
        /// Every explicit capture should use this.
        /// </summary>
        static void Capture(int id, string description)
        {
            Requirement.Capture(MakeReqId(id, description));
        }

        /// <summary>
        /// Returns the minimum of a set of integers, or -1, if the set is empty.
        /// </summary>
        static int Min(SetContainer<int> set)
        {
            int min = -1;
            foreach (int x in set)
            {
                if (min == -1 || x < min)
                    min = x;
            }
            return min;
        }

        /// <summary>
        /// Returns an enumeration of integers from a minimum to a maximum value.
        /// </summary>
        static IEnumerable<int> FromTo(int min, int max)
        {
            while (min <= max)
                yield return min++;
        }

        #endregion

        #region Parameter domains

        /// <summary>
        /// Returns a domain for the potential credit response of the server.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<int> CreditDomain()
        {
           return FromTo(1, Parameters.creditGoal);
        }

        /// <summary>
        /// Returns a domain for the available shares.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<int> ShareDomain()
        {
            return shares.Keys;
        }

        /// <summary>
        /// Returns a domain for the open trees.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<int> OpenTreeDomain()
        {
            return openTrees.Keys;
        }

        /// <summary>
        /// Returns a domain for the open files.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<int> OpenFileDomain()
        {
            return openFiles.Keys;
        }

        /// <summary>
        /// Returns a domain for file names.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> FileNameDomain()
        {
            return Parameters.testFileNames;
        }

      
        /// <summary>
        /// A parameter domain for the data to write.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Sequence<byte>> WriteDataDomain()
        {
            yield return new Sequence<byte>(13,88,11,86,75);
            //
            //foreach (byte[] data in Parameters.testData)
            //{
            //    yield return new Sequence<byte>(data);
            //}
        }

        /// <summary>
        /// A parameter domain for the data which can be read.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Sequence<byte>> ReadDataDomain()
        {
            yield return new Sequence<byte>(); // we can read from the empty file
            foreach (Sequence<byte> data in WriteDataDomain())
                yield return data;
        }


        #endregion

        #region Accepting State

        /// <summary>
        /// The condition when the model is in an accepting state,
        /// which is when no files are open.
        /// </summary>
        [AcceptingStateCondition]
        static bool Accepting
        {
            get
            {
                return openFiles.Count == 0;
            }
        }

        #endregion

        #region General handling of requests and responses

        /// <summary>
        /// Checks any request and binds sequence id.
        /// </summary>
        static void CheckRequest(int sequenceId, int creditRequest)
        {
            Condition.IsTrue(connected);

            Condition.IsTrue(creditRequest == Parameters.creditGoal - sequenceIdWindow.Count + 1);
                                // try to ensure we meet our credit goal

            Requires(sequenceIdWindow.Count > 0, 1, "client should not exceed credit");
            if (Parameters.useSequentialIds)
                Condition.IsTrue(sequenceId == Min(sequenceIdWindow));
            else
                Condition.IsTrue(sequenceIdWindow.Contains(sequenceId));
            sequenceIdWindow.Remove(sequenceId);
            Capture(2, "client should use messages from the window");
        }
       

        /// <summary>
        /// Checks whether a response of given command and sequence id is possible
        /// in the current state and returns it. Als check and process
        /// credit response value.
        /// </summary>
        static Request CheckResponse(Command_Values command, int sequenceId, int creditResponse)
        {
            Requires(sequenceIdWindow.Count + creditResponse > 0,
                        3, "the sequence window must never be empty");
           
            foreach (Request r in inflight)
            {
                if (r.command == command & r.sequenceId == sequenceId)
                {
                    inflight.Remove(r);
                    sequenceIdPool.Add(r.sequenceId); // recylce sequence id
                    while (creditResponse-- > 0 && sequenceIdPool.Count > 0)
                    {
                        // add credits to the window
                        int id = Min(sequenceIdPool);
                        sequenceIdPool.Remove(id);
                        sequenceIdWindow.Add(id);
                    }
                    return r;
                }
            }
            // if we reach here, then the response has no matching request
            Requires(false, 4, "server must response only to matching requests");
            return null; 
        }

       

        #endregion

        #region Configuration and Setup

        /// <summary>
        /// Assumes that a share exists.
        /// </summary>
        [Rule]
        static void AssumeShareExists(int shareId, ShareType shareType)
        {
            Condition.IsTrue(!connected);
            shares[shareId] = new Share(shareType);
        }

        /// <summary>
        /// Assumes that a share does not exists.
        /// </summary>
        [Rule]
        static void AssumeShareDoesNotExist(int shareId)
        {
            Condition.IsTrue(!connected);
            shares[shareId] = null;
        }
       
        /// <summary>
        /// Create a connection and a session.
        /// </summary>
        [Rule]
        static void SetupConnectionAndSession(int maxCredits)
        {
            Condition.IsTrue(!connected);
            connected = true;
            Parameters.creditGoal = maxCredits;

            // initialize state
            sequenceIdWindow = new SetContainer<int>(1); // initial assumption is one credit
            sequenceIdPool = new SetContainer<int>(FromTo(2, Parameters.creditGoal));
            treeIds = new SetContainer<int>(FromTo(1, Parameters.maxNoOfTrees));
            fileIds = new SetContainer<int>(FromTo(1, Parameters.maxNoOfFiles));
        }

        #endregion

        #region Tree Connect
         
        /// <summary>
        /// Describes a tree connect request.
        /// </summary>
        [Rule]
        static void TreeConnectRequest(int sequenceId, int creditRequest, [Domain("ShareDomain")] int shareId)
        {
            Condition.IsTrue(treeIds.Count - treeIdsInFlight > 0);
            CheckRequest(sequenceId, creditRequest);
            inflight.Add(new TreeConnectRequest(sequenceId, shareId));
            treeIdsInFlight++;
        }

        /// <summary>
        /// Describes a tree connect response.
        /// </summary>
        [Rule]
        static void TreeConnectResponse(int sequenceId, [Domain("CreditDomain")]int creditResponse, 
                                        int treeId, ShareType shareType)
        {
            TreeConnectRequest request = 
                (TreeConnectRequest)CheckResponse(Command_Values.TREE_CONNECT, sequenceId, creditResponse);
            Capture(5, "tree connect request must be responded");
            Requires(shares.ContainsKey(request.shareId) && shares[request.shareId] != null,
                               6, "only existing share should have successful connect response");
            Share share = shares[request.shareId];
            Condition.IsTrue(shareType == share.type);
            Condition.IsTrue(treeId == Min(treeIds));
            treeIdsInFlight--;
            treeIds.Remove(treeId);
            openTrees[treeId] = request.shareId;
        }

        /// <summary>
        /// Describes a tree connect error response.
        /// </summary>
        [Rule(Action = "ErrorResponse(Command_Values.TREE_CONNECT, sequenceId, creditResponse)")]
        static void TreeConnectErrorResponse(int sequenceId, [Domain("CreditDomain")]int creditResponse)
        {
            TreeConnectRequest request = 
                (TreeConnectRequest)CheckResponse(Command_Values.TREE_CONNECT, sequenceId, creditResponse);
            Capture(7, "connect to non-existing share must have error response");
            Requires(!shares.ContainsKey(request.shareId) || shares[request.shareId] == null,
                        8, "Non-existing share have error response");
            treeIdsInFlight--;
        }

        #endregion

        #region Create

        /// <summary>
        /// Describes a file creation request.
        /// </summary>
        [Rule]
        static void CreateRequest(int sequenceId, int creditRequest,
                                 [Domain("OpenTreeDomain")]int treeId, 
                                 CreateType disposition, 
                                 [Domain("FileNameDomain")]string fileName)
        {
            Condition.IsTrue(fileIds.Count - fileIdsInFlight > 0);
            CheckRequest(sequenceId, creditRequest);
            inflight.Add(new CreateRequest(sequenceId, treeId, disposition, fileName));
            fileIdsInFlight++;
        }

        /// <summary>
        /// Describes a file creation response.
        /// </summary>
        [Rule]
        static void CreateResponse(int sequenceId, [Domain("CreditDomain")]int creditResponse,
                                   int fileId)
        {
            CreateRequest request =
                (CreateRequest)CheckResponse(Command_Values.CREATE, sequenceId, creditResponse);
            Capture(9, "correct create request must be responded"); 
            Requires(openTrees.ContainsKey(request.treeId),
                        10, "create must only succeed on connected tree");
            Share share = shares[openTrees[request.treeId]];
            Condition.IsTrue(request.disposition != CreateType.Open || share.files.ContainsKey(request.fileName));
            Condition.IsTrue(fileId == Min(fileIds));
            fileIds.Remove(fileId);
            fileIdsInFlight--;
            // Initialize file to empty if its no on the share
            if (!share.files.ContainsKey(request.fileName))
                share.files[request.fileName] = new Sequence<byte>();
            openFiles[fileId] = new File(share, request.fileName);
        }
        

        /// <summary>
        /// Describes a file creation error response.
        /// </summary>
        [Rule(Action = "ErrorResponse(Command_Values.CREATE, sequenceId, creditResponse)")]
        static void CreateErrorResponse(int sequenceId, [Domain("CreditDomain")]int creditResponse)
        {
            CreateRequest request =
                (CreateRequest)CheckResponse(Command_Values.CREATE, sequenceId, creditResponse);
            Capture(11, "invalid create request must be responded with error");
            Share share = shares[openTrees[request.treeId]];
            Condition.IsTrue(request.disposition == CreateType.Open && !share.files.ContainsKey(request.fileName));
            fileIdsInFlight--;
        }

        #endregion

        #region Write

        /// <summary>
        /// Describes a write request on open file.
        /// </summary>
        [Rule]
        static void WriteRequest(int sequenceId, int creditRequest,
                                 [Domain("OpenTreeDomain")]int treeId,
                                 [Domain("OpenFileDomain")]int fileId,
                                 [Domain("WriteDataDomain")]Sequence<byte> data)
        {
            CheckRequest(sequenceId, creditRequest);
            inflight.Add(new WriteRequest(sequenceId, treeId, fileId, data));
        }

        /// <summary>
        /// Describes a write response.
        /// </summary>
        [Rule]
        static void WriteResponse(int sequenceId, [Domain("CreditDomain")]int creditResponse)
        {
            WriteRequest request =
                (WriteRequest)CheckResponse(Command_Values.WRITE, sequenceId, creditResponse);
            Capture(12, "correct write request must be responded");
            Condition.IsTrue(openTrees.ContainsKey(request.treeId));
            Requires(openFiles.ContainsKey(request.fileId),
                         13, "write must only succeed on created file");

            File file = openFiles[request.fileId];
            file.share.files[file.fileName] = request.data;
        }

        /// <summary>
        /// Describes a write error response.
        /// </summary>
        [Rule(Action = "ErrorResponse(Command_Values.WRITE, sequenceId, creditResponse)")]
        static void WriteErrorResponse(int sequenceId, [Domain("CreditDomain")]int creditResponse)
        {
            WriteRequest request =
                (WriteRequest)CheckResponse(Command_Values.WRITE, sequenceId, creditResponse);
            Capture(14, "invalid write request must be responded with error");
            Condition.IsTrue(!openTrees.ContainsKey(request.treeId) ||
                               !openFiles.ContainsKey(request.fileId));
        }


        #endregion

        #region Read

        /// <summary>
        /// Describes read request on open file.
        /// </summary>
        [Rule]
        static void ReadRequest(int sequenceId, int creditRequest,
                                 [Domain("OpenTreeDomain")]int treeId,
                                 [Domain("OpenFileDomain")]int fileId)
        {
            CheckRequest(sequenceId, creditRequest);
            inflight.Add(new ReadRequest(sequenceId, treeId, fileId));
        }

        /// <summary>
        /// Describes a read response.
        /// </summary>
        [Rule]
        static void ReadResponse(int sequenceId, 
                                 [Domain("CreditDomain")]int creditResponse,
                                 [Domain("ReadDataDomain")]Sequence<byte> data)
        {
            ReadRequest request =
                (ReadRequest)CheckResponse(Command_Values.READ, sequenceId, creditResponse);
            Capture(15, "correct read request must be responded");
            Condition.IsTrue(openTrees.ContainsKey(request.treeId));
            Condition.IsTrue(openFiles.ContainsKey(request.fileId));
            File file = openFiles[request.fileId];
            Sequence<byte> fileData = file.share.files[file.fileName];
            Condition.IsTrue(fileData.Count > 0 || Parameters.allowReadEmptyFile);
            Condition.IsTrue(data.Equals(fileData));
        }

        /// <summary>
        /// Describes a read error response.
        /// </summary>
        [Rule(Action = "ErrorResponse(Command_Values.READ, sequenceId, creditResponse)")]
        static void ReadErrorResponse(int sequenceId, [Domain("CreditDomain")]int creditResponse)
        {
            ReadRequest request =
                (ReadRequest)CheckResponse(Command_Values.READ, sequenceId, creditResponse);
            Capture(16, "invalid read request must be responded with error");
            if (openTrees.ContainsKey(request.treeId) && openFiles.ContainsKey(request.fileId))
            {
                File file = openFiles[request.fileId];
                Sequence<byte> fileData = file.share.files[file.fileName];
                Condition.IsTrue(fileData.Count == 0 && !Parameters.allowReadEmptyFile);
            }
            else
                Condition.IsTrue(false);      
        }

        #endregion

        #region Close

        /// <summary>
        /// Describes a close request on open file.
        /// </summary>
        [Rule]
        static void CloseRequest(int sequenceId, int creditRequest,
                                 [Domain("OpenTreeDomain")]int treeId,
                                 [Domain("OpenFileDomain")]int fileId)
        {
            CheckRequest(sequenceId, creditRequest);
            inflight.Add(new CloseRequest(sequenceId, treeId, fileId));
        }

        /// <summary>
        /// Describes a close response.
        /// </summary>
        [Rule]
        static void CloseResponse(int sequenceId, [Domain("CreditDomain")]int creditResponse)
        {
            CloseRequest request =
                (CloseRequest)CheckResponse(Command_Values.CLOSE, sequenceId, creditResponse);
            Capture(17, "correct close request must be responded");
            Condition.IsTrue(openTrees.ContainsKey(request.treeId));
            Condition.IsTrue(openFiles.ContainsKey(request.fileId));
            openFiles.Remove(request.fileId);
            fileIds.Add(request.fileId);
        }

        /// <summary>
        /// Describes a close error response.
        /// </summary>
        [Rule(Action = "ErrorResponse(Command_Values.CLOSE, sequenceId, creditResponse)")]
        static void CloseErrorResponse(int sequenceId, [Domain("CreditDomain")]int creditResponse)
        {
            CloseRequest request =
                (CloseRequest)CheckResponse(Command_Values.CLOSE, sequenceId, creditResponse);
            Capture(18, "invalid close request must be responded with error");
            Condition.IsTrue(!openTrees.ContainsKey(request.treeId) ||
                               !openFiles.ContainsKey(request.fileId));
        }

        #endregion


    }

}
