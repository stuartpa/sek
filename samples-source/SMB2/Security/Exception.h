/*=====================================================================

  File:        Exception.h

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
//                         Using Statements
//
// ------------------------------ ** ------------------------------

using namespace System;
using namespace System::Runtime::Serialization;
using namespace System::Security;
using namespace System::Security::Permissions;


namespace SMB2
{
namespace SSPI
{
	// ------------------------------ ** ------------------------------
	//
	//                          SSPIException
	//
	// ------------------------------ ** ------------------------------

	/// <summary>
	/// SSPIException is used as specified exception type in SSPI library.
	/// </summary>
	CA_SUPPRESS_MESSAGE("Microsoft.Naming", "CA1705:LongAcronymsShouldBePascalCased")
	[SerializableAttribute]
	public ref class SSPIException : public Exception, public ISerializable
	{
	protected:
		/// <summary>
		/// Constructor with serialization information and streaming context.
		/// </summary>
		/// <param name="info">serialization information</param>
		/// <param name="context">streaming context</param>
		SSPIException(SerializationInfo^ info, StreamingContext context)
			: Exception(info, context)
		{
			if (info == nullptr)
			{
				throw gcnew ArgumentNullException("info");
			}
			this->message = info->GetString("message");
			this->errorCode = info->GetInt32("errorCode");
		}

	public:
		/// <summary>
		/// Default empty constructor.
		/// </summary>
		SSPIException()
		{
			this->message = String::Empty;
		}

		/// <summary>
		/// Constructor with message.
		/// </summary>
		/// <param name="message">message text</param>
		SSPIException(String^ message)
			: Exception(message)
		{
			this->message = message;
		}

		/// <summary>
		/// Constructor with message and innerException.
		/// </summary>
		/// <param name="message">message text</param>
		/// <param name="innerException">inner exception</param>
		SSPIException(String^ message, Exception^ innerException)
			: Exception(message, innerException)
		{
			this->message = message;
		}

		/// <summary>
		/// Constructor with message and error code.
		/// </summary>
		/// <param name="message">message text</param>
		/// <param name="errorCode">error code</param>
		SSPIException(String^ message, int errorCode)
			: Exception(message)
		{
			this->message = message; 
			this->errorCode = errorCode;
		}


		/// <summary>
		/// System.Exception method overrides
		/// </summary>
		/// <param name="info">serialization information</param>
		/// <param name="context">streaming context</param>
		[SecurityPermissionAttribute(SecurityAction::Demand, SerializationFormatter=true)]
		virtual void GetObjectData(SerializationInfo^ info, StreamingContext context) override
		{
			if (info == nullptr)
			{
				throw gcnew ArgumentNullException("info");
			}
			Exception::GetObjectData(info, context);

			info->AddValue("message", this->message);
			info->AddValue("errorCode", this->errorCode);
		}



		/// <summary>
		/// System.Exception property overrides.
		/// </summary>
		property String^ Message
		{
			virtual String^ get() override
			{
				String^ msg = String::Format(System::Globalization::CultureInfo::InvariantCulture, "{0}. Error Code = '{1:X}'.", this->message, this->errorCode);
				return msg;
			}
		}



		/// <summary>
		/// Error Code.
		/// </summary>
		property int ErrorCode
		{
			int get()
			{
				return this->errorCode;
			}
		}

	private:
		// private data members
		String^ message;
		int errorCode;
	};





	// ------------------------------ ** ------------------------------
	//
	//                 DelegateNotSupportedByNTLMException
	//
	// ------------------------------ ** ------------------------------

	/// <summary>
	/// DelegateNotSupportedByNTLMException is thrown when DELEGATE context attribute has been specified where the security package is NTLM.
	/// </summary>
	CA_SUPPRESS_MESSAGE("Microsoft.Naming", "CA1705:LongAcronymsShouldBePascalCased")
	[SerializableAttribute]
	public ref class DelegateNotSupportedByNTLMException : public Exception, public ISerializable
	{
	protected:
		/// <summary>
		/// Constructor with serialization information and streaming context.
		/// </summary>
		/// <param name="info">serialization information</param>
		/// <param name="context">streaming context</param>
		DelegateNotSupportedByNTLMException(SerializationInfo^ info, StreamingContext context)
			: Exception(info, context)
		{
		}
	public:
		/// <summary>
		/// Default empty constructor.
		/// </summary>
		DelegateNotSupportedByNTLMException()
		{
		}

		/// <summary>
		/// Constructor with message.
		/// </summary>
		/// <param name="message">message text</param>
		DelegateNotSupportedByNTLMException(String^ message)
			: Exception(message)
		{
		}

		/// <summary>
		/// Constructor with message and innerException.
		/// </summary>
		/// <param name="message">message text</param>
		/// <param name="innerException">inner exception</param>
		DelegateNotSupportedByNTLMException(String^ message, Exception^ innerException)
			: Exception(message, innerException)
		{
		}

		/// <summary>
		/// System.Exception property overrides
		/// </summary>
		property String^ Message
		{
			virtual String^ get() override
			{
				return "The DELEGATE context attribute has been specified where the security package is NTLM.  Delegate is not supported by NTLM.";
			}
		}
	};
}
}
