using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Modeling;

namespace Chat.Adapter
{
    public delegate void LogonResponseHandler(int user);
    public delegate void LogoffResponseHandler(int user);
    public delegate void ListResponseHandler(int user, Set<int> userData);
    public delegate void BroadcastAckHandler(int user, int publisher, string message);
    public delegate void ErrorResponseHandler(int user, Command command);
    public delegate void ExceptionResponseHandler(string msg);

    public class ChatAdapter
    {

        static string hostName = "localhost";
        static int portNumber = 1333;
        static TcpClient client;
        static NetworkStream ns;
        static BinaryFormatter formatter = new BinaryFormatter();
        static Thread receiveThread;
        static bool isReceiveThreadOn;


        /// <summary>
        /// Construct chat implementation
        /// </summary>
        static ChatAdapter()
        {
        }

        #region ChatAdapter Members

        public static void ConnectToServer()
        {
            client = new TcpClient(hostName, portNumber);
            ns = client.GetStream();
            isReceiveThreadOn = true;
            receiveThread = new Thread(new ThreadStart(ThreadProc));
            receiveThread.Start();
        }

        public static void LogonRequest(int user)
        {
            Message message = new Message();
            message.Command = Command.LogonRequest;
            User userData;

            // Create  new user data:
            // in a real protocol, we would enumerate
            // existing users from a DC; here we just
            // fake some.
            userData = new User();
            userData.Id = user.ToString();
            message.User = userData;
            message.ErrorCode = 0;
            formatter.Serialize(ns, message);
        }

        public static void LogoffRequest(int user)
        {
            Message message = new Message();
            message.Command = Command.LogoffRequest;
            message.User.Id = user.ToString();
            message.ErrorCode = 0;
            formatter.Serialize(ns, message);
        }

        public static void ListRequest(int user)
        {
            Message message = new Message();
            message.Command = Command.ListRequest;
            message.User.Id = user.ToString();
            message.ErrorCode = 0;
            formatter.Serialize(ns, message);
        }

        public static void BroadcastRequest(int user, string messageData)
        {
            Message message = new Message();
            message.Command = Command.BroadcastRequest;
            message.User.Id = user.ToString();
            message.ErrorCode = 0;
            PublishData concreteData = new PublishData();
            concreteData.User.Id = user.ToString();
            concreteData.Message = messageData;
            formatter.Serialize(ns, message);
            formatter.Serialize(ns, concreteData);
        }

        public static void StopThread()
        {
            isReceiveThreadOn = false;
        }

        public static event LogonResponseHandler LogonResponse;

        public static event LogoffResponseHandler LogoffResponse;

        public static event ListResponseHandler ListResponse;

        public static event BroadcastAckHandler BroadcastAck;

        public static event ErrorResponseHandler ErrorResponse;

        public static event ExceptionResponseHandler ExceptionResponse;


        public static void ThreadProc()
        {
            while (isReceiveThreadOn)
            {
                while (ns.DataAvailable)
                {
                    try
                    {
                        Message message = (Message)formatter.Deserialize(ns);
                        if (message.ErrorCode != 0)
                        {
                            if (ErrorResponse != null)
                                ErrorResponse(Int32.Parse(message.User.Id), message.Command);
                            continue;
                        }
                        switch (message.Command)
                        {
                            case Command.LogonResponse:
                                if (LogonResponse != null)
                                    LogonResponse(Int32.Parse(message.User.Id));
                                continue;

                            case Command.LogoffResponse:
                                if (LogoffResponse != null)
                                    LogoffResponse(Int32.Parse(message.User.Id));
                                continue;

                            case Command.ListResponse:
                                ListData listData = new ListData();
                                if (message.ErrorCode == 0)
                                    listData = (ListData)formatter.Deserialize(ns);
                                if (ListResponse != null)
                                {
                                    Set<int> userSet = new Set<int>();
                                    for (int i = 0; i < listData.UserData.Count; i++)
                                        userSet = userSet.Add(Int32.Parse(listData.UserData[i].Id));
                                    ListResponse(Int32.Parse(message.User.Id), userSet);
                                }
                                continue;

                            case Command.BroadcastAck:
                                PublishData publishData = new PublishData();
                                int publisher = 0;
                                string publishedMessage = null;
                                publishData = (PublishData)formatter.Deserialize(ns);
                                publisher = Int32.Parse(publishData.User.Id);
                                publishedMessage = publishData.Message;
                                if (BroadcastAck != null)
                                    BroadcastAck(Int32.Parse(message.User.Id), publisher, publishedMessage);
                                continue;

                            default:
                                ExceptionResponse("unexpected messages: " + message.ToString());
                                continue;

                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionResponse(e.Message);
                    }
                }
            }
        }

        #endregion
    }
}
