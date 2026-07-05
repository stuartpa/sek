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

    /// <summary>
    /// A message-based SMB2 protocol model, ported to SEK. Requests carry a message id and are
    /// tracked as outstanding until a matching response arrives. The credit window bounds the
    /// number of outstanding requests: window == 1 is synchronous (responses necessarily match
    /// the single outstanding request, in order); window == 2 is asynchronous (two requests may
    /// be outstanding, so a response can complete them out of order).
    /// </summary>
    public sealed class Smb2Model : ModelProgram
    {
        public static int maxNoOfFiles = 1;

        public bool ShareKnown { get; set; }
        public bool SessionUp { get; set; }
        public int Window { get; set; }
        public bool TreeConnected { get; set; }
        public int OpenFiles { get; set; }
        /// <summary>Outstanding request message ids, in arrival order.</summary>
        public List<int> Pending { get; set; } = new List<int>();

        [Rule("AssumeShareExists")]
        public void AssumeShareExists(int shareId, ShareType type)
        {
            Require(!ShareKnown, "share already assumed");
            ShareKnown = true;
        }

        [Rule("SetupConnectionAndSession")]
        public void SetupConnectionAndSession(int windowSize)
        {
            Require(ShareKnown, "no share");
            Require(!SessionUp, "session already up");
            Require(windowSize >= 1 && windowSize <= 2, "window in 1..2");
            SessionUp = true;
            Window = windowSize;
        }

        [Rule("TreeConnectRequest")]
        public void TreeConnectRequest(int msgId)
        {
            Require(SessionUp && !TreeConnected, "cannot connect tree now");
            Require(Pending.Count < Window && !Pending.Contains(msgId), "window full or id reused");
            Pending.Add(msgId);
        }

        [Rule("TreeConnectResponse")]
        public void TreeConnectResponse(int msgId)
        {
            Require(Pending.Contains(msgId), "no matching outstanding request");
            Pending.Remove(msgId);
            TreeConnected = true;
        }

        [Rule("CreateRequest")]
        public void CreateRequest(int msgId, CreateType type)
        {
            Require(TreeConnected && OpenFiles < maxNoOfFiles, "cannot create now");
            Require(Pending.Count < Window && !Pending.Contains(msgId), "window full or id reused");
            Pending.Add(msgId);
        }

        [Rule("CreateResponse")]
        public void CreateResponse(int msgId)
        {
            Require(Pending.Contains(msgId), "no matching outstanding request");
            Pending.Remove(msgId);
            OpenFiles++;
        }

        [Rule("CloseRequest")]
        public void CloseRequest(int msgId)
        {
            Require(TreeConnected && OpenFiles > 0, "nothing to close");
            Require(Pending.Count < Window && !Pending.Contains(msgId), "window full or id reused");
            Pending.Add(msgId);
        }

        [Rule("CloseResponse")]
        public void CloseResponse(int msgId)
        {
            Require(Pending.Contains(msgId), "no matching outstanding request");
            Pending.Remove(msgId);
            OpenFiles--;
        }

        [Rule("ErrorResponse")]
        public void ErrorResponse(int msgId)
        {
            Require(Pending.Contains(msgId), "no matching outstanding request");
            Pending.Remove(msgId);
        }

        /// <summary>Accepting when nothing is outstanding (a quiescent protocol point).</summary>
        [AcceptingCondition]
        public bool Quiescent() => Pending.Count == 0;
    }
}
