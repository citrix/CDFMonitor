// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="WindowsNetworking.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************
namespace CDFM.Network
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Class WindowsNetworking
    /// </summary>
    public static class WindowsNetworking
    {
        #region Private Fields

        private const int CONNECT_CMD_SAVECRED = 0x00001000;
        private const int CONNECT_COMMANDLINE = 0x00000800;
        private const int CONNECT_INTERACTIVE = 0x00000008;
        private const int CONNECT_LOCALDRIVE = 0x00000100;
        private const int CONNECT_PROMPT = 0x00000010;
        private const int CONNECT_REDIRECT = 0x00000080;
        private const int CONNECT_UPDATE_PROFILE = 0x00000001;
        private const int ERROR_ACCESS_DENIED = 5;
        private const int ERROR_ALREADY_ASSIGNED = 85;
        private const int ERROR_BAD_DEVICE = 1200;
        private const int ERROR_BAD_NET_NAME = 67;
        private const int ERROR_BAD_PROFILE = 1206;
        private const int ERROR_BAD_PROVIDER = 1204;
        private const int ERROR_CANCELLED = 1223;
        private const int ERROR_CANNOT_OPEN_PROFILE = 1205;
        private const int ERROR_DEVICE_IN_USE = 2404;
        private const int ERROR_EXTENDED_ERROR = 1208;
        private const int ERROR_INVALID_ADDRESS = 487;
        private const int ERROR_INVALID_PARAMETER = 87;
        private const int ERROR_INVALID_PASSWORD = 1216;
        private const int ERROR_MORE_DATA = 234;
        private const int ERROR_NO_MORE_ITEMS = 259;
        private const int ERROR_NO_NET_OR_BAD_PATH = 1203;
        private const int ERROR_NO_NETWORK = 1222;
        private const int ERROR_NOT_CONNECTED = 2250;
        private const int ERROR_OPEN_FILES = 2401;
        private const int NO_ERROR = 0;
        private const int RESOURCE_CONNECTED = 0x00000001;
        private const int RESOURCE_GLOBALNET = 0x00000002;
        private const int RESOURCE_REMEMBERED = 0x00000003;
        private const int RESOURCEDISPLAYTYPE_DOMAIN = 0x00000001;
        private const int RESOURCEDISPLAYTYPE_FILE = 0x00000004;
        private const int RESOURCEDISPLAYTYPE_GENERIC = 0x00000000;
        private const int RESOURCEDISPLAYTYPE_GROUP = 0x00000005;
        private const int RESOURCEDISPLAYTYPE_SERVER = 0x00000002;
        private const int RESOURCEDISPLAYTYPE_SHARE = 0x00000003;
        private const int RESOURCETYPE_ANY = 0x00000000;
        private const int RESOURCETYPE_DISK = 0x00000001;
        private const int RESOURCETYPE_PRINT = 0x00000002;
        private const int RESOURCEUSAGE_CONNECTABLE = 0x00000001;
        private const int RESOURCEUSAGE_CONTAINER = 0x00000002;

        private static readonly ErrorClass[] ERROR_LIST = new[]
                                                              {
                                                                  new ErrorClass(ERROR_ACCESS_DENIED,
                                                                                 "Error: Access Denied"),
                                                                  new ErrorClass(ERROR_ALREADY_ASSIGNED,
                                                                                 "Error: Already Assigned"),
                                                                  new ErrorClass(ERROR_BAD_DEVICE, "Error: Bad Device"),
                                                                  new ErrorClass(ERROR_BAD_NET_NAME,
                                                                                 "Error: Bad Net Name"),
                                                                  new ErrorClass(ERROR_BAD_PROVIDER,
                                                                                 "Error: Bad Provider"),
                                                                  new ErrorClass(ERROR_CANCELLED, "Error: Cancelled"),
                                                                  new ErrorClass(ERROR_EXTENDED_ERROR,
                                                                                 "Error: Extended Error"),
                                                                  new ErrorClass(ERROR_INVALID_ADDRESS,
                                                                                 "Error: Invalid Address"),
                                                                  new ErrorClass(ERROR_INVALID_PARAMETER,
                                                                                 "Error: Invalid Parameter"),
                                                                  new ErrorClass(ERROR_INVALID_PASSWORD,
                                                                                 "Error: Invalid Password"),
                                                                  new ErrorClass(ERROR_MORE_DATA, "Error: More Data"),
                                                                  new ErrorClass(ERROR_NO_MORE_ITEMS,
                                                                                 "Error: No More Items"),
                                                                  new ErrorClass(ERROR_NO_NET_OR_BAD_PATH,
                                                                                 "Error: No Net Or Bad Path"),
                                                                  new ErrorClass(ERROR_NO_NETWORK, "Error: No Network"),
                                                                  new ErrorClass(ERROR_BAD_PROFILE, "Error: Bad Profile")
                                                                  ,
                                                                  new ErrorClass(ERROR_CANNOT_OPEN_PROFILE,
                                                                                 "Error: Cannot Open Profile"),
                                                                  new ErrorClass(ERROR_DEVICE_IN_USE,
                                                                                 "Error: Device In Use"),
                                                                  new ErrorClass(ERROR_EXTENDED_ERROR,
                                                                                 "Error: Extended Error"),
                                                                  new ErrorClass(ERROR_NOT_CONNECTED,
                                                                                 "Error: Not Connected"),
                                                                  new ErrorClass(ERROR_OPEN_FILES, "Error: Open Files"),
                                                              };

        #endregion Private Fields

        #region Public Methods

        /// <summary>
        /// Connects to remote.
        /// </summary>
        /// <param name="remoteUNC">The remote UNC.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>System.Int32.</returns>
        public static int ConnectToRemote(string remoteUNC, string username, string password)
        {
            return ConnectToRemote(remoteUNC, username, password, false);
        }

        /// <summary>
        /// Connects to remote.
        /// </summary>
        /// <param name="remoteUNC">The remote UNC.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="promptUser">if set to <c>true</c> [prompt user].</param>
        /// <returns>System.Int32.</returns>
        public static int ConnectToRemote(string remoteUNC, string username, string password, bool promptUser)
        {
            var nr = new NETRESOURCE();
            nr.dwType = RESOURCETYPE_DISK;
            nr.lpRemoteName = remoteUNC;

            if (string.IsNullOrEmpty(username)) username = null;
            if (string.IsNullOrEmpty(password)) password = null;

            int ret;
            if (promptUser)
                ret = WNetUseConnection(IntPtr.Zero, nr, "", "", CONNECT_INTERACTIVE | CONNECT_PROMPT, null, null, null);
            else
                ret = WNetUseConnection(IntPtr.Zero, nr, password, username, 0, null, null, null);

            if (ret == NO_ERROR) return 0;
            return (ret);
        }

        /// <summary>
        /// Disconnects the remote.
        /// </summary>
        /// <param name="remoteUNC">The remote UNC.</param>
        /// <returns>System.Int32.</returns>
        public static int DisconnectRemote(string remoteUNC)
        {
            int ret = WNetCancelConnection2(remoteUNC, CONNECT_UPDATE_PROFILE, false);
            if (ret == NO_ERROR) return 0;
            return (ret);
        }

        /// <summary>
        /// Gets the error for number.
        /// </summary>
        /// <param name="errNum">The err num.</param>
        /// <returns>System.String.</returns>
        public static string GetErrorForNumber(int errNum)
        {
            foreach (ErrorClass er in ERROR_LIST)
            {
                if (er.num == errNum) return er.message;
            }
            return "Error: Unknown, " + errNum;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Ws the net cancel connection2.
        /// </summary>
        /// <param name="lpName">Name of the lp.</param>
        /// <param name="dwFlags">The dw flags.</param>
        /// <param name="fForce">if set to <c>true</c> [f force].</param>
        /// <returns>System.Int32.</returns>
        [DllImport("Mpr.dll")]
        private static extern int WNetCancelConnection2(
            string lpName,
            int dwFlags,
            bool fForce
            );

        /// <summary>
        /// Ws the net use connection.
        /// </summary>
        /// <param name="hwndOwner">The HWND owner.</param>
        /// <param name="lpNetResource">The lp net resource.</param>
        /// <param name="lpPassword">The lp password.</param>
        /// <param name="lpUserID">The lp user ID.</param>
        /// <param name="dwFlags">The dw flags.</param>
        /// <param name="lpAccessName">Name of the lp access.</param>
        /// <param name="lpBufferSize">Size of the lp buffer.</param>
        /// <param name="lpResult">The lp result.</param>
        /// <returns>System.Int32.</returns>
        [DllImport("Mpr.dll")]
        private static extern int WNetUseConnection(
            IntPtr hwndOwner,
            NETRESOURCE lpNetResource,
            string lpPassword,
            string lpUserID,
            int dwFlags,
            string lpAccessName,
            string lpBufferSize,
            string lpResult
            );

        #endregion Private Methods

        #region Private Structs

        /// <summary>
        /// Struct ErrorClass
        /// </summary>
        private struct ErrorClass
        {
            #region Public Fields

            /// <summary>
            /// The message
            /// </summary>
            public readonly string message;

            /// <summary>
            /// The num
            /// </summary>
            public readonly int num;

            #endregion Public Fields

            #region Public Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="ErrorClass" /> struct.
            /// </summary>
            /// <param name="num">The num.</param>
            /// <param name="message">The message.</param>
            public ErrorClass(int num, string message)
            {
                this.num = num;
                this.message = message;
            }

            #endregion Public Constructors
        }

        #endregion Private Structs

        #region Private Classes

        /// <summary>
        /// Class NETRESOURCE
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private class NETRESOURCE
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string lpLocalName = "";
            public string lpRemoteName = "";
            public string lpComment = "";
            public string lpProvider = "";
        }

        #endregion Private Classes
    }
}