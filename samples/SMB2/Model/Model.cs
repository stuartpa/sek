using System.Collections.Generic;
using System.Linq;
using Sek.Modeling;

namespace SMB2.Model
{
    /// <summary>The kind of share a tree connects to.</summary>
    public enum ShareType
    {
        DISK,
        PIPE,
        PRINT,
    }

    /// <summary>The disposition of a create request.</summary>
    public enum CreateType
    {
        Create,
        Open,
        OpenIf,
    }

    /// <summary>Model-level parameters, settable via a Cord state slice
    /// (<c>{. Parameters.maxNoOfFiles = 2; .}:</c>).</summary>
    public static class Parameters
    {
        public static int maxNoOfFiles = 1;
    }

    /// <summary>
    /// A message-based SMB2 protocol model, ported to SEK. Requests carry a message id and are
    /// tracked as outstanding until a matching response arrives; the credit window bounds how
    /// many requests may be outstanding at once (window == 1 is synchronous — responses match
    /// the single outstanding request, in order; window == 2 is asynchronous — two requests may
    /// be outstanding, so a response can complete them out of order). The additional message
    /// fields (credit, tree/file handles, payload sizes) are present to match the wire signatures
    /// and are bounded to a single value so they do not expand the state space.
    /// </summary>
    public sealed class Smb2Model : ModelProgram
    {
        public bool ShareKnown { get; set; }
        public bool SessionUp { get; set; }
        public int Window { get; set; }
        public bool TreeConnected { get; set; }
        public int OpenFiles { get; set; }
        /// <summary>Outstanding request message ids, in arrival order.</summary>
        public List<int> Pending { get; set; } = new List<int>();

        private int[] MsgIds() => new[] { 1, 2 };
        private int[] Zero() => new[] { 0 };
        private int[] Windows() => new[] { 1, 2 };

        private bool IsOutstanding(int msgId) => Pending.Contains(msgId);
        private void Track(int msgId) => Pending.Add(msgId);
        private void Complete(int msgId) => Pending.Remove(msgId);

        // ---- Setup adapter ----

        [Rule("AssumeShareExists")]
        public void AssumeShareExists([Domain("Zero")] int shareId, ShareType type)
        {
            Require(!ShareKnown, "share already assumed");
            ShareKnown = true;
        }

        [Rule("SetupConnectionAndSession")]
        public void SetupConnectionAndSession([Domain("Windows")] int windowSize)
        {
            Require(ShareKnown && !SessionUp, "cannot set up session now");
            SessionUp = true;
            Window = windowSize;
        }

        // ---- Tree connect ----

        [Rule("TreeConnectRequest")]
        public void TreeConnectRequest([Domain("MsgIds")] int msgId, [Domain("Zero")] int credit, [Domain("Zero")] int shareId)
        {
            Require(SessionUp && !TreeConnected, "cannot connect tree now");
            Require(Pending.Count < Window && !IsOutstanding(msgId), "window full or id reused");
            Track(msgId);
        }

        [Rule("TreeConnectResponse")]
        public void TreeConnectResponse([Domain("MsgIds")] int msgId, [Domain("Zero")] int credit, [Domain("Zero")] int treeId, [Domain("Zero")] int status)
        {
            Require(IsOutstanding(msgId), "no matching outstanding request");
            Complete(msgId);
            TreeConnected = true;
        }

        // ---- Create ----

        [Rule("CreateRequest")]
        public void CreateRequest([Domain("MsgIds")] int msgId, [Domain("Zero")] int credit, [Domain("Zero")] int treeId, CreateType disposition, [Domain("Zero")] int flags)
        {
            Require(TreeConnected && OpenFiles < Parameters.maxNoOfFiles, "cannot create now");
            Require(Pending.Count < Window && !IsOutstanding(msgId), "window full or id reused");
            Track(msgId);
        }

        [Rule("CreateResponse")]
        public void CreateResponse([Domain("MsgIds")] int msgId, [Domain("Zero")] int credit, [Domain("Zero")] int fileId)
        {
            Require(IsOutstanding(msgId), "no matching outstanding request");
            Complete(msgId);
            OpenFiles++;
        }

        // ---- Read ----

        [Rule("ReadRequest")]
        public void ReadRequest([Domain("MsgIds")] int msgId, [Domain("Zero")] int credit, [Domain("Zero")] int treeId, [Domain("Zero")] int fileId)
        {
            Require(TreeConnected && OpenFiles > 0, "nothing to read");
            Require(Pending.Count < Window && !IsOutstanding(msgId), "window full or id reused");
            Track(msgId);
        }

        [Rule("ReadResponse")]
        public void ReadResponse([Domain("MsgIds")] int msgId, [Domain("Zero")] int credit, [Domain("Zero")] int length)
        {
            Require(IsOutstanding(msgId), "no matching outstanding request");
            Complete(msgId);
        }

        // ---- Write ----

        [Rule("WriteRequest")]
        public void WriteRequest([Domain("MsgIds")] int msgId, [Domain("Zero")] int credit, [Domain("Zero")] int treeId, [Domain("Zero")] int fileId, [Domain("Zero")] int length)
        {
            Require(TreeConnected && OpenFiles > 0, "nothing to write");
            Require(Pending.Count < Window && !IsOutstanding(msgId), "window full or id reused");
            Track(msgId);
        }

        [Rule("WriteResponse")]
        public void WriteResponse([Domain("MsgIds")] int msgId, [Domain("Zero")] int credit)
        {
            Require(IsOutstanding(msgId), "no matching outstanding request");
            Complete(msgId);
        }

        // ---- Close ----

        [Rule("CloseRequest")]
        public void CloseRequest([Domain("MsgIds")] int msgId, [Domain("Zero")] int credit, [Domain("Zero")] int treeId, [Domain("Zero")] int fileId)
        {
            Require(TreeConnected && OpenFiles > 0, "nothing to close");
            Require(Pending.Count < Window && !IsOutstanding(msgId), "window full or id reused");
            Track(msgId);
        }

        [Rule("CloseResponse")]
        public void CloseResponse([Domain("MsgIds")] int msgId, [Domain("Zero")] int credit)
        {
            Require(IsOutstanding(msgId), "no matching outstanding request");
            Complete(msgId);
            OpenFiles--;
        }

        // ---- Error ----

        [Rule("ErrorResponse")]
        public void ErrorResponse([Domain("Zero")] int status, [Domain("MsgIds")] int msgId, [Domain("Zero")] int credit)
        {
            Require(IsOutstanding(msgId), "no matching outstanding request");
            Complete(msgId);
        }

        /// <summary>Accepting when nothing is outstanding (a quiescent protocol point).</summary>
        [AcceptingCondition]
        public bool Quiescent() => Pending.Count == 0;
    }
}
