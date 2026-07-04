namespace SMB2.Adapter {
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// ISMB2Packet is the interface representing all SMB2 packet
    /// </summary>
    public interface ISMB2Packet
    {
        int GetSize();
    }

    /// <summary>
    /// ISMB2Request is the interface representing all SMB2 request packets
    /// </summary>
    public interface ISMB2Request : ISMB2Packet
    {
        byte[] ToBytes();
    }

    /// <summary>
    /// ISMB2Response is the interface representing all SMB2 response packets
    /// </summary>
    public interface ISMB2Response : ISMB2Packet
    {
        void ToSMB2Packet(byte[] buf, int offset);
    }

    /// <summary>
    ///         The SMB2 NEGOTIATE Request packet is used by
    ///  the client to notify the server what dialects of the
    ///  SMB 2.0 Protocol the client understands.  This request
    ///  is composed of an SMB 2.0 Protocol header, as specified
    ///  in section , followed by this request structure:  
    ///    
    /// </summary>
    public struct NEGOTIATE_Request : ISMB2Request
    {

        /// <summary>
        ///                 The client MUST set this field to 36,
        ///  indicating the size of a NEGOTIATE request. This is
        ///  not the size of the structure with a single dialect
        ///  in the Dialects[] array.  This value MUST be set regardless
        ///  of the number of dialects sent.             
        /// </summary>
        public ushort StructureSize;

        /// <summary>
        ///                 The number of dialects that are contained
        ///  in the Dialects[] array.  This value MUST be greater
        ///  than 0.             
        /// </summary>
        public ushort DialectCount;

        /// <summary>
        ///  The security mode field MUST be constructed by using
        ///  the following values:
        /// </summary>
        public SecurityMode_Values SecurityMode;

        /// <summary>
        ///  Unused at the present, and MUST be treated as reserved.
        ///   The client MUST set this to 0, and the server MUST
        ///  ignore it on receipt.
        /// </summary>
        public NEGOTIATE_Request_Reserved_Values Reserved;

        /// <summary>
        ///  Specifies protocol capabilities for the client.  This
        ///  field MUST be constructed by using the following values:
        /// </summary>
        public NEGOTIATE_Request_Capabilities_Values Capabilities;

        /// <summary>
        ///  Unused at the present, and MUST be treated as reserved.
        ///  The client MUST set this to 0, and the server MUST
        ///  ignore it on receipt.
        /// </summary>
        /// <summary>
        ///  Please refer to type 'ClientGuid_Guid' for the possible
        ///  values
        /// </summary>
        public System.Guid ClientGuid;

        /// <summary>
        ///  Unused at the present, and MUST be treated as reserved.
        ///  The client MUST set this to 0, and the server MUST
        ///  ignore it on receipt.
        /// </summary>
        /// <summary>
        ///  Please refer to type 'ClientStartTime_FILETIME' for
        ///  the possible values
        /// </summary>
        public byte[] ClientStartTime;

        /// <summary>
        ///                 An array of one or more supported dialect
        ///  revision numbers.  The array MUST contain at least
        ///  one element of value 0x0202.                   A  RTM
        ///  based client would send a value of zero in Dialects
        ///  array in SMB2 NEGOTIATE request and a  RTM based server
        ///  would acknowledge with a value of 6 in DialectRevision
        ///  in SMB2 NEGOTIATE Response. This behavior is deprecated.
        ///                  
        /// </summary>
        public byte[] Dialects;

        public byte[] ToBytes()
        {
            byte[] requestBytes;
            byte[][] fieldList = new byte[8][]{BitConverter.GetBytes(StructureSize), BitConverter.GetBytes(DialectCount),
                                                BitConverter.GetBytes((ushort)SecurityMode), BitConverter.GetBytes((ushort)Reserved),
                                                BitConverter.GetBytes((uint)Capabilities), ClientGuid.ToByteArray(),
                                                ClientStartTime, Dialects
                                                };

            requestBytes = Smb2MessageUtils.MergeByteArray(GetSize(), fieldList);
            return requestBytes;
        }

        public int GetSize()
        {
            return StructureSize + Dialects.Length;
        }
    }

    [Flags()]
    public enum SecurityMode_Values : ushort
    {

        /// <summary>
        ///  When set, indicates that security signatures are enabled
        ///  on the client.
        /// </summary>
        NEGOTIATE_SIGNING_ENABLED = 0x0001,

        /// <summary>
        ///  When set, indicates that security signatures are required
        ///  by the client.
        /// </summary>
        NEGOTIATE_SIGNING_REQUIRED = 0x0002,
    }

    public enum NEGOTIATE_Request_Reserved_Values : ushort
    {

        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }

    [Flags()]
    public enum NEGOTIATE_Request_Capabilities_Values : uint
    {

        /// <summary>
        ///  When set, indicates that the client supports DFS.
        /// </summary>
        GLOBAL_CAP_DFS = 0x00000001,
    }

    public class ClientGuid_Guid
    {

        /// <summary>
        ///  Possible value.
        /// </summary>
        public static System.Guid V1 = new System.Guid("0");
    }


    /// <summary>
    ///  The SMB2 NEGOTIATE Response packet is sent by the server
    ///  to notify the client of the preferred common dialect.
    ///   This response is composed of an SMB2 header, as specified
    ///  in section , followed by this response structure:
    /// </summary>
    public struct NEGOTIATE_Response : ISMB2Response
    {

        /// <summary>
        ///  The server MUST set this field to 65, indicating the
        ///  size of the response structure, not including the header.
        ///   The server MUST set it to this value, regardless of
        ///  how long Buffer[] actually is in the response being
        ///  sent.
        /// </summary>
        public ushort StructureSize;

        /// <summary>
        ///  The security mode field MUST be constructed by using
        ///  the following values:
        /// </summary>
        public NEGOTIATE_Response_SecurityMode_Values SecurityMode;

        /// <summary>
        ///  The preferred common SMB 2.0 Protocol dialect number
        ///  from the Dialect array that is sent in the SMB2 negotiate
        ///  request.  The server must set this field to 0x0202.A
        ///   RTM based client would send a value of zero in Dialects
        ///  array in SMB2 NEGOTIATE request and a  RTM based server
        ///  would acknowledge with a value of 6 in DialectRevision
        ///  in SMB2 NEGOTIATE Response. This behavior is deprecated.
        /// </summary>
        public DialectRevision_Values DialectRevision;

        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MUST set this to 0.
        /// </summary>
        public NEGOTIATE_Response_Reserved_Values Reserved;

        /// <summary>
        ///  A globally unique identifier that is generated by the
        ///  server to uniquely identify this server.  This field
        ///  MUST NOT be used by a client as a secure method of
        ///  identifying a server.
        /// </summary>
        public System.Guid ServerGuid;

        /// <summary>
        ///  Specifies protocol capabilities for the server.  This
        ///  field MUST be constructed by using the following values:
        /// </summary>
        public NEGOTIATE_Response_Capabilities_Values Capabilities;

        /// <summary>
        ///  The maximum buffer size, in bytes, that can be used
        ///  for operations that are not read or write operations.
        /// </summary>
        public uint MaxTransactSize;

        /// <summary>
        ///  The maximum size, in bytes, of the Length in an SMB2
        ///  READ Request that the server will accept.
        /// </summary>
        public uint MaxReadSize;

        /// <summary>
        ///  The maximum size, in bytes, of the Length in an SMB2
        ///  WRITE Request that the server will accept.
        /// </summary>
        public uint MaxWriteSize;

        /// <summary>
        ///  The FILETIME format, as specified in [MS-DTYP] section
        ///  2.3.5.The current system time of the server.
        /// </summary>
        public byte[] SystemTime;

        /// <summary>
        ///  The FILETIME format, as specified in [MS-DTYP] section
        ///  2.3.5.The time the server was started up.
        /// </summary>
        public byte[] ServerStartTime;

        /// <summary>
        ///  The offset, in bytes, from the beginning of the SMB
        ///  2.0 Protocol header to the security buffer.
        /// </summary>
        public ushort SecurityBufferOffset;

        /// <summary>
        ///  The length, in bytes, of the security buffer.
        /// </summary>
        public ushort SecurityBufferLength;

        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MAY set this to any value, and the client
        ///  MUST ignore it on receipt.
        /// </summary>
        public uint Reserved2;

        /// <summary>
        ///  The variable-length buffer that contains the security
        ///  buffer for the response, as specified by SecurityBufferOffset
        ///  and SecurityBufferLength. The buffer MUST contain a
        ///  token as produced by the GSS protocol  as specified
        ///  in section .
        /// </summary>
        public byte[] Buffer;

        public int GetSize()
        {
            return StructureSize + Buffer.Length;
        }

        public void ToSMB2Packet(byte[] buf, int offset)
        {

        }
    }

    [Flags()]
    public enum NEGOTIATE_Response_SecurityMode_Values : ushort
    {

        /// <summary>
        ///  When set, indicates that security signatures are enabled
        ///  on the server.
        /// </summary>
        NEGOTIATE_SIGNING_ENABLED = 0x0001,

        /// <summary>
        ///  When set, indicates that security signatures are required
        ///  by the server.
        /// </summary>
        NEGOTIATE_SIGNING_REQUIRED = 0x0002,
    }

    public enum DialectRevision_Values : ushort
    {

        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 6,

        V0202 = 0x0202
    }

    [Flags()]
    public enum NEGOTIATE_Response_Reserved_Values : ushort
    {

        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 6,

        /// <summary>
        ///  Value generated from PAC config.
        /// </summary>
        WrongValueReturnedByServer = 0,
    }

    [Flags()]
    public enum NEGOTIATE_Response_Capabilities_Values : uint
    {

        /// <summary>
        ///  When set, indicates that the server supports the Distributed
        ///  File System (DFS).
        /// </summary>
        GLOBAL_CAP_DFS = 0x00000001,
    }


    /// <summary>
    ///  The SMB2 SESSION_SETUP Request packet is sent by the
    ///  client to request a new authenticated session within
    ///  a new or existing SMB 2.0 Protocol transport connection
    ///  to the server.  This request is composed of an SMB
    ///  2.0 Protocol header as specified in section  followed
    ///  by this request structure:
    /// </summary>
    public struct SESSION_SETUP_Request : ISMB2Request
    {

        /// <summary>
        ///  The client MUST set this field to 25, indicating the
        ///  size of the request structure, not including the header.
        ///   The client MUST set it to this value regardless of
        ///  how long Buffer[] actually is in the request being
        ///  sent.
        /// </summary>
        public ushort StructureSize;

        /// <summary>
        ///  The number of other transport connections that are already
        ///  established.  The client MAY choose to set this field
        ///  to 0 regardless of the number of outstanding connections.
        ///  clients always set VcNumber to 0.
        /// </summary>
        public byte VcNumber;

        /// <summary>
        ///  The security mode field MUST be constructed by using
        ///  the following values:
        /// </summary>
        public SESSION_SETUP_Request_SecurityMode_Values SecurityMode;

        /// <summary>
        ///  Specifies protocol capabilities for the client.  This
        ///  field MUST be constructed by using the following values:
        /// </summary>
        public SESSION_SETUP_Request_Capabilities_Values Capabilities;

        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The client MUST set this to 0, and the server MUST
        ///  ignore it on receipt.
        /// </summary>
        public SESSION_SETUP_Request_Channel_Values Channel;

        /// <summary>
        ///  The offset, in bytes, from the beginning of the SMB
        ///  2.0 Protocol header to the security buffer.
        /// </summary>
        public ushort SecurityBufferOffset;

        /// <summary>
        ///  The length, in bytes, of the security buffer.
        /// </summary>
        public ushort SecurityBufferLength;

        /// <summary>
        ///  A previously established session identifier.  If this
        ///  is a reconnect, the client MUST set this value to its
        ///  previous session identifier to allow the server to
        ///  reconnect.  If this is not a reconnect, the client
        ///  MUST set this to 0.
        /// </summary>
        public ulong PreviousSessionId;

        /// <summary>
        ///  A variable-length buffer that contains the security
        ///  buffer for the request, as specified by SecurityBufferOffset
        ///  and SecurityBufferLength. The buffer MUST contain a
        ///  token as produced by the GSS protocol  as specified
        ///  in section .
        /// </summary>
        public byte[] Buffer;

        public byte[] ToBytes()
        {
            byte[] requestBytes;
            byte[][] fieldList = new byte[8][]{BitConverter.GetBytes(StructureSize), new byte[] {VcNumber, (byte)SecurityMode},
                                                BitConverter.GetBytes((uint)Capabilities), BitConverter.GetBytes((uint)Channel),
                                                BitConverter.GetBytes(SecurityBufferOffset), BitConverter.GetBytes(SecurityBufferLength),
                                                BitConverter.GetBytes(PreviousSessionId), Buffer
                                                };

            requestBytes = Smb2MessageUtils.MergeByteArray(GetSize(), fieldList);
            return requestBytes;
        }

        public int GetSize()
        {
            return 24 + Buffer.Length;
        }
    }


    [Flags()]
    public enum SESSION_SETUP_Request_SecurityMode_Values : byte
    {

        /// <summary>
        ///  When set, indicates that security signatures are enabled
        ///  on the client.
        /// </summary>
        NEGOTIATE_SIGNING_ENABLED = 0x01,

        /// <summary>
        ///  When set, indicates that security signatures are required
        ///  by the client.
        /// </summary>
        NEGOTIATE_SIGNING_REQUIRED = 0x02,
    }

    [Flags()]
    public enum SESSION_SETUP_Request_Capabilities_Values : uint
    {

        /// <summary>
        ///  When set, indicates that the client supports the Distributed
        ///  File System (DFS).
        /// </summary>
        GLOBAL_CAP_DFS = 0x00000001,

        /// <summary>
        ///  MAY be set to any value and server MUST ignore.
        /// </summary>
        GLOBAL_CAP_UNUSED1 = 0x00000002,

        /// <summary>
        ///  MAY be set to any value and server MUST ignore.
        /// </summary>
        GLOBAL_CAP_UNUSED2 = 0x00000004,

        /// <summary>
        ///  MAY be set to any value and server MUST ignore.
        /// </summary>
        GLOBAL_CAP_UNUSED3 = 0x00000008,
    }

    public enum SESSION_SETUP_Request_Channel_Values : uint
    {

        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }

    /// <summary>
    ///  The SMB2 SESSION_SETUP Response packet is sent by the
    ///  server in response to an SMB2 SESSION_SETUP Request
    ///  packet. This response is composed of an SMB 2.0 Protocol
    ///  header, as specified in section , that is followed
    ///  by this response structure:
    /// </summary>
    public struct SESSION_SETUP_Response : ISMB2Response
    {

        /// <summary>
        ///  The server MUST set this to 9, indicating the size of
        ///  the fixed part of the response structure not including
        ///  the header. The server MUST set it to this value regardless
        ///  of how long Buffer[] actually is in the response.
        /// </summary>
        public ushort StructureSize;

        /// <summary>
        ///  A flags field that indicates additional information
        ///  about the session.  This field MUST be constructed
        ///  by using the following values:
        /// </summary>
        public SessionFlags_Values SessionFlags;

        /// <summary>
        ///  The offset, in bytes, from the beginning of the SMB
        ///  2.0 Protocol header to the security buffer.
        /// </summary>
        public ushort SecurityBufferOffset;

        /// <summary>
        ///  The length, in bytes, of the security buffer.
        /// </summary>
        public ushort SecurityBufferLength;

        /// <summary>
        ///  A variable-length buffer that contains the security
        ///  buffer for the response, as specified by SecurityBufferOffset
        ///  and SecurityBufferLength. The buffer MUST contain a
        ///  token as produced by the GSS protocol  as specified
        ///  in section .
        /// </summary>
        public byte[] Buffer;

        public int GetSize()
        {
            return 8 + Buffer.Length;
        }

        public void ToSMB2Packet(byte[] buf, int offset)
        {
            //Parse the session setup packet and get the security buffer
            int index = offset + 4;
            SecurityBufferOffset = BitConverter.ToUInt16(buf, index);
            index = index + 2;
            SecurityBufferLength = BitConverter.ToUInt16(buf, index);
            index = index + 2;
            Buffer = new byte[SecurityBufferLength];
            Array.Copy(buf, SecurityBufferOffset, Buffer, 0, SecurityBufferLength);
        }
    }


    [Flags()]
    public enum SessionFlags_Values : ushort
    {

        /// <summary>
        ///  If set, the client has been authenticated as a guest
        ///  user.
        /// </summary>
        SESSION_FLAG_IS_GUEST = 0x0001,

        /// <summary>
        ///  If set, the client has been authenticated as a NULL
        ///  user.
        /// </summary>
        SESSION_FLAG_IS_NULL = 0x0002,
    }


    /// <summary>
    ///  The SMB2 TREE_CONNECT Request packet is sent by a client
    ///  to request access to a particular share on the server.
    ///   This request is composed of an SMB2 Packet Header
    ///  that is followed by this request structure:
    /// </summary>
    public struct TREE_CONNECT_Request : ISMB2Request
    {

        /// <summary>
        ///  The client MUST set this field to 9, indicating the
        ///  size of the request structure, not including the header.
        ///   The client MUST set it to this value regardless of
        ///  how long Buffer[] actually is in the request being
        ///  sent.
        /// </summary>
        public ushort StructureSize;

        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The client MUST set this to 0, and the server MUST
        ///  ignore it on receipt.
        /// </summary>
        public TREE_CONNECT_Request_Reserved_Values Reserved;

        /// <summary>
        ///  The offset, in bytes, of the full share path name from
        ///  the beginning of the packet header.
        /// </summary>
        public ushort PathOffset;

        /// <summary>
        ///  The length, in bytes, of the path name.
        /// </summary>
        public ushort PathLength;

        /// <summary>
        ///  A variable-length buffer that contains the path name
        ///  of the share in Unicode in the form "\\server\share"
        ///  for the request, as described by PathOffset and PathLength.
        ///   The server component of the path MUST be a NetBIOS
        ///  name, a DNS name, or a textual IPv4 or IPv6 address,
        ///  and it MUST be fewer than 256 characters in length.
        ///   The share component of the path MUST be less than
        ///  or equal to 80 characters in length.The SMB 2.0 Protocol
        ///  client translates any names of the form \\server\pipe
        ///  to \\server\IPC$ before sending a request on the network.
        /// </summary>
        public byte[] Buffer;

        public byte[] ToBytes()
        {
            byte[] negotiateRequestBytes;
            byte[][] fieldList = new byte[5][]{BitConverter.GetBytes(StructureSize), BitConverter.GetBytes((ushort)Reserved),
                                                BitConverter.GetBytes((ushort)PathOffset), BitConverter.GetBytes((ushort)PathLength),
                                                Buffer};

            negotiateRequestBytes = Smb2MessageUtils.MergeByteArray(GetSize(), fieldList);
            return negotiateRequestBytes;
        }

        public int GetSize()
        {
            return 8 + Buffer.Length;
        }
    }

    public enum TREE_CONNECT_Request_Reserved_Values : ushort
    {

        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }


    /// <summary>
    ///  The FILETIME structure is a 64-bit value  that represents
    ///   the number of 100-nanosecond intervals  that have
    ///  elapsed since January 1, 1601, in Coordinated Universal
    ///  Time (UTC) format.
    /// </summary>
    public struct FILETIME
    {

        /// <summary>
        ///  A 32-bit unsigned integer that contains the low-order
        ///  bits of the file time.
        /// </summary>
        public uint dwLowDateTime;

        /// <summary>
        ///  A 32-bit unsigned integer that contains the high-order
        ///  bits of the file time.
        /// </summary>
        public uint dwHighDateTime;
    }

    /// <summary>
    ///  The SMB2 TREE_CONNECT Response packet is sent by the
    ///  server when an SMB2 TREE_CONNECT request is processed
    ///  successfully by the server.  The server MUST set the
    ///  TreeId  of the newly created tree connect in the SMB
    ///  2.0 Protocol header of the response.  This response
    ///  is composed of an SMB2 Packet Header that is followed
    ///  by this response structure:
    /// </summary>
    public struct TREE_CONNECT_Response : ISMB2Response{
        
        /// <summary>
        ///  The server MUST set this field to 16, indicating the
        ///  size of the response structure, not including the header.
        /// </summary>
        public ushort StructureSize;
        
        /// <summary>
        ///  The type of share being accessed.  This field MUST contain
        ///  one of the following values:
        /// </summary>
        public ShareType_Values ShareType;
        
        /// <summary>
        ///  Unused at the present, and MUST be treated as reserved.
        ///   The server MUST set this to 0, and the client MUST
        ///  ignore it on receipt.
        /// </summary>
        public Reserved_Values Reserved;
        
        /// <summary>
        ///  The offline caching properties for this share.  For
        ///  more information, see [OFFLINE].  This field MUST contain
        ///  one of the following values:
        /// </summary>
        public ShareFlags_Values ShareFlags;
        
        /// <summary>
        ///  Indicates various capabilities for this share.  This
        ///  field MUST be constructed by using the following values:
        /// </summary>
        public Capabilities_Values Capabilities;
        
        /// <summary>
        ///  Contains the maximal access for the user that establishes
        ///  the tree connect on the share based on the share's
        ///  permissions.  This value takes the form as specified
        ///  in section .
        /// </summary>
        public uint MaximalAccess;

        public int GetSize()
        {
            return StructureSize;
        }

        public void ToSMB2Packet(byte[] buf, int offset)
        {
            int index = offset + 2;
            ShareType = (ShareType_Values)(buf[index]);

        }
    }
    
    [Flags()]
    public enum ShareType_Values : byte {
        
        /// <summary>
        ///  Physical disk share.
        /// </summary>
        SHARE_TYPE_DISK = 0x01,
        
        /// <summary>
        ///  Named pipe share.
        /// </summary>
        SHARE_TYPE_PIPE = 0x02,
        
        /// <summary>
        ///  Printer share.
        /// </summary>
        SHARE_TYPE_PRINT = 0x03,
    }
    
    public enum Reserved_Values : byte {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    [Flags()]
    public enum ShareFlags_Values : uint {
        
        /// <summary>
        ///  The client MAY cache files that are explicitly selected
        ///  by the user for offline use.
        /// </summary>
        SHAREFLAG_MANUAL_CACHING = 0x00000000,
        
        /// <summary>
        ///  The client MAY automatically cache files that are used
        ///  by the user for offline access.
        /// </summary>
        SHAREFLAG_AUTO_CACHING = 0x00000010,
        
        /// <summary>
        ///  The client MAY automatically cache files that are used
        ///  by the user for offline access, and MAY use those files
        ///  in an offline mode even if the share is available.
        /// </summary>
        SHAREFLAG_VDO_CACHING = 0x00000020,
        
        /// <summary>
        ///  Offline caching MUST NOT occur.
        /// </summary>
        SHAREFLAG_NO_CACHING = 0x00000030,
        
        /// <summary>
        ///  The specified share is present in a DFS tree structure.
        /// </summary>
        SHI1005_FLAGS_DFS = 0x00000001,
        
        /// <summary>
        ///  The specified share is the root volume in a DFS tree
        ///  structure.
        /// </summary>
        SHI1005_FLAGS_DFS_ROOT = 0x00000002,
        
        /// <summary>
        ///  The specified share disallows exclusive file opens that
        ///  deny reads to an open file.
        /// </summary>
        SHI1005_FLAGS_RESTRICT_EXCLUSIVE_OPENS = 0x00000100,
        
        /// <summary>
        ///  Shared files in the specified share can be forcibly
        ///  deleted.
        /// </summary>
        SHI1005_FLAGS_FORCE_SHARED_DELETE = 0x00000200,
        
        /// <summary>
        ///  Clients are allowed to cache the namespace of the specified
        ///  share.
        /// </summary>
        SHI1005_FLAGS_ALLOW_NAMESPACE_CACHING = 0x00000400,
        
        /// <summary>
        ///  The server will filter directory entries based on the
        ///  access permissions of the client.
        /// </summary>
        SHI1005_FLAGS_ACCESS_BASED_DIRECTORY_ENUM = 0x00000800,
    }
    
    [Flags()]
    public enum Capabilities_Values : uint {
        
        /// <summary>
        ///  When set, indicates that the client supports the Distributed
        ///  File System (DFS).
        /// </summary>
        GLOBAL_CAP_DFS = 0x00000001,
    }
    
    /// <summary>
    ///  The SMB2 CREATE  Request packet is sent by a client
    ///  to request either creation or access to a file.  In
    ///  case of a named pipe or printer, the server MUST create
    ///  a new file. This request is composed of an SMB2 Packet
    ///  Header, as specified in section , that is followed
    ///  by this request structure:
    /// </summary>
    public struct CREATE_Request : ISMB2Request{
        
        /// <summary>
        ///  The client MUST set this field  to 57, indicating the
        ///  size of the request structure, not including the header.
        ///   The client MUST set it to this value regardless of
        ///  how long Buffer[] actually is in the request being
        ///  sent.
        /// </summary>
        public ushort StructureSize;
        
        /// <summary>
        ///  The Quality of Service (QoS) security flags.  This field
        ///  MUST be constructed by using the following values:
        /// </summary>
        public SecurityFlags_Values SecurityFlags;
        
        /// <summary>
        ///  The requested oplock level.  This field MUST contain
        ///  one of the following values.-based clients never use
        ///  exclusive oplocks.  Because there are no situations
        ///  where the client would want an exclusive oplock  where
        ///  it would not also want an SMB2_OPLOCK_LEVEL_BATCH,
        ///  it always requests an SMB2_OPLOCK_LEVEL_BATCH. For
        ///  named pipes, the server MUST always revert to SMB2_OPLOCK_LEVEL_NONE
        ///  irrespective of the value of this field.
        /// </summary>
        public RequestedOplockLevel_Values RequestedOplockLevel;
        
        /// <summary>
        ///  This field specifies the impersonation level of the
        ///  application that is issuing the create request.  This
        ///  field MUST contain one of the following values:
        /// </summary>
        public ImpersonationLevel_Values ImpersonationLevel;
        
        /// <summary>
        ///  Unused at the present, and MUST be treated as reserved.
        ///   The client MAY set this to any value, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public ulong SmbCreateFlags;
        
        /// <summary>
        ///  Unused at the present, and MUST be treated as reserved.
        ///   The client MAY set this to any value, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public ulong Reserved;
        
        /// <summary>
        ///  The level of access that is wanted, as specified in
        ///  section .
        /// </summary>
        public uint DesiredAccess;
        
        /// <summary>
        ///  This field MUST be one of the values specified in [MS-FSCC]
        ///  section 2.6.  For a printer, all attributes except
        ///  FILE_ATTRIBUTE_DIRECTORY are valid.
        /// </summary>
        public uint FileAttributes;
        
        /// <summary>
        ///  Specifies the sharing mode for the open.  This field
        ///  MUST be constructed by using the following values:
        /// </summary>
        public ShareAccess_Values ShareAccess;
        
        /// <summary>
        ///  Defines the action the server MUST take if the file
        ///  that is specified in the name field already exists.
        ///   For opening named pipes, this field MUST be ignored.
        ///  For other files, this field MUST contain one of the
        ///  following values:
        /// </summary>
        public CreateDisposition_Values CreateDisposition;
        
        /// <summary>
        ///  Specifies the options to be applied when creating or
        ///  opening the file.  Combinations of the bit positions
        ///  listed below are valid, unless otherwise noted.  This
        ///  field MUST be constructed by using the following values:
        /// </summary>
        public CreateOptions_Values CreateOptions;
        
        /// <summary>
        ///  The offset, in bytes, from the beginning of the SMB2
        ///  header to the quad word aligned file name.  The file
        ///  name is relative to the share that is found by the
        ///  TreeId  in the SMB2 header.  If no name is provided,
        ///  indicating an open of the root directory of the share,
        ///  this field MUST be 0.
        /// </summary>
        public ushort NameOffset;
        
        /// <summary>
        ///  The length of the file name, in bytes.  Each individual
        ///  file name component MUST NOT exceed 255 Unicode characters
        ///  and the full path name MUST NOT exceed 32760 Unicode
        ///  characters. If no file name is provided, this field
        ///  MUST be set to 0.
        /// </summary>
        public ushort NameLength;
        
        /// <summary>
        ///  The offset, in bytes, from the beginning of the SMB2
        ///  header to the first quad word aligned SMB2_CREATE_CONTEXT
        ///  structure in the request.  If no SMB2_CREATE_CONTEXTs
        ///  are being sent, this value MUST be 0.
        /// </summary>
        public uint CreateContextsOffset;
        
        /// <summary>
        ///  The length, in bytes, of all the SMB2_CREATE_CONTEXT
        ///  structures that are sent in this request.
        /// </summary>
        public uint CreateContextsLength;
        
        /// <summary>
        ///  A variable-length buffer that contains the Unicode file
        ///  name and create context array, as defined by NameOffset,
        ///  NameLength, CreateContextsOffset, and CreateContextsLength.
        /// </summary>
        public byte[] Buffer;

        public byte[] ToBytes()
        {
            byte[] requestBytes;
            byte[][] fieldList = new byte[15][]{BitConverter.GetBytes(StructureSize), new byte[]{(byte)SecurityFlags, (byte)RequestedOplockLevel},
                                                BitConverter.GetBytes((uint)ImpersonationLevel), BitConverter.GetBytes(SmbCreateFlags),
                                                BitConverter.GetBytes(Reserved), BitConverter.GetBytes(DesiredAccess),
                                                BitConverter.GetBytes(FileAttributes), BitConverter.GetBytes((uint)ShareAccess),
                                                BitConverter.GetBytes((uint)CreateDisposition), BitConverter.GetBytes((uint)CreateOptions),
                                                BitConverter.GetBytes(NameOffset), BitConverter.GetBytes(NameLength),
                                                BitConverter.GetBytes(CreateContextsOffset), BitConverter.GetBytes(CreateContextsLength),
                                                Buffer};

            requestBytes = Smb2MessageUtils.MergeByteArray(GetSize(), fieldList);
            return requestBytes;
        }

        public int GetSize()
        {
            return 56 + Buffer.Length;
        }
    }
    
    
    [Flags()]
    public enum SecurityFlags_Values : byte {
        
        /// <summary>
        ///  When set, indicates that the security tracking mode
        ///  is dynamic.  When not set, indicates that the security
        ///  tracking mode is static.
        /// </summary>
        SECURITY_DYNAMIC_TRACKING = 0x01,
        
        /// <summary>
        ///  When set, indicates that only the enabled aspects of
        ///  the client security context are available to the server.
        /// </summary>
        SECURITY_EFFECTIVE_ONLY = 0x02,
    }
    
    [Flags()]
    public enum RequestedOplockLevel_Values : byte {
        
        /// <summary>
        ///  No oplock is requested.
        /// </summary>
        OPLOCK_LEVEL_NONE = 0x00,
        
        /// <summary>
        ///  A level II oplock is requested.
        /// </summary>
        OPLOCK_LEVEL_II = 0x01,
        
        /// <summary>
        ///  An exclusive oplock is requested.
        /// </summary>
        OPLOCK_LEVEL_EXCLUSIVE = 0x08,
        
        /// <summary>
        ///  A batch oplock is requested.
        /// </summary>
        OPLOCK_LEVEL_BATCH = 0x09,
    }
    
    [Flags()]
    public enum ImpersonationLevel_Values : uint {
        
        /// <summary>
        ///  The client is anonymous to the server.  The server process
        ///  MAY impersonate the client, but the impersonation token
        ///  MUST NOT contain any information about the client.
        ///  								The server process MUST NOT obtain identification
        ///  information about the client.
        /// </summary>
        Anonymous = 0x00000000,
        
        /// <summary>
        ///  The server MAY obtain the client's identity, and the
        ///  server MAY impersonate the client to perform access
        ///  control list (ACL) checks.
        /// </summary>
        Identification = 0x00000001,
        
        /// <summary>
        ///  The server MAY impersonate the client's security context
        ///  while acting on behalf of the client.  The server MAY
        ///  access local resources as the client.
        /// </summary>
        Impersonation = 0x00000002,
        
        /// <summary>
        ///  The server MAY impersonate the client's security context
        ///  while acting on behalf of the client.  During impersonation,
        ///  the client's credentials (both local and network) MAY
        ///  be passed to any number of machines.
        /// </summary>
        Delegate = 0x00000003,
    }
    
    [Flags()]
    public enum ShareAccess_Values : uint {
        
        /// <summary>
        ///  When set, indicates that other opens are allowed to
        ///  read this file while this open is present.   This bit
        ///  must not be set for a named pipe or a printer file.
        ///  Each open creates a new instance of a named pipe. 
        ///  Likewise, opening a printer file always creates a new
        ///  file.
        /// </summary>
        FILE_SHARE_READ = 0x00000001,
        
        /// <summary>
        ///  When set, indicates that other opens are allowed to
        ///  write this file while this open is present.    This
        ///  bit must not be set for a named pipe or a printer file.
        ///  Each open creates a new instance of a named pipe. 
        ///  Likewise, opening a printer file always creates a new
        ///  file.
        /// </summary>
        FILE_SHARE_WRITE = 0x00000002,
        
        /// <summary>
        ///  When set, indicates that other opens are allowed to
        ///  delete or rename this file while this open is present.
        ///       This bit must not be set for a named pipe or a
        ///  printer file. Each open creates a new instance of a
        ///  named pipe.  Likewise, opening a printer file always
        ///  creates a new file.
        /// </summary>
        FILE_SHARE_DELETE = 0x00000004,
    }
    
    [Flags()]
    public enum CreateDisposition_Values : uint {
        
        /// <summary>
        ///  If the file already exists, supersede it by the specified
        ///  file.  Otherwise, create the file.
        /// </summary>
        FILE_SUPERSEDE = 0x00000000,
        
        /// <summary>
        ///  If the file already exists, return success; otherwise,
        ///  fail the operation. MUST NOT be set for a printer object.
        /// </summary>
        FILE_OPEN = 0x00000001,
        
        /// <summary>
        ///  If the file already exists, fail the operation; otherwise,
        ///  create the file.
        /// </summary>
        FILE_CREATE = 0x00000002,
        
        /// <summary>
        ///  Open the file if it already exists; otherwise, create
        ///  the file.
        /// </summary>
        FILE_OPEN_IF = 0x00000003,
        
        /// <summary>
        ///  Overwrite the file if it already exists; otherwise,
        ///  fail the operation. MUST NOT be set for a printer object.
        /// </summary>
        FILE_OVERWRITE = 0x00000004,
        
        /// <summary>
        ///  Overwrite the file if it already exists; otherwise,
        ///  create the file.
        /// </summary>
        FILE_OVERWRITE_IF = 0x00000005,
    }
    
    [Flags()]
    public enum CreateOptions_Values : uint {
        
        /// <summary>
        ///  The file being created or opened is a directory file.
        ///   With this flag, the CreateDisposition   field MUST
        ///  be set to FILE_CREATE or FILE_OPEN_IF.  With this flag,
        ///  only the following CreateOptions values are valid:
        ///  FILE_WRITE_THROUGH, and FILE_OPEN_FOR_BACKUP_INTENT.
        /// </summary>
        FILE_DIRECTORY_FILE = 0x00000001,
        
        /// <summary>
        ///  The server MUST propagate writes to this open to persistent
        ///  storage before returning success to the client on write
        ///  operations.
        /// </summary>
        FILE_WRITE_THROUGH = 0x00000002,
        
        /// <summary>
        ///  A hint indicating that accesses to the file will be
        ///  sequential.  This flag value is incompatible with the
        ///  FILE_RANDOM_ACCESS value, which indicates that the
        ///  accesses to the file can be random.
        /// </summary>
        FILE_SEQUENTIAL_ONLY = 0x00000004,
        
        /// <summary>
        ///  The server or underlying object store SHOULD NOT cache
        ///  data at intermediate layers and SHOULD allow it to
        ///  flow through to persistent storage.
        /// </summary>
        FILE_NO_INTERMEDIATE_BUFFERING = 0x00000008,
        
        /// <summary>
        ///  The file being opened MUST NOT be a directory file or
        ///  this call MUST be failed.  This flag MUST NOT be used
        ///  with FILE_DIRECTORY_FILE.
        /// </summary>
        FILE_NON_DIRECTORY_FILE = 0x00000040,
        
        /// <summary>
        ///  If the extended attributes on an existing file being
        ///  opened indicate that the caller must understand extended
        ///  attributes (EAs) to properly interpret the file, the
        ///  server MUST fail this request because the caller does
        ///  not understand how to deal with EAs.
        /// </summary>
        FILE_NO_EA_KNOWLEDGE = 0x00000100,
        
        /// <summary>
        ///  The file is being opened for backup intent.  That is,
        ///  it is being opened or created for the purposes of either
        ///  a backup or a restore operation. Thus, the server MAY
        ///  make appropriate checks to ensure that the caller is
        ///  capable of overriding whatever security checks have
        ///  been placed on the file to allow a backup or restore
        ///  operation to occur. The server MAY choose to check
        ///  for certain access rights to the file before checking
        ///  the DesiredAccess field.
        /// </summary>
        FILE_OPEN_FOR_BACKUP_INTENT = 0x00004000,
        
        /// <summary>
        ///  A hint that indicates that accesses to the file can
        ///  be random; so sequential read-ahead operations SHOULD
        ///  NOT be performed on the file. This flag value is incompatible
        ///  with the FILE_SEQUENTIAL_ONLY value, which indicates
        ///  that the accesses to the file will be sequential.
        /// </summary>
        FILE_RANDOM_ACCESS = 0x00000400,
    }
   
    [Flags()]
    public enum File_Pipe_Printer_Access_Mask_Values : uint {
        
        /// <summary>
        ///  This value indicates the right to read data from the
        ///  file or named pipe.
        /// </summary>
        FILE_READ_DATA = 0x00000001,
        
        /// <summary>
        ///  This value indicates the right to write data into the
        ///  file or named pipe.
        /// </summary>
        FILE_WRITE_DATA = 0x00000002,
        
        /// <summary>
        ///  This value indicates the right to write data into the
        ///  file or named pipe beyond its current file size.
        /// </summary>
        FILE_APPEND_DATA = 0x00000004,
        
        /// <summary>
        ///  This value indicates the right to read the extended
        ///  attributes of the file or named pipe.
        /// </summary>
        FILE_READ_EA = 0x00000008,
        
        /// <summary>
        ///  This value indicates the right to write or change the
        ///  extended attributes to the file or named pipe.
        /// </summary>
        FILE_WRITE_EA = 0x00000010,
        
        /// <summary>
        ///  This value indicates the right to execute the file.
        /// </summary>
        FILE_EXECUTE = 0x00000020,
        
        /// <summary>
        ///  This value indicates the right to read the attributes
        ///  of the file.
        /// </summary>
        FILE_READ_ATTRIBUTES = 0x00000080,
        
        /// <summary>
        ///  This value indicates the right to change the attributes
        ///  of the file.
        /// </summary>
        FILE_WRITE_ATTRIBUTES = 0x00000100,
        
        /// <summary>
        ///  This value indicates the right to delete the file.
        /// </summary>
        DELETE = 0x00010000,
        
        /// <summary>
        ///  This value indicates the right to read the security
        ///  descriptor for the file or named pipe.
        /// </summary>
        READ_CONTROL = 0x00020000,
        
        /// <summary>
        ///  This value indicates the right to change the discretionary
        ///  access control list (DACL) in the security descriptor
        ///  for the file or named pipe.  For the DACL data structure,
        ///  see ACL in [MS-DTYP].
        /// </summary>
        WRITE_DAC = 0x00040000,
        
        /// <summary>
        ///  This value indicates the right to change the owner in
        ///  the security descriptor for the file or named pipe.
        /// </summary>
        WRITE_OWNER = 0x00080000,
        
        /// <summary>
        ///  This value SHOULD be set to 0 by the sender and MUST
        ///  be ignored by the receiver.
        /// </summary>
        SYNCHRONIZE = 0x00100000,
        
        /// <summary>
        ///  This value indicates the right to read or change the
        ///  system access control list (SACL) in the security descriptor
        ///  for the file or named pipe.  For  the SACL data structure,
        ///  see ACL in [MS-DTYP].
        /// </summary>
        ACCESS_SYSTEM_SECURITY = 0x01000000,
        
        /// <summary>
        ///  This value indicates that the client is requesting an
        ///  open to the file with the highest level of access the
        ///  client has on this file.  If no access is granted for
        ///  the client on this file, the server MUST fail the open
        ///  with STATUS_ACCESS_DENIED.
        /// </summary>
        MAXIMAL_ACCESS = 0x02000000,
        
        /// <summary>
        ///  This value indicates a request for all the access flags
        ///  that are previously listed except MAXIMAL_ACCESS and
        ///  ACCESS_SYSTEM_SECURITY.
        /// </summary>
        GENERIC_ALL = 0x10000000,
        
        /// <summary>
        ///  This value indicates a request for the following combination
        ///  of access flags listed above: FILE_READ_ATTRIBUTES|
        ///  FILE_EXECUTE| SYNCHRONIZE| READ_CONTROL.
        /// </summary>
        GENERIC_EXECUTE = 0x20000000,
        
        /// <summary>
        ///  This value indicates a request for the following combination
        ///  of access flags listed above: FILE_WRITE_DATA| FILE_APPEND_DATA|
        ///   FILE_WRITE_ATTRIBUTES| FILE_WRITE_EA| SYNCHRONIZE|
        ///  READ_CONTROL.
        /// </summary>
        GENERIC_WRITE = 0x40000000,
        
        /// <summary>
        ///  This value indicates a request for the following combination
        ///  of access flags listed above: FILE_READ_DATA| FILE_READ_ATTRIBUTES|
        ///  FILE_READ_EA| SYNCHRONIZE| READ_CONTROL.
        /// </summary>
        GENERIC_READ = 0x80000000,
    }
    
    [Flags()]
    public enum Directory_Access_Mask_Values : uint {
        
        /// <summary>
        ///  This value indicates the right to enumerate the contents
        ///  of the directory.
        /// </summary>
        FILE_LIST_DIRECTORY = 0x00000001,
        
        /// <summary>
        ///  This value indicates the right to create a file under
        ///  the directory.
        /// </summary>
        FILE_ADD_FILE = 0x00000002,
        
        /// <summary>
        ///  This value indicates the right to add a sub-directory
        ///  under the directory.
        /// </summary>
        FILE_ADD_SUBDIRECTORY = 0x00000004,
        
        /// <summary>
        ///  This value indicates the right to read the extended
        ///  attributes of the directory.
        /// </summary>
        FILE_READ_EA = 0x00000008,
        
        /// <summary>
        ///  This value indicates the right to write or change the
        ///  extended attributes of the directory.
        /// </summary>
        FILE_WRITE_EA = 0x00000010,
        
        /// <summary>
        ///  This value indicates the right to traverse this directory
        ///  if the server enforces traversal checking.
        /// </summary>
        FILE_TRAVERSE = 0x00000020,
        
        /// <summary>
        ///  This value indicates the right to delete the files and
        ///  directories within this directory.
        /// </summary>
        FILE_DELETE_CHILD = 0x00000040,
        
        /// <summary>
        ///  This value indicates the right to read the attributes
        ///  of the directory.
        /// </summary>
        FILE_READ_ATTRIBUTES = 0x00000080,
        
        /// <summary>
        ///  This value indicates the right to change the attributes
        ///  of the directory.
        /// </summary>
        FILE_WRITE_ATTRIBUTES = 0x00000100,
        
        /// <summary>
        ///  This value indicates the right to delete the directory.
        /// </summary>
        DELETE = 0x00010000,
        
        /// <summary>
        ///  This value indicates the right to read the security
        ///  descriptor for the directory.
        /// </summary>
        READ_CONTROL = 0x00020000,
        
        /// <summary>
        ///  This value indicates the right to change the DACL in
        ///  the security descriptor for the directory.  For the
        ///  DACL data structure, see  ACL in [MS-DTYP].
        /// </summary>
        WRITE_DAC = 0x00040000,
        
        /// <summary>
        ///  This value indicates the right to change the owner in
        ///  the security descriptor for the directory.
        /// </summary>
        WRITE_OWNER = 0x00080000,
        
        /// <summary>
        ///  This value SHOULD be set to 0 by the sender and MUST
        ///  be ignored by the receiver.
        /// </summary>
        SYNCHRONIZE = 0x00100000,
        
        /// <summary>
        ///  This value indicates the right to read or change the
        ///  SACL in the security descriptor for the directory.
        ///  For the SACL data structure, see ACL in [MS-DTYP].
        /// </summary>
        ACCESS_SYSTEM_SECURITY = 0x01000000,
        
        /// <summary>
        ///  This value indicates that the client is requesting an
        ///  open to the directory with the highest level of access
        ///  the client has on this directory.  If no access is
        ///  granted for the client on this directory, the server
        ///  MUST fail the open with STATUS_ACCESS_DENIED.
        /// </summary>
        MAXIMAL_ACCESS = 0x02000000,
        
        /// <summary>
        ///  This value indicates a request for all the access flags
        ///  that are listed above except MAXIMAL_ACCESS and ACCESS_SYSTEM_SECURITY.
        /// </summary>
        GENERIC_ALL = 0x10000000,
        
        /// <summary>
        ///  This value indicates a request for the following access
        ///  flags listed above: FILE_READ_ATTRIBUTES| FILE_TRAVERSE|
        ///  SYNCHRONIZE| READ_CONTROL.
        /// </summary>
        GENERIC_EXECUTE = 0x20000000,
        
        /// <summary>
        ///  This value indicates a request for the following access
        ///  flags listed above: FILE_ADD_FILE| FILE_ADD_SUBDIRECTORY|
        ///  FILE_WRITE_ATTRIBUTES| FILE_WRITE_EA| SYNCHRONIZE|
        ///  READ_CONTROL.
        /// </summary>
        GENERIC_WRITE = 0x40000000,
        
        /// <summary>
        ///  This value indicates a request for the following access
        ///  flags listed above: FILE_LIST_DIRECTORY| FILE_READ_ATTRIBUTES|
        ///  FILE_READ_EA| SYNCHRONIZE| READ_CONTROL.
        /// </summary>
        GENERIC_READ = 0x80000000,
    }
    
    /// <summary>
    ///  The SMB2 CREATE Response packet is sent by the server
    ///  to notify the client of the status of its SMB2 CREATE
    ///  Request.  This response is composed of an SMB2 header,
    ///  as specified in section , followed by this response
    ///  structure:
    /// </summary>
    public struct CREATE_Response : ISMB2Response{
        
        /// <summary>
        ///  The server MUST set this field to 89, indicating the
        ///  size of the request structure, not including the header.
        ///   The server MUST set this field to this value regardless
        ///  of how long Buffer[] actually is in the request being
        ///  sent.
        /// </summary>
        public ushort StructureSize;
        
        /// <summary>
        ///  The oplock level that is granted to the client for this
        ///  open.  This field MUST contain one of the following
        ///  values:-based clients never use exclusive oplocks.
        ///   Because there are no situations where it would want
        ///  an exclusive oplock  where it would not also want an
        ///  SMB2_OPLOCK_LEVEL_BATCH, it always requests an SMB2_OPLOCK_LEVEL_BATCH.
        /// </summary>
        public OplockLevel_Values OplockLevel;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MAY set this to any value, and the client
        ///  MUST ignore it on receipt.
        /// </summary>
        public byte Reserved;
        
        /// <summary>
        ///  The action taken in establishing the open.  This field
        ///  MUST contain one of the following values:
        /// </summary>
        public CreateAction_Values CreateAction;
        
        /// <summary>
        ///  A FILETIME, as specified in FILETIME.The time when the
        ///  file was created.
        /// </summary>
        public FILETIME CreationTime;
        
        /// <summary>
        ///  A FILETIME, as specified in FILETIME.The time the file
        ///  was last accessed.
        /// </summary>
        public FILETIME LastAccessTime;
        
        /// <summary>
        ///  A FILETIME, as specified in FILETIME.The time when data
        ///  was last written to the file.
        /// </summary>
        public FILETIME LastWriteTime;
        
        /// <summary>
        ///  A FILETIME, as specified in FILETIME.The time when the
        ///  file was last modified.
        /// </summary>
        public FILETIME ChangeTime;
        
        /// <summary>
        ///  The size, in bytes, of the data that is allocated to
        ///  the file.
        /// </summary>
        public ulong AllocationSize;
        
        /// <summary>
        ///  The size, in bytes, of the file.
        /// </summary>
        public ulong EndofFile;
        
        /// <summary>
        ///  The attributes of the file.  The valid flags are as
        ///  specified in [MS-FSCC] section 2.6.
        /// </summary>
        public uint FileAttributes;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MAY set this to any value, and the client
        ///  MUST ignore it on receipt.
        /// </summary>
        public Reserved2_Values Reserved2;
        
        /// <summary>
        ///  The SMB2_FILEID, as specified in section . 						The
        ///  identifier of the open to a file or pipe that was established.
        /// </summary>
        public FILEID FileId;
        
        /// <summary>
        ///  The offset, in bytes, from the beginning of the SMB2
        ///  header to the first quad word aligned SMB2_CREATE_CONTEXT
        ///  responses that are contained in this response.  If
        ///  none are being returned in the response, this value
        ///  MUST be 0.  These values are specified in section .
        /// </summary>
        public uint CreateContextsOffset;
        
        /// <summary>
        ///  The length, in bytes, of the SMB2_CREATE_CONTEXT responses
        ///  that are contained in this response.
        /// </summary>
        public uint CreateContextsLength;
        
        /// <summary>
        ///  A variable-length buffer that contains the create contexts
        ///  that are contained in this response, as described by
        ///  CreateContextsOffset and CreateContextsLength.  The
        ///  format of this element is one of the SMB2 CREATE_CONTEXT
        ///  Response Values, as specified in section .
        /// </summary>
        public byte[] Buffer;

        public int GetSize()
        {
            return StructureSize;
        }

        public void ToSMB2Packet(byte[] buf, int offset)
        {
            int index = offset + 64;
            FileId.Persistent = BitConverter.ToUInt64(buf, index);
            FileId.Volatile = BitConverter.ToUInt64(buf, index + 8);
        }
    }
 
    [Flags()]
    public enum OplockLevel_Values : byte {
        
        /// <summary>
        ///  No oplock was granted.
        /// </summary>
        OPLOCK_LEVEL_NONE = 0x00,
        
        /// <summary>
        ///  A level II oplock was granted.
        /// </summary>
        OPLOCK_LEVEL_II = 0x01,
        
        /// <summary>
        ///  An exclusive oplock was granted.
        /// </summary>
        OPLOCK_LEVEL_EXCLUSIVE = 0x08,
        
        /// <summary>
        ///  A batch oplock was granted.
        /// </summary>
        OPLOCK_LEVEL_BATCH = 0x09,
    }
    
    [Flags()]
    public enum CreateAction_Values : uint {
        
        /// <summary>
        ///  An existing file was deleted and a new file was created
        ///  in its place.
        /// </summary>
        FILE_SUPERSEDED = 0x00000000,
        
        /// <summary>
        ///  An existing file was opened.
        /// </summary>
        FILE_OPENED = 0x00000001,
        
        /// <summary>
        ///  A new file was created.
        /// </summary>
        FILE_CREATED = 0x00000002,
        
        /// <summary>
        ///  An existing file was overwritten.
        /// </summary>
        FILE_OVERWRITTEN = 0x00000003,
        
        /// <summary>
        ///  The CreateDisposition field of the SMB2 CREATE Request
        ///  was set to FILE_CREATE, and the operation failed because
        ///  the file existed.
        /// </summary>
        FILE_EXISTS = 0x00000004,
        
        /// <summary>
        ///  The CreateDisposition field of the SMB2 CREATE Request
        ///  was set to FILE_OPEN, and the operation failed because
        ///  the file does not exist.
        /// </summary>
        FILE_DOES_NOT_EXIST = 0x00000005,
    }
    
    public enum Reserved2_Values : uint {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    /// <summary>
    ///  The SMB2 FILEID is used to represent an open to a file.
    /// </summary>
    public struct FILEID {
        
        /// <summary>
        ///  A file handle that remains persistent when an open is
        ///  reconnected after being lost on a disconnect, as specified
        ///  in section .  The server MUST return this file handle
        ///  as part of an SMB2 CREATE Response.
        /// </summary>
        public ulong Persistent;
        
        /// <summary>
        ///  A file handle that MAY be changed when an open is reconnected
        ///  after being lost on a disconnect, as specified in section
        ///  .    The server MUST return this file handle as part
        ///  of an SMB2 CREATE Response. This value MUST NOT change
        ///  unless a reconnection is performed.
        /// </summary>
        public ulong Volatile;
    }
    
 
    /// <summary>
    ///  The SMB2 CLOSE Request packet is used by the client
    ///  to close an instance of a file that was opened previously
    ///  with a successful SMB2 CREATE Request.  This request
    ///  is composed of an SMB2 header, as specified in section
    ///  , followed by this request structure:
    /// </summary>
    public struct CLOSE_Request : ISMB2Request{
        
        /// <summary>
        ///  The client MUST set this field to 24, indicating the
        ///  size of the request structure, not including the header.
        /// </summary>
        public ushort StructureSize;
        
        /// <summary>
        ///  A Flags field, which indicates how the operation MUST
        ///  be processed.  This field MUST be constructed by using
        ///  the following values:
        /// </summary>
        public Flags_Values Flags;
        
        /// <summary>
        ///  Unused at the present, and MUST be treated as reserved.
        ///   The client MUST set this to 0, and the server MUST
        ///  ignore it on receipt.
        /// </summary>
        public CLOSE_Request_Reserved_Values Reserved;
        
        /// <summary>
        ///  An SMB2_FILEID structure, as specified in section .The
        ///  identifier of the open to a file, or of a named pipe
        ///  that is being closed.
        /// </summary>
        public FILEID FileId;

        public byte[] ToBytes()
        {
            byte[] requestBytes;
            byte[][] fieldList = new byte[5][]{BitConverter.GetBytes(StructureSize), BitConverter.GetBytes((ushort)Flags),
                                                BitConverter.GetBytes((uint)Reserved), BitConverter.GetBytes((ulong)FileId.Persistent),
                                                BitConverter.GetBytes((ulong)FileId.Volatile)};

            requestBytes = Smb2MessageUtils.MergeByteArray(GetSize(), fieldList);
            return requestBytes;
        }

        public int GetSize()
        {
            return 24;
        }
    }

    [Flags()]
    public enum Flags_Values : ushort {
        
        /// <summary>
        ///  If set, the server MUST set the attribute fields in
        ///  the response, as specified in section , to valid values.
        ///   If not set, the client MUST NOT use the values that
        ///  are returned in the response.
        /// </summary>
        CLOSE_FLAG_POSTQUERY_ATTRIB = 0x0001,
    }
    
    public enum CLOSE_Request_Reserved_Values : uint {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    /// <summary>
    ///  The SMB2 CLOSE Response packet is sent by the server
    ///  to indicate that an SMB2 CLOSE Request was processed
    ///  successfully.  This response is composed of an SMB2
    ///  header, as specified in section , followed by this
    ///  response structure:
    /// </summary>
    public struct CLOSE_Response : ISMB2Response{
        
        /// <summary>
        ///  The server MUST set this field to 60, indicating the
        ///  size of the response structure, not including the header.
        /// </summary>
        public ushort StructureSize;
        
        /// <summary>
        ///  A Flags field that indicates how the operation MUST
        ///  be processed.  This field MUST be constructed by using
        ///  the following values:
        /// </summary>
        public CLOSE_Response_Flags_Values Flags;
        
        /// <summary>
        ///  Unused at present, and MUST be treated as reserved.
        ///   The server MUST set this to 0, and the client MUST
        ///  ignore it on receipt.
        /// </summary>
        public CLOSE_Response_Reserved_Values Reserved;
        
        /// <summary>
        ///  A FILETIME, as specified in FILETIME.The time when the
        ///  file was created.  If the SMB2_CLOSE_FLAG_POSTQUERY_ATTRIB
        ///  flag in the SMB2 CLOSE Request was set, this field
        ///  MUST be set to the value that is returned by the attribute
        ///  query. If the flag is not set, the field SHOULD be
        ///  set to zero and MUST NOT be checked on receipt.
        /// </summary>
        public FILETIME CreationTime;
        
        /// <summary>
        ///  A FILETIME, as specified in FILETIME.The time when the
        ///  file was last accessed.  If the SMB2_CLOSE_FLAG_POSTQUERY_ATTRIB
        ///  flag in the SMB2 CLOSE Request was set, this field
        ///  MUST be set to the value that is returned by the attribute
        ///  query. If the flag is not set, this field MUST  be
        ///  set to zero.
        /// </summary>
        public FILETIME LastAccessTime;
        
        /// <summary>
        ///  A FILETIME, as specified in FILETIME.The time when data
        ///  was last written to the file.  If the SMB2_CLOSE_FLAG_POSTQUERY_ATTRIB
        ///  flag in the SMB2 CLOSE Request was set, this field
        ///  MUST be set to the value that is returned by the attribute
        ///  query. If the flag is not set, this field MUST  be
        ///  set to zero.
        /// </summary>
        public FILETIME LastWriteTime;
        
        /// <summary>
        ///  A FILETIME, as specified in FILETIME.The time when the
        ///  file was last modified.  If the SMB2_CLOSE_FLAG_POSTQUERY_ATTRIB
        ///  flag in the SMB2 CLOSE Request was set, this field
        ///  MUST be set to the value that is returned by the attribute
        ///  query. If the flag is not set, this field MUST  be
        ///  set to zero.
        /// </summary>
        public FILETIME ChangeTime;
        
        /// <summary>
        ///  The size, in bytes,  of the data that is allocated to
        ///  the file.  If the SMB2_CLOSE_FLAG_POSTQUERY_ATTRIB
        ///  flag in the SMB2 CLOSE Request was set, this field
        ///  MUST be set to the value that is returned by the attribute
        ///  query. If the flag is not set, this field MUST  be
        ///  set to zero.
        /// </summary>
        public ulong AllocationSize;
        
        /// <summary>
        ///  The size, in bytes, of the file.  If the SMB2_CLOSE_FLAG_POSTQUERY_ATTRIB
        ///  flag in the SMB2 CLOSE Request was set, this field
        ///  MUST be set to the value that is returned by the attribute
        ///  query. If the flag is not set, this field MUST  be
        ///  set to zero.
        /// </summary>
        public ulong EndofFile;
        
        /// <summary>
        ///  The attributes of the file.  If the SMB2_CLOSE_FLAG_POSTQUERY_ATTRIB
        ///  flag in the SMB2 CLOSE Request was set, this field
        ///  MUST be set to the value that is returned by the attribute
        ///  query. If the flag is not set, this field MUST  be
        ///  set to zero.  For more information about valid flags,
        ///  see [MS-FSCC] section 2.6.
        /// </summary>
        public uint FileAttributes;

        public int GetSize()
        {
            return StructureSize;
        }

        public void ToSMB2Packet(byte[] buf, int offset)
        {
        }
    }
 
    [Flags()]
    public enum CLOSE_Response_Flags_Values : ushort {
        
        /// <summary>
        ///  If set, the client MUST use the attribute fields in
        ///  the response.  If not set, the client MUST NOT use
        ///  the attribute fields that are returned in the response.
        /// </summary>
        CLOSE_FLAG_POSTQUERY_ATTRIB = 0x0001,
    }
    
    public enum CLOSE_Response_Reserved_Values : uint {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    /// <summary>
    ///  The SMB2 READ Request packet is sent by the client to
    ///  request a read operation on the file that is specified
    ///  by the FileId.  This request is composed of an SMB2
    ///  header, as specified in section , followed by this
    ///  request structure:
    /// </summary>
    public struct READ_Request : ISMB2Request{
        
        /// <summary>
        ///  The client MUST set this field to 49, indicating the
        ///  size of the request structure, not including the header.
        ///   The client MUST set it to this value regardless of
        ///  how long Buffer[] actually is in the request being
        ///  sent.
        /// </summary>
        public ushort StructureSize;
        
        /// <summary>
        ///  The requested offset, in bytes, to place the data read
        ///  in the SMB2 READ Response.  This value is provided
        ///  to optimize data placement on the client and is not
        ///  binding on the server.
        /// </summary>
        public byte Padding;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The client MUST set this field to 0, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public READ_Request_Reserved_Values Reserved;
        
        /// <summary>
        ///  The length, in bytes, of the data to read from the specified
        ///  file or pipe.
        /// </summary>
        public uint Length;
        
        /// <summary>
        ///  The offset, in bytes, into the file from which the data
        ///  MUST be read.
        /// </summary>
        public ulong Offset;
        
        /// <summary>
        ///  The SMB2_FILEID, as specified in section .The identifier
        ///  of the file or pipe on which to perform the read.
        /// </summary>
        public FILEID FileId;
        
        /// <summary>
        ///  The minimum number of bytes to be read for this operation
        ///  to be successful.  If fewer than the minimum number
        ///  of bytes are read by the server, the server MUST return
        ///  an error rather than the bytes read.
        /// </summary>
        public uint MinimumCount;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The client MUST set this field to 0, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public Channel_Values Channel;
        
        /// <summary>
        ///  The number of subsequent bytes that the client intends
        ///  to read from the file after this operation completes.
        ///   This value is provided to facilitate read-ahead caching,
        ///  and is not binding on the server.
        /// </summary>
        public uint RemainingBytes;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///  The client MUST set this field to 0, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public ReadChannelInfoOffset_Values ReadChannelInfoOffset;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The client MUST set this field to 0, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public ReadChannelInfoLength_Values ReadChannelInfoLength;
        
        /// <summary>
        ///  A variable-length buffer that contains the read channel
        ///  information, as described by ReadChannelInfoOffset
        ///  and ReadChannelInfoLength. Unused at present. The client
        ///  MUST set one byte of this field to 0, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public byte[] Buffer;

        public byte[] ToBytes()
        {
            byte[] requestBytes;
            byte[][] fieldList = new byte[12][]{BitConverter.GetBytes(StructureSize), new byte[]{Padding, (byte)Reserved},
                                                BitConverter.GetBytes(Length), BitConverter.GetBytes(Offset),
                                                BitConverter.GetBytes(FileId.Persistent), BitConverter.GetBytes(FileId.Volatile),
                                                BitConverter.GetBytes(MinimumCount), BitConverter.GetBytes((uint)Channel),
                                                BitConverter.GetBytes(RemainingBytes), BitConverter.GetBytes((ushort)ReadChannelInfoOffset),
                                                BitConverter.GetBytes((ushort)ReadChannelInfoLength), Buffer};

            requestBytes = Smb2MessageUtils.MergeByteArray(GetSize(), fieldList);
            return requestBytes;
        }

        public int GetSize()
        {
            return 48 + Buffer.Length;
        }
    }

    
    public enum READ_Request_Reserved_Values : byte {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum Channel_Values : uint {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum ReadChannelInfoOffset_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum ReadChannelInfoLength_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    /// <summary>
    ///  The SMB2 Packet Header - SYNC packet is the header of
    ///  all SMB 2.0 Protocol packets. If the SMB2_FLAGS_ASYNC_COMMAND
    ///  is not set in Flags, the header takes the following
    ///  form:
    /// </summary>
    public class Packet_Header : ISMB2Request, ISMB2Response {
        
        /// <summary>
        ///  The protocol identifier.  The value must be (in network
        ///  order) 0xFE, 'S', 'M', and 'B'.
        /// </summary>
        public byte[] ProtocolId;
        
        /// <summary>
        ///  This MUST be set to 64, which is the size, in bytes,
        ///  of the SMB2 header structure.
        /// </summary>
        public ushort StructureSize;
        
        /// <summary>
        ///  Unused at the present, and MUST be treated as reserved.
        ///   The sender MUST set this to 0, and the receiver MUST
        ///  ignore it.
        /// </summary>
        public Epoch_Values Epoch;
        
        /// <summary>
        ///  The status code for the response.  The client MUST set
        ///  this to 0 and the server MUST ignore it on receipt.
        ///   For a list of valid status codes, see [MS-ERREF] section
        ///  4.
        /// </summary>
        public int Status;
        
        /// <summary>
        ///  The command code of this packet.  This field MUST contain
        ///  one of the following valid commands:
        /// </summary>
        public Command_Values Command;
        
        /// <summary>
        ///  On a request, this field indicates the number of credits
        ///  the client is requesting.  On a response, it indicates
        ///  the number of credits that are granted to the client.
        /// </summary>
        public ushort Credit;
        
        /// <summary>
        ///  A Flags field, which indicates how the operation must
        ///  be processed.  This field MUST be constructed by using
        ///  the following values:
        /// </summary>
        public Packet_Header_Flags_Values Flags;
        
        /// <summary>
        ///  For a compounded request, this value MUST be set to
        ///  the offset, in bytes, from the beginning of this SMB
        ///  2.0 Protocol header to the start of the subsequent
        ///  quad word aligned SMB 2.0 Protocol header.  If this
        ///  is not a compounded request, this value MUST be 0.
        /// </summary>
        public uint NextCommand;
        
        /// <summary>
        ///  A value that identifies a message request and response
        ///  uniquely across all messages that are sent on the same
        ///  SMB 2.0 Protocol transport connection.
        /// </summary>
        public ulong MessageId;
        
        /// <summary>
        ///  The client-side identification of the process that is
        ///  issuing the request.
        /// </summary>
        public uint ProcessId;
        
        /// <summary>
        ///  Uniquely identifies the tree connect for the command.
        ///   This MUST be 0 for the SMB2 TREE_CONNECT request.
        ///    The TreeId MAY be any unsigned 32-bit integer that
        ///  is received from a previous SMB2 TREE_CONNECT response.
        ///   The following SMB 2.0 Protocol commands do not require
        ///  the TreeId to be set to a nonzero value received from
        ///  a previous SMB2 TREE_CONNECT response.  TreeId SHOULD
        ///  be set to 0 for the following commands:SMB2 NEGOTIATE
        ///  requestSMB2 NEGOTIATE response SMB2 SESSION_SETUP requestSMB2
        ///  SESSION_SETUP responseSMB2 LOGOFF requestSMB2 LOGOFF
        ///  responseSMB2 ECHO requestSMB2 ECHO response
        /// </summary>
        public uint TreeId;
        
        /// <summary>
        ///  Uniquely identifies the established session for the
        ///  command.  This MUST be 0 for requests that do not have
        ///  a user context that is associated with them.  This
        ///  MUST be 0 for the first SMB2 SESSION_SETUP request
        ///  for a specified security principal.  The following
        ///  SMB 2.0 Protocol commands do not require the SessionId
        ///  to be set to a nonzero value received from a previous
        ///  SMB2 SESSION_SETUP response.  SessionId SHOULD be set
        ///  to 0 for the following commands:SMB2 NEGOTIATE requestSMB2
        ///  NEGOTIATE responseSMB2 SESSION_SETUP requestSMB2 ECHO
        ///  requestSMB2 ECHO response
        /// </summary>
        public ulong SessionId;
        
        /// <summary>
        ///  The 16-byte signature of the message, if SMB2_FLAGS_SIGNED
        ///  is set in the Flags field of the SMB 2.0 Protocol header.
        ///   If the message is not signed, this field MUST be 0.
        /// </summary>
        public byte[] Signature;

        public byte[]  ToBytes()
        {
            byte[] headerBytes;
            byte[][] fieldList = new byte[13][]{ProtocolId, BitConverter.GetBytes(StructureSize),
                                                BitConverter.GetBytes((ushort)Epoch), BitConverter.GetBytes(Status),
                                                BitConverter.GetBytes((ushort)Command), BitConverter.GetBytes(Credit),
                                                BitConverter.GetBytes((uint)Flags), BitConverter.GetBytes(NextCommand),
                                                BitConverter.GetBytes(MessageId), BitConverter.GetBytes(ProcessId),
                                                BitConverter.GetBytes(TreeId), BitConverter.GetBytes(SessionId),
                                                Signature};

            headerBytes = Smb2MessageUtils.MergeByteArray(64, fieldList);
            return headerBytes;
        }

        public int GetSize()
        {
            return StructureSize;
        }

        public void ToSMB2Packet(byte[] buf, int offset)
        {
            int index = offset;
            ProtocolId = new byte[4] { buf[index], buf[index+1], buf[index+2], buf[index+3] };
            index = index + 4;
            StructureSize = BitConverter.ToUInt16(buf, index);
            index = index + 2;
            Epoch = (Epoch_Values)(BitConverter.ToUInt16(buf, index));
            index = index + 2;
            Status = BitConverter.ToInt32(buf, index);
            index = index + 4;
            Command = (Command_Values)(BitConverter.ToUInt16(buf, index));
            index = index + 2;
            Credit = BitConverter.ToUInt16(buf, index);
            index = index + 2;
            Flags = (Packet_Header_Flags_Values)(BitConverter.ToInt32(buf, index));
            index = index + 4;
            NextCommand = BitConverter.ToUInt32(buf, index);
            index = index + 4;
            MessageId = BitConverter.ToUInt64(buf, index);
            index = index + 8;
            ProcessId = BitConverter.ToUInt32(buf, index);
            index = index + 4;
            TreeId = BitConverter.ToUInt32(buf, index);
            index = index + 4;
            SessionId = BitConverter.ToUInt64(buf, index);
            index = index + 8;
            Signature = new byte[16];
            Array.Copy(buf, index, Signature, 0, 16);
        }

    }
    
    public enum Packet_Header_StructureSize_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 64,
    }
    
    public enum Epoch_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    [Flags()]
    public enum Command_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        NEGOTIATE = 0x0000,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        SESSION_SETUP = 0x0001,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        LOGOFF = 0x0002,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        TREE_CONNECT = 0x0003,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        TREE_DISCONNECT = 0x0004,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        CREATE = 0x0005,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        CLOSE = 0x0006,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        FLUSH = 0x0007,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        READ = 0x0008,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        WRITE = 0x0009,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        LOCK = 0x000A,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        IOCTL = 0x000B,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        CANCEL = 0x000C,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        ECHO = 0x000D,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        QUERY_DIRECTORY = 0x000E,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        CHANGE_NOTIFY = 0x000F,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        QUERY_INFO = 0x0010,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        SET_INFO = 0x0011,
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        OPLOCK_BREAK = 0x0012,
    }
    
    [Flags()]
    public enum Packet_Header_Flags_Values : uint {
        
        /// <summary>
        ///  When set, indicates the message is a response, rather
        ///  than a request.  This MUST be set on responses sent
        ///  from the server to the client and MUST NOT be set on
        ///  requests sent from the client to the server.
        /// </summary>
        FLAGS_SERVER_TO_REDIR = 0x00000001,
        
        /// <summary>
        ///  When set, indicates that this is an asynchronously processed
        ///  command. For asynchronous requests, see section . For
        ///  asynchronous responses, see section .
        /// </summary>
        FLAGS_ASYNC_COMMAND = 0x00000002,
        
        /// <summary>
        ///  When set, indicates that this command is a related operation
        ///  in a compounded request chain.
        /// </summary>
        FLAGS_RELATED_OPERATIONS = 0x00000004,
        
        /// <summary>
        ///  When set, indicates that this packet has been signed.
        /// </summary>
        FLAGS_SIGNED = 0x00000008,
        
        /// <summary>
        ///  When set, indicates that this command is a Distributed
        ///  File System (DFS) operation.
        /// </summary>
        FLAGS_DFS_OPERATIONS = 0x10000000,
    }
    
    /// <summary>
    ///  The SMB2 ERROR Response packet is sent by the server
    ///  to respond to a request that has failed or encountered
    ///  an error.  This response is composed of an SMB2 Packet
    ///  Header followed by this response structure:
    /// </summary>
    public struct ERROR_Response_packet {
        
        /// <summary>
        ///  The server MUST set this field to 9, indicating the
        ///  size of the response structure, not including the header.
        ///   The server MUST set it to this value regardless of
        ///  how long ErrorData[] actually is in the request being
        ///  sent.
        /// </summary>
        public ERROR_Response_packet_StructureSize_Values StructureSize;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MUST set this to 0, and the client MUST
        ///  ignore it on receipt.
        /// </summary>
        public ERROR_Response_packet_Reserved_Values Reserved;
        
        /// <summary>
        ///  The number of bytes of data contained in ErrorData[].
        /// </summary>
        public uint ByteCount;
        
        /// <summary>
        ///  A variable-length data field that contains extended
        ///  error information. If the Status code in the header
        ///  of that response is set to STATUS_STOPPED_ON_SYMLINK,
        ///  this field MUST contain a Symbolic Link Error Response
        ///  as specified in section . If the ByteCount field is
        ///  zero then this one-byte field MAY be set to any value
        ///  and the client MUST ignore it on receipt. 
        /// </summary>
        public byte[] ErrorData;
    }
    
    public enum ERROR_Response_packet_StructureSize_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 9,
    }
    
    public enum ERROR_Response_packet_Reserved_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    /// <summary>
    ///  The SMB2 READ Response packet is sent in response to
    ///  an SMB2 READ Request packet.  This response is composed
    ///  of an SMB2 header, as specified in section , followed
    ///  by this response structure:
    /// </summary>
    public struct READ_Response :ISMB2Response {
        
        /// <summary>
        ///  The server MUST set this field to 17, indicating the
        ///  size of the response structure, not including the header.
        ///   This value MUST be used regardless of how large Buffer[]
        ///  is in the actual response.
        /// </summary>
        public ushort StructureSize;
        
        /// <summary>
        ///  The offset, in bytes, from the beginning of the header
        ///  to the data read being returned in this response.
        /// </summary>
        public byte DataOffset;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MUST set this to 0, and the client MUST
        ///  ignore it on receipt.
        /// </summary>
        public READ_Response_Reserved_Values Reserved;
        
        /// <summary>
        ///  The length, in bytes, of the data read being returned
        ///  in this response. The minimum size is 1 byte.
        /// </summary>
        public uint DataLength;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MUST set this field to 0, and the client
        ///  MUST ignore it on receipt.
        /// </summary>
        public DataRemaining_Values DataRemaining;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MUST set this to 0, and the client MUST
        ///  ignore it on receipt.
        /// </summary>
        public READ_Response_Reserved2_Values Reserved2;
        
        /// <summary>
        ///  A variable-length buffer that contains the data read
        ///  for the response, as described by DataOffset and DataLength.
        ///  The minimum length is 1 byte. If 0 bytes are returned
        ///  from the file system, the server MUST send a failure
        ///  response with status == STATUS_END_OF_FILE.
        /// </summary>
        public byte[] Buffer;

        public int GetSize()
        {
            return 16+Buffer.Length;
        }

        public void ToSMB2Packet(byte[] buf, int offset)
        {
            int index = offset + 2;
            DataOffset = buf[index];
            index = index + 2;
            DataLength = BitConverter.ToUInt32(buf, index);
            Buffer = new byte[DataLength];
            Array.Copy(buf, DataOffset, Buffer, 0, DataLength);
        }
    }
 
    public enum READ_Response_Reserved_Values : byte {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum DataRemaining_Values : uint {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum READ_Response_Reserved2_Values : uint {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    /// <summary>
    ///  The SMB2 WRITE Request packet is sent by the client
    ///  to write data to the file or named pipe on the server.
    ///   This request is composed of an SMB2 header, as specified
    ///  in section , followed by this request structure:
    /// </summary>
    public struct WRITE_Request : ISMB2Request{
        
        /// <summary>
        ///  The server MUST set this field to 49, indicating the
        ///  size of the request structure, not including the header.
        ///   The server MUST set it to this value regardless of
        ///  how long Buffer[] actually is in the request being
        ///  sent.
        /// </summary>
        public ushort StructureSize;
        
        /// <summary>
        ///  The offset, in bytes, from the header where the data
        ///  to be written is located in the request.
        /// </summary>
        public ushort DataOffset;
        
        /// <summary>
        ///  The length of the data being written, in bytes.
        /// </summary>
        public uint Length;
        
        /// <summary>
        ///  The offset, in bytes, of where to write the data in
        ///  the destination file.
        /// </summary>
        public ulong Offset;
        
        /// <summary>
        ///  SMB2_FILEID, as specified in section . 						The identifier
        ///  of the file or pipe on which to perform the write.
        /// </summary>
        public FILEID FileId;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The client MUST set this field to 0, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public WRITE_Request_Channel_Values Channel;
        
        /// <summary>
        ///  The number of subsequent bytes the client intends to
        ///  write to the file after this operation completes. 
        ///  This value is provided to facilitate write caching
        ///  and is not binding on the server.
        /// </summary>
        public uint RemainingBytes;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The client MUST set this field to 0, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public WriteChannelInfoOffset_Values WriteChannelInfoOffset;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The client MUST set this field to 0, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public WriteChannelInfoLength_Values WriteChannelInfoLength;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The client MUST set this field to 0, and the server
        ///  MUST ignore it on receipt.
        /// </summary>
        public WRITE_Request_Flags_Values Flags;
        
        /// <summary>
        ///  A variable-length buffer that contains the data to write
        ///  and the write channel information, as described by
        ///  DataOffset, Length, WriteChannelInfoOffset, and WriteChannelInfoLength.
        /// </summary>
        public byte[] Buffer;

        public byte[] ToBytes()
        {
            byte[] requestBytes;
            byte[][] fieldList = new byte[12][]{BitConverter.GetBytes(StructureSize), BitConverter.GetBytes(DataOffset),
                                                BitConverter.GetBytes(Length), BitConverter.GetBytes(Offset),
                                                BitConverter.GetBytes(FileId.Persistent), BitConverter.GetBytes(FileId.Volatile),
                                                BitConverter.GetBytes((uint)Channel), BitConverter.GetBytes(RemainingBytes),
                                                BitConverter.GetBytes((ushort)WriteChannelInfoOffset), BitConverter.GetBytes((ushort)WriteChannelInfoLength),
                                                BitConverter.GetBytes((uint)Flags), Buffer};

            requestBytes = Smb2MessageUtils.MergeByteArray(GetSize(), fieldList);
            return requestBytes;
        }

        public int GetSize()
        {
            return 48 + Buffer.Length;
        }
    }
    
   
    public enum WRITE_Request_Channel_Values : uint {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum WriteChannelInfoOffset_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum WriteChannelInfoLength_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum WRITE_Request_Flags_Values : uint {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    /// <summary>
    ///  The SMB2 WRITE Response packet is sent by the server
    ///  to write data to the file or named pipe on the server.
    ///   This response is composed of an SMB2 header, as specified
    ///  in section , followed by this response structure:
    /// </summary>
    public struct WRITE_Response : ISMB2Response {
        
        /// <summary>
        ///  The server MUST set this field to 17, indicating the
        ///  size of the response structure, not including the header.
        ///   This value MUST be used regardless of how large Buffer[]
        ///  is in the actual response.
        /// </summary>
        public ushort StructureSize;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MUST set this to 0, and the client MUST
        ///  ignore it on receipt.
        /// </summary>
        public WRITE_Response_Reserved_Values Reserved;
        
        /// <summary>
        ///  The number of bytes written.
        /// </summary>
        public uint Count;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MUST set this to 0, and the client MUST
        ///  ignore it on receipt.
        /// </summary>
        public Remaining_Values Remaining;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MUST set this to 0, and the client MUST
        ///  ignore it on receipt.
        /// </summary>
        public WRITE_Response_WriteChannelInfoOffset_Values WriteChannelInfoOffset;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        ///   The server MUST set this to 0, and the client MUST
        ///  ignore it on receipt.
        /// </summary>
        public WRITE_Response_WriteChannelInfoLength_Values WriteChannelInfoLength;
        
        /// <summary>
        ///  Unused at the present and MUST be treated as reserved.
        /// </summary>
        public byte[] Buffer;

        public int GetSize()
        {
            return 16;
        }

        public void ToSMB2Packet(byte[] buf, int offset)
        {
        }
    }
    
    public enum WRITE_Response_StructureSize_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 17,
    }
    
    public enum WRITE_Response_Reserved_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum Remaining_Values : uint {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum WRITE_Response_WriteChannelInfoOffset_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }
    
    public enum WRITE_Response_WriteChannelInfoLength_Values : ushort {
        
        /// <summary>
        ///  Possible value.
        /// </summary>
        V1 = 0,
    }

    

}
