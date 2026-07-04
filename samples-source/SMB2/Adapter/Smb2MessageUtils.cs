using System;
using System.Collections.Generic;
using System.Text;

namespace SMB2.Adapter
{
    static class Smb2MessageUtils
    {

        internal static Packet_Header CreateSyncHeader(
            Command_Values command, long sessionId, int creditRequest, long sequenceId)
        {
            Packet_Header header = new Packet_Header();
            header.ProtocolId = new byte[] { 0xfe, Convert.ToByte('S'), Convert.ToByte('M'),
                                      Convert.ToByte('B') };
            header.Command = command;
            header.StructureSize = 64;
            header.SessionId = (ulong)sessionId;
            header.Credit = (ushort)creditRequest;
            header.MessageId = (ulong)sequenceId;
            header.ProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            header.Signature = new byte[16];
            return header;
        }

        internal static NEGOTIATE_Request CreateNegotiateRequest()
        {
            NEGOTIATE_Request negotiateRequest = new NEGOTIATE_Request();
            negotiateRequest.StructureSize = 36;
            negotiateRequest.Capabilities = NEGOTIATE_Request_Capabilities_Values.GLOBAL_CAP_DFS;
            negotiateRequest.DialectCount = 1;
            negotiateRequest.Dialects = new byte[2];
            negotiateRequest.Dialects[0] = 0x02;
            negotiateRequest.Dialects[1] = 0x02;
            negotiateRequest.ClientStartTime = new byte[8];
            return negotiateRequest;
        }

        internal static SESSION_SETUP_Request CreateSessionSetupRequest()
        {
            SESSION_SETUP_Request sessionSetupRequest = new SESSION_SETUP_Request();
            sessionSetupRequest.StructureSize = 25;
            sessionSetupRequest.SecurityBufferOffset = 88;
            return sessionSetupRequest;
        }

        internal static TREE_CONNECT_Request CreateTreeConnectRequest()
        {
            TREE_CONNECT_Request treeConnectRequest = new TREE_CONNECT_Request();
            treeConnectRequest.StructureSize = 9;
            treeConnectRequest.PathOffset = 72;
            return treeConnectRequest;
        }

        internal static CREATE_Request CreateCreateRequest()
        {
            CREATE_Request createRequest = new CREATE_Request();
            createRequest.StructureSize = 57;
            createRequest.ShareAccess = ShareAccess_Values.FILE_SHARE_WRITE |
                               ShareAccess_Values.FILE_SHARE_READ |
                               ShareAccess_Values.FILE_SHARE_DELETE;
            createRequest.FileAttributes = 0x80;
            createRequest.DesiredAccess = 127;
            createRequest.CreateOptions = CreateOptions_Values.FILE_NON_DIRECTORY_FILE;
            createRequest.ImpersonationLevel = ImpersonationLevel_Values.Impersonation;
            createRequest.NameOffset = 120;
            return createRequest;
        }

        internal static CLOSE_Request CreateCloseRequest()
        {
            CLOSE_Request closeRequest = new CLOSE_Request();
            closeRequest.StructureSize = 24;
            return closeRequest;
        }

        internal static READ_Request CreateReadRequest()
        {
            READ_Request readRequest = new READ_Request();
            readRequest.StructureSize = 49;
            readRequest.Length = 16384;
            readRequest.Buffer = new byte[] { 0x00 };
            return readRequest;
        }

        internal static WRITE_Request CreateWriteRequest()
        {
            WRITE_Request writeRequest = new WRITE_Request();
            writeRequest.StructureSize = 49;
            writeRequest.DataOffset = 112;
            return writeRequest;
        }

        internal static byte[] MergeByteArray(int len, params byte[][] list)
        {
            int i;
            int index = 0;
            byte[] MergedArray = new byte[len];
            for (i = 0; i < list.Length; i++)
            {
                if (index > len)
                {
                    break;
                }
                list[i].CopyTo(MergedArray, index);
                index = index + list[i].Length;
            }
            return MergedArray;
        }

        internal static int GetSize(object x)
        {
            return 0;
        }

        internal static T Create<T> ()
        {
            return (T)(new object());
        }

        internal static void Validate<T>(object x)
        {
        }

        internal static byte[] CreatePacketSize(int size)
        {
            byte[] byteArray = BitConverter.GetBytes(size);
            byte[] packetSize = new byte[] { byteArray[3], byteArray[2], byteArray[1], byteArray[0] };
            return packetSize;
        }
    }
}
