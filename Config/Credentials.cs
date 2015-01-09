// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="Credentials.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Config
{
    using CDFM.Engine;
    using System;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Class Credentials
    /// </summary>
    internal class Credentials
    {
        #region Private Fields

        private const int MAX_DOMAIN = 100;
        private const int MAX_PASSWORD = 100;
        private const int MAX_USER_NAME = 100;

        #endregion Private Fields

        #region Public Enums

        /// <summary>
        /// Enum CREDUI_FLAGS
        /// </summary>
        [Flags]
        public enum CREDUI_FLAGS
        {
            INCORRECT_PASSWORD = 0x1,
            DO_NOT_PERSIST = 0x2,
            REQUEST_ADMINISTRATOR = 0x4,
            EXCLUDE_CERTIFICATES = 0x8,
            REQUIRE_CERTIFICATE = 0x10,
            SHOW_SAVE_CHECK_BOX = 0x40,
            ALWAYS_SHOW_UI = 0x80,
            REQUIRE_SMARTCARD = 0x100,
            PASSWORD_ONLY_OK = 0x200,
            VALIDATE_USERNAME = 0x400,
            COMPLETE_USERNAME = 0x800,
            PERSIST = 0x1000,
            SERVER_CREDENTIAL = 0x4000,
            EXPECT_CONFIRMATION = 0x20000,
            GENERIC_CREDENTIALS = 0x40000,
            USERNAME_TARGET_CREDENTIALS = 0x80000,
            KEEP_USERNAME = 0x100000,
        }

        /// <summary>
        /// Enum CredUIReturnCodes
        /// </summary>
        public enum CredUIReturnCodes
        {
            NO_ERROR = 0,
            ERROR_CANCELLED = 1223,
            ERROR_NO_SUCH_LOGON_SESSION = 1312,
            ERROR_NOT_FOUND = 1168,
            ERROR_INVALID_ACCOUNT_NAME = 1315,
            ERROR_INSUFFICIENT_BUFFER = 122,
            ERROR_INVALID_PARAMETER = 87,
            ERROR_INVALID_FLAGS = 1004,
            ERROR_BAD_ARGUMENTS = 160
        }

        #endregion Public Enums

        #region Private Enums

        private enum CRED_TYPE
        {
            GENERIC = 1,
            DOMAIN_PASSWORD = 2,
            DOMAIN_CERTIFICATE = 3,
            DOMAIN_VISIBLE_PASSWORD = 4,
            MAXIMUM = 5
        }

        #endregion Private Enums

        #region Public Methods

        public static bool DeleteCredentials(string name)
        {
            try
            {
                return CredDelete(name, CRED_TYPE.GENERIC, 0);
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:DeleteCredentials:exception" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Prompts for credentials.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="host">The host.</param>
        /// <returns>NetworkCredential.</returns>
        public static NetworkCredential PromptForCredentials(string username, string password, string host)
        {
            CDFMonitor.LogOutputHandler(string.Format("DEBUG:PromptForCredentials: enter:{0}", host));
            if (CDFMonitor.Instance.Config.AppOperators.RunningAsService)
            {
                CDFMonitor.LogOutputHandler("ERROR:PromptForCredentials: called while running as a service. returning");
                return (new NetworkCredential(username, password, host));
            }
            var info = new CREDUI_INFO { pszCaptionText = host, pszMessageText = "Please Enter Your Credentials" };

            const CREDUI_FLAGS flags = CREDUI_FLAGS.GENERIC_CREDENTIALS |
                                       CREDUI_FLAGS.SHOW_SAVE_CHECK_BOX;

            bool savePwd = false;
            GetCredentials(ref info, host, 0, ref username,
                           ref password, ref savePwd, flags);

            // Get domain  and username from username
            var sbUser = new StringBuilder(MAX_USER_NAME);
            sbUser.Append(username);
            var sbDomain = new StringBuilder(MAX_DOMAIN);
            sbDomain.Append(host);

            if (CredUIParseUserName(username, sbUser, MAX_USER_NAME, sbDomain, MAX_DOMAIN) == CredUIReturnCodes.NO_ERROR)
            {
                return (new NetworkCredential(sbUser.ToString(), password, sbDomain.ToString()));
            }
            return (new NetworkCredential(username, password, host));
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Uncs the read with credentials.
        /// </summary>
        /// <param name="handle">The handle.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode)]
        private static extern bool CredDelete(string target, CRED_TYPE type, int flags);

        /// <summary>
        /// Creds the name of the UI parse user.
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        /// <param name="user">The user.</param>
        /// <param name="userMaxChars">The user max chars.</param>
        /// <param name="domain">The domain.</param>
        /// <param name="domainMaxChars">The domain max chars.</param>
        /// <returns>CredUIReturnCodes.</returns>
        [DllImport("credui.dll", EntryPoint = "CredUIParseUserNameW", CharSet = CharSet.Unicode)]
        private static extern CredUIReturnCodes CredUIParseUserName(
            string userName,
            StringBuilder user,
            int userMaxChars,
            StringBuilder domain,
            int domainMaxChars);

        /// <summary>
        /// Creds the UI prompt for credentials W.
        /// </summary>
        /// <param name="creditUR">The credit UR.</param>
        /// <param name="targetName">Name of the target.</param>
        /// <param name="reserved1">The reserved1.</param>
        /// <param name="iError">The i error.</param>
        /// <param name="userName">Name of the user.</param>
        /// <param name="maxUserName">Name of the max user.</param>
        /// <param name="password">The password.</param>
        /// <param name="maxPassword">The max password.</param>
        /// <param name="pfSave">if set to <c>true</c> [pf save].</param>
        /// <param name="flags">The flags.</param>
        /// <returns>CredUIReturnCodes.</returns>
        [DllImport("credui", CharSet = CharSet.Unicode)]
        private static extern CredUIReturnCodes CredUIPromptForCredentialsW(ref CREDUI_INFO creditUR,
            string targetName,
            IntPtr reserved1,
            int iError,
            StringBuilder userName,
            int maxUserName,
            StringBuilder password,
            int maxPassword,
            [MarshalAs(UnmanagedType.Bool)] ref bool
            pfSave,
            CREDUI_FLAGS flags);

        /// <summary>
        /// Gets the credentials.
        /// </summary>
        /// <param name="creditUI">The credit UI.</param>
        /// <param name="targetName">Name of the target.</param>
        /// <param name="netError">The net error.</param>
        /// <param name="userName">Name of the user.</param>
        /// <param name="password">The password.</param>
        /// <param name="save">if set to <c>true</c> [save].</param>
        /// <param name="flags">The flags.</param>
        /// <returns>CredUIReturnCodes.</returns>
        private static CredUIReturnCodes GetCredentials(
            ref CREDUI_INFO creditUI,
            string targetName,
            int netError,
            ref string userName,
            ref string password,
            ref bool save,
            CREDUI_FLAGS flags)
        {
            StringBuilder user = new StringBuilder(MAX_USER_NAME);

            //var user = new StringBuilder(userName);
            user.Append(userName);
            StringBuilder pwd = new StringBuilder(MAX_PASSWORD);
            pwd.Append(password);

            //var pwd = new StringBuilder(password);
            creditUI.cbSize = Marshal.SizeOf(creditUI);

            CredUIReturnCodes result = CredUIPromptForCredentialsW(
                ref creditUI,
                targetName,
                IntPtr.Zero,
                netError,
                user,
                MAX_USER_NAME,
                pwd,
                MAX_PASSWORD,
                ref save,
                flags);

            userName = user.ToString();
            password = pwd.ToString();

            return result;
        }

        /// <summary>
        /// Logons the user.
        /// </summary>
        /// <param name="lpszUsername">The LPSZ username.</param>
        /// <param name="lpszDomain">The LPSZ domain.</param>
        /// <param name="lpszPassword">The LPSZ password.</param>
        /// <param name="dwLogonType">Type of the dw logon.</param>
        /// <param name="dwLogonProvider">The dw logon provider.</param>
        /// <param name="phToken">The ph token.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword,
            int dwLogonType, int dwLogonProvider, out IntPtr phToken);

        #endregion Private Methods

        #region Public Structs

        /// <summary>
        /// Struct CREDUI_INFO
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CREDUI_INFO
        {
            public int cbSize;
            public IntPtr hwndParent;
            public string pszMessageText;
            public string pszCaptionText;
            public IntPtr hbmBanner;
        }

        #endregion Public Structs
    }
}