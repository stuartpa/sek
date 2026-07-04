using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Microsoft.Modeling;
using Chat.Adapter;

namespace Chat.Model
{
    /// <summary>
    /// A model of the MS-CHAT sample.
    /// </summary>
    public static class Model
    {

        #region State
               
        /// <summary>
        /// State of the user.
        /// </summary>
        enum UserState
        {
            WaitingForLogon,
            LoggedOn,
            WaitingForList,
            WatingForLogoff,
        }

        /// <summary>
        /// A class representing a user
        /// </summary>
        partial class User
        {
            /// <summary>
            /// The state in which user currently is.
            /// </summary>
            internal UserState state;

            /// <summary>
            /// The broadcast messages which are waiting for delivery to this user.
            /// This is a map indexed by the user which broadcasted the message,
            /// mapping into a sequence of broadcast messages from this same user.
            /// </summary>
            internal MapContainer<int, Sequence<string>> waitingForDelivery = new MapContainer<int,Sequence<string>>();

        }              
        
        /// <summary>
        /// A mapping from loggedon users to their associated data.
        /// </summary>
        static MapContainer<int, User> users = new MapContainer<int,User>();
      
             


        /// <summary>
        /// Accepting state condition: no requests must be in flight.
        /// </summary>
        [AcceptingStateCondition]
        public static bool IsAccepting
        {
            get
            {
                foreach (User user in users.Values)
                {
                    if (user.state != UserState.LoggedOn ||
                        user.waitingForDelivery.Count > 0)
                        return false;
                }
                return true;
            }
        }

        static User GetLoggedOnUser(int userId)
        {
            Requires(users.ContainsKey(userId));
            User user = users[userId];
            Requires(user.state == UserState.LoggedOn);
            return user;
        }

        static bool EveryoneReceived(int senderId, string message)
        {
            return !users.Exists(u =>
              u.Value.state == UserState.LoggedOn &&
              u.Value.HasPendingDeliveryFrom(senderId, message));
        }


        #endregion

        #region User Helper Methods

        partial class User
        {
            internal bool HasPendingDeliveryFrom(int senderId)
            { return waitingForDelivery.ContainsKey(senderId); }

            internal bool HasPendingDeliveryFrom(int senderId, string message)
            {
                return HasPendingDeliveryFrom(senderId) &&
                          waitingForDelivery[senderId].Contains(message);
            }

            internal string FirstPendingMessageFrom(int senderId)
            { return waitingForDelivery[senderId][0]; }

            internal void AddLastMessageFrom(int senderId, string message)
            {
                if (!HasPendingDeliveryFrom(senderId))
                    waitingForDelivery[senderId] = new Sequence<string>();
                waitingForDelivery[senderId] = waitingForDelivery[senderId].Add(message);
            }

            internal void ConsumeFirstMessageFrom(int senderId)
            {
                if (waitingForDelivery[senderId].Count == 1)
                    waitingForDelivery.Remove(senderId);
                else
                    waitingForDelivery[senderId] = waitingForDelivery[senderId].RemoveAt(0);
            }
        }              

	    #endregion
        
        #region Rule Methods

        #region Logon

        [Rule]
        static void LogonRequest(int userId)
        {
            Requires(!users.ContainsKey(userId));
            User user = new User();
            user.state = UserState.WaitingForLogon;
            user.waitingForDelivery = new MapContainer<int, Sequence<string>>();
            users[userId] = user;
        }

        [Rule]
        static void LogonResponse(int userId)
        {
            Requires(users.ContainsKey(userId));
            User user = users[userId];
            Requires(user.state == UserState.WaitingForLogon,
                1, "User MUST receive response for logon request");
            user.state = UserState.LoggedOn;
        }
    
        #endregion

        #region Logoff

        [Rule]
        static void LogoffRequest(int userId)
        {
            User user = GetLoggedOnUser(userId);
            user.state = UserState.WatingForLogoff;
        }

        [Rule]
        static void LogoffResponse(int userId)
        {
            Requires(users.ContainsKey(userId));
            User user = users[userId];
            Requires(user.state == UserState.WatingForLogoff,
                2, "User MUST receive response for logoff request");
            users.Remove(userId);
        }

       

        #endregion

        #region List

        [Rule]
        static void ListRequest(int listerId)
        {
            User lister = GetLoggedOnUser(listerId);
            lister.state = UserState.WaitingForList;
        }

        [Rule]
        static void ListResponse(int listerId, Set<int> userData)
        {
            Requires(users.ContainsKey(listerId));
            User user = users[listerId];
            Requires(user.state == UserState.WaitingForList,
                 3, "User MUST receive response for list request");
            Requires(userData == new Set<int>(users.Keys),
                 4, "List response MUST contain the list of logged-on users if successful");
            user.state = UserState.LoggedOn;
        }

        #endregion

        #region Broadcast


        [Rule]
        static void BroadcastRequest(int senderId, string message)
        {
            GetLoggedOnUser(senderId);
            foreach (User receiver in users.Values)
                receiver.AddLastMessageFrom(senderId, message);
        }    



        [Rule]
        static void BroadcastAck(int receiverId, int senderId, string message)
        {
            User receiver = GetLoggedOnUser(receiverId);
            Requires(receiver.HasPendingDeliveryFrom(senderId));
            Requires(receiver.FirstPendingMessageFrom(senderId) == message);
            Capture(6, "Messages from one sender MUST be received in order ");
            receiver.ConsumeFirstMessageFrom(senderId);
            if (EveryoneReceived(senderId, message))
                Capture(5, "All logged-on users MUST receive broadcasted message");
        }

        #endregion

        #endregion

        #region Helpers

        /// <summary>
        /// Construct a requirement Id.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        static string MakeRequirementId(int id, string description)
        {
            return "ms-chat_R" + id;
        }

        /// <summary>
        /// Asserts a  requirement.
        /// </summary>
        /// <param name="condition"></param>
        static void Requires(bool condition)
        {
            Condition.IsTrue(condition);
        }

        /// <summary>
        /// Asserts a requirement with associated requirement description.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="id"></param>
        /// <param name="description"></param>
        static void Requires(bool condition, int id, string description)
        {
            Condition.IsTrue(condition, MakeRequirementId(id, description));
        }

        static void Capture(int id, string description)
        {
            Requirement.Capture(MakeRequirementId(id,description));
        }

        #endregion


    }
}
