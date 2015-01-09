// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="LsaUtility.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Config
{
    using CDFM.Engine;
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Class LsaUtility
    /// </summary>
    public class LsaUtility
    {
        #region Private Enums

        // enum all policies
        /// <summary>
        /// Enum LSA_AccessPolicy
        /// </summary>
        private enum LSA_AccessPolicy : long
        {
            POLICY_VIEW_LOCAL_INFORMATION = 0x00000001L,
            POLICY_VIEW_AUDIT_INFORMATION = 0x00000002L,
            POLICY_GET_PRIVATE_INFORMATION = 0x00000004L,
            POLICY_TRUST_ADMIN = 0x00000008L,
            POLICY_CREATE_ACCOUNT = 0x00000010L,
            POLICY_CREATE_SECRET = 0x00000020L,
            POLICY_CREATE_PRIVILEGE = 0x00000040L,
            POLICY_SET_DEFAULT_QUOTA_LIMITS = 0x00000080L,
            POLICY_SET_AUDIT_REQUIREMENTS = 0x00000100L,
            POLICY_AUDIT_LOG_ADMIN = 0x00000200L,
            POLICY_SERVER_ADMIN = 0x00000400L,
            POLICY_LOOKUP_NAMES = 0x00000800L,
            POLICY_NOTIFICATION = 0x00001000L
        }

        #endregion Private Enums

        #region Public Methods

        /// <summary>
        /// Frees the sid.
        /// </summary>
        /// <param name="pSid">The p sid.</param>
        [DllImport("advapi32")]
        public static extern void FreeSid(IntPtr pSid);

        /// <summary>
        /// Adds a privilege to an account
        /// </summary>
        /// <param name="accountName">Name of an account - "domain\account" or only
        /// "account"</param>
        /// <param name="privilegeName">Name ofthe privilege</param>
        /// <returns>The windows error code returned by LsaAddAccountRights</returns>
        public static long SetRight(String accountName, String privilegeName)
        {
            long winErrorCode = 0; // Contains the last error

            // Pointer an size for the SID
            IntPtr sid = IntPtr.Zero;
            int sidSize = 0;

            // StringBuilder and size for the domain name
            var domainName = new StringBuilder();
            int nameSize = 0;

            // Account-type variable for lookup
            int accountType = 0;

            // Get required buffer size
            LookupAccountName(String.Empty, accountName, sid, ref sidSize, domainName, ref nameSize, ref accountType);

            // Allocate buffers
            domainName = new StringBuilder(nameSize);
            sid = Marshal.AllocHGlobal(sidSize);

            // Look up the SID for the account
            bool result = LookupAccountName(String.Empty, accountName, sid, ref sidSize, domainName, ref nameSize,
                                            ref accountType);

            // Say what you're doing
            CDFMonitor.LogOutputHandler("SetRight:LookupAccountName result = " + result);
            CDFMonitor.LogOutputHandler("SetRight:IsValidSid: " + IsValidSid(sid));
            CDFMonitor.LogOutputHandler("SetRight:LookupAccountName domainName: " + domainName);

            if (!result)
            {
                winErrorCode = GetLastError();
                CDFMonitor.LogOutputHandler("SetRight:LookupAccountName failed: " + winErrorCode);
            }
            else
            {
                // Initialize an empty unicode-string
                var systemName = new LSA_UNICODE_STRING();

                // Combine all policies
                var access = (int)(
                                       LSA_AccessPolicy.POLICY_AUDIT_LOG_ADMIN |
                                       LSA_AccessPolicy.POLICY_CREATE_ACCOUNT |
                                       LSA_AccessPolicy.POLICY_CREATE_PRIVILEGE |
                                       LSA_AccessPolicy.POLICY_CREATE_SECRET |
                                       LSA_AccessPolicy.POLICY_GET_PRIVATE_INFORMATION |
                                       LSA_AccessPolicy.POLICY_LOOKUP_NAMES |
                                       LSA_AccessPolicy.POLICY_NOTIFICATION |
                                       LSA_AccessPolicy.POLICY_SERVER_ADMIN |
                                       LSA_AccessPolicy.POLICY_SET_AUDIT_REQUIREMENTS |
                                       LSA_AccessPolicy.POLICY_SET_DEFAULT_QUOTA_LIMITS |
                                       LSA_AccessPolicy.POLICY_TRUST_ADMIN |
                                       LSA_AccessPolicy.POLICY_VIEW_AUDIT_INFORMATION |
                                       LSA_AccessPolicy.POLICY_VIEW_LOCAL_INFORMATION
                                   );

                // Initialize a pointer for the policy handle
                IntPtr policyHandle = IntPtr.Zero;

                // These attributes are not used, but LsaOpenPolicy wants them to exists
                var ObjectAttributes = new LSA_OBJECT_ATTRIBUTES();
                ObjectAttributes.Length = 0;
                ObjectAttributes.RootDirectory = IntPtr.Zero;
                ObjectAttributes.Attributes = 0;
                ObjectAttributes.SecurityDescriptor = IntPtr.Zero;
                ObjectAttributes.SecurityQualityOfService = IntPtr.Zero;

                // Get a policy handle
                uint resultPolicy = LsaOpenPolicy(ref systemName, ref ObjectAttributes, access, out policyHandle);
                winErrorCode = LsaNtStatusToWinError(resultPolicy);

                if (winErrorCode != 0)
                {
                    CDFMonitor.LogOutputHandler("SetRight:OpenPolicy failed: " + winErrorCode);
                }
                else
                {
                    // Now that we have the SID an the policy,
                    // We can add rights to the account.

                    // Initialize an unicode-string for the privilege name
                    var userRights = new LSA_UNICODE_STRING[1];
                    userRights[0] = new LSA_UNICODE_STRING();
                    userRights[0].Buffer = Marshal.StringToHGlobalUni(privilegeName);
                    userRights[0].Length = (UInt16)(privilegeName.Length * UnicodeEncoding.CharSize);
                    userRights[0].MaximumLength = (UInt16)((privilegeName.Length + 1) * UnicodeEncoding.CharSize);

                    // Add the right to the account
                    long res = LsaAddAccountRights(policyHandle, sid, userRights, 1);
                    winErrorCode = LsaNtStatusToWinError(res);
                    if (winErrorCode != 0)
                    {
                        CDFMonitor.LogOutputHandler("SetRight:LsaAddAccountRights failed: " + winErrorCode);
                    }

                    LsaClose(policyHandle);
                }
                FreeSid(sid);
            }

            return winErrorCode;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Gets the last error.
        /// </summary>
        /// <returns>System.Int32.</returns>
        [DllImport("kernel32.dll")]
        private static extern int GetLastError();

        /// <summary>
        /// Determines whether [is valid sid] [the specified p sid].
        /// </summary>
        /// <param name="pSid">The p sid.</param>
        /// <returns><c>true</c> if [is valid sid] [the specified p sid]; otherwise, /c>.</returns>
        [DllImport("advapi32.dll")]
        private static extern bool IsValidSid(IntPtr pSid);

        /// <summary>
        /// Lookups the name of the account.
        /// </summary>
        /// <param name="lpSystemName">Name of the lp system.</param>
        /// <param name="lpAccountName">Name of the lp account.</param>
        /// <param name="psid">The psid.</param>
        /// <param name="cbsid">The cbsid.</param>
        /// <param name="domainName">Name of the domain.</param>
        /// <param name="cbdomainLength">Length of the cbdomain.</param>
        /// <param name="use">The use.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true, PreserveSig = true)]
        private static extern bool LookupAccountName(
            string lpSystemName, string lpAccountName,
            IntPtr psid,
            ref int cbsid,
            StringBuilder domainName, ref int cbdomainLength, ref int use);

        /// <summary>
        /// Lsas the add account rights.
        /// </summary>
        /// <param name="PolicyHandle">The policy handle.</param>
        /// <param name="AccountSid">The account sid.</param>
        /// <param name="UserRights">The user rights.</param>
        /// <param name="CountOfRights">The count of rights.</param>
        /// <returns>System.Int64.</returns>
        [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
        private static extern long LsaAddAccountRights(
            IntPtr PolicyHandle,
            IntPtr AccountSid,
            LSA_UNICODE_STRING[] UserRights,
            long CountOfRights);

        /// <summary>
        /// Lsas the close.
        /// </summary>
        /// <param name="ObjectHandle">The object handle.</param>
        /// <returns>System.Int64.</returns>
        [DllImport("advapi32.dll")]
        private static extern long LsaClose(IntPtr ObjectHandle);

        /// <summary>
        /// Lsas the nt status to win error.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns>System.Int64.</returns>
        [DllImport("advapi32.dll")]
        private static extern long LsaNtStatusToWinError(long status);

        // Import the LSA functions
        /// <summary>
        /// Lsas the open policy.
        /// </summary>
        /// <param name="SystemName">Name of the system.</param>
        /// <param name="ObjectAttributes">The object attributes.</param>
        /// <param name="DesiredAccess">The desired access.</param>
        /// <param name="PolicyHandle">The policy handle.</param>
        /// <returns>UInt32.</returns>
        [DllImport("advapi32.dll", PreserveSig = true)]
        private static extern UInt32 LsaOpenPolicy(
            ref LSA_UNICODE_STRING SystemName,
            ref LSA_OBJECT_ATTRIBUTES ObjectAttributes,
            Int32 DesiredAccess,
            out IntPtr PolicyHandle
            );

        #endregion Private Methods

        #region Private Structs

        /// <summary>
        /// Struct LSA_OBJECT_ATTRIBUTES
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct LSA_OBJECT_ATTRIBUTES
        {
            public int Length;
            public IntPtr RootDirectory;
            public readonly LSA_UNICODE_STRING ObjectName;
            public UInt32 Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        /// <summary>
        /// Struct LSA_UNICODE_STRING
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct LSA_UNICODE_STRING
        {
            public UInt16 Length;
            public UInt16 MaximumLength;
            public IntPtr Buffer;
        }

        #endregion Private Structs

        // define the structures
    }
}