using System;
using System.Collections.Generic;
using System.Text;

namespace Chat.Adapter
{
    [Serializable]
    public struct User
    {
        public string Id;

        public override string ToString()
        {
            return String.Format("User(Id={0}", Id);
        }
    }


    public enum Command
    {
        LogonRequest,       // -> Message
        LogonResponse,      // <- Message
        LogoffRequest,      // -> Message
        LogoffResponse,     // <- Message
        ListRequest,        // -> Message
        ListResponse,       // <- Message, ListData
        BroadcastRequest,   // -> Message, PublishData
        BroadcastAck,       // <- Message, PublishData
    }

    [Serializable]
    public struct Message
    {
        public Command Command;
        public User User;
        public int ErrorCode;

        public override string ToString()
        {
            return String.Format("Message(Command={0},User={1},ErrorCode={2})", Command, User, ErrorCode);
        }
    }

    [Serializable]
    public struct ListData
    {
        public int UserCount;

        public List<User> UserData;

        public override string ToString()
        {
            return String.Format("ListData(UserCount={0},UserData={1})", UserCount, UserData);
        }
    }

    [Serializable]
    public struct PublishData
    {
        public User User;

        public string Message;

        public override string ToString()
        {
            return String.Format("PublishData(User={0}, Message={1})", User, Message);
        }
    }

}
