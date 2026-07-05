using System.Collections.Generic;
using System.Linq;
using Sek.Modeling;

namespace Chat.Model
{
    public enum UserState
    {
        WaitingForLogon,
        LoggedOn,
        WaitingForLogoff,
    }

    /// <summary>A chat user with a protocol state and an inbox of undelivered broadcasts.</summary>
    public sealed class User
    {
        public int Id { get; set; }
        public UserState State { get; set; }
        public List<string> Inbox { get; set; } = new List<string>();
    }

    /// <summary>
    /// The MS-CHAT protocol model, ported to SEK. Users are created on logon and held in
    /// model state; response/broadcast/logoff rules take a User parameter whose domain is
    /// the reachable users. User ids and broadcast payloads come from Cord (Z3).
    /// </summary>
    public sealed class ChatModel : ModelProgram
    {
        public bool ServerStarted { get; set; }
        public bool ClientConnected { get; set; }
        public List<User> Users { get; set; } = new List<User>();

        [Rule("StartServer")]
        public void StartServer()
        {
            Require(!ServerStarted, "server already started");
            ServerStarted = true;
        }

        [Rule("ConnectToServer")]
        public void ConnectToServer()
        {
            Require(ServerStarted, "server not started");
            Require(!ClientConnected, "already connected");
            ClientConnected = true;
        }

        [Rule("LogonRequest")]
        public void LogonRequest(int userId)
        {
            Require(Users.All(u => u.Id != userId), "user id already in use");
            Users.Add(new User { Id = userId, State = UserState.WaitingForLogon });
        }

        [Rule("LogonResponse")]
        public void LogonResponse(User user)
        {
            Require(user.State == UserState.WaitingForLogon, "not awaiting logon");
            user.State = UserState.LoggedOn;
        }

        /// <summary>Request the list of logged-on users.</summary>
        [Rule("ListRequest")]
        public void ListRequest()
        {
            // Observation request only.
        }

        /// <summary>Returns the ids of the currently logged-on users (the classic sample's
        /// Set&lt;int&gt; list response). Observation only during exploration.</summary>
        [Rule("ListResponse")]
        public IEnumerable<int> ListResponse()
        {
            return Users.Where(u => u.State == UserState.LoggedOn).Select(u => u.Id).ToList();
        }

        [Rule("BroadcastRequest")]
        public void BroadcastRequest(User sender, string message)
        {
            Require(sender.State == UserState.LoggedOn, "sender not logged on");
            foreach (var u in Users.Where(u => u.State == UserState.LoggedOn))
            {
                u.Inbox.Add(message);
            }
        }

        [Rule("BroadcastAck")]
        public void BroadcastAck(User receiver)
        {
            Require(receiver.Inbox.Count > 0, "nothing to acknowledge");
            receiver.Inbox.RemoveAt(0);
        }

        [Rule("LogoffRequest")]
        public void LogoffRequest(User user)
        {
            Require(user.State == UserState.LoggedOn, "not logged on");
            user.State = UserState.WaitingForLogoff;
        }

        [Rule("LogoffResponse")]
        public void LogoffResponse(User user)
        {
            Require(user.State == UserState.WaitingForLogoff, "not awaiting logoff");
            Users.Remove(user);
        }

        /// <summary>Error path for a logoff (an alternative to LogoffResponse).</summary>
        [Rule("ErrorResponse")]
        public void ErrorResponse(User user)
        {
            Require(user.State == UserState.WaitingForLogoff, "not awaiting logoff");
            Users.Remove(user);
        }

        /// <summary>Accepting when no request is in flight: everyone is logged on and no
        /// broadcasts are still pending acknowledgement.</summary>
        [AcceptingCondition]
        public bool Quiescent() =>
            Users.All(u => u.State == UserState.LoggedOn && u.Inbox.Count == 0);
    }
}
