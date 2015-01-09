// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="ServiceInstaller.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************
namespace CDFM.Service
{
    using CDFM.Engine;
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Enum ServiceBootFlag
    /// </summary>
    public enum ServiceBootFlag
    {
        Start = 0x00000000,
        SystemStart = 0x00000001,
        AutoStart = 0x00000002,
        DemandStart = 0x00000003,
        Disabled = 0x00000004
    }

    /// <summary>
    /// Enum ServiceControl
    /// </summary>
    public enum ServiceControl
    {
        Stop = 0x00000001,
        Pause = 0x00000002,
        Continue = 0x00000003,
        Interrogate = 0x00000004,
        Shutdown = 0x00000005,
        ParamChange = 0x00000006,
        NetBindAdd = 0x00000007,
        NetBindRemove = 0x00000008,
        NetBindEnable = 0x00000009,
        NetBindDisable = 0x0000000A
    }

    /// <summary>
    /// Enum ServiceError
    /// </summary>
    public enum ServiceError
    {
        Ignore = 0x00000000,
        Normal = 0x00000001,
        Severe = 0x00000002,
        Critical = 0x00000003
    }

    /// <summary>
    /// Enum ServiceManagerRights
    /// </summary>
    [Flags]
    public enum ServiceManagerRights
    {
        Connect = 0x0001,
        CreateService = 0x0002,
        EnumerateService = 0x0004,
        Lock = 0x0008,
        QueryLockStatus = 0x0010,
        ModifyBootConfig = 0x0020,
        StandardRightsRequired = 0xF0000,

        AllAccess = (StandardRightsRequired | Connect | CreateService |
                     EnumerateService | Lock | QueryLockStatus | ModifyBootConfig)
    }

    /// <summary>
    /// Enum ServiceRights
    /// </summary>
    [Flags]
    public enum ServiceRights
    {
        QueryConfig = 0x1,
        ChangeConfig = 0x2,
        QueryStatus = 0x4,
        EnumerateDependants = 0x8,
        Start = 0x10,
        Stop = 0x20,
        PauseContinue = 0x40,
        Interrogate = 0x80,
        UserDefinedControl = 0x100,
        Delete = 0x00010000,
        StandardRightsRequired = 0xF0000,

        AllAccess = (StandardRightsRequired | QueryConfig | ChangeConfig |
                     QueryStatus | EnumerateDependants | Start | Stop | PauseContinue |
                     Interrogate | UserDefinedControl)
    }

    /// <summary>
    /// Enum ServiceState
    /// </summary>
    public enum ServiceState
    {
        Unknown = -1, // The state cannot be (has not been) retrieved.
        NotFound = 0, // The service is not known on the host server.
        Stop = 1, // The service is NET stopped.
        Run = 4, // The service is NET started.
        Stopping = 3,
        Starting = 2,
    }

    /// <summary>
    /// Installs and provides functionality for handling windows services
    /// </summary>
    public class CDFServiceInstaller
    {
        #region Private Fields

        private const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;
        private const int SERVICE_INTERACTIVE_PROCESS = 0x00000100;
        private const uint SERVICE_NO_CHANGE = 0XFFFFFFFF;
        private const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        private const int STANDARD_RIGHTS_REQUIRED = 0xF0000;

        #endregion Private Fields

        #region Public Methods

        /// <summary>
        /// Changes the service config.
        /// </summary>
        /// <param name="hService">The h service.</param>
        /// <param name="nServiceType">Type of the n service.</param>
        /// <param name="nStartType">Start type of the n.</param>
        /// <param name="nErrorControl">The n error control.</param>
        /// <param name="lpBinaryPathName">Name of the lp binary path.</param>
        /// <param name="lpLoadOrderGroup">The lp load order group.</param>
        /// <param name="lpdwTagId">The LPDW tag id.</param>
        /// <param name="lpDependencies">The lp dependencies.</param>
        /// <param name="lpServiceStartName">Start name of the lp service.</param>
        /// <param name="lpPassword">The lp password.</param>
        /// <param name="lpDisplayName">Display name of the lp.</param>
        /// <returns>System.Int32.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern int ChangeServiceConfig(IntPtr hService,
            UInt32 nServiceType,
            UInt32 nStartType,
            UInt32 nErrorControl,
            String lpBinaryPathName,
            String lpLoadOrderGroup,
            IntPtr lpdwTagId,
            [In] char[] lpDependencies,
            String lpServiceStartName,
            String lpPassword,
            String lpDisplayName);

        /// <summary>
        /// Changes the service config.
        /// </summary>
        /// <param name="ServiceName">Name of the service.</param>
        /// <param name="bootFlag">The boot flag.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool ChangeServiceConfig(string ServiceName, ServiceBootFlag bootFlag)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                IntPtr service = OpenService(scman, ServiceName,
                                             ServiceRights.StandardRightsRequired | ServiceRights.Stop |
                                             ServiceRights.QueryStatus | ServiceRights.ChangeConfig);
                if (service == IntPtr.Zero)
                {
                    CDFMonitor.LogOutputHandler("Service not installed.");
                    return false;
                }
                try
                {
                    int ret = ChangeServiceConfig(service,
                                                  SERVICE_NO_CHANGE,
                                                  (uint)bootFlag,
                                                  SERVICE_NO_CHANGE,
                                                  null,
                                                  null,
                                                  IntPtr.Zero,
                                                  null,
                                                  null,
                                                  null,
                                                  null);

                    if (ret == 0)
                    {
                        int error = Marshal.GetLastWin32Error();
                        CDFMonitor.LogOutputHandler("Could not change service " + error);
                        return false;
                    }

                    return true;
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Gets the service config.
        /// </summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <returns>QUERY_SERVICE_CONFIG.</returns>
        public static QueryServiceConfig GetServiceConfig(string serviceName)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.AllAccess);
            IntPtr hService = OpenService(scman, serviceName, ServiceRights.QueryConfig);

            try
            {
                UInt32 dwBytesNeeded = 0;

                // Allocate memory for struct.
                IntPtr ptr = Marshal.AllocHGlobal(4096);

                if (!QueryServiceConfig(hService, ptr, 4096, out dwBytesNeeded))
                {
                    CDFMonitor.LogOutputHandler("Failed to query service config.");
                }

                QueryServiceConfig queryServiceConfig = new QueryServiceConfig();

                // Copy
                Marshal.PtrToStructure(ptr, queryServiceConfig);

                // Free memory for struct.
                Marshal.FreeHGlobal(ptr);
                return queryServiceConfig;
            }
            finally
            {
                CloseServiceHandle(hService);
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Takes a service name and returns the <code>ServiceState</code> of the corresponding
        /// service
        /// </summary>
        /// <param name="ServiceName">The service name that we will check for his /code></param>
        /// <returns>The ServiceState of the service we wanted to check</returns>
        public static ServiceState GetServiceStatus(string ServiceName)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                IntPtr hService = OpenService(scman, ServiceName,
                                              ServiceRights.QueryStatus);
                if (hService == IntPtr.Zero)
                {
                    return ServiceState.NotFound;
                }
                try
                {
                    return GetServiceStatus(hService);
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Takes a service name, a service display name and the path to the service executable and
        /// installs / starts the windows service.
        /// </summary>
        /// <param name="ServiceName">The service name that this service will have</param>
        /// <param name="DisplayName">The display name that this service will have</param>
        /// <param name="FileName">The path to the executable of the service</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool InstallAndStart(string ServiceName, string DisplayName,
            string FileName)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect |
                                         ServiceManagerRights.CreateService);
            try
            {
                IntPtr service = OpenService(scman, ServiceName,
                                             ServiceRights.QueryStatus | ServiceRights.Start);
                if (service == IntPtr.Zero)
                {
                    service = CreateService(scman, ServiceName, DisplayName,
                                            ServiceRights.QueryStatus | ServiceRights.Start,
                                            SERVICE_WIN32_OWN_PROCESS | SERVICE_INTERACTIVE_PROCESS,
                                            ServiceBootFlag.AutoStart, ServiceError.Normal, FileName, null, IntPtr.Zero,
                                            null, null, null);
                }
                if (service == IntPtr.Zero)
                {
                    CDFMonitor.LogOutputHandler("Failed to install service.");
                    return false;
                }
                try
                {
                    StartService(service);
                    return true;
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Queries the service config.
        /// </summary>
        /// <param name="hService">The h service.</param>
        /// <param name="intPtrQueryConfig">The int PTR query config.</param>
        /// <param name="cbBufSize">Size of the cb buf.</param>
        /// <param name="pcbBytesNeeded">The PCB bytes needed.</param>
        /// <returns>Boolean.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern Boolean QueryServiceConfig(IntPtr hService, IntPtr intPtrQueryConfig, UInt32 cbBufSize, out UInt32 pcbBytesNeeded);

        /// <summary>
        /// Accepts a service name and returns true if the service with that service name exists
        /// </summary>
        /// <param name="ServiceName">The service name that we will check for existence</param>
        /// <returns>True if that service exists false otherwise</returns>
        public static bool ServiceIsInstalled(string ServiceName)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect);
            IntPtr service = IntPtr.Zero;

            try
            {
                service = OpenService(scman, ServiceName,
                                             ServiceRights.QueryStatus);
                if (service == IntPtr.Zero)
                {
                    CDFMonitor.LogOutputHandler("DEBUG:ServiceIsInstalled: false");
                    return false;
                }

                // make sure its not delete pending
                SERVICE_STATUS ss = new SERVICE_STATUS();
                int ret = QueryServiceStatus(service, ss);

                if (ss.dwCurrentState == ServiceState.NotFound ||
                    ss.dwCurrentState == ServiceState.Unknown)
                {
                    return false;
                }

                if (ret == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    CDFMonitor.LogOutputHandler("ServiceIsInstalled: error:" + error);

                    return false;
                }
                else
                {
                    CDFMonitor.LogOutputHandler("DEBUG:ServiceIsInstalled: true");
                    return true;
                }
            }
            finally
            {
                CloseServiceHandle(service);
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Takes a service name and starts it
        /// </summary>
        /// <param name="Name">The service name</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool StartService(string Name)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                IntPtr hService = OpenService(scman, Name, ServiceRights.QueryStatus |
                                                           ServiceRights.Start);
                if (hService == IntPtr.Zero)
                {
                    CDFMonitor.LogOutputHandler("Could not open service.");
                    return false;
                }
                try
                {
                    StartService(hService);
                    return true;
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Stops the provided windows service
        /// </summary>
        /// <param name="Name">The service name that will be stopped</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool StopService(string Name)
        {
            CDFMonitor.LogOutputHandler("StopService: entry");
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                IntPtr hService = OpenService(scman, Name, ServiceRights.QueryStatus |
                                                           ServiceRights.Stop);
                if (hService == IntPtr.Zero)
                {
                    CDFMonitor.LogOutputHandler("Could not open service.");
                    return false;
                }
                try
                {
                    StopService(hService);
                    return true;
                }
                finally
                {
                    CDFMonitor.LogOutputHandler("StopService: exit");
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CDFMonitor.LogOutputHandler("StopService: exit2");
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Takes a service name and tries to stop and then uninstall the windows serviceError
        /// </summary>
        /// <param name="ServiceName">The windows service name to uninstall</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool UninstallService(string ServiceName)
        {
            CDFMonitor.LogOutputHandler("Uninstall: entry");
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                IntPtr service = OpenService(scman, ServiceName,
                                             ServiceRights.StandardRightsRequired | ServiceRights.Stop |
                                             ServiceRights.QueryStatus);
                if (service == IntPtr.Zero)
                {
                    CDFMonitor.LogOutputHandler("Service not installed.");
                    return false;
                }
                try
                {
                    StopService(service);
                    int ret = DeleteService(service);
                    if (ret == 0)
                    {
                        int error = Marshal.GetLastWin32Error();

                        //throw new ApplicationException("Could not delete service " + error);
                        if (error == ERROR_SERVICE_MARKED_FOR_DELETE)
                        {
                            return true;
                        }

                        CDFMonitor.LogOutputHandler("Could not delete service " + error);
                        return false;
                    }
                    return true;
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Closes the service handle.
        /// </summary>
        /// <param name="hSCObject">The h SC object.</param>
        /// <returns>System.Int32.</returns>
        [DllImport("advapi32.dll")]
        private static extern int CloseServiceHandle(IntPtr hSCObject);

        /// <summary>
        /// Controls the service.
        /// </summary>
        /// <param name="hService">The h service.</param>
        /// <param name="dwControl">The dw control.</param>
        /// <param name="lpServiceStatus">The lp service status.</param>
        /// <returns>System.Int32.</returns>
        [DllImport("advapi32.dll")]
        private static extern int ControlService(IntPtr hService, ServiceControl
            dwControl, SERVICE_STATUS lpServiceStatus);

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="hSCManager">The h SC manager.</param>
        /// <param name="lpServiceName">Name of the lp service.</param>
        /// <param name="lpDisplayName">Display name of the lp.</param>
        /// <param name="dwDesiredAccess">The dw desired access.</param>
        /// <param name="dwServiceType">Type of the dw service.</param>
        /// <param name="dwStartType">Start type of the dw.</param>
        /// <param name="dwErrorControl">The dw error control.</param>
        /// <param name="lpBinaryPathName">Name of the lp binary path.</param>
        /// <param name="lpLoadOrderGroup">The lp load order group.</param>
        /// <param name="lpdwTagId">The LPDW tag id.</param>
        /// <param name="lpDependencies">The lp dependencies.</param>
        /// <param name="lp">The lp.</param>
        /// <param name="lpPassword">The lp password.</param>
        /// <returns>IntPtr.</returns>
        [DllImport("advapi32.dll", EntryPoint = "CreateServiceA")]
        private static extern IntPtr CreateService(IntPtr hSCManager, string
            lpServiceName, string lpDisplayName,
            ServiceRights dwDesiredAccess, int
            dwServiceType,
            ServiceBootFlag dwStartType, ServiceError dwErrorControl,
            string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId,
            string
            lpDependencies, string lp, string lpPassword);

        /// <summary>
        /// Deletes the service.
        /// </summary>
        /// <param name="hService">The h service.</param>
        /// <returns>System.Int32.</returns>
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int DeleteService(IntPtr hService);

        /// <summary>
        /// Gets the service state by using the handle of the provided windows service
        /// </summary>
        /// <param name="hService">The handle to the service</param>
        /// <returns>The <code>ServiceState</code> of the service</returns>
        private static ServiceState GetServiceStatus(IntPtr hService)
        {
            var ssStatus = new SERVICE_STATUS();
            if (QueryServiceStatus(hService, ssStatus) == 0)
            {
                CDFMonitor.LogOutputHandler("Failed to query service status.");
                return ServiceState.Unknown;
            }
            return ssStatus.dwCurrentState;
        }

        /// <summary>
        /// Opens the SC manager.
        /// </summary>
        /// <param name="lpMachineName">Name of the lp machine.</param>
        /// <param name="lpDatabaseName">Name of the lp database.</param>
        /// <param name="dwDesiredAccess">The dw desired access.</param>
        /// <returns>IntPtr.</returns>
        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerA")]
        private static extern IntPtr OpenSCManager(string lpMachineName, string
            lpDatabaseName,
            ServiceManagerRights dwDesiredAccess);

        /// <summary>
        /// Opens the service manager
        /// </summary>
        /// <param name="Rights">The service manager rights</param>
        /// <returns>the handle to the service manager</returns>
        private static IntPtr OpenSCManager(ServiceManagerRights Rights)
        {
            IntPtr scman = OpenSCManager(null, null, Rights);
            if (scman == IntPtr.Zero)
            {
                CDFMonitor.LogOutputHandler("Could not connect to service control manager.");
                return IntPtr.Zero;
            }
            return scman;
        }

        /// <summary>
        /// Opens the service.
        /// </summary>
        /// <param name="hSCManager">The h SC manager.</param>
        /// <param name="lpServiceName">Name of the lp service.</param>
        /// <param name="dwDesiredAccess">The dw desired access.</param>
        /// <returns>IntPtr.</returns>
        [DllImport("advapi32.dll", EntryPoint = "OpenServiceA",
            CharSet = CharSet.Ansi)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string
            lpServiceName, ServiceRights dwDesiredAccess);

        /// <summary>
        /// Queries the service status.
        /// </summary>
        /// <param name="hService">The h service.</param>
        /// <param name="lpServiceStatus">The lp service status.</param>
        /// <returns>System.Int32.</returns>
        [DllImport("advapi32.dll")]
        private static extern int QueryServiceStatus(IntPtr hService,
            SERVICE_STATUS lpServiceStatus);

        /// <summary>
        /// Starts the service.
        /// </summary>
        /// <param name="hService">The h service.</param>
        /// <param name="dwNumServiceArgs">The dw num service args.</param>
        /// <param name="lpServiceArgVectors">The lp service arg vectors.</param>
        /// <returns>System.Int32.</returns>
        [DllImport("advapi32.dll", EntryPoint = "StartServiceA")]
        private static extern int StartService(IntPtr hService, int
            dwNumServiceArgs, int lpServiceArgVectors);

        /// <summary>
        /// Stars the provided windows service
        /// </summary>
        /// <param name="hService">The handle to the windows service</param>
        private static void StartService(IntPtr hService)
        {
            CDFMonitor.LogOutputHandler("StartService entry.");
            var status = new SERVICE_STATUS();
            StartService(hService, 0, 0);
            WaitForServiceStatus(hService, ServiceState.Starting, ServiceState.Run);
        }

        /// <summary>
        /// Stops the provided windows service
        /// </summary>
        /// <param name="hService">The handle to the windows service</param>
        private static void StopService(IntPtr hService)
        {
            var status = new SERVICE_STATUS();
            ControlService(hService, ServiceControl.Stop, status);
            WaitForServiceStatus(hService, ServiceState.Stopping, ServiceState.Stop);
        }

        /// <summary>
        /// Returns true when the service status has been changes from wait status to desired status
        /// ,this method waits around 10 seconds for this operation.
        /// </summary>
        /// <param name="hService">The handle to the service</param>
        /// <param name="WaitStatus">The current state of the service</param>
        /// <param name="DesiredStatus">The desired state of the service</param>
        /// <returns>bool if the service has successfully changed states within the allowed
        /// timeline</returns>
        private static bool WaitForServiceStatus(IntPtr hService, ServiceState
            WaitStatus, ServiceState DesiredStatus)
        {
            CDFMonitor.LogOutputHandler("WaitForServiceStatus entry.");
            var ssStatus = new SERVICE_STATUS();
            int loopCount = 0;
            int maxCount = 600;

            QueryServiceStatus(hService, ssStatus);
            if (ssStatus.dwCurrentState == DesiredStatus)
            {
                CDFMonitor.LogOutputHandler("WaitForServiceStatus service state = " +
                                              ssStatus.dwCurrentState.ToString());
                return true;
            }

            while (ssStatus.dwCurrentState == WaitStatus && loopCount < maxCount)
            {
                if (QueryServiceStatus(hService, ssStatus) == 0
                    || CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000))
                {
                    break;
                }

                loopCount++;
            }

            if (loopCount == maxCount)
            {
                CDFMonitor.LogOutputHandler("WaitForServiceStatus: timed out waiting for service.");
            }

            CDFMonitor.LogOutputHandler("WaitForServiceStatus service state return = " +
                                          ssStatus.dwCurrentState.ToString());
            return (ssStatus.dwCurrentState == DesiredStatus);
        }

        #endregion Private Methods

        #region Private Classes

        /// <summary>
        /// Class SERVICE_STATUS
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private class SERVICE_STATUS
        {
            public int dwServiceType;
            public ServiceState dwCurrentState = 0;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        }

        #endregion Private Classes
    }

    /// <summary>
    /// Class QUERY_SERVICE_CONFIG
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class QueryServiceConfig
    {
        /// <summary>
        /// The dw service type
        /// </summary>
        [MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
        public UInt32 dwServiceType;

        /// <summary>
        /// The dw start type
        /// </summary>
        [MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
        public UInt32 dwStartType;

        /// <summary>
        /// The dw error control
        /// </summary>
        [MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
        public UInt32 dwErrorControl;

        /// <summary>
        /// The lp binary path name
        /// </summary>
        [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public String lpBinaryPathName;

        /// <summary>
        /// The lp load order group
        /// </summary>
        [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public String lpLoadOrderGroup;

        /// <summary>
        /// The dw tag ID
        /// </summary>
        [MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
        public UInt32 dwTagID;

        /// <summary>
        /// The lp dependencies
        /// </summary>
        [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public String lpDependencies;

        /// <summary>
        /// The lp service start name
        /// </summary>
        [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public String lpServiceStartName;

        /// <summary>
        /// The lp display name
        /// </summary>
        [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public String lpDisplayName;
    }
}