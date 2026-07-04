using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Modeling;
using System.Net.Sockets;
using SMB2.SSPI;

namespace SMB2.Adapter
{
    public delegate void TreeConnectResponseHandler(int relativeMessageId,
    int creditResponse, int treeId, ShareType shareType);
    public delegate void CreateResponseHandler(int relativeMessageId, int creditResponse, int fileId);
    public delegate void ReadResponseHandler(int relativeMessageId, int creditResponse, Sequence<byte> data);
    public delegate void WriteResponseHandler(int relativeMessageId, int creditResponse);
    public delegate void CloseResponseHandler(int relativeMessageId, int creditResponse);
    public delegate void ErrorResponseHandler(Command_Values command, int relativeMessageId, int creditResponse);


    /// <summary>
    /// A share type.
    /// </summary>
    public enum ShareType
    {
        DISK,
        PIPE,
        PRINTER
    }

    /// <summary>
    /// A (simplified) version of creation types.
    /// </summary>
    public enum CreateType
    {
        Create,
        Open
    }


    public static class Smb2SetupAdapter
    {
        #region Smb2SetupAdapter Members

        public static void AssumeShareExists(int shareId, ShareType type)
        {
            string shareName = Smb2Adapter.GetShareName(shareId);
            Smb2Adapter.BindShare(shareId, shareName);

            // check existance of share
            if (!Directory.Exists(shareName))
                Assert.Inconclusive("share '{0}' does exist", shareName);

            // check whether there is an existing directory named "smb2test_writetest".
            // if there is a directory named "smb2test_writetest", we cannot create a file with
            // the same name.
            if (Directory.Exists(Path.Combine(shareName, "smb2test_writetest")))
                Assert.Inconclusive("There should be no directory named '{0}' in '{1}'", "smb2test_writetest", shareName);

            // delete old files
            try
            {
                foreach (string file in Directory.GetFiles(shareName, "smb2test*"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception e)
                    {
                        Assert.Inconclusive("delete old test file '{0}': {1}", file, e.Message);
                    }
                }
            }
            catch (IOException e)
            {
                Assert.Inconclusive("delete old test files at share '{0}': {1}", shareName, e.Message);
            }

            // check whether we can write to the share
            try
            {
                string testFile = Path.Combine(shareName, "smb2test_writetest");
                string testText = "Test";
                File.WriteAllText(testFile, testText);
                if (!(File.ReadAllText(testFile) == testText))
                    Assert.Inconclusive("cannot write and read on share '{0}'", shareName);
            }
            catch (IOException e)
            {
                Assert.Inconclusive("cannot write and read on share '{0}': {1}", shareName, e.Message);
            }
        }

        public static void AssumeShareDoesNotExist(int shareId)
        {
            string shareName = Smb2Adapter.GetShareName(shareId);
            if (File.Exists(shareName))
                Assert.Inconclusive("share '{0}' exists", shareName);
            Smb2Adapter.BindShare(shareId, shareName);
        }

        #endregion
    }

    public static class Smb2Adapter
    {
        const uint PendingStatusCode = 0x00000103;
        const uint MoreProcessingRequired = 0xC0000016;

        // see http://msdn2.microsoft.com/en-us/library/ms973911.aspx for usage
        // of the managed SSPI. 
        static ClientCredential securityCredential;
        static ClientContext securityContext;

        static int maxCreditsToGrant;
        static long internalMessageId;
        static long internalSessionId;
        static int treeIdCounter;
        static int fileIdCounter;
        static SetContainer<int> availableTreeIds;
        static SetContainer<int> availableFileIds;
        static SortedList<int, long> messageIdBinding;
        static Dictionary<int, string> shareBinding;
        static Dictionary<int, uint> treeBinding;
        static Dictionary<int, FILEID> fileBinding;
        static Dictionary<long, int> messageIdToClosedFile;
        static AutoResetEvent waitHandle;
        static int internalCredits;

        //The target machine, share names and tcp port
        static string hostName = "localhost";
        static string[] shareNameList = new string[] { "\\\\" + hostName + "\\smb2test" };
        static int portNumber = 445;
        static TcpClient client;
        static NetworkStream ns;

        static Thread receiveThread;
        static bool isReceiveThreadOn;



        static Smb2Adapter()
        {
            client = new TcpClient(hostName, portNumber);
            ns = client.GetStream();
            waitHandle = new AutoResetEvent(false);

            messageIdBinding = new SortedList<int, long>();
            shareBinding = new Dictionary<int, string>();
            treeBinding = new Dictionary<int, uint>();
            fileBinding = new Dictionary<int, FILEID>();
            availableFileIds = new SetContainer<int>();
            availableTreeIds = new SetContainer<int>();
            messageIdToClosedFile = new Dictionary<long, int>();
            securityCredential = null;
            securityContext = null;

            isReceiveThreadOn = true;
            receiveThread = new Thread(new ThreadStart(ThreadProc));
            receiveThread.Start();
        }


        public static void Reset()
        {
            isReceiveThreadOn = false;
            Thread.Sleep(1000);
            client.Close();
            messageIdBinding.Clear();
            shareBinding.Clear();
            treeBinding.Clear();
            fileBinding.Clear();
            availableFileIds.Clear();
            availableTreeIds.Clear();
            messageIdToClosedFile.Clear();

            internalMessageId = 0;
            fileIdCounter = 0;
            treeIdCounter = 0;
            internalCredits = 0;

            waitHandle.Reset();
            client = new TcpClient(hostName, portNumber);
            ns = client.GetStream();
            
            securityCredential = null;
            securityContext = null;
            isReceiveThreadOn = true;
            receiveThread = new Thread(new ThreadStart(ThreadProc));
            receiveThread.Start();
            
        }

        public static void BindShare(int shareId, string name)
        {
            shareBinding[shareId] = name;
        }

        public static string GetShareName(int shareId)
        {
            return shareNameList[shareId - 1];
        }


        #region Requests


        public static void SetupConnectionAndSession(int maxCredits)
        {
            maxCreditsToGrant = maxCredits;

            //Negotiate
            Packet_Header header = Smb2MessageUtils.CreateSyncHeader(Command_Values.NEGOTIATE, 0, 1, 
                                        internalMessageId++);
            NEGOTIATE_Request negotiateRequest = Smb2MessageUtils.CreateNegotiateRequest();
            SendSMB2Packet(header, negotiateRequest);

            Command_Values command;
            ISMB2Response response;
            if (!ReceiveSMB2Packet(out command, out header, out response))
            {
                Assert.Fail("cannot setup connection");
            }
           
            //Session Setup
            bool sessionNotSetup = true;
            securityCredential = new ClientCredential(Credential.Package.Negotiate);
            securityContext = new ClientContext(securityCredential, "cifs/" + hostName, 
                                                ClientContext.ContextAttribute.None);
            header = Smb2MessageUtils.CreateSyncHeader(Command_Values.SESSION_SETUP, 0, 1, internalMessageId++);
            while (sessionNotSetup)
            {
                SESSION_SETUP_Request sessionSetupRequest = Smb2MessageUtils.CreateSessionSetupRequest();
                sessionSetupRequest.Buffer = securityContext.Token;
                sessionSetupRequest.SecurityBufferLength = (ushort)sessionSetupRequest.Buffer.Length;
                SendSMB2Packet(header, sessionSetupRequest);

                if (!ReceiveSMB2Packet(out command, out header, out response))
                {
                    Assert.Fail("cannot setup session");
                }
                
                if (header.Status != 0)
                {
                    Assert.IsTrue((uint)header.Status == MoreProcessingRequired,
                            "none-success code must be MOREPROCESSINGREQUIRED");
                    
                    // continue autentication
                    header = Smb2MessageUtils.CreateSyncHeader(Command_Values.SESSION_SETUP,
                                    (long)header.SessionId, 1, internalMessageId++);
                    header.ProcessId = 0xfeff;
                    securityContext.Initialize(((SESSION_SETUP_Response)response).Buffer);
                }
                else
                {
                    internalSessionId = (long)header.SessionId;
                    sessionNotSetup = false;
                }
            }
            waitHandle.Set();
        }

        public static long NextMessageId(int relativeId)
        {
            long id = internalMessageId++;
            messageIdBinding.Remove(relativeId);
            messageIdBinding.Add(relativeId, id);
            return id;
        }


        public static void TreeConnectRequest(int relativeMessageId, int requestedCredits, int shareId)
        {
            Packet_Header header = Smb2MessageUtils.CreateSyncHeader(
                Command_Values.TREE_CONNECT, internalSessionId, requestedCredits, 
                NextMessageId(relativeMessageId));
            TREE_CONNECT_Request load = Smb2MessageUtils.CreateTreeConnectRequest();
            string shareName = shareBinding[shareId];
            load.Buffer = Encoding.Unicode.GetBytes(shareName);
            load.PathLength = (ushort)load.Buffer.Length;

            SendSMB2Packet(header, load);
        }

        public static void CreateRequest(int relativeMessageId, int requestedCredits, 
                    int treeId, CreateType disposition, string fileName)
        {
            Packet_Header header = Smb2MessageUtils.CreateSyncHeader(
                Command_Values.CREATE, internalSessionId, requestedCredits, 
                NextMessageId(relativeMessageId));
            header.TreeId = treeBinding[treeId];
            CREATE_Request load = Smb2MessageUtils.CreateCreateRequest();
            if (disposition == CreateType.Open)
                load.CreateDisposition = CreateDisposition_Values.FILE_OPEN;
            else
                load.CreateDisposition = CreateDisposition_Values.FILE_OPEN_IF;
            load.Buffer = Encoding.Unicode.GetBytes(fileName);
            load.NameLength = (ushort)load.Buffer.Length;
            SendSMB2Packet(header, load);
        }


        public static void ReadRequest(int relativeMessageId, int requestedCredits, 
                    int treeId, int fileId)
        {
            Packet_Header header = Smb2MessageUtils.CreateSyncHeader(
                Command_Values.READ, internalSessionId, requestedCredits, 
                NextMessageId(relativeMessageId));
            header.TreeId = treeBinding[treeId];
            READ_Request load = Smb2MessageUtils.CreateReadRequest();
            load.FileId = fileBinding[fileId];
            SendSMB2Packet(header, load);
        }


        public static void WriteRequest(int relativeMessageId, int requestedCredits, 
                int treeId, int fileId, Sequence<byte> data)
        {
            Packet_Header header = Smb2MessageUtils.CreateSyncHeader(
                Command_Values.WRITE, internalSessionId, requestedCredits, 
                NextMessageId(relativeMessageId));
            header.TreeId = treeBinding[treeId];
            WRITE_Request load = Smb2MessageUtils.CreateWriteRequest();
            load.FileId = fileBinding[fileId];
            load.Buffer = data.ToArray();
            load.Length = (uint)load.Buffer.Length;
            SendSMB2Packet(header, load);
        }


        public static void CloseRequest(int relativeMessageId, int requestedCredits, 
                int treeId, int fileId)
        {
            Packet_Header header = Smb2MessageUtils.CreateSyncHeader(
                Command_Values.CLOSE, internalSessionId, requestedCredits, 
                NextMessageId(relativeMessageId));
            header.TreeId = treeBinding[treeId];
            CLOSE_Request load = Smb2MessageUtils.CreateCloseRequest();
            load.FileId = fileBinding[fileId];
            messageIdToClosedFile[(long)header.MessageId] = fileId;

            SendSMB2Packet(header, load);
        }    


        #endregion

        #region Responses

        public static event TreeConnectResponseHandler TreeConnectResponse;
        public static event CreateResponseHandler CreateResponse;
        public static event CloseResponseHandler CloseResponse;
        public static event WriteResponseHandler WriteResponse;
        public static event ReadResponseHandler ReadResponse;
        public static event ErrorResponseHandler ErrorResponse;

        public static void ThreadProc()
        {
            waitHandle.WaitOne();

            while (isReceiveThreadOn)
            {
                while (ns.DataAvailable)
                {
                    Packet_Header header;
                    Command_Values command;
                    ISMB2Response response;

                    if (!ReceiveSMB2Packet(out command, out header, out response))
                    {
                        if (ErrorResponse != null)
                        {
                            if (header.Status != PendingStatusCode)
                                ErrorResponse((Command_Values)header.Command,
                                               GetRelativeMessageId((long)header.MessageId),
                                               PruneGrant((int)header.Credit));
                            else
                                // swallows PENDING error messages, but we have to remember
                                // the credits 
                                internalCredits += (int)header.Credit;
                        }
                        continue;
                    }
                    
                    switch (command)
                    {
                        case Command_Values.TREE_CONNECT:
                            {
                                TREE_CONNECT_Response treeConnectResponse = (TREE_CONNECT_Response)response;
                                int logicalTreeId = AllocateId(ref treeIdCounter, availableTreeIds);
                                treeBinding[logicalTreeId] = header.TreeId;
                                if (TreeConnectResponse != null)
                                    TreeConnectResponse(GetRelativeMessageId((long)header.MessageId),
                                                        PruneGrant((int)header.Credit),
                                                        logicalTreeId,
                                                        MakeShareType(treeConnectResponse.ShareType));
                            }
                            break;
                        case Command_Values.CREATE:
                            {
                                CREATE_Response createResponse = (CREATE_Response)response;
                                int logicalFileId = AllocateId(ref fileIdCounter, availableFileIds);
                                fileBinding[logicalFileId] = createResponse.FileId;
                                if (CreateResponse != null)
                                    CreateResponse(GetRelativeMessageId((long)header.MessageId),
                                                   PruneGrant((int)header.Credit),
                                                   logicalFileId);
                            }
                            break;
                        case Command_Values.READ:
                            {
                                READ_Response readResponse = (READ_Response)response;
                                if (ReadResponse != null)
                                {
                                    ReadResponse(GetRelativeMessageId((long)header.MessageId),
                                                   PruneGrant((int)header.Credit),
                                                   new Sequence<byte>(readResponse.Buffer));
                                }
                            }
                            break;
                        case Command_Values.WRITE:
                            {
                                WRITE_Response writeResponse = (WRITE_Response)response;
                                if (WriteResponse != null)
                                {
                                    WriteResponse(GetRelativeMessageId((long)header.MessageId),
                                                   PruneGrant((int)header.Credit));
                                }
                            }
                            break;
                        case Command_Values.CLOSE:
                            {
                                CLOSE_Response closeResponse = (CLOSE_Response)response;
                                int fileId;
                                if (messageIdToClosedFile.TryGetValue((long)header.MessageId, out fileId))
                                {
                                    availableFileIds.Add(fileId); // reclaim file identifier
                                }
                                else
                                {
                                    Assert.Inconclusive("inconsistent internal state of adapter");
                                }

                                if (CloseResponse != null)
                                {
                                    CloseResponse(GetRelativeMessageId((long)header.MessageId),
                                                  PruneGrant((int)header.Credit));
                                }
                            }
                            break;
                        default:
                            Assert.Fail("unexpected command {0}", command);
                            break;
                    }
                }
            }
        }

        static int AllocateId(ref int counter, SetContainer<int> available)
        {
            if (available.Count == 0)
                return ++counter;
            else
            {
                int min = Int32.MaxValue;
                foreach (int v in available)
                {
                    if (v < min) min = v;
                }
                available.Remove(min);
                return min;
            }
        }

        static int GetRelativeMessageId(long msgId)
        {
            int idx = messageIdBinding.IndexOfValue(msgId);
            return messageIdBinding.Keys[idx];
        }

        static int PruneGrant(int grant)
        {
            // add credits of response which we skipped
            grant += internalCredits;
            internalCredits = 0;
            // prune down to our maximal value
            return grant < maxCreditsToGrant ? grant : maxCreditsToGrant;
        }


        static ShareType MakeShareType(ShareType_Values value)
        {
            switch (value)
            {
                case ShareType_Values.SHARE_TYPE_DISK:
                    return ShareType.DISK;
                case ShareType_Values.SHARE_TYPE_PIPE:
                    return ShareType.PIPE;
                case ShareType_Values.SHARE_TYPE_PRINT:
                    return ShareType.PRINTER;
                default:
                    Assert.Fail("unexpected share type: {0}", value);
                    throw new InvalidOperationException();
            }
        }

        #endregion

        #region Helpers

        static void SendSMB2Packet(Packet_Header header, ISMB2Request load)
        {
            int headerSize = header.GetSize();
            int loadSize = load.GetSize();
            byte[] buf = new byte[headerSize + loadSize + 4];
            byte[] packetSize = Smb2MessageUtils.CreatePacketSize(headerSize + loadSize);
            packetSize.CopyTo(buf, 0);
            header.ToBytes().CopyTo(buf, 4);
            load.ToBytes().CopyTo(buf, 4 + headerSize);
            ns.Write(buf, 0, buf.Length);
        }

        static bool ReceiveSMB2Packet(out Command_Values command, out Packet_Header header, out ISMB2Response load)
        {
            byte[] byteArray = new byte[4];
            byte[] packetSize;
            int size;

            //Read the total packet size
            ns.Read(byteArray, 0, 4);
            packetSize = new byte[4] { byteArray[3], byteArray[2], byteArray[1], byteArray[0] };
            size = BitConverter.ToInt32(packetSize, 0);

            //Read SMB2 packet
            byte[] packetBuf = new byte[size];
            ns.Read(packetBuf, 0, size);
            
            //Parse SMB2 packet
            header = new Packet_Header();
            header.ToSMB2Packet(packetBuf, 0);
            command = header.Command;

            Assert.IsTrue((header.Flags & Packet_Header_Flags_Values.FLAGS_SERVER_TO_REDIR) != 0,
               "server must set FLAGS_SERVER_TO_REDIR in response");

            //Error Response
            if (header.Status != 0 && (uint)header.Status != MoreProcessingRequired)
            {
                load = null;
                Console.WriteLine("receiving error response code {0:x} ({1})",
                             header.Status, StatusToString((uint)header.Status));
                return false;
            }

            switch (command)
            {
                case Command_Values.NEGOTIATE:
                    {
                        load = new NEGOTIATE_Response();
                        load.ToSMB2Packet(packetBuf, header.GetSize());
                    }
                    break;
                case Command_Values.SESSION_SETUP:
                    {
                        load = new SESSION_SETUP_Response();
                        load.ToSMB2Packet(packetBuf, header.GetSize());
                    }
                    break;
                case Command_Values.TREE_CONNECT:
                    {
                        load = new TREE_CONNECT_Response();
                        load.ToSMB2Packet(packetBuf, header.GetSize());
                    }
                    break;
                case Command_Values.CREATE:
                    {
                        load = new CREATE_Response();
                        load.ToSMB2Packet(packetBuf, header.GetSize());
                    }
                    break;
                case Command_Values.CLOSE:
                    {
                        load = new CLOSE_Response();
                        load.ToSMB2Packet(packetBuf, header.GetSize());
                    }
                    break;
                case Command_Values.READ:
                    {
                        load = new READ_Response();
                        load.ToSMB2Packet(packetBuf, header.GetSize());
                    }
                    break;
                case Command_Values.WRITE:
                    {
                        load = new WRITE_Response();
                        load.ToSMB2Packet(packetBuf, header.GetSize());
                    }
                    break;
                default:
                    load = null;
                    return false;
            }
            return true;
            
            
        }

        static string StatusToString(uint status)
        {
            // This is not complete and potentially not accurate.
            // The NTSTATUS enumeration should be part of TestTools framework.
            switch (status)
            {
                case 0x00000000:
                    {
                        return "SUCCESS";
                    }
                case 0x00000103:
                    {
                        return "PENDING";
                    }
                case 0xC0000120:
                    {
                        return "CANCELLED";
                    }
                case 0xC00000F9:
                case 0xC00000FA:
                case 0xC000000D:
                    {
                       return "INVALID_PARAMETER";
                    }
                case 0xC00000BB:
                    {
                        return "NOT_SUPPORTED";
                    }
                case 0xC0000203:
                    {
                        return "USER_SESSION_DELETED";
                    }
                case 0xC00000D0:
                    {
                        return "REQUEST_NOT_ACCEPTED";
                    }
                case 0xC0000016:
                    {
                        return "MORE_PROCESSING_REQUIRED";
                    }
                case 0xC000035C:
                    {
                        return "NETWORK_SESSION_EXPIRED";
                    }
                case 0xC0000022:
                    {
                        return "ACCESS_DENIED";
                    }
                case 0xC0000023:
                    {
                        return "BUFFER_TOO_SMALL";
                    }
                case 0x80000005:
                    {
                        return "BUFFER_OVERFLOW";
                    }
                case 0xC00000CC:
                    {
                        return "BAD_NETWORK_NAME";
                    }
                case 0xC00000C9:
                    {
                       return "NETWORK_NAME_DELETED";
                    }
                case 0x8000002D:
                    {
                        return "STOPPED_ON_SYMLINK";
                    }
                case 0xC000007F:
                    {
                        return "DISK_FULL";
                    }
                case 0xC0000034:
                    {
                        return "OBJECT_NAME_NOT_FOUND";
                    }
                default:
                    {
                        return "UNKNOWN_ERROR";
                    }
            }
        }


        #endregion


    }
}
