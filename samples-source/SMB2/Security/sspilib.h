/*=====================================================================

File:        SSPILib.h

---------------------------------------------------------------------

This file is part of the Microsoft MSDN Code Samples.

Copyright (C) Microsoft Corporation 2002.  All rights reserved.

This source code is intended only as a supplement to Microsoft
Development Tools and/or on-line documentation.  See these other
materials for detailed information regarding Microsoft code samples.

THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
PARTICULAR PURPOSE.

=====================================================================*/

#pragma once

// ------------------------------ ** ------------------------------
//
//                             Includes
//
// ------------------------------ ** ------------------------------

#include "Exception.h"

#include "wincrypt.h" // Certificate and Certificate Store functions
#include "Schnlsp.h"  // schannel security provider structures and functions


// ------------------------------ ** ------------------------------
//
//                         Using Statements
//
// ------------------------------ ** ------------------------------

using namespace System;
using namespace System::Collections::Specialized;
using namespace System::Security::Permissions;




// ------------------------------ ** ------------------------------
//
//                             Constants
//
// ------------------------------ ** ------------------------------

#define TOKENBUFSIZE                    12288
#define szNegotiatePackageName          __TEXT("Negotiate")
#define szKerberosPackageName           __TEXT("Kerberos")
#define szNTLMPackageName               __TEXT("NTLM")
#define szSchannelPackageName           __TEXT("Schannel")
#define szMYCertSystemStore             __TEXT("MY")
#define szCACertSystemStore             __TEXT("CA")
#define szROOTCertSystemStore           __TEXT("ROOT")

#define STANDARD_CONTEXT_ATTRIBUTES        ISC_REQ_CONFIDENTIALITY | ISC_REQ_REPLAY_DETECT | ISC_REQ_SEQUENCE_DETECT | ISC_REQ_CONNECTION

namespace SMB2
{
namespace SSPI
{
    // ------------------------------ ** ------------------------------
    //
    //                            Certificate Store
    //
    // ------------------------------ ** ------------------------------
    /// <summary>
    /// A class to open/close the certificate store and find the certificate in the store.
    /// </summary>
    public ref class CertStore : public IDisposable
    {
    public:
 
        /// <summary>
        /// An enumeration type which defines the type of stores.
        /// </summary>
        enum class CertStoreProvider 
        {
            /// <summary>
            /// Initializes the store with certificates, CRLs, and CTLs read from a specified open file.
            /// </summary>
            File, 
            /// <summary>
            /// Creates a certificate store in cached memory. 
            /// </summary>
            Memory, 
            /// <summary>
            /// Initializes the store with certificates, CRLs, and CTLs from the specified system store.
            /// </summary>
            System, 
            /// <summary>
            /// Initializes the store with certificates, CRLs, and CTLs from a physical registry store.
            /// </summary>
            Registry
        }; 

        /// <summary>
        /// An enumeration type which defines the type of stores.
        /// </summary>
        enum class CertStoreLocation 
        {
            //system stores are at the following registry location: 

            /// <summary>
            /// HKEY_CURRENT_USER
            ///     Software
            ///         Microsoft
            ///             SystemCertificates
            /// </summary>
            CurrentUser,

            /// <summary>
            /// HKEY_LOCAL_MACHINE
            ///     Software
            ///         Microsoft
            ///             SystemCertificates
            /// </summary>
            LocalMachine, 

            /// <summary>
            /// HKEY_CURRENT_USER
            ///     Software
            ///         Microsoft
            ///             Cryptography
            ///                 Services
            ///                     ServiceName
            ///                         SystemCertificates
            /// </summary>
            CurrentService,

            /// <summary>
            /// HKEY_LOCAL_MACHINE
            ///     Software
            ///         Policy
            ///             Microsoft
            ///                 SystemCertificates
            /// </summary>
            CurrentGroupPolicy
        }; 

        /// <summary>
        /// An enumeration type which defines the type of stores.
        /// </summary>
        enum class RegKey 
        {
            /// <summary>
            /// HKEY_CLASSES_ROOT
            /// </summary>
            ClassesRoot, 

            /// <summary>
            /// HKEY_CURRENT_USER
            /// </summary>
            CurrentUser, 

            /// <summary>
            /// HKEY_LOCAL_MACHINE
            /// </summary>
            LocalMachine, 

            /// <summary>
            /// HKEY_USERS
            /// </summary>
            Users
        }; 

    public:
        /// <summary>
        /// default constructor with package and credential type
        /// </summary>
        CertStore()
        {
            this->certStoreHandle = NULL;
            this->certContext = NULL;
            disposed = false;
        }


        /// <summary>
        /// destructor cleans up all resources
        /// </summary>
        ~CertStore()
        {
            if (!this->disposed)
            {
                this->!CertStore();
                this->disposed = true;
            }
        }


        /// <summary>
        /// Finalizer cleans up unmanaged resources
        /// destructor or garbage collector will
        /// clean up managed resources
        /// </summary>
        !CertStore()
        {
            //close a certificate store handle and reduces the ref-cnt on the store.
            if (this->certStoreHandle != NULL)
            {
                //Forces the freeing of memory for all contexts associated with the store
                ::CertCloseStore(certStoreHandle, CERT_CLOSE_STORE_FORCE_FLAG);
            }
             //closes a handle to the specified registry key.
            if (this->regSubKey != NULL)
            {
                //Forces to closes the specified registry key.
                RegCloseKey(regSubKey);
            }
            this->certStoreHandle = NULL;
            this->certContext = NULL;
            this->regSubKey = NULL;
            disposed = true;
        }


    public:
        /// <summary>
        /// Internal unmanaged certificate store handle.
        /// </summary>
        property IntPtr CertStoreHandle
        {
            IntPtr get()
            {
                if(this->disposed)
                {
                    throw gcnew ObjectDisposedException(this->GetType()->Name);
                }

                return IntPtr(this->certStoreHandle);
            }
        };

        /// <summary>
        /// Internal unmanaged certificate context handle.
        /// </summary>
        property IntPtr CertContextHandle
        {
            IntPtr get()
            {
                if(this->disposed)
                {
                    throw gcnew ObjectDisposedException(this->GetType()->Name);
                }

                return IntPtr((void *)this->certContext);
            }
        };


        /// <summary>
        /// Internal unmanaged registry subkey handle.
        /// </summary>
        property IntPtr RegSubKey
        {
            IntPtr get()
            {
                if(this->disposed)
                {
                    throw gcnew ObjectDisposedException(this->GetType()->Name);
                }

                return IntPtr((void *)this->regSubKey);
            }
        };

    private:
        // member variables
        HCERTSTORE      certStoreHandle;
        PCCERT_CONTEXT  certContext;
        HKEY            regSubKey;
        bool disposed;

    internal:
        // methods
        /// <summary>
        /// This function converts managed byte array to a TCHAR string.
        /// </summary>
        /// <param name="inArray">The managed byte array to be converted</param>
        /// <returns>converted TCHAR string.</returns>
        TCHAR * ArrayToChar(array<byte>^ inArray)
        {
            ULONG arrLen = inArray->Length;

            // allocate new memory for output TCHAR string
            TCHAR * outStr = new TCHAR[arrLen + 2];    
            
            // pin array
            pin_ptr<Byte> pArray = &inArray[0];
            
            // copy to destination
            CopyMemory(outStr, pArray, arrLen);

            return outStr;
        }

    public:
        // methods
        /// <summary>
        /// opens a certificate store by using a specified store provider type and location
        /// </summary>
        /// <param name="certProv">The certificate provider</param>
        /// <param name="certLoc">The certificate store location</param>
        void CertOpenStore(CertStoreProvider certProv, CertStoreLocation certLoc)
        {
            VOID *pCertSystemStore = szMYCertSystemStore;
            LPCSTR storeProvider = CERT_STORE_PROV_SYSTEM;
            DWORD storeLocation = CERT_SYSTEM_STORE_CURRENT_USER;
            
            // determine the certificate provider type
            switch(certProv)
            {
            case CertStoreProvider::File:
                storeProvider = CERT_STORE_PROV_FILE;
                break;

            case CertStoreProvider::Memory:
                storeProvider = CERT_STORE_PROV_MEMORY;
                break;

            case CertStoreProvider::System:
                storeProvider = CERT_STORE_PROV_SYSTEM;

                // determine the certificate location
                switch(certLoc)
                {
                case CertStoreLocation::LocalMachine:
                    storeLocation = CERT_SYSTEM_STORE_LOCAL_MACHINE;
                    break;

                case CertStoreLocation::CurrentUser:
                    storeLocation = CERT_SYSTEM_STORE_CURRENT_USER;
                    break;

                case CertStoreLocation::CurrentService:
                    storeLocation = CERT_SYSTEM_STORE_CURRENT_SERVICE;
                    break;

                case CertStoreLocation::CurrentGroupPolicy:
                    storeLocation = CERT_SYSTEM_STORE_CURRENT_USER_GROUP_POLICY;
                    break;

                default:
                    // may not schannel application
                    break;
                }
                break;

            case CertStoreProvider::Registry:
                storeProvider = CERT_STORE_PROV_REG;
                storeLocation = 0;
                pCertSystemStore = regSubKey;

                if(NULL == regSubKey)
                {
                    SSPIException^ ex = gcnew SSPIException("The register subkey must be opened by RegOpenKey firstly.");
                    throw ex;
                }
              break;

            default:
                // may not schannel application
                break;
            }

            
            // opens a certificate store by using a specified store provider type
            if (NULL == (certStoreHandle = ::CertOpenStore(
               storeProvider,                   // The store provider type
               0,                               // The encoding type is not needed
               NULL,                            // Use the default HCRYPTPROV
               storeLocation,                   // Set the store location in a registry location
               pCertSystemStore                 // The store name as a Unicode string
               )))
            {
                SSPIException^ ex = gcnew SSPIException("CertOpenStore failed");
                throw ex;
            }
            
        }

        /// <summary>
        /// opens a certificate store by using system store provider and location
        /// </summary>
        /// <param name="certLoc">The certificate store location</param>
        void CertOpenSysStore(CertStoreLocation certLoc)
        {
            TCHAR *pszCertSystemStore = szMYCertSystemStore;
            LPCSTR storeProvider = CERT_STORE_PROV_SYSTEM;
            DWORD storeLocation = CERT_SYSTEM_STORE_CURRENT_USER;

            // determine the certificate location
            switch(certLoc)
            {
            case CertStoreLocation::LocalMachine:
                storeLocation = CERT_SYSTEM_STORE_LOCAL_MACHINE;
                break;

            case CertStoreLocation::CurrentUser:
                storeLocation = CERT_SYSTEM_STORE_CURRENT_USER;
                break;

            case CertStoreLocation::CurrentService:
                storeLocation = CERT_SYSTEM_STORE_CURRENT_SERVICE;
                break;

            case CertStoreLocation::CurrentGroupPolicy:
                storeLocation = CERT_SYSTEM_STORE_CURRENT_USER_GROUP_POLICY;
                break;

            default:
                // may not schannel application
                break;
            }
            
            // opens a certificate store by using a specified store provider type
            if (NULL == (certStoreHandle = ::CertOpenStore(
               storeProvider,                   // The store provider type
               0,                               // The encoding type is not needed
               NULL,                            // Use the default HCRYPTPROV
               storeLocation,                   // Set the store location in a registry location
               pszCertSystemStore               // The store name as a Unicode string
               )))
            {
                SSPIException^ ex = gcnew SSPIException("CertOpenSysStore failed");
                throw ex;
            }

        }

        /// <summary>
        /// opens a certificate store by using registry store provider and location
        /// </summary>
        void CertOpenRegStore()
        {
            LPCSTR storeProvider = CERT_STORE_PROV_REG;
            
            // opens a certificate store by using a specified store provider type
            if(NULL == regSubKey)
            {
                SSPIException^ ex = gcnew SSPIException("The register subkey must be opened by RegOpenKey firstly.");
                throw ex;
            }
            // opens a certificate store by using a specified store provider type
            if (NULL == (certStoreHandle = ::CertOpenStore(
               storeProvider,                   // The store provider type
               0,                               // The encoding type is not needed
               NULL,                            // Use the default HCRYPTPROV
               0,                   // Set the store location in a registry location
               regSubKey               // The store name as a Unicode string
               )))
            {
                SSPIException^ ex = gcnew SSPIException("CertOpenRegStore failed");
                throw ex;
            }

        }

        /// <summary>
        /// finds the first or next certificate context in a certificate store that matches a search criteria 
        /// </summary>
        /// <param name="certName">The certificate name</param>
        void CertFindStoreByName(String^ certName)
        {
            // convert the string to wchar_t structure
            pin_ptr<const wchar_t> certCharName = PtrToStringChars(certName);

            if (NULL == (certContext = CertFindCertificateInStore(
                certStoreHandle,
                X509_ASN_ENCODING | PKCS_7_ASN_ENCODING,
                0,
                CERT_FIND_SUBJECT_STR, 
                certCharName, 
                NULL)))
            {
                SSPIException^ ex = gcnew SSPIException("CertFindStoreByName failed");
                throw ex;
            }

        }

        /// <summary>
        /// finds the first certificate context in a certificate store that matches a search criteria 
        /// </summary>
        /// <param name="certName">The certificate's issuer name</param>
        void CertFindStoreByIssuer(String^ certName)
        {
            // convert the string to wchar_t structure
            pin_ptr<const wchar_t> certCharName = PtrToStringChars(certName);

            if (NULL == (certContext = CertFindCertificateInStore(
                certStoreHandle,
                X509_ASN_ENCODING | PKCS_7_ASN_ENCODING,
                0,
                CERT_FIND_ISSUER_STR,
                certCharName, 
                NULL)))
            {
                SSPIException^ ex = gcnew SSPIException("CertFindStoreByIssuer failed");
                throw ex;
            }

        }

        /// <summary>
        /// finds the first certificate in the store, useful in the testcases
        /// </summary>
        void CertFindStoreAny()
        {

            if (NULL == (certContext = CertFindCertificateInStore(
                certStoreHandle,
                X509_ASN_ENCODING | PKCS_7_ASN_ENCODING,
                0,
                CERT_FIND_ANY,
                NULL, 
                NULL)))
            {
                SSPIException^ ex = gcnew SSPIException("CertFindStore failed");
                throw ex;
            }

        }

        /// <summary>
        /// closes a certificate store handle and reduces the reference count on the store
        /// </summary>
        void CertCloseStore()
        {
            //Forces the freeing of memory for all contexts associated with the store
            if(certStoreHandle != NULL)
            {
                ::CertCloseStore(certStoreHandle, CERT_CLOSE_STORE_FORCE_FLAG);
                this->certStoreHandle = NULL;
            }
        }


        //using namespace System::Security::Permissions;

        /// <summary>
        /// opens a certificate store by using registry store provider and location
        /// </summary>
        /// <param name="subKeyStr">The register subkey opened by RegOpenKeyEX or created and opened by  RegCreateKeyEX</param>
        /// <param name="regKey">A handle to an open registry key</param>
        void RegOpenKey(RegKey regKey, String ^ subKeyStr)
        {
             // convert the string to wchar_t structure
            pin_ptr<const wchar_t> chSubKey = PtrToStringChars(subKeyStr);
            LONG lRet = 0;
            HKEY hKey = HKEY_LOCAL_MACHINE;
            HKEY hRetKey = 0;
           
             // determine the predefined registry key
            switch(regKey)
            {
            case RegKey::ClassesRoot:
                hKey = HKEY_CLASSES_ROOT;
                break;

            case RegKey::CurrentUser:
                hKey = HKEY_CURRENT_USER;
                break;

            case RegKey::LocalMachine:
                hKey = HKEY_LOCAL_MACHINE;
                break;

            case RegKey::Users:
                hKey = HKEY_USERS;
                break;

            default:
                break;
            }
          
            if(ERROR_SUCCESS != (lRet = RegOpenKeyEx(
                hKey, 
                chSubKey, 
                0, 
                KEY_QUERY_VALUE, 
                &hRetKey)))
            {
                SSPIException^ ex = gcnew SSPIException("RegOpenKey failed ", lRet);
                throw ex;
            }

            regSubKey = hRetKey;

        }
    };

    // ------------------------------ ** ------------------------------
    //
    //                            Credential
    //
    // ------------------------------ ** ------------------------------
    /// <summary>
    /// An abstract class to store credential used in SSPI.
    /// </summary>
    public ref class Credential abstract : public IDisposable
    {
    public:
        // types
        /// <summary>
        /// An enumeration type which defines the mode for security package.
        /// </summary>
        enum class Package 
        {
            ///<summary>
            ///Microsoft Negotiate is a security support provider (SSP) that acts as an application layer between SSPI and the other SSPs. 
            ///When an application calls into SSPI to log on to a network, it can specify an SSP to process the request. 
            ///If the application specifies Negotiate, Negotiate analyzes the request and picks the best SSP to handle the request 
            ///based on customer-configured security policy.
            ///</summary>
            Negotiate, 

            ///<summary>
            ///The Kerberos protocol defines how clients interact with a network authentication service. 
            ///Clients obtain tickets from the Kerberos Key Distribution Center (KDC), 
            ///and they present these tickets to servers when connections are established.
            ///</summary>
            Kerberos, 

            ///<summary>
            ///Windows NT Challenge/Response (NTLM) is the authentication protocol used on networks 
            ///that include systems running the Windows NT operating system and on stand-alone systems. 
            ///NTLM stands for Windows NT LAN Manager, a name chosen to distinguish this more advanced 
            ///challenge/response-based protocol from its weaker predecessor LAN Manager (LM). 
            ///</summary>
            NTLM, 

            ///<summary>
            ///Secure Channel, also known as Schannel, is a security support provider (SSP) 
            ///that contains a set of security protocols that provide identity authentication and secure, 
            ///private communication through encryption.
            ///</summary>
            Schannel
        }; 

        /// <summary>
        /// An enumeration type which defines credential type.
        /// </summary>
        enum class CredentialType 
        {
            ///<summary>
            ///Client credential
            ///</summary>
            Client, 

            ///<summary>
            ///Server credential
            ///</summary>
            Server
        };

    protected:
        /// <summary>
        /// default constructor with package and credential type
        /// </summary>
        /// <param name="package">The package mode</param>
        /// <param name="credentialType">The credential type</param>
        /// <param name="certStoreProv">The certificate provider</param>
        /// <param name="certStoreLoc">The certificate store location</param>
        /// <param name="certName">The certificate name string or the search criteria string</param>
        Credential(Package package, CredentialType credentialType, CertStore::CertStoreProvider certStoreProv, CertStore::CertStoreLocation certStoreLoc, String^ certName) 
        {
            // create a new credential handle
            // note: we're allocating the CredHandle on the C++ heap.  This needs to be
            // explicitly deleted upon Disposal
            this->credentialHandle = new CredHandle;
            this->credentialHandle->dwLower = 0; this->credentialHandle->dwUpper = 0;


            // capture the package
            this->securityPackage = package;

            // determine credential use
            ULONG fCredentialUse = 0;
            switch (credentialType)
            {
            case CredentialType::Client:
                fCredentialUse = SECPKG_CRED_OUTBOUND;
                break;

            case CredentialType::Server:
                fCredentialUse = SECPKG_CRED_INBOUND;
                break;

            default:
                SSPIException^ ex = gcnew SSPIException("Unsupported credential type");
                throw ex;
            }

            // determine package name for the call the AcquireCredentialsHandle
            TCHAR *pszPackageName           = NULL;
            PVOID pAuthData                 = NULL;
            SCHANNEL_CRED schlCred;
            HCERTSTORE rootStoreHandle      = NULL;
            DWORD enabledProto              = 0;
            PCCERT_CONTEXT paMSMQCert[1];
            IntPtr ^ certHdlPtr;
            IntPtr ^ certCxtPtr;
            CertStore certStore;
            switch (package)
            {
            case Package::Negotiate:
                pszPackageName = szNegotiatePackageName;
                break;

            case Package::Kerberos:
                pszPackageName = szKerberosPackageName;
                break;

            case Package::NTLM:
                pszPackageName = szNTLMPackageName;
                break;

            case Package::Schannel:
                pszPackageName = UNISP_NAME;
                //Schannel specific operations
                memset(&schlCred, 0, sizeof(SCHANNEL_CRED));
                pAuthData = &schlCred;
                
                //opens a certificate store by using a specified store provider type and location
                certStore.CertOpenStore(certStoreProv, certStoreLoc);

                if(credentialType == CredentialType::Client)
                {
                    //Find the certificate store by the certificate's name, e.g. name = "Simon";
                    certStore.CertFindStoreByName(certName);

                    rootStoreHandle = 0;
                    enabledProto = SP_PROT_CLIENTS;
                }
                else //if the credential type is server
                {
                    //Find the certificate store by the isser's name, e.g. issuerName = "Microsoft";
                    certStore.CertFindStoreByIssuer(certName);
                    
                    certHdlPtr = certStore.CertStoreHandle;
                    rootStoreHandle = (HCERTSTORE)(certHdlPtr->ToPointer());
                    enabledProto = SP_PROT_SERVERS;
                }
                certCxtPtr = certStore.CertContextHandle;
                // fill the schannel credential structure
                schlCred.dwVersion                  = SCHANNEL_CRED_VERSION;
                schlCred.cCreds                     = 1;
                paMSMQCert[0]                       = (PCCERT_CONTEXT)(certCxtPtr->ToPointer());
                schlCred.paCred                     = paMSMQCert;
                schlCred.hRootStore                 = rootStoreHandle; // Valid for server application only
                schlCred.cMappers                   = 0;
                schlCred.aphMappers                 = NULL;
                schlCred.cSupportedAlgs             = 0;
                schlCred.palgSupportedAlgs          = NULL;
                schlCred.grbitEnabledProtocols      = enabledProto; 
                schlCred.dwMinimumCipherStrength    = 0;
                schlCred.dwMaximumCipherStrength    = 0;
                schlCred.dwSessionLifespan          = 0;
                schlCred.dwFlags = SCH_CRED_MANUAL_CRED_VALIDATION | SCH_CRED_NO_DEFAULT_CREDS;
                schlCred.dwCredFormat = SCH_CRED_FORMAT_CERT_CONTEXT;
                break;

            default:
                SSPIException^ ex = gcnew SSPIException("Unsupported package type");
                throw ex;
            }

            // CredHandle hCredential;
            // acquire credentials handle
            TimeStamp tsExpiry = { 0, 0 };
            SECURITY_STATUS sResult = AcquireCredentialsHandle(
                NULL,                                               // [in] name of principal. NULL = principal of current security context
                pszPackageName,                                     // [in] name of package
                fCredentialUse,                                     // [in] flags indicating use.
                NULL,                                               // [in] pointer to logon identifier.  NULL = we're not specifying the id of another logon session
                pAuthData,                                          // [in] package-specific data.  NULL = default credentials for security package
                NULL,                                               // [in] pointer to GetKey function.  NULL = we're not using a callback to retrieve the credentials
                NULL,                                               // [in] value to pass to GetKey
                this->credentialHandle,                             // [out] credential handle (this must be already allocated)
                &tsExpiry                                           // [out] lifetime of the returned credentials
                );
            
            // check for errors
            if (sResult != SEC_E_OK)
            {
                SSPIException^ ex = gcnew SSPIException("AcquireCredentialsHandle failed", sResult);
                throw ex;
            }
        }

        /// <summary>
        /// destructor cleans up all resources
        /// </summary>
        ~Credential()
        {
            if (!this->disposed)
            {
                this->!Credential();
                this->disposed = true;
            }
        }

        /// <summary>
        /// finalizer cleans up unmanaged resources
        /// destructor or garbage collector will
        /// clean up managed resources
        /// </summary>
        !Credential()
        {
            // clean up
            if (this->credentialHandle != NULL)
            {
                // Ignore failure because at that time the credentialHandle is already invalid.
                FreeCredentialsHandle(
                    this->credentialHandle                            // [in] handle to free
                    );
            }

            // delete the memory allocated for the handle
            delete this->credentialHandle;
            this->credentialHandle = NULL;
        }

    public:

        // properties
        /// <summary>
        /// The security package property.
        /// </summary>
        property Package SecurityPackage
        {
            Package get()
            {
                if(this->disposed)
                {
                    throw gcnew ObjectDisposedException(this->GetType()->Name);
                }

                return this->securityPackage;
            }
        };

        /// <summary>
        /// Internal unmanaged credential handle.
        /// </summary>
        property IntPtr CredentialHandle
        {
            IntPtr get()
            {
                if(this->disposed)
                {
                    throw gcnew ObjectDisposedException(this->GetType()->Name);
                }

                return IntPtr(this->credentialHandle);
            }
        };

        /// <summary>
        /// The name of the user associated with the context.
        /// </summary>
        property String^ Name
        {
            String^ get()
            {
                if(this->disposed)
                    throw gcnew ObjectDisposedException(this->GetType()->Name);


                String^ name = String::Empty;


                // get the name associated with this credential
                SecPkgCredentials_Names secPkgCredentials_Names = { 0 };


                try
                {
                    SECURITY_STATUS sResult = QueryCredentialsAttributes(this->credentialHandle, SECPKG_CRED_ATTR_NAMES, &secPkgCredentials_Names);


                    // check for errors
                    if (sResult != SEC_E_OK)
                    {
                        SSPIException^ ex = gcnew SSPIException("QueryCredentialsAttributes failed", sResult);
                        throw ex;
                    }


                    // copy the name for the caller
                    name = gcnew String(secPkgCredentials_Names.sUserName);
                }
                finally
                {
                    // free the buffer
                    if (secPkgCredentials_Names.sUserName != NULL)
                    {
                        FreeContextBuffer(secPkgCredentials_Names.sUserName);
                        secPkgCredentials_Names.sUserName = NULL;
                    }
                }


                // return the name to the caller
                return name;
            }
        };



    private:

        // member variables
        CredHandle * credentialHandle;
        Package securityPackage;
        bool disposed;
    public:
    };


    // ------------------------------ ** ------------------------------
    //
    //                         ClientCredential
    //
    // ------------------------------ ** ------------------------------

    /// <summary>
    /// Client credential class.
    /// </summary>
    public ref class ClientCredential : public Credential
    {
    public:

        // constructor/destructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="package">The package mode</param>
        ClientCredential(Package package) : Credential(package, CredentialType::Client, CertStore::CertStoreProvider::System, CertStore::CertStoreLocation::CurrentUser, nullptr)
        {
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="package">The package mode</param>
        /// <param name="certName">The certificate name string or the search criteria string</param>
        ClientCredential(Package package, String^ certName) : Credential(package, CredentialType::Client, CertStore::CertStoreProvider::System, CertStore::CertStoreLocation::CurrentUser, certName)
        {
        }

    };





    // ------------------------------ ** ------------------------------
    //
    //                         ServerCredential
    //
    // ------------------------------ ** ------------------------------

    /// <summary>
    /// Server credential class.
    /// </summary>
    public ref class ServerCredential : public Credential
    {
    public:

        // constructor/destructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="package">The package mode</param>
        ServerCredential(Package package) : Credential(package, CredentialType::Server, CertStore::CertStoreProvider::System, CertStore::CertStoreLocation::LocalMachine, nullptr)
        {
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="package">The package mode</param>
        /// <param name="certName">The certificate name string or the search criteria string</param>
        ServerCredential(Package package, String^ certName) : Credential(package, CredentialType::Server, CertStore::CertStoreProvider::System, CertStore::CertStoreLocation::LocalMachine, certName)
        {
        }

    };





    // ------------------------------ ** ------------------------------
    //
    //                              Context
    //
    // ------------------------------ ** ------------------------------

    /// <summary>
    /// An abstract class to store context used in SSPI.
    /// </summary>
    public ref class Context abstract: public IDisposable
    {
    protected:
        // member variables

        /// <summary>
        /// Credential to be used in context
        /// </summary>
        SMB2::SSPI::Credential^ credential;
        
        /// <summary>
        /// Internal context handle
        /// </summary>
        CtxtHandle * contextHandle;
        
        /// <summary>
        /// Token
        /// </summary>
        array<Byte>^ token;
        
        /// <summary>
        /// Indicate continue process or not
        /// </summary>
        Boolean continueProcess;
        
        /// <summary>
        /// Flagged context Attributes
        /// </summary>
        ULONG contextAttribute;
        
        /// <summary>
        /// Session Key
        /// </summary>        
        array<Byte>^ sessionKey;

    private:
        // member variables
        bool disposed;

    protected:        

        // constructor/destructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="credential">The credential to be used in context.</param>
        Context(Credential^ credential)
        {

            // initialize members
            this->token = nullptr;
            this->sessionKey = nullptr;
            this->continueProcess = true;
            this->contextAttribute = 0;


            // create a new context handle
            // note: we're allocating the CtxtHandle on the C++ heap.  This needs to be
            // explicitly deleted upon Disposal
            this->contextHandle = new CtxtHandle;
            this->contextHandle->dwLower = 0; this->contextHandle->dwUpper = 0;


            // hang onto a reference to the associated credential
            this->credential = credential;
        }

        /// <summary>
        /// Destructor cleans up all resources
        /// </summary>
        ~Context()
        {
            if (!this->disposed)
            {
                if (this->credential != nullptr)
                {
                    this->credential->~Credential();
                    this->credential = nullptr;
                }
                this->!Context();
                this->disposed = true;
            }
        }

        /// <summary>
        /// Finalizer cleans up unmanaged resources
        /// destructor or garbage collector will
        /// clean up managed resources
        /// </summary>
        !Context()
        {
            // clean up
            if (this->contextHandle != NULL)
            {
                // Ignore failure because at that time the contextHandle is already invalid.
                DeleteSecurityContext(
                    this->contextHandle                                            // [in] handle to free
                    );
            }
            // delete the memory allocated for the handle
            delete this->contextHandle;
            this->contextHandle = NULL;
        }

    public:

        // methods
        /// <summary>
        /// This takes the given byte array, signs it, and returns another
        /// byte array containing the original message and signature.
        /// the format of the returned byte array is as follows:
        /// |MESSAGE_LENGTH|MESSAGE|SIGNATURE|
        /// </summary>
        /// <param name="msg">Original message to be signed.</param>
        /// <returns>Signed message and signature.</returns>
        array<Byte>^ SignMessage(array<Byte>^ msg)
        {
            if(this->disposed)
                throw gcnew ObjectDisposedException(this->GetType()->Name);

            if(credential->SecurityPackage == SMB2::SSPI::Credential::Package::Schannel)
            {
                SSPIException^ ex = gcnew SSPIException("Context::SignMessage is not supported by Schannel");
                throw ex;
            }
            // get the size of the message
            DWORD cbMsg = msg->Length;


            // get the size of the message length
            DWORD cbMsgLength = sizeof(cbMsg);


            // get the maximum size of a signature
            SecPkgContext_Sizes sizes;
            QueryContextAttributes(this->contextHandle, SECPKG_ATTR_SIZES, &sizes);

            // allocate space for the signed message
            array<Byte>^ signedMsg = gcnew array<Byte>(cbMsgLength + cbMsg + sizes.cbMaxSignature);
            pin_ptr<Byte> pSignedMsg = &signedMsg[0]; 

            // copy the message length and message into the signed message buffer
            pin_ptr<Byte> pMsg = &msg[0];
            CopyMemory(pSignedMsg, &cbMsg, cbMsgLength);
            CopyMemory(pSignedMsg+cbMsgLength, pMsg, cbMsg);
            pMsg = nullptr;


            // prepare the message buffer
            SecBuffer rgsb[] = {
                {cbMsg,                    SECBUFFER_DATA,  pSignedMsg + cbMsgLength},
                {sizes.cbMaxSignature,    SECBUFFER_TOKEN, pSignedMsg + cbMsg + cbMsgLength},
            };
            SecBufferDesc sbd = {SECBUFFER_VERSION, sizeof rgsb / sizeof *rgsb, rgsb};


            // sign the message
            SECURITY_STATUS sResult = MakeSignature(
                this->contextHandle,                        // context to use
                0,                                          // quality of protection
                &sbd,                                       // message to sign
                0                                           // message sequence number
                );


            // release pinned objects
            pSignedMsg = nullptr;

            // check for errors
            if (sResult != SEC_E_OK)
            {
                SSPIException^ ex = gcnew SSPIException("Context::SignMessage: MakeSignature failed", sResult);
                throw ex;
            }


            // return signed message to caller
            return signedMsg;
        }

        /// <summary>
        /// this takes the given byte array and verifies it using the SSPI
        /// VerifySignature method.  the given byte array is assumed to be of the format:
        /// |MESSAGE_LENGTH|MESSAGE|SIGNATURE|
        /// </summary>
        /// <param name="msg">Signed message to be verified</param>
        /// <returns>Verified message</returns>
        array<Byte>^ VerifyMessage(array<Byte>^ msg)
        {
            // this takes the given byte array and verifies it using the SSPI
            // VerifySignature method.  the given byte array is assumed to be of the format:
            // |MESSAGE_LENGTH|MESSAGE|SIGNATURE|

            if(this->disposed)
                throw gcnew ObjectDisposedException(this->GetType()->Name);
            
            // allocate a backup array to save the original input message
            array<Byte> ^ signedMsg = gcnew array<Byte> (msg->Length);

            // copy the input message to the destination
            Array::Copy(msg, signedMsg, msg->Length);

            // get a pinned reference to parameters appropriate to call unmanaged methods
            pin_ptr<Byte> pSignedMsg = &signedMsg[0]; 

            if(credential->SecurityPackage == SMB2::SSPI::Credential::Package::Schannel)
            {
                SSPIException^ ex = gcnew SSPIException("Context::VerifyMessage is not supported by Schannel");
                throw ex;
            }



            // get the size of the message length
            DWORD cbMsgLength = sizeof(DWORD);


            // get the size of the original message
            DWORD cbMsg = 0;
            CopyMemory(&cbMsg, pSignedMsg, cbMsgLength);


            // get the size of the signature
            const DWORD cbSignature = signedMsg->Length - cbMsg - cbMsgLength;


            // prepare the message buffer
            SecBuffer rgsb[] = {
                {cbMsg,                    SECBUFFER_DATA,  pSignedMsg + cbMsgLength},
                {cbSignature,            SECBUFFER_TOKEN, pSignedMsg + cbMsg + cbMsgLength},
            };
            SecBufferDesc sbd = {SECBUFFER_VERSION, sizeof rgsb / sizeof *rgsb, rgsb};


            // sign the message
            ULONG qop = 0;
            SECURITY_STATUS sResult = VerifySignature(
                this->contextHandle,                    // context to use
                &sbd,                                    // message to verify
                0,                                        // message sequence number
                &qop                                    // quality of protection
                );


            // retrieve the verified message (which is the original message passed to SignMessage)
            array<Byte>^ verifiedMsg = gcnew array<Byte>(cbMsg);
            pin_ptr<Byte> pVerifiedMsg = &verifiedMsg[0]; 
            CopyMemory(pVerifiedMsg, pSignedMsg + cbMsgLength, cbMsg);
            pVerifiedMsg = nullptr;

            // release pinned objects
            pSignedMsg = nullptr;


            // check for errors
            if (sResult != SEC_E_OK)
            {
                SSPIException^ ex = gcnew SSPIException("Context::VerifyMessage: VerifySignature failed", sResult);
                throw ex;
            }


            // return the verified message to the caller
            return verifiedMsg;
        }

        /// <summary>
        /// this takes the given byte array, encrypts it, and returns
        /// another byte array containing the encrypted message.
        /// the format of the returned byte array is as follows:
        /// |MESSAGE_LENGTH|ENCRYPTED_MESSAGE|
        /// </summary>
        /// <param name="msg">Message to be encrypted</param>
        /// <returns>Encrypted message</returns>
        CA_SUPPRESS_MESSAGE("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes")
        array<Byte>^ EncryptMessage(array<Byte>^ msg)
        {
            if(this->disposed)
                throw gcnew ObjectDisposedException(this->GetType()->Name);


            // get the size of the message
            DWORD cbMsg = msg->Length;


            // get the size of the message length
            DWORD cbMsgLength = sizeof(cbMsg);


            // get the maximum size of a signature
            SecPkgContext_Sizes sizes;
            QueryContextAttributes(this->contextHandle, SECPKG_ATTR_SIZES, &sizes);


            // allocate space for the encrypted message
            array<Byte>^ encryptedMsg = gcnew array<Byte>(cbMsgLength + cbMsg + sizes.cbSecurityTrailer);
            pin_ptr<Byte> pEncryptedMsg = &encryptedMsg[0]; 


            // copy the message into the signed message buffer
            pin_ptr<Byte> pMsg = &msg[0];
            CopyMemory(pEncryptedMsg, &cbMsg, cbMsgLength);
            CopyMemory(pEncryptedMsg+cbMsgLength, pMsg, cbMsg);
            pMsg = nullptr;


            // prepare the message buffer
            SecBuffer rgsb[] = {
                {cbMsg,                        SECBUFFER_DATA,  pEncryptedMsg + cbMsgLength},
                {sizes.cbSecurityTrailer,    SECBUFFER_TOKEN, pEncryptedMsg + cbMsgLength + cbMsg},
            };
            SecBufferDesc sbd = {SECBUFFER_VERSION, sizeof rgsb / sizeof *rgsb, rgsb};


            // sign the message
            SECURITY_STATUS sResult = ::EncryptMessage(
                this->contextHandle,                        // context to use
                0,                                          // quality of protection
                &sbd,                                       // message to encrypt
                0                                           // message sequence number
                );


            // The encrypted message may have taken less space than was allocated.
            // It's important we return a correctly sized message to the caller.
            array<Byte>^ sealedMsg = gcnew array<Byte>(cbMsgLength + cbMsg + rgsb[1].cbBuffer);
            pin_ptr<Byte> pSealedMsg = &sealedMsg[0]; 
            CopyMemory(pSealedMsg, pEncryptedMsg, cbMsgLength + cbMsg + rgsb[1].cbBuffer);
            pSealedMsg = nullptr;


            // release pinned objects
            pEncryptedMsg = nullptr;


            // check for errors
            if (sResult != SEC_E_OK)
            {
                SSPIException^ ex = gcnew SSPIException("Context::EncryptMessage: EncryptMessage failed", sResult);
                throw ex;
            }


            // return signed message to caller
            return sealedMsg;
        }

        /// <summary>
        /// this takes the given byte array, decrypts it, and returns
        /// the original, unencrypted byte array.
        /// the given byte array is assumed to be of the format:
        /// |MESSAGE_LENGTH|ENCRYPTED_MESSAGE|
        /// </summary>
        /// <param name="msg">Encrypted message</param>
        /// <returns>Decrypted message</returns>
        CA_SUPPRESS_MESSAGE("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes")
        array<Byte>^ DecryptMessage(array<Byte>^ msg /*encryptedMsg*/)
        {
            if(this->disposed)
                throw gcnew ObjectDisposedException(this->GetType()->Name);
            
            // allocate a backup array to save the original input message
            array<Byte> ^ encryptedMsg = gcnew array<Byte> (msg->Length);
            
            // copy the input message to the destination
            Array::Copy(msg, encryptedMsg, msg->Length);

            // get a pinned reference to parameters to the unmanaged API
            pin_ptr<Byte> pEncryptedMsg = &encryptedMsg[0]; 

            // get the size of the message length
            DWORD cbMsgLength = sizeof(DWORD);


            // get the size of the original, unencrypted message
            DWORD cbMsg = 0;
            CopyMemory(&cbMsg, pEncryptedMsg, cbMsgLength);


            // get the size of the trailer
            const DWORD cbTrailer = encryptedMsg->Length - cbMsg - cbMsgLength;


            // prepare the message buffer
            SecBuffer rgsb[] = {
                {cbMsg,                        SECBUFFER_DATA,  pEncryptedMsg + cbMsgLength},
                {cbTrailer,                    SECBUFFER_TOKEN, pEncryptedMsg + cbMsgLength + cbMsg},
            };
            SecBufferDesc sbd = {SECBUFFER_VERSION, sizeof rgsb / sizeof *rgsb, rgsb};


            // sign the message
            ULONG qop = 0;
            CtxtHandle * hdl = this->contextHandle;
            SECURITY_STATUS sResult = ::DecryptMessage(
                hdl,                                            // context to use
                &sbd,                                           // message to decrypt
                0,                                              // expected sequence number
                &qop                                            // quality of protection
                );


            // check for errors
            if (sResult != SEC_E_OK)
            {
                SSPIException^ ex = gcnew SSPIException("Context::DecryptMessage: DecryptMessage failed", sResult);
                throw ex;
            }


            // retrieve the decrypted message
            array<Byte>^ decryptedMsg = gcnew array<Byte>(cbMsg);
            pin_ptr<Byte> pDecryptedMsg = &decryptedMsg[0]; 
            CopyMemory(pDecryptedMsg, pEncryptedMsg + cbMsgLength, cbMsg);
            pDecryptedMsg = nullptr;


            // release pinned objects
            pEncryptedMsg = nullptr;


            // return the decrypted message to the caller
            return decryptedMsg;
        }



        /// <summary>
        /// This returns the session key to be used in the security context, for both client and server side.
        /// </summary>
        /// <returns>Session key to be queried.</returns>
        array<Byte>^ QuerySessionKey(void)
        {
            if(this->disposed)
                throw gcnew ObjectDisposedException(this->GetType()->Name);
            
            // SESSION_KEY cannot be queried by schannel package
            if(credential->SecurityPackage == SMB2::SSPI::Credential::Package::Schannel)
            {
                SSPIException^ ex = gcnew SSPIException("QuerySessionKey is not supported by Schannel");
                throw ex;
            }
            
            // initialize the output session key parameter
            SecPkgContext_SessionKey ssnKey;
            memset(&ssnKey, 0, sizeof(SecPkgContext_SessionKey));
            
            // get the information about the session key
            SECURITY_STATUS sResult = QueryContextAttributes(
                this->contextHandle,        //a handle to the security context to be queried.
                SECPKG_ATTR_SESSION_KEY,    //specifies the attribute of context to be returned.
                &ssnKey                     // contains a pointer to a SecPkgContext_SessionKey structure
                );
            if(sResult != SEC_E_OK)
            {
                // SECPKG_ATTR_SESSION_KEY is Kernel mode only in XP and 2003
                // i.e. in side of QueryContextAttributes when getting session key, the caller must upgrade its privilege
                this->sessionKey = nullptr;
            }
			else // SEC_E_OK
			{
            
				// copy the session key from SecPkgContext_SessionKey structure into managed byte array
				if (ssnKey.SessionKeyLength > 0)
				{

					// alocate managed space to save the session key
					if(this->sessionKey == nullptr)
					{
						this->sessionKey = gcnew array<Byte>(ssnKey.SessionKeyLength);
					}
				    
					pin_ptr<Byte> pOutBuff = &this->sessionKey[0];    

					// pin array
					for (unsigned long i = 0;(i < ssnKey.SessionKeyLength);i++)
						this->sessionKey[i] = *((BYTE *)ssnKey.SessionKey + i);

					// free the original unmanaged space of session key, as required by the QueryContextAttributes function
					FreeContextBuffer(ssnKey.SessionKey);

					pOutBuff = nullptr;    //unpin array
				}
			}

            return this->sessionKey;
        }

    protected:

        // methods
        /// <summary>
        /// helper function to copy SecBuffer buffers into byte arrays.  This is typically
        /// used to populate this->token.
        /// </summary>
        /// <param name="outSecBuff">SecBuffer struct value to be copied.</param>
        /// <returns>Copied array of bytes.</returns>
        array<Byte>^ SecBufferToByteArray(SecBuffer &outSecBuff)
        {
            if(this->disposed)
                throw gcnew ObjectDisposedException(this->GetType()->Name);

            array<Byte>^ outBuff = nullptr;

            if (outSecBuff.cbBuffer > 0)
            {
                outBuff = gcnew array<Byte>(outSecBuff.cbBuffer);
                pin_ptr<Byte> pOutBuff = &outBuff[0];                       // pin array
                for (unsigned long i = 0;(i < outSecBuff.cbBuffer);i++)
                    pOutBuff[i] = *((BYTE *)outSecBuff.pvBuffer + i);
                pOutBuff = nullptr;                                         // unpin array
            }

            return outBuff;
        }




    public:

        // properties
        /// <summary>
        /// The credential used in context.
        /// </summary>
        property SMB2::SSPI::Credential^ Credential
        {
            SMB2::SSPI::Credential^ get()
            {
                if(this->disposed)
                {
                    throw gcnew ObjectDisposedException(this->GetType()->Name);
                }

                return credential;
            }
        }

        /// <summary>
        /// Context attributes.
        /// </summary>
        virtual property StringCollection^ ContextAttributes
        {
            StringCollection^ get()
            {
                if(this->disposed)
                {
                    throw gcnew ObjectDisposedException(this->GetType()->Name);
                }

                // only derived classes should have a valid ContextAttributes property
                return nullptr;
            }
        }

        /// <summary>
        /// The name of the user associated with the context.
        /// </summary>
        property String^ Name
        {
            String^ get()
            {
                if(this->disposed)
                    throw gcnew ObjectDisposedException(this->GetType()->Name);


                String^ name = String::Empty;


                // get the name associated with this context
                SecPkgContext_Names secPkgContext_Names = { 0 };


                try
                {
                    SECURITY_STATUS sResult = QueryContextAttributes(this->contextHandle, SECPKG_ATTR_NAMES, &secPkgContext_Names);


                    // check for errors
                    if (sResult != SEC_E_OK)
                    {
                        SSPIException^ ex = gcnew SSPIException("QueryContextAttributes failed", sResult);
                        throw ex;
                    }

                    // copy the name for the caller
                    name = gcnew String(secPkgContext_Names.sUserName);
                }
                finally
                {
                    // free the buffer
                    if (secPkgContext_Names.sUserName != NULL)
                    {
                        FreeContextBuffer(secPkgContext_Names.sUserName);
                        secPkgContext_Names.sUserName = NULL;
                    }
                }


                // return the name to the caller
                return name;
            }
        };

        /// <summary>
        /// The security package mode.
        /// </summary>
        property SMB2::SSPI::Credential::Package SecurityPackage
        {
            SMB2::SSPI::Credential::Package get()
            {
                if(this->disposed)
                    throw gcnew ObjectDisposedException(this->GetType()->Name);


                SMB2::SSPI::Credential::Package securityPackage;


                // get the SSP package associated with this context
                SecPkgContext_PackageInfo secPkgContext_PackageInfo = { 0 };


                try
                {
                    SECURITY_STATUS sResult = QueryContextAttributes(this->contextHandle, SECPKG_ATTR_PACKAGE_INFO, &secPkgContext_PackageInfo);


                    // check for errors
                    if (sResult != SEC_E_OK)
                    {
                        SSPIException^ ex = gcnew SSPIException("QueryContextAttributes failed", sResult);
                        throw ex;
                    }


                    // copy the name for the caller
                    String^ securityPackageName = gcnew String(secPkgContext_PackageInfo.PackageInfo->Name);

                    // map the name to an enumeration
                    if (String::Compare(securityPackageName, "NTLM", true, System::Globalization::CultureInfo::InvariantCulture) == 0)
                        securityPackage = SMB2::SSPI::Credential::Package::NTLM;
                    else if (String::Compare(securityPackageName, "Kerberos", true, System::Globalization::CultureInfo::InvariantCulture) == 0)
                        securityPackage = SMB2::SSPI::Credential::Package::Kerberos;
                    else if (String::Compare(securityPackageName, "Schannel", true, System::Globalization::CultureInfo::InvariantCulture) == 0)
                        securityPackage = SMB2::SSPI::Credential::Package::Kerberos;
                    else
                    {
                        SSPIException^ ex = gcnew SSPIException("Could not map security package name", sResult);
                        throw ex;
                    }
                }
                finally
                {
                    // free the buffer
                    if (secPkgContext_PackageInfo.PackageInfo != NULL)
                    {
                        if (secPkgContext_PackageInfo.PackageInfo->Name != NULL)
                        {
                            FreeContextBuffer(secPkgContext_PackageInfo.PackageInfo->Name);
                            secPkgContext_PackageInfo.PackageInfo->Name = NULL;
                        }

                        if (secPkgContext_PackageInfo.PackageInfo->Comment != NULL)
                        {
                            FreeContextBuffer(secPkgContext_PackageInfo.PackageInfo->Comment);
                            secPkgContext_PackageInfo.PackageInfo->Comment = NULL;
                        }

                        FreeContextBuffer(secPkgContext_PackageInfo.PackageInfo);
                        secPkgContext_PackageInfo.PackageInfo = NULL;
                    }
                }

                // return the name to the caller
                return securityPackage;
            }
        };

        /// <summary>
        /// The token.
        /// </summary>
        CA_SUPPRESS_MESSAGE("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")
        property array<Byte>^ Token
        {
            array<Byte>^ get()
            {
                if(this->disposed)
                {
                    throw gcnew ObjectDisposedException(this->GetType()->Name);
                }

                return this->token;
            }
        };
        /// <summary>
        /// The session key.
        /// </summary>
        CA_SUPPRESS_MESSAGE("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")
        property array<Byte>^ SessionKey
        {
            array<Byte>^ get()
            {
                if(this->disposed)
                {
                    throw gcnew ObjectDisposedException(this->GetType()->Name);
                }

                return this->sessionKey;
            }
        };

        /// <summary>
        /// Whether to continue process.
        /// </summary>
        property Boolean ContinueProcessing
        {
            Boolean get()
            {
                if(this->disposed)
                {
                    throw gcnew ObjectDisposedException(this->GetType()->Name);
                }

                return this->continueProcess;
            }
        }
    };





    // ------------------------------ ** ------------------------------
    //
    //                           ClientContext
    //
    // ------------------------------ ** ------------------------------

    /// <summary>
    /// The client context class.
    /// </summary>
    public ref class ClientContext : public Context
    {
    public:

        // type definitions
        /// <summary>
        /// The enumeration type of context attributes.
        /// </summary>
        CA_SUPPRESS_MESSAGE("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")
        [Flags]
        enum struct ContextAttribute : int
        {
            None                        = 0x00000000,
            Delegate                    = 0x00000001,
            MutualAuthentication        = 0x00000002,
            ReplayDetect                = 0x00000004,
            SequenceDetect              = 0x00000008,
            Confindentiality            = 0x00000010,
            UseSessionKey               = 0x00000020,
            PromptForCreds              = 0x00000040,
            UseSuppliedCreds            = 0x00000080,
            AllocMemory                 = 0x00000100,
            UseDceStyle                 = 0x00000200,
            Datagram                    = 0x00000400,
            Connection                  = 0x00000800,
            CallLevel                   = 0x00001000,
            FragmentSupplied            = 0x00002000,
            ExtendedError               = 0x00004000,
            Stream                      = 0x00008000,
            Integrity                   = 0x00010000,
            Identify                    = 0x00020000
        };



        // constructor/destructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="credential">Credential to be used in context</param>
        /// <param name="serverPrincipal">Server principal name</param>
        /// <param name="contextAttributeFlags">Context attribute flags</param>
        ClientContext(ClientCredential^ credential, String^ serverPrincipal, ContextAttribute contextAttributeFlags) : Context(credential)
        {
            // if the caller sets the DELEGATE flag where the security package is NTLM,
            // throw an exception
            if ( ((contextAttributeFlags & ContextAttribute::Delegate) != ContextAttribute::None) && 
                (credential->SecurityPackage == SMB2::SSPI::Credential::Package::NTLM) )
            {
                DelegateNotSupportedByNTLMException^ ex = gcnew DelegateNotSupportedByNTLMException();
                throw ex;
            }

            if( ((contextAttributeFlags & ContextAttribute::Datagram) != ContextAttribute::None) && 
                ((contextAttributeFlags & ContextAttribute::Connection) != ContextAttribute::None)   )
            {
                SSPIException^ ex = gcnew SSPIException("Datagram and Connection can not be specified at the same time.");
                throw ex;
            }
            // capture the server principal name
            this->serverPrincipalName = serverPrincipal;


            // prepare the output buffer
            SecBufferDesc outBuffDesc;
            SecBuffer outSecBuff;
            BYTE outBuff[TOKENBUFSIZE];

            outBuffDesc.ulVersion = SECBUFFER_VERSION;
            outBuffDesc.cBuffers = 1;
            outBuffDesc.pBuffers = &outSecBuff;

            outSecBuff.cbBuffer = TOKENBUFSIZE;
            outSecBuff.BufferType = SECBUFFER_TOKEN;
            outSecBuff.pvBuffer = outBuff;


            // output parameters
            ULONG reqContextAttributes = STANDARD_CONTEXT_ATTRIBUTES;
            TimeStamp tsLifeSpan = { 0, 0 };


            // add requested context attributes
            if ((contextAttributeFlags & ContextAttribute::Delegate) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_DELEGATE | ISC_REQ_MUTUAL_AUTH;
            if ((contextAttributeFlags & ContextAttribute::MutualAuthentication) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_MUTUAL_AUTH;
            if ((contextAttributeFlags & ContextAttribute::ReplayDetect) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_REPLAY_DETECT;
            if ((contextAttributeFlags & ContextAttribute::SequenceDetect) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_SEQUENCE_DETECT;
            if ((contextAttributeFlags & ContextAttribute::Confindentiality) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_CONFIDENTIALITY;
            if ((contextAttributeFlags & ContextAttribute::UseSessionKey) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_USE_SESSION_KEY;
            if ((contextAttributeFlags & ContextAttribute::PromptForCreds) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_PROMPT_FOR_CREDS;
            if ((contextAttributeFlags & ContextAttribute::UseSuppliedCreds) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_USE_SUPPLIED_CREDS;
            if ((contextAttributeFlags & ContextAttribute::AllocMemory) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_ALLOCATE_MEMORY;
            if ((contextAttributeFlags & ContextAttribute::UseDceStyle) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_USE_DCE_STYLE;
            if ((contextAttributeFlags & ContextAttribute::Datagram) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_DATAGRAM;
            if ((contextAttributeFlags & ContextAttribute::Connection) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_CONNECTION;
            if ((contextAttributeFlags & ContextAttribute::CallLevel) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_CALL_LEVEL;
            if ((contextAttributeFlags & ContextAttribute::FragmentSupplied) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_FRAGMENT_SUPPLIED;
            if ((contextAttributeFlags & ContextAttribute::ExtendedError) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_EXTENDED_ERROR;
            if ((contextAttributeFlags & ContextAttribute::MutualAuthentication) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_MUTUAL_AUTH;
            if ((contextAttributeFlags & ContextAttribute::Stream) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_STREAM;
            if ((contextAttributeFlags & ContextAttribute::Integrity) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_INTEGRITY;
            if ((contextAttributeFlags & ContextAttribute::Identify) != ContextAttribute::None)
                reqContextAttributes = reqContextAttributes | ISC_REQ_IDENTIFY;


            // get a reference to the credential
            CredHandle *phCredential = (CredHandle *)credential->CredentialHandle.ToPointer();


            // get a pinned reference to types appropriate for calling unmanaged methods
            pin_ptr<ULONG> pulContextAttributes = &this->contextAttribute;
            pin_ptr<const wchar_t> pwszServerPrincipalName = nullptr;
            ULONG targetDataRep = 0;
            if ((credential->SecurityPackage == SMB2::SSPI::Credential::Package::Kerberos)
                || (credential->SecurityPackage == SMB2::SSPI::Credential::Package::Negotiate))
            {
                pwszServerPrincipalName = PtrToStringChars(serverPrincipalName); 
                targetDataRep = SECURITY_NATIVE_DREP;
            }
            else if(credential->SecurityPackage == SMB2::SSPI::Credential::Package::Schannel)
            {
                pwszServerPrincipalName = PtrToStringChars(serverPrincipalName); 
            }


            // initialize the context
            SECURITY_STATUS sResult = InitializeSecurityContext(
                phCredential,                                           // [in] handle to the credentials
                NULL,                                                   // [in/out] handle of partially formed context. Always NULL the first time through
                (SEC_WCHAR*)pwszServerPrincipalName,                    // [in] name of the target of the context. Not needed by NTLM
                reqContextAttributes,                                   // [in] required context attributes
                0,                                                      // [reserved] reserved; must be zero
                targetDataRep,                                          // [in] data representation on the target
                NULL,                                                   // [in/out] pointer to the input buffers.  Always NULL the first time through
                0,                                                      // [reserved] reserved; must be zero
                this->contextHandle,                                    // [in/out] receives the new context handle (must be pre-allocated)
                &outBuffDesc,                                           // [out] pointer to the output buffers
                pulContextAttributes,                                   // [out] receives the context attributes
                &tsLifeSpan                                             // [out] receives the life span of the security context
                );

            // we're finished with the credential pointer
            phCredential = NULL;


            // release pinned objects
            pulContextAttributes = nullptr;
            pwszServerPrincipalName = nullptr;


            // check for errors
            if (sResult == SEC_E_OK)
                continueProcess = false;
            else if (sResult == SEC_I_CONTINUE_NEEDED)
                continueProcess = true;
            else
            {
                SSPIException^ ex = gcnew SSPIException("InitializeSecurityContext failed.", sResult);
                throw ex;
            }


            // Set the token property
            this->token = SecBufferToByteArray(outSecBuff);
        }

        /// <summary>
        /// Destructor cleans up all resources
        /// </summary>
        ~ClientContext()
        {
            if(!this->disposed)
            {
                Context::~Context();
                this->disposed = true;
            }
        }

    public:

        // methods
        /// <summary>
        /// Initialize the context from a token.
        /// </summary>
        /// <param name="inToken">The token used to initialize.</param>
        void Initialize(array<Byte>^ inToken)
        {
            if(this->disposed)
                throw gcnew ObjectDisposedException(this->GetType()->Name);


            // prepare the input buffer
            SecBufferDesc inBuffDesc;
            SecBuffer inSecBuff;
            pin_ptr<Byte> pInToken = &inToken[0];

            inBuffDesc.ulVersion = SECBUFFER_VERSION;
            inBuffDesc.cBuffers = 1;
            inBuffDesc.pBuffers = &inSecBuff;

            inSecBuff.cbBuffer = inToken->Length;
            inSecBuff.BufferType = SECBUFFER_TOKEN;
            inSecBuff.pvBuffer = pInToken;


            // prepare the output buffer
            SecBufferDesc outBuffDesc;
            SecBuffer outSecBuff;
            BYTE outBuff[TOKENBUFSIZE];

            outBuffDesc.ulVersion = SECBUFFER_VERSION;
            outBuffDesc.cBuffers = 1;
            outBuffDesc.pBuffers = &outSecBuff;

            outSecBuff.cbBuffer = TOKENBUFSIZE;
            outSecBuff.BufferType = SECBUFFER_TOKEN;
            outSecBuff.pvBuffer = outBuff;


            // output parameters
            ULONG reqContextAttributes = STANDARD_CONTEXT_ATTRIBUTES;
            TimeStamp tsLifeSpan = { 0, 0 };


            // get a reference to the credential
            CredHandle *phCredential = (CredHandle *)credential->CredentialHandle.ToPointer();


            // get a pinned reference to the context
            pin_ptr<ULONG> pulContextAttributes = &this->contextAttribute;
            pin_ptr<const wchar_t> pwszServerPrincipalName = nullptr; 
            pwszServerPrincipalName = PtrToStringChars(serverPrincipalName); 
            ULONG targetDataRep = 0;
            if ((credential->SecurityPackage == SMB2::SSPI::Credential::Package::Kerberos)
                || (credential->SecurityPackage == SMB2::SSPI::Credential::Package::Negotiate))
            {
                pwszServerPrincipalName = PtrToStringChars(serverPrincipalName); 
                targetDataRep = SECURITY_NATIVE_DREP;
            }
            else if(credential->SecurityPackage == SMB2::SSPI::Credential::Package::Schannel)
            {
                pwszServerPrincipalName = PtrToStringChars(serverPrincipalName); 
            }


            // initialize the context
            SECURITY_STATUS sResult = InitializeSecurityContext(
                phCredential,                                           // [in] handle to the credentials
                this->contextHandle,                                    // [in/out] handle of partially formed context. Always NULL the first time through
                (SEC_WCHAR*)pwszServerPrincipalName,                    // [in] name of the target of the context. Not needed by NTLM
                reqContextAttributes,                                   // [in] required context attributes
                0,                                                      // [reserved] reserved; must be zero
                targetDataRep,                                          // [in] data representation on the target
                &inBuffDesc,                                            // [in/out] pointer to the input buffers.  Always NULL the first time through
                0,                                                      // [reserved] reserved; must be zero
                this->contextHandle,                                    // [in/out] receives the new context handle
                &outBuffDesc,                                           // [out] pointer to the output buffers
                pulContextAttributes,                                   // [out] receives the context attributes
                &tsLifeSpan                                             // [out] receives the life span of the security context
                );

            // we're finished with the credential pointer
            phCredential = NULL;


            // release pinned objects
            pInToken = nullptr;
            pulContextAttributes = nullptr;
            pwszServerPrincipalName = nullptr;


            // check for errors
            if (sResult == SEC_E_OK)
            {
                continueProcess = false;

                // Set the session key property
                if((credential->SecurityPackage == SMB2::SSPI::Credential::Package::NTLM)
                    || (credential->SecurityPackage == SMB2::SSPI::Credential::Package::Kerberos))
                {

                    this->sessionKey = QuerySessionKey();

                }
            }
            else if (sResult == SEC_I_CONTINUE_NEEDED)
                continueProcess = true;
            else
            {
                SSPIException^ ex = gcnew SSPIException("InitializeSecurityContext(Client) failed", sResult);
                throw ex;
            }


            // Set the token property
            this->token = SecBufferToByteArray(outSecBuff);
        }


        // properties
        /// <summary>
        /// Context attributes.
        /// </summary>
        property StringCollection^ ContextAttributes
        {
            virtual StringCollection^ get() override
            {
                if(this->disposed)
                    throw gcnew ObjectDisposedException(this->GetType()->Name);


                // create a new collection
                StringCollection^ contextAttributes = gcnew StringCollection();


                // add all of the elements to the collection
                if (this->contextAttribute & ISC_RET_DELEGATE)
                    contextAttributes->Add("Delegate");
                if (this->contextAttribute & ISC_RET_MUTUAL_AUTH)
                    contextAttributes->Add("Mutual Authentication");
                if (this->contextAttribute & ISC_RET_REPLAY_DETECT )
                    contextAttributes->Add("Replay Detection");
                if (this->contextAttribute & ISC_RET_SEQUENCE_DETECT)
                    contextAttributes->Add("Sequence Detection");
                if (this->contextAttribute & ISC_RET_CONFIDENTIALITY)
                    contextAttributes->Add("Confidentiality");
                if (this->contextAttribute & ISC_RET_USE_SESSION_KEY)
                    contextAttributes->Add("Use Session Key");
                if (this->contextAttribute & ISC_RET_USED_COLLECTED_CREDS)
                    contextAttributes->Add("Used collected credentials");
                if (this->contextAttribute & ISC_RET_USED_SUPPLIED_CREDS)
                    contextAttributes->Add("Used Supplied Credentials");
                if (this->contextAttribute & ISC_RET_ALLOCATED_MEMORY)
                    contextAttributes->Add("Allocated Memory");
                if (this->contextAttribute & ISC_RET_USED_DCE_STYLE)
                    contextAttributes->Add("Used DCE Style");
                if (this->contextAttribute & ISC_RET_DATAGRAM)
                    contextAttributes->Add("Datagram");
                if (this->contextAttribute & ISC_RET_CONNECTION)
                    contextAttributes->Add("Connection");
                if (this->contextAttribute & ISC_RET_INTERMEDIATE_RETURN)
                    contextAttributes->Add("Intermediate Return");
                if (this->contextAttribute & ISC_RET_CALL_LEVEL)
                    contextAttributes->Add("Call Level");
                if (this->contextAttribute & ISC_RET_EXTENDED_ERROR)
                    contextAttributes->Add("Extended Error");
                if (this->contextAttribute & ISC_RET_STREAM)
                    contextAttributes->Add("Stream");
                if (this->contextAttribute & ISC_RET_INTEGRITY)
                    contextAttributes->Add("Integrity");
                if (this->contextAttribute & ISC_RET_IDENTIFY)
                    contextAttributes->Add("Identify");
                if (this->contextAttribute & ISC_RET_NULL_SESSION)
                    contextAttributes->Add("NULL Session");
                if (this->contextAttribute & ISC_RET_MANUAL_CRED_VALIDATION)
                    contextAttributes->Add("Manual Cred Validation");
                if (this->contextAttribute & ISC_RET_RESERVED1)
                    contextAttributes->Add("Reserved1");
                if (this->contextAttribute & ISC_RET_FRAGMENT_ONLY)
                    contextAttributes->Add("Fragment Only");


                // return the collection to the caller
                return contextAttributes;
            }
        };

    private:
        // data members
        String^ serverPrincipalName;
        bool disposed;
    };





    // ------------------------------ ** ------------------------------
    //
    //                           ServerContext
    //
    // ------------------------------ ** ------------------------------

    /// <summary>
    /// The server context class.
    /// </summary>
    public ref class ServerContext : public Context
    {
    public:

        // constructor/destructor
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="credential">Cerver credential to be used in context.</param>
        /// <param name="inToken">Token to be used in context.</param>
        ServerContext(ServerCredential^ credential, array<Byte>^ inToken) : Context(credential)
        {

            // prepare the input buffer
            SecBufferDesc inBuffDesc;
            SecBuffer inSecBuff;

            pin_ptr<Byte> pInToken = &inToken[0];
            
            inBuffDesc.ulVersion = SECBUFFER_VERSION;
            inBuffDesc.cBuffers = 1;
            inBuffDesc.pBuffers = &inSecBuff;

            inSecBuff.cbBuffer = inToken->Length;
            inSecBuff.BufferType = SECBUFFER_TOKEN;
            inSecBuff.pvBuffer = pInToken;


            // prepare the output buffer
            SecBufferDesc outBuffDesc;
            SecBuffer outSecBuff;
            BYTE outBuff[TOKENBUFSIZE];

            outBuffDesc.ulVersion = SECBUFFER_VERSION;
            outBuffDesc.cBuffers = 1;
            outBuffDesc.pBuffers = &outSecBuff;

            outSecBuff.cbBuffer = TOKENBUFSIZE;
            outSecBuff.BufferType = SECBUFFER_TOKEN;
            outSecBuff.pvBuffer = outBuff;


            // output parameters
            ULONG reqContextAttributes = STANDARD_CONTEXT_ATTRIBUTES;
            TimeStamp tsLifeSpan = { 0, 0 };


            // get a reference to the credential
            CredHandle *phCredential = (CredHandle *)this->credential->CredentialHandle.ToPointer();


            // get a pinned reference to the context attributes
            pin_ptr<ULONG> pulContextAttributes = &this->contextAttribute;


            // initialize the context
            SECURITY_STATUS sResult = AcceptSecurityContext(
                phCredential,                                       // [in] handle to the credentials
                NULL,                                               // [in/out] handle of partially formed context.  Always NULL the first time through
                &inBuffDesc,                                        // [in] pointer to the input buffers
                reqContextAttributes,                               // [in] required context attributes
                SECURITY_NATIVE_DREP,                               // [in] data representation on the target
                this->contextHandle,                                // [in/out] receives the new context handle    
                &outBuffDesc,                                       // [in/out] pointer to the output buffers
                pulContextAttributes,                               // [out] receives the context attributes
                &tsLifeSpan                                         // [out] receives the life span of the security context
                );


            // release pinned objects
            phCredential = NULL;
            pInToken = nullptr;
            pulContextAttributes = nullptr;


            // check for errors
            if (sResult == SEC_E_OK)
                continueProcess = false;
            else if (sResult == SEC_I_CONTINUE_NEEDED)
                continueProcess = true;
            else
            {
                SSPIException^ ex = gcnew SSPIException("AcceptSecurityContext failed", sResult);
                throw ex;
            }


            // Set the token property
            this->token = SecBufferToByteArray(outSecBuff);
        }

        /// <summary>
        /// Destructor cleans up all resources
        /// </summary>
        ~ServerContext()
        {
            if(!this->disposed)
            {
                Context::~Context();
                this->disposed = true;
            }
        }

    public:

        // methods
        /// <summary>
        /// The function enables the server component of a transport application to establish a security context between the server and a remote client. 
        /// </summary>
        /// <param name="inToken">The token to be used in context.</param>
        void Accept(array<Byte>^ inToken)
        {
            if(this->disposed)
            {
                throw gcnew ObjectDisposedException(this->GetType()->Name);
            }

            if (credential == nullptr)
            {
                throw gcnew SSPIException();
            }

            // prepare the input buffer
            SecBufferDesc inBuffDesc;
            SecBuffer inSecBuff;
            pin_ptr<Byte> pInToken = &inToken[0];

            inBuffDesc.ulVersion = SECBUFFER_VERSION;
            inBuffDesc.cBuffers = 1;
            inBuffDesc.pBuffers = &inSecBuff;

            inSecBuff.cbBuffer = inToken->Length;
            inSecBuff.BufferType = SECBUFFER_TOKEN;
            inSecBuff.pvBuffer = pInToken;


            // prepare the output buffer
            SecBufferDesc outBuffDesc;
            SecBuffer outSecBuff;
            BYTE outBuff[TOKENBUFSIZE];

            outBuffDesc.ulVersion = SECBUFFER_VERSION;
            outBuffDesc.cBuffers = 1;
            outBuffDesc.pBuffers = &outSecBuff;

            outSecBuff.cbBuffer = TOKENBUFSIZE;
            outSecBuff.BufferType = SECBUFFER_TOKEN;
            outSecBuff.pvBuffer = outBuff;


            // output parameters
            ULONG reqContextAttributes = STANDARD_CONTEXT_ATTRIBUTES;
            TimeStamp tsLifeSpan = { 0, 0 };


            // get a reference to the credential
            CredHandle *phCredential = (CredHandle *)credential->CredentialHandle.ToPointer();


            // get a pinned reference to the context
            pin_ptr<ULONG> pulContextAttributes = &this->contextAttribute;


            // initialize the context
            SECURITY_STATUS sResult = AcceptSecurityContext(
                phCredential,                                       // [in] handle to the credentials
                this->contextHandle,                                // [in/out] handle of partially formed context.  Always NULL the first time through
                &inBuffDesc,                                        // [in] pointer to the input buffers
                reqContextAttributes,                               // [in] required context attributes
                SECURITY_NATIVE_DREP,                               // [in] data representation on the target
                this->contextHandle,                                // [in/out] receives the new context handle
                &outBuffDesc,                                       // [in/out] pointer to the output buffers
                pulContextAttributes,                               // [out] receives the context attributes
                &tsLifeSpan                                         // [out] receives the life span of the security context
                );


            // we're finished with the credential pointer
            phCredential = NULL;


            // release pinned objects
            pInToken = nullptr;
            pulContextAttributes = nullptr;


            // check for errors
            if (sResult == SEC_E_OK)
            {
                continueProcess = false;
                
                // Set the session key property
                if((credential->SecurityPackage == SMB2::SSPI::Credential::Package::NTLM)
                    || (credential->SecurityPackage == SMB2::SSPI::Credential::Package::Kerberos))
                {

                    this->sessionKey = QuerySessionKey();

                }
            }
            else if (sResult == SEC_I_CONTINUE_NEEDED)
                continueProcess = true;
            else
            {
                SSPIException^ ex = gcnew SSPIException("AcceptSecurityContext failed", sResult);
                throw ex;
            }

            // Set the token property
            this->token = SecBufferToByteArray(outSecBuff);
        }

        /// <summary>
        /// Allows a server to impersonate a client by using the token stored in context.
        /// </summary>
        void ImpersonateClient(void)
        {
            if(this->disposed)
                throw gcnew ObjectDisposedException(this->GetType()->Name);


            // impersonate the client
            SECURITY_STATUS sResult = ImpersonateSecurityContext(
                this->contextHandle                                        // [in] handle of context to impersonate
                );


            // check for errors
            if (sResult != SEC_E_OK)
            {
                SSPIException^ ex = gcnew SSPIException("ImpersonateSecurityContext failed", sResult);
                throw ex;
            }
        }

        /// <summary>
        /// Discontinue the impersonation and restore security context.
        /// </summary>
        void RevertImpersonation(void)
        {
            if(this->disposed)
                throw gcnew ObjectDisposedException(this->GetType()->Name);


            // revert the impersonation
            SECURITY_STATUS sResult = RevertSecurityContext(
                this->contextHandle                                        // [in] handle of context to revert
                );


            // check for errors
            if (sResult != SEC_E_OK)
            {
                SSPIException^ ex = gcnew SSPIException("RevertSecurityContext failed", sResult);
                throw ex;
            }
        }



        // properties
        /// <summary>
        /// The context attributes.
        /// </summary>
        property StringCollection^ ContextAttributes
        {
            virtual StringCollection^ get() override
            {
                if(this->disposed)
                    throw gcnew ObjectDisposedException(this->GetType()->Name);


                // create a new collection
                StringCollection^ contextAttributes = gcnew StringCollection();


                // add all of the elements to the collection
                if (this->contextAttribute & ASC_RET_DELEGATE)
                    contextAttributes->Add("Delegate");
                if (this->contextAttribute & ASC_RET_MUTUAL_AUTH)
                    contextAttributes->Add("Mutual Authentication");
                if (this->contextAttribute & ASC_RET_REPLAY_DETECT )
                    contextAttributes->Add("Replay Detection");
                if (this->contextAttribute & ASC_RET_SEQUENCE_DETECT)
                    contextAttributes->Add("Sequence Detection");
                if (this->contextAttribute & ASC_RET_CONFIDENTIALITY)
                    contextAttributes->Add("Confidentiality");
                if (this->contextAttribute & ASC_RET_USE_SESSION_KEY)
                    contextAttributes->Add("Use Session Key");
                if (this->contextAttribute & ASC_RET_ALLOCATED_MEMORY)
                    contextAttributes->Add("Allocated Memory");
                if (this->contextAttribute & ASC_RET_USED_DCE_STYLE)
                    contextAttributes->Add("Used DCE Style");
                if (this->contextAttribute & ASC_RET_DATAGRAM)
                    contextAttributes->Add("Datagram");
                if (this->contextAttribute & ASC_RET_CONNECTION)
                    contextAttributes->Add("Connection");
                if (this->contextAttribute & ASC_RET_CALL_LEVEL)
                    contextAttributes->Add("Call Level");
                if (this->contextAttribute & ASC_RET_THIRD_LEG_FAILED)
                    contextAttributes->Add("Third Leg Failed");
                if (this->contextAttribute & ASC_RET_EXTENDED_ERROR)
                    contextAttributes->Add("Extended Error");
                if (this->contextAttribute & ASC_RET_STREAM)
                    contextAttributes->Add("Stream");
                if (this->contextAttribute & ASC_RET_INTEGRITY)
                    contextAttributes->Add("Integrity");
                if (this->contextAttribute & ASC_RET_LICENSING)
                    contextAttributes->Add("Licensing");
                if (this->contextAttribute & ASC_RET_IDENTIFY)
                    contextAttributes->Add("Identify");
                if (this->contextAttribute & ASC_RET_NULL_SESSION)
                    contextAttributes->Add("NULL Session");
                if (this->contextAttribute & ASC_RET_ALLOW_NON_USER_LOGONS)
                    contextAttributes->Add("Allow Non User Logons");
                if (this->contextAttribute & ASC_RET_ALLOW_CONTEXT_REPLAY)
                    contextAttributes->Add("Allow Context Replay");
                if (this->contextAttribute & ASC_RET_FRAGMENT_ONLY)
                    contextAttributes->Add("Fragment Only");


                // return the collection to the caller
                return contextAttributes;
            }
        };

        /// <summary>
        /// The internal client security token.
        /// </summary>
        property IntPtr ClientSecurityToken
        {
            IntPtr get()
            {
                if(this->disposed)
                    throw gcnew ObjectDisposedException(this->GetType()->Name);


                // retrieve the token
                HANDLE hToken = NULL;
                SECURITY_STATUS sResult = QuerySecurityContextToken(this->contextHandle, &hToken);


                // check for errors
                if (sResult != SEC_E_OK)
                {
                    SSPIException^ ex = gcnew SSPIException("QuerySecurityContextToken failed", sResult);
                    throw ex;
                }


                // return the handle to the caller
                return IntPtr(hToken);
            }
        };



    private:

        // data members
        bool disposed;

    };





    // ------------------------------ ** ------------------------------
    //
    //                         Security Packages
    //
    // ------------------------------ ** ------------------------------

    /// <summary>
    /// The security package class
    /// </summary>
    public ref class SecurityPackages sealed
    {
    private:
        SecurityPackages(){}
    public:
        // methods
        /// <summary>
        /// All security packages available to the client.
        /// </summary>
        static property StringCollection^ AllSecurityPackages
        {
            StringCollection^ get()
            {
                // create a new collection
                StringCollection^ packageNames = gcnew StringCollection();


                // list of packages
                SecPkgInfo *ppSecPkgInfo = NULL;


                SECURITY_STATUS sResult = 0;

                try
                {
                    // get the list of packages
                    ULONG cPackages = 0;
                    sResult = EnumerateSecurityPackages(&cPackages, &ppSecPkgInfo);


                    // check for errors
                    if (sResult != SEC_E_OK)
                    {
                        SSPIException^ ex = gcnew SSPIException("EnumerateSecurityPackages failed", sResult);
                        throw ex;
                    }


                    // add the security package names to the collection
                    for (ULONG i = 0;(i < cPackages);i++)
                    {
                        packageNames->Add(gcnew String(ppSecPkgInfo[i].Name));
                    }
                }
                finally
                {
                    // free memory
                    if (ppSecPkgInfo != NULL)
                    {
                        sResult = FreeContextBuffer(ppSecPkgInfo);
                        ppSecPkgInfo = NULL;
                    }
                }

                // return the collection to the caller
                return packageNames;
            }
        }

    };
   }
}
