// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="WMIServicesManager.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************
namespace CDFM.Network
{
    using CDFM.Config;
    using CDFM.Engine;
    using System;
    using System.Management;
    using System.Net;

    /// <summary>
    /// Class WMIServicesManager
    /// </summary>
    internal class WMIServicesManager : WMI
    {
        #region Private Fields

        private const int TIME_OUT = 60000 * 3;

        #endregion Private Fields

        //120000;

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="WMIServicesManager" /> class.
        /// </summary>
        /// <param name="remoteMachine">The remote machine.</param>
        /// <param name="creds">The creds.</param>
        public WMIServicesManager(string remoteMachine, ResourceCredential creds)
            : base(remoteMachine, null, creds)
        {
        }

        #endregion Public Constructors

        #region Public Enums

        /// <summary>
        /// Enum OnError
        /// </summary>
        public enum OnError
        {
            UserIsNotNotified = 0,
            UserIsNotified = 1,
            SystemRestartedLastGoodConfiguraion = 2,
            SystemAttemptStartWithGoodConfiguration = 3
        }

        /// <summary>
        /// Enum ReturnValue
        /// </summary>
        public enum ReturnValue
        {
            Success = 0,
            NotSupported = 1,
            AccessDenied = 2,
            DependentServicesRunning = 3,
            InvalidServiceControl = 4,
            ServiceCannotAcceptControl = 5,
            ServiceNotActive = 6,
            ServiceRequestTimeout = 7,
            UnknownFailure = 8,
            PathNotFound = 9,
            ServiceAlreadyRunning = 10,
            ServiceDatabaseLocked = 11,
            ServiceDependencyDeleted = 12,
            ServiceDependencyFailure = 13,
            ServiceDisabled = 14,
            ServiceLogonFailure = 15,
            ServiceMarkedForDeletion = 16,
            ServiceNoThread = 17,
            StatusCircularDependency = 18,
            StatusDuplicateName = 19,
            StatusInvalidName = 20,
            StatusInvalidParameter = 21,
            StatusInvalidServiceAccount = 22,
            StatusServiceExists = 23,
            ServiceAlreadyPaused = 24,
            ServiceNotFound = 25,
            True = 26,
            False = 27
        }

        /// <summary>
        /// Enum ServiceState
        /// </summary>
        public enum ServiceState
        {
            Running,
            Stopped,
            Paused,
            StartPending,
            StopPending,
            PausePending,
            ContinuePending,
            Unknown
        }

        /// <summary>
        /// Enum ServiceType
        /// </summary>
        public enum ServiceType : uint
        {
            KernelDriver = 0x1,
            FileSystemDriver = 0x2,
            Adapter = 0x4,
            RecognizerDriver = 0x8,
            OwnProcess = 0x10,
            ShareProcess = 0x20,
            Interactive = 0x100
        }

        /// <summary>
        /// Enum StartMode
        /// </summary>
        public enum StartMode
        {
            //Boot = 0,
            //System = 1,
            Automatic = 2,

            Manual = 3,
            Disabled = 4
        }

        #endregion Public Enums

        #region Public Methods

        /// <summary>
        /// Determines whether this instance [can pause and continue] the specified SVC name.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <returns><c>true</c> if this instance [can pause and continue] the specified SVC name;
        /// otherwise, /c>.</returns>
        public bool CanPauseAndContinue(string svcName)
        {
            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);

            // using (ManagementObject service = new ManagementObject(new ManagementPath(objPath)))
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    return bool.Parse(service.Properties["AcceptPause"].Value.ToString());
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Determines whether this instance can stop the specified SVC name.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <returns><c>true</c> if this instance can stop the specified SVC name; otherwise,
        /// /c>.</returns>
        public bool CanStop(string svcName)
        {
            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    return bool.Parse(service.Properties["AcceptStop"].Value.ToString());
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Changes the start mode.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <param name="startMode">The start mode.</param>
        /// <returns>ReturnValue.</returns>
        public ReturnValue ChangeStartMode(string svcName, StartMode startMode)
        {
            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                ManagementBaseObject inParams = service.GetMethodParameters("ChangeStartMode");
                inParams["StartMode"] = startMode.ToString();
                try
                {
                    ManagementBaseObject outParams = service.InvokeMethod("ChangeStartMode", inParams, null);

                    return (ReturnValue)Enum.Parse(typeof(ReturnValue), outParams["ReturnValue"].ToString());
                }
                catch (Exception ex)
                {
                    CDFMonitor.LogOutputHandler("ChangeStartMode:exception:" + ex.ToString());
                    return (ReturnValue.UnknownFailure);
                }
            }
        }

        /// <summary>
        /// Gets the path.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <returns>System.String.</returns>
        public string GetPath(string svcName)
        {
            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);

            // using (ManagementObject service = new ManagementObject(new ManagementPath(objPath)))
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    return service.Properties["PathName"].Value.ToString();
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// Gets the process id.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <returns>System.Int32.</returns>
        public int GetProcessId(string svcName)
        {
            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);

            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    return int.Parse(service.Properties["ProcessId"].Value.ToString());
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Checks remote service state through WMI returns ServiceState
        /// </summary>
        /// <param name="svcName">string name of service</param>
        /// <returns>ServiceState.</returns>
        public ServiceState GetServiceState(string svcName)
        {
            ServiceState toReturn = ServiceState.Stopped;
            string _state = string.Empty;
            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    _state = service.Properties["State"].Value.ToString().Trim();
                    switch (_state)
                    {
                        case "Running":
                            toReturn = ServiceState.Running;
                            break;

                        case "Stopped":
                            toReturn = ServiceState.Stopped;
                            break;

                        case "Paused":
                            toReturn = ServiceState.Paused;
                            break;

                        case "Start Pending":
                            toReturn = ServiceState.StartPending;
                            break;

                        case "Stop Pending":
                            toReturn = ServiceState.StopPending;
                            break;

                        case "Continue Pending":
                            toReturn = ServiceState.ContinuePending;
                            break;

                        case "Pause Pending":
                            toReturn = ServiceState.PausePending;
                            break;
                    }

                    CDFMonitor.LogOutputHandler("ServiceState:" + toReturn.ToString());
                }
                catch (Exception ex)
                {
                    CDFMonitor.LogOutputHandler("GetServiceState:exception:" + ex.ToString());
                    return (ServiceState.Unknown);
                }
            }
            return toReturn;
        }

        /// <summary>
        /// Installs the service.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <param name="svcDispName">Name of the SVC disp.</param>
        /// <param name="svcPath">The SVC path.</param>
        /// <param name="svcType">Type of the SVC.</param>
        /// <param name="errHandle">The err handle.</param>
        /// <param name="svcStartMode">The SVC start mode.</param>
        /// <param name="interactWithDesktop">if set to <c>true</c> [interact with desktop].</param>
        /// <param name="creds">The creds.</param>
        /// <param name="loadOrderGroup">The load order group.</param>
        /// <param name="loadOrderGroupDependencies">The load order group dependencies.</param>
        /// <param name="svcDependencies">The SVC dependencies.</param>
        /// <returns>ReturnValue.</returns>
        public ReturnValue InstallService(string svcName, string svcDispName, string svcPath, ServiceType svcType,
            OnError errHandle, StartMode svcStartMode, bool interactWithDesktop,
            NetworkCredential creds, string loadOrderGroup,
            string[] loadOrderGroupDependencies, string[] svcDependencies)
        {
            CDFMonitor.LogOutputHandler("InstallService:enter");
            ReturnValue retval = ReturnValue.UnknownFailure;

            string svcPassword = creds.Password.ToString();
            string svcStartName = creds.UserName.ToString();

            if (!CDFMonitor.Instance.Config.AppSettings.UseServiceCredentials)
            {
                svcStartName = string.Empty;
                svcPassword = string.Empty;
            }

            // fixes issue when using local credentials stored in utility creds
            else if (creds.Domain != Properties.Resources.SessionName)
            {
                svcStartName = string.Format("{0}\\{1}",
                    string.IsNullOrEmpty(creds.Domain) ? "." : creds.Domain,
                    creds.UserName);
            }
            else if (creds.Domain == Properties.Resources.SessionName)
            {
                svcStartName = string.Empty;
                svcPassword = string.Empty;
            }

            if (string.IsNullOrEmpty(svcStartName) || string.IsNullOrEmpty(svcPassword))
            {
                svcStartName = "LocalSystem";
                svcPassword = string.Empty;
            }

            CDFMonitor.LogOutputHandler("InstallService:user:" + svcStartName);

            var mc = new ManagementClass(ManScope, new ManagementPath("Win32_Service"), new ObjectGetOptions());
            ManagementBaseObject inParams = mc.GetMethodParameters("create");
            inParams["Name"] = svcName;
            inParams["DisplayName"] = svcDispName;
            inParams["PathName"] = svcPath;
            inParams["ServiceType"] = svcType;
            inParams["ErrorControl"] = errHandle;
            inParams["StartMode"] = svcStartMode.ToString();
            inParams["DesktopInteract"] = interactWithDesktop;
            inParams["StartName"] = svcStartName; // ".\\" + svcStartName;
            inParams["StartPassword"] = svcPassword;
            inParams["LoadOrderGroup"] = loadOrderGroup;
            inParams["LoadOrderGroupDependencies"] = loadOrderGroupDependencies;
            inParams["ServiceDependencies"] = svcDependencies;

            try
            {
                ManagementBaseObject outParams = mc.InvokeMethod("create", inParams, null);
                retval = (ReturnValue)Enum.Parse(typeof(ReturnValue), outParams["ReturnValue"].ToString());
                CDFMonitor.LogOutputHandler("InstallService:exit:return:" + retval);
                return retval;
            }
            catch (Exception ex)
            {
                CDFMonitor.LogOutputHandler("InstallService:exception:" + ex.ToString());
                return retval;
            }
        }

        /// <summary>
        /// Determines whether [is service installed] [the specified SVC name].
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        public ReturnValue IsServiceInstalled(string svcName)
        {
            try
            {
                SelectQuery msQuery = new SelectQuery("SELECT * FROM Win32_Service "
                                            + "Where Name = '" + svcName + "'");
                using (ManagementObjectSearcher searchProcedure = new ManagementObjectSearcher(ManScope, msQuery))
                {
                    if (searchProcedure.Get().Count > 0)
                    {
                        return ReturnValue.True;
                    }
                    else
                    {
                        return ReturnValue.False;
                    }
                }
            }
            catch (Exception ex)
            {
                CDFMonitor.LogOutputHandler("DEBUG:IsServiceInstalled:exception:" + ex.ToString());
                return ReturnValue.False;
            }
        }

        /// <summary>
        /// Pauses the service.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <returns>ReturnValue.</returns>
        public ReturnValue PauseService(string svcName)
        {
            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    ManagementBaseObject outParams = service.InvokeMethod("PauseService", null, null);

                    return (ReturnValue)Enum.Parse(typeof(ReturnValue), outParams["ReturnValue"].ToString());
                }
                catch (Exception ex)
                {
                    if (ex.Message.ToLower().Trim() == "not found" || ex.GetHashCode() == 41149443)
                    {
                        return ReturnValue.ServiceNotFound;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("PauseService:exception:" + ex.ToString());
                        return (ReturnValue.UnknownFailure);
                    }
                }
            }
        }

        /// <summary>
        /// Resumes the service.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <returns>ReturnValue.</returns>
        public ReturnValue ResumeService(string svcName)
        {
            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    ManagementBaseObject outParams = service.InvokeMethod("ResumeService", null, null);

                    return (ReturnValue)Enum.Parse(typeof(ReturnValue), outParams["ReturnValue"].ToString());
                }
                catch (Exception ex)
                {
                    if (ex.Message.ToLower().Trim() == "not found" || ex.GetHashCode() == 41149443)
                    {
                        return ReturnValue.ServiceNotFound;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("ResumeService:exception:" + ex.ToString());
                        return (ReturnValue.UnknownFailure);
                    }
                }
            }
        }

        /// <summary>
        /// Shows the properties.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool ShowProperties(string svcName)
        {
            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    CDFMonitor.LogOutputHandler("Current service status:");
                    foreach (PropertyData a in service.Properties)
                    {
                        CDFMonitor.LogOutputHandler("\t" + a.Name + ":" + a.Value);
                    }
                }
                catch (Exception ex)
                {
                    CDFMonitor.LogOutputHandler("ShowProperties:exception:" + ex.ToString());
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Starts the service.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <returns>ReturnValue.</returns>
        public ReturnValue StartService(string svcName)
        {
            CDFMonitor.LogOutputHandler("StartService:enter");
            ReturnValue retval = ReturnValue.UnknownFailure;

            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    ManagementBaseObject outParams = service.InvokeMethod("StartService", null, null);
                    retval = (ReturnValue)Enum.Parse(typeof(ReturnValue), outParams["ReturnValue"].ToString());
                    CDFMonitor.LogOutputHandler("StartService:exit:" + retval);
                    return WaitForServiceState(svcName, ServiceState.Running);
                }
                catch (Exception ex)
                {
                    if (ex.Message.ToLower().Trim() == "not found" || ex.GetHashCode() == 41149443)
                    {
                        return ReturnValue.ServiceNotFound;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("StartService:exception:" + ex.ToString());
                        return (ReturnValue.UnknownFailure);
                    }
                }
            }
        }

        /// <summary>
        /// Stops given service. Returns ReturnValue.Success if successful or if service not running
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <returns>ReturnValue</returns>
        public ReturnValue StopService(string svcName)
        {
            CDFMonitor.LogOutputHandler("StopService:enter");
            ReturnValue retval = ReturnValue.UnknownFailure;

            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    if (GetServiceState(svcName) == ServiceState.Running)
                    {
                        ManagementBaseObject outParams = service.InvokeMethod("StopService", null, null);
                        retval = (ReturnValue)Enum.Parse(typeof(ReturnValue), outParams["ReturnValue"].ToString());
                        CDFMonitor.LogOutputHandler("StopService:exit:" + retval);

                        return WaitForServiceState(svcName, ServiceState.Stopped);
                    }

                    return ReturnValue.Success;
                }
                catch (Exception ex)
                {
                    if (ex.Message.ToLower().Trim() == "not found" || ex.GetHashCode() == 41149443)
                    {
                        return ReturnValue.ServiceNotFound;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("StopService:exception:" + ex.ToString());
                        return (ReturnValue.UnknownFailure);
                    }
                }
            }
        }

        /// <summary>
        /// Uninstalls the service.
        /// </summary>
        /// <param name="svcName">Name of the SVC.</param>
        /// <returns>ReturnValue.</returns>
        public ReturnValue UninstallService(string svcName)
        {
            CDFMonitor.LogOutputHandler("UnInstallService:enter");
            ReturnValue retval = ReturnValue.UnknownFailure;

            string objPath = string.Format("Win32_Service.Name='{0}'", svcName);
            using (ManagementObject service = new ManagementObject(ManScope, new ManagementPath(objPath), new ObjectGetOptions()))
            {
                try
                {
                    ManagementBaseObject outParams = service.InvokeMethod("delete", null, null);

                    retval = (ReturnValue)Enum.Parse(typeof(ReturnValue), outParams["ReturnValue"].ToString());
                    CDFMonitor.LogOutputHandler("UnInstallService:exit:" + retval);
                    return retval;
                }
                catch (Exception ex)
                {
                    if (ex.Message.ToLower().Trim() == "not found" || ex.GetHashCode() == 41149443)
                    {
                        return ReturnValue.ServiceNotFound;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("UninstallService:exception:" + ex.ToString());
                        return (ReturnValue.UnknownFailure);
                    }
                }
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Waits for given service to be in desired state up to TIME_OUT. Returns
        /// ReturnValue.Success if successful.
        /// </summary>
        /// <param name="svcName">service name to wait on</param>
        /// <param name="serviceState">desired state</param>
        /// <returns>ReturnValue.</returns>
        private ReturnValue WaitForServiceState(string svcName, ServiceState serviceState)
        {
            CDFMonitor.LogOutputHandler("WaitForServiceState:enter");
            ReturnValue retval = ReturnValue.UnknownFailure;
            DateTime timeout = DateTime.Now.Add(new TimeSpan(0, 0, 0, 0, TIME_OUT));
            ServiceState intermediateState = ServiceState.Unknown;
            ServiceState currentState = ServiceState.Unknown;

            switch (serviceState)
            {
                case ServiceState.Running:
                    intermediateState = ServiceState.StartPending;
                    break;

                case ServiceState.Stopped:
                    intermediateState = ServiceState.StopPending;
                    break;

                case ServiceState.Paused:
                    intermediateState = ServiceState.PausePending;
                    break;

                default:
                    break;
            }

            while (DateTime.Now < timeout)
            {
                if ((currentState = GetServiceState(svcName)) == serviceState)
                {
                    CDFMonitor.LogOutputHandler("WaitForServiceState:exit:success");

                    // even though service may be stopped we need to wait
                    if (serviceState == ServiceState.Stopped && CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000))
                    {
                        return ReturnValue.False;
                    }

                    return ReturnValue.Success;
                }

                if (currentState != intermediateState)
                {
                    CDFMonitor.LogOutputHandler("WaitForServiceState:invalid intermediate state:exit:fail");
                    return ReturnValue.False;
                }

                if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000))
                {
                    CDFMonitor.LogOutputHandler("WaitForServiceState:exit:fail");
                    return ReturnValue.False;
                }
            }

            return retval;
        }

        #endregion Private Methods
    }
}