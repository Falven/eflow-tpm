/* 
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See the LICENSE file in the project root for full license information.
 */

using System;
#if !TSS_NO_STACK
using System.Diagnostics;
#endif

namespace Tpm2Lib
{
    /// <summary>
    /// Represents exceptions generated by the TSS.Net library. Encapsulates call stack
    /// at the point where the exception was generated. Serves as the base for the
    /// TpmException class.
    /// </summary>
    public class TssException : Exception
    {
#if !TSS_NO_STACK
        public StackTrace CallerStack;
#endif

        public TssException(string message)
            : base(message)
        {
#if !TSS_NO_STACK
            CallerStack = new StackTrace(true);
#endif
        }
    }

    public class TssAssertException : TssException
    {
        public TssAssertException()
            : base(null)
        {}
    }

    /// <summary>
    /// Represents and encapsulates TPM error codes. Generally TSS.Net propagates TPM
    /// errors as exceptions, although this behavior can be overridden with _ExpectError(),
    /// _AllowError(), etc.
    /// </summary>
    public class TpmException : TssException
    {
        public string ErrorString = "None";
        public TpmRc RawResponse = TpmRc.Success;
        public TpmStructureBase CmdParms;

        public TpmException(TpmRc rawResponse, string errorDescription, TpmStructureBase cmdParms)
            : base(errorDescription)
        {
            ErrorString = TpmErrorHelpers.ErrorNumber(rawResponse).ToString();
            RawResponse = rawResponse;
            CmdParms = cmdParms;
        }
    }

    public class TpmFailure : Exception
    {
        public TpmFailure(string errMsg) : base(errMsg) {}
    }

    public class EscapeException : Exception
    {
        public EscapeException(string errMsg = "") : base(errMsg) { }
    }
}
