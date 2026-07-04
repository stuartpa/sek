using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

using Chat.Adapter;
using Microsoft.Modeling;

namespace Server
{
    /// <summary>
    /// A mock server implementation for chat.
    /// </summary>
    class Program
    {
        static int serverPort = 1333;

        static bool serverListBug = false; // set this to true to inject a bug
        static bool dontAllowEmptyBroadcast = false;

        static Random rgen = new Random();

        static Dictionary<User, bool> users = new Dictionary<User, bool>();


        static void Main(string[] args)
        {
            Console.WriteLine("chat mock server starting");

            IPAddress addr = Dns.GetHostEntry("localhost").AddressList[0];
            TcpListener listener = new TcpListener(addr, serverPort);

            Console.WriteLine("waiting for TCP connection...");
            listener.Start(0);
            TcpClient tcpClient = listener.AcceptTcpClient();
            Console.WriteLine("connected to {0}", tcpClient.ToString());

            NetworkStream stream = tcpClient.GetStream();

            try
            {
                while (true)
                {
                    // read request          
                    BinaryFormatter formatter = new BinaryFormatter();
                    Message message = (Message)formatter.Deserialize(stream);
                    Console.WriteLine("received " + message);

                    switch (message.Command)
                    {
                        case Command.LogonRequest:
                            if (users.ContainsKey(message.User))
                                message.ErrorCode = 1;
                            else
                                message.ErrorCode = 0;
                            users[message.User] = true;
                            message.Command = Command.LogonResponse;
                            formatter.Serialize(stream, message);
                            continue;

                        case Command.LogoffRequest:
                            if (!users.ContainsKey(message.User))
                                message.ErrorCode = 1;
                            else
                                message.ErrorCode = 0;
                            users.Remove(message.User);
                            message.Command = Command.LogoffResponse;
                            formatter.Serialize(stream, message);
                            continue;

                        case Command.ListRequest:
                            if (!users.ContainsKey(message.User))
                            {
                                message.ErrorCode = 1;
                                formatter.Serialize(stream, message);
                            }
                            else
                            {
                                message.ErrorCode = 0;
                                message.Command = Command.ListResponse;
                                formatter.Serialize(stream, message);
                                ListData data = new ListData();
                                data.UserCount = users.Count;
                                data.UserData = new List<User>();
                                foreach (User luser in users.Keys)
                                    if (!serverListBug || !(luser.Equals(message.User)))
                                        data.UserData.Add(luser);
                                formatter.Serialize(stream, data);
                            }
                            continue;

                        case Command.BroadcastRequest:
                            PublishData pdata = (PublishData)formatter.Deserialize(stream);
                            message.Command = Command.BroadcastAck;
                            if (!users.ContainsKey(message.User) || dontAllowEmptyBroadcast && pdata.Message.Length == 0)
                            {
                                message.ErrorCode = 1;
                                formatter.Serialize(stream, message);
                            }
                            else
                            {
                                message.ErrorCode = 0;
                                foreach (User user in users.Keys)
                                {
                                    message.User = user;
                                    formatter.Serialize(stream, message);
                                    formatter.Serialize(stream, pdata);
                                }
                            }
                            continue;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("terminating: " + e.Message);
            }

        }


    }
}
