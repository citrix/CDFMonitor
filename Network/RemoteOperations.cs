// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="RemoteOperations.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************
namespace CDFM.Network
{
    using CDFM.Config;
    using CDFM.Engine;
    using CDFM.FileManagement;
    using CDFM.Properties;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Text;

    /// <summary>
    /// Class RemoteOperations
    /// </summary>
    public class RemoteOperations
    {
        #region Private Fields

        private const int SLEEP_TIME = 60000;
        private readonly ResourceCredential _creds;
        private readonly string _path = string.Empty;
        private readonly string _processName = Process.GetCurrentProcess().MainModule.ModuleName;
        private readonly ResourceManagement _resourceCredential = new ResourceManagement();
        private readonly int _retry = 0;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes static members of the <see cref="RemoteOperations" /> class.
        /// </summary>
        static RemoteOperations()
        {
            Initialize();
        }

        /// <summary>
        /// RemoteOperations ctor
        /// </summary>
        /// <param name="args">multiple string argument. use string constructor for single.</param>
        /// <param name="path">The path.</param>
        /// <param name="retry">The timeout.</param>
        /// <param name="creds">The creds.</param>
        /// <param name="sender">The sender.</param>
        public RemoteOperations(Dictionary<string, RemoteStatus> args, string path, int retry = 0, ResourceCredential creds = null)//, object sender = null)
        {
            Initialize();
            RemoteList = args;
            _creds = creds;
            _path = path;
            _retry = retry;
        }

        #endregion Public Constructors

        #region Public Enums

        /// <summary>
        /// Enum RemoteOperationMethods
        /// </summary>
        public enum RemoteOperationMethods
        {
            Check,
            Deploy,
            Gather,
            UnDeploy,
            Modify,
            Start,
            Stop,
            Unknown
        }

        /// <summary>
        /// Return enumerator for RemoteOperations class
        /// </summary>
        public enum RemoteStatus
        {
            Deployed,
            Gathered,
            Error,
            Modified,
            Stopped,
            Stopping,
            Started,
            Starting,
            Success,
            UnDeployed,
            Unknown,
        }

        #endregion Public Enums

        #region Public Properties

        /// <summary>
        /// Gets the remote list.
        /// </summary>
        /// <value>The remote list.</value>
        public static Dictionary<string, RemoteStatus> RemoteList
        {
            get;
            set;
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Pings the specified entry.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool Ping(string entry)
        {
            try
            {
                if (string.IsNullOrEmpty(entry))
                {
                    CDFMonitor.LogOutputHandler("Ping: entry empty. returning false.");
                    return false;
                }

                CDFMonitor.LogOutputHandler("Ping: entry:" + entry);
                Ping pingSender = new Ping();
                PingOptions options = new PingOptions();
                int count = 0;

                // Use the default Ttl value which is 128, but change the fragmentation behavior.
                options.DontFragment = true;

                // Create a buffer of 32 bytes of data to be transmitted.
                string data = "--------------------------------";
                byte[] buffer = Encoding.ASCII.GetBytes(data);
                int timeout = 120;
                while (count < 5)
                {
                    PingReply reply = pingSender.Send(entry, timeout, buffer, options);
                    if (reply.Status == IPStatus.Success)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Ping:Reply Address: {0}", reply.Address));
                        CDFMonitor.LogOutputHandler(string.Format("Ping:RoundTrip time: {0}", reply.RoundtripTime));
                        return true;
                    }

                    CDFMonitor.LogOutputHandler("Ping failed. retrying");
                    if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000))
                    {
                        return false;
                    }

                    count++;
                }

                return false;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("Ping failed");
                CDFMonitor.LogOutputHandler("DEBUG:Ping: exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Checks remote machine for existence of utility. Returns true if successful.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool Check()
        {
            bool retVal = true;
            string source = string.Empty;
            int i = 0;

            do
            {
                i++;

                foreach (string machine in new List<string>(RemoteList.Keys))
                {
                    if (RemoteList[machine] != RemoteStatus.Unknown
                        && RemoteList[machine] != RemoteStatus.Error)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Check: skipping machine {0} because of cached state: {1}", machine, RemoteList[machine]));
                        continue;
                    }

                    source = "\\\\" + machine + "\\admin$\\cdfmonitor";
                    CDFMonitor.LogOutputHandler("Check: processing machine:" + machine + " state:" +
                                                  RemoteList[machine].ToString());
                    WMIServicesManager wmi;
                    RemoteStatus rs = RemoteStatus.Error;

                    if (Ping(machine)
                        && (wmi = new WMIServicesManager(machine, _creds)) != null
                        && wmi.Status
                        )
                    {
                        if (wmi.IsServiceInstalled(Resources.ServiceName) == WMIServicesManager.ReturnValue.False)
                        {
                            rs = RemoteStatus.UnDeployed;
                        }
                        else
                        {
                            wmi.ShowProperties(Resources.ServiceName);
                            rs = CheckServiceState(wmi);
                        }

                        RemoteList[machine] = rs;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("Fail:Check: unsuccessful.");
                        RemoteList[machine] = RemoteStatus.Error;
                        retVal = false;
                    }

                    if (!CheckProgress(false, machine, i)) return retVal;
                }
            }
            while ((RemoteList.Count(v => (v.Value == RemoteStatus.Unknown | v.Value == RemoteStatus.Error)) != 0)
                && CheckProgress(true, string.Empty, i));

            CDFMonitor.LogOutputHandler("Check: exit:" + retVal);

            return retVal;
        }

        /// <summary>
        /// Deploys the specified start mode.
        /// </summary>
        /// <param name="startMode">The start mode.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool Deploy(string startMode)
        {
            bool retVal = true;
            string destination = string.Empty;
            if (!CheckSource(_path))
            {
                return false;
            }

            WMIServicesManager.StartMode sm;
            if (string.IsNullOrEmpty(startMode))
            {
                sm = WMIServicesManager.StartMode.Automatic;
            }
            else if (!ConvertToStartMode(startMode, out sm))
            {
                return false;
            }

            int i = 0;

            // Add current running exe to list so it doesnt have to be in staged dir
            List<string> fileList = FileManager.GetFiles(_path, "*.*", SearchOption.AllDirectories).ToList();
            fileList.Add(AppDomain.CurrentDomain.FriendlyName);

            do
            {
                i++;

                foreach (string machine in new List<string>(RemoteList.Keys))
                {
                    if (RemoteList[machine] == RemoteStatus.Deployed)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Deploy: skipping machine {0} because of cached state: {1}", machine, RemoteList[machine]));
                        continue;
                    }

                    CDFMonitor.LogOutputHandler("Deploy: processing machine:" + machine + " state:" +
                                                  RemoteList[machine].ToString());
                    WMIServicesManager wmi;
                    destination = "\\\\" + machine + "\\admin$\\cdfmonitor";

                    if (Ping(machine)
                        && (wmi = new WMIServicesManager(machine, _creds)) != null
                        && wmi.Status
                        && (wmi.IsServiceInstalled(Resources.ServiceName) == WMIServicesManager.ReturnValue.True
                            && (wmi.StopService(Resources.ServiceName) == WMIServicesManager.ReturnValue.Success))
                           | (FileManager.CheckPath(destination, true)
                           && FileManager.CopyFiles(fileList.ToArray(), destination, true)
                           && SetSeServiceLogonRight(wmi)
                           && wmi.InstallService(Resources.ServiceName,
                                                 Resources.FriendlyName,
                                                 wmi.GetSYSTEMROOT() + "\\cdfmonitor\\cdfmonitor.exe /runningasservice",
                                                 WMIServicesManager.ServiceType.OwnProcess,
                                                 WMIServicesManager.OnError.UserIsNotNotified,
                                                 sm,
                                                 false,
                                                 _creds,
                                                 null,
                                                 null,
                                                 null) == WMIServicesManager.ReturnValue.Success))
                    {
                        if (sm == WMIServicesManager.StartMode.Automatic
                            &&
                            (wmi.ChangeStartMode(Resources.ServiceName, sm)) != WMIServicesManager.ReturnValue.Success
                            || (wmi.StartService(Resources.ServiceName) != WMIServicesManager.ReturnValue.Success))
                        {
                            RemoteList[machine] = RemoteStatus.Error;
                        }
                        else
                        {
                            RemoteList[machine] = RemoteStatus.Deployed;
                        }

                        wmi.ShowProperties(Resources.ServiceName);
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("Fail:Deploy: unsuccessful.");
                        RemoteList[machine] = RemoteStatus.Error;
                        retVal = false;
                    }

                    if (!CheckProgress(false, machine, i)) return retVal;
                }
            }
            while ((RemoteList.Count(v => v.Value == RemoteStatus.Deployed) != RemoteList.Count) && CheckProgress(true, string.Empty, i));

            return retVal;
        }

        /// <summary>
        /// Gathers this instance.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool Gather()
        {
            bool retVal = true;
            string source = string.Empty;
            string destination = string.Empty;
            if (!FileManager.CheckPath(_path, true))
            {
                return false;
            }

            int i = 0;
            do
            {
                i++;

                foreach (string machine in new List<string>(RemoteList.Keys))
                {
                    if (RemoteList[machine] == RemoteStatus.Gathered)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Gather: skipping machine {0} because of cached state: {1}", machine, RemoteList[machine]));
                        continue;
                    }

                    CDFMonitor.LogOutputHandler("Gather: processing machine:" + machine + " state:" +
                                                  RemoteList[machine].ToString());
                    WMIServicesManager wmi;
                    source = "\\\\" + machine + "\\admin$\\cdfmonitor";
                    destination = _path + "\\" + machine;
                    int pid = 0;
                    RemoteStatus rs = RemoteStatus.Unknown;

                    if (Ping(machine)
                        && (wmi = new WMIServicesManager(machine, _creds)) != null
                        && wmi.Status
                        && (wmi.IsServiceInstalled(Resources.ServiceName) == WMIServicesManager.ReturnValue.True
                            && ((rs = CheckServiceState(wmi)) != RemoteStatus.Started
                                || wmi.StopService(Resources.ServiceName) == WMIServicesManager.ReturnValue.Success))
                        | (FileManager.CheckPath(source, false)
                           && (pid = wmi.RunProcess(wmi.GetSYSTEMROOT() + "\\cdfmonitor\\cdfmonitor.exe /zip")) != 0
                           && wmi.CheckProcess(_processName, pid, true) == 0
                           && FileManager.CopyFiles(FileManager.GetFiles(source, "*.zip", SearchOption.TopDirectoryOnly), destination, true)
                           && (FileManager.DeleteFiles(FileManager.GetFiles(source, "*.zip", SearchOption.TopDirectoryOnly), true) == FileManager.Results.Success)
                           && (rs != RemoteStatus.Started
                                || wmi.StartService(Resources.ServiceName) == WMIServicesManager.ReturnValue.Success)))
                    {
                        RemoteList[machine] = RemoteStatus.Gathered;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("Fail:Gather: unsuccessful.");
                        RemoteList[machine] = RemoteStatus.Error;
                        retVal = false;
                    }

                    if (!CheckProgress(false, machine, i, destination)) return retVal;
                }
            }
            while ((RemoteList.Count(v => v.Value == RemoteStatus.Gathered) != RemoteList.Count) && CheckProgress(true, string.Empty, i));

            return retVal;
        }

        /// <summary>
        /// Modifies the specified start mode.
        /// </summary>
        /// <param name="startMode">The start mode.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool Modify(string startMode)
        {
            bool retVal = true;
            string destination = string.Empty;
            WMIServicesManager.StartMode sm;
            if (!ConvertToStartMode(startMode, out sm))
            {
                return false;
            }

            int i = 0;
            do
            {
                i++;

                foreach (string machine in new List<string>(RemoteList.Keys))
                {
                    if (RemoteList[machine] == RemoteStatus.Modified)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Modify: skipping machine {0} because of cached state: {1}", machine, RemoteList[machine]));
                        continue;
                    }

                    CDFMonitor.LogOutputHandler("Modify: processing machine:" + machine + " state:" +
                                                  RemoteList[machine].ToString());
                    WMIServicesManager wmi;
                    destination = "\\\\" + machine + "\\admin$\\cdfmonitor";

                    if (Ping(machine)
                        && (wmi = new WMIServicesManager(machine, _creds)) != null
                        && wmi.Status
                        && (wmi.IsServiceInstalled(Resources.ServiceName) == WMIServicesManager.ReturnValue.True
                            && (wmi.GetServiceState(Resources.ServiceName) != WMIServicesManager.ServiceState.Stopped
                                && wmi.StopService(Resources.ServiceName) == WMIServicesManager.ReturnValue.Success)
                            | wmi.ChangeStartMode(Resources.ServiceName, sm) == WMIServicesManager.ReturnValue.Success))
                    {
                        if (sm == WMIServicesManager.StartMode.Automatic
                            && (wmi.StartService(Resources.ServiceName) != WMIServicesManager.ReturnValue.Success))
                        {
                            RemoteList[machine] = RemoteStatus.Error;
                        }
                        else
                        {
                            RemoteList[machine] = RemoteStatus.Modified;
                        }
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("Fail:Modify: unsuccessful.");
                        RemoteList[machine] = RemoteStatus.Error;
                        retVal = false;
                    }

                    if (!CheckProgress(false, machine, i)) return retVal;
                }
            }
            while ((RemoteList.Count(v => v.Value == RemoteStatus.Modified) != RemoteList.Count) && CheckProgress(true, string.Empty, i));

            return retVal;
        }

        /// <summary>
        /// Reads the registry modules.
        /// </summary>
        /// <param name="registryKey">The registry key.</param>
        /// <param name="result">The result.</param>
        /// <returns>Dictionary{System.StringSystem.String}.</returns>
        public Dictionary<string, string> ReadRegistryModules(string registryKey, out RemoteStatus result)
        {
            bool retVal = true;
            string source = string.Empty;
            Dictionary<string, string> remoteRegList = new Dictionary<string, string>();
            result = RemoteStatus.Error;

            foreach (string machine in new List<string>(RemoteList.Keys))
            {
                CDFMonitor.LogOutputHandler("ReadRegistry: processing machine:" + machine + " state:" +
                                                RemoteList[machine].ToString());
                WMI wmi;

                if (Ping(machine)
                    && (wmi = new WMI(machine, @"\\{0}\ROOT\DEFAULT", _creds)) != null
                    && wmi.Status
                    && ((remoteRegList = wmi.ReadRegistryModules(registryKey)).Count > 0)
                    )
                {
                    if (remoteRegList.Count > 0)
                    {
                        RemoteList[machine] = RemoteStatus.Success;
                        result = RemoteStatus.Success;
                    }
                    else
                    {
                        RemoteList[machine] = RemoteStatus.Error;
                    }
                }
                else
                {
                    CDFMonitor.LogOutputHandler("Fail:Check: unsuccessful.");
                    RemoteList[machine] = RemoteStatus.Error;
                }

                // Right now this function only for one machine so break
                break;
            }

            CDFMonitor.LogOutputHandler("DEBUG:Check: exit:" + retVal);

            return remoteRegList;
        }

        /// <summary>
        /// Starts the remote.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool StartRemote()
        {
            bool retVal = true;
            string destination = string.Empty;

            int i = 0;
            do
            {
                i++;

                foreach (string machine in new List<string>(RemoteList.Keys))
                {
                    if (RemoteList[machine] == RemoteStatus.Started)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("StartRemote: skipping machine {0} because of cached state: {1}", machine, RemoteList[machine]));
                        continue;
                    }

                    CDFMonitor.LogOutputHandler("StartRemote: processing machine:" + machine + " state:" +
                                                  RemoteList[machine].ToString());
                    WMIServicesManager wmi;

                    if (Ping(machine)
                        && (wmi = new WMIServicesManager(machine, _creds)) != null
                        && wmi.Status
                        && (wmi.IsServiceInstalled(Resources.ServiceName) == WMIServicesManager.ReturnValue.True
                            && wmi.StartService(Resources.ServiceName) == WMIServicesManager.ReturnValue.Success))
                    {
                        if (wmi.GetServiceState(Resources.ServiceName) == WMIServicesManager.ServiceState.Running)
                        {
                            RemoteList[machine] = RemoteStatus.Started;
                        }
                        else
                        {
                            RemoteList[machine] = RemoteStatus.Error;
                        }
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("Fail:StartRemote: unsuccessful.");
                        RemoteList[machine] = RemoteStatus.Error;
                        retVal = false;
                    }

                    if (!CheckProgress(false, machine, i)) return retVal;
                }
            }
            while ((RemoteList.Count(v => v.Value == RemoteStatus.Started) != RemoteList.Count) && CheckProgress(true, string.Empty, i));

            return retVal;
        }

        /// <summary>
        /// Stops the remote.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool StopRemote()
        {
            bool retVal = true;
            string destination = string.Empty;

            int i = 0;
            do
            {
                i++;

                foreach (string machine in new List<string>(RemoteList.Keys))
                {
                    if (RemoteList[machine] == RemoteStatus.Stopped)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("StopRemote: skipping machine {0} because of cached state: {1}", machine, RemoteList[machine]));
                        continue;
                    }

                    CDFMonitor.LogOutputHandler("StopRemote: processing machine:" + machine + " state:" +
                                                  RemoteList[machine].ToString());
                    WMIServicesManager wmi;

                    if (Ping(machine)
                        && (wmi = new WMIServicesManager(machine, _creds)) != null
                        && wmi.Status
                        && (wmi.IsServiceInstalled(Resources.ServiceName) == WMIServicesManager.ReturnValue.True
                            && wmi.StopService(Resources.ServiceName) == WMIServicesManager.ReturnValue.Success))
                    {
                        if (wmi.GetServiceState(Resources.ServiceName) == WMIServicesManager.ServiceState.Stopped)
                        {
                            RemoteList[machine] = RemoteStatus.Stopped;
                        }
                        else
                        {
                            RemoteList[machine] = RemoteStatus.Error;
                        }
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("Fail:StopRemote: unsuccessful.");
                        RemoteList[machine] = RemoteStatus.Error;
                        retVal = false;
                    }

                    if (!CheckProgress(false, machine, i)) return retVal;
                }
            }
            while ((RemoteList.Count(v => v.Value == RemoteStatus.Stopped) != RemoteList.Count) && CheckProgress(true, string.Empty, i));

            return retVal;
        }

        /// <summary>
        /// Uns the deploy.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool UnDeploy()
        {
            bool retVal = true;
            string destination = string.Empty;

            int i = 0;
            do
            {
                i++;

                foreach (string machine in new List<string>(RemoteList.Keys))
                {
                    if (RemoteList[machine] == RemoteStatus.UnDeployed)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("UnDeploy: skipping machine {0} because of cached state: {1}", machine, RemoteList[machine]));
                        continue;
                    }

                    CDFMonitor.LogOutputHandler("UnDeploy: processing machine:" + machine + " state:" +
                                                  RemoteList[machine].ToString());
                    WMIServicesManager wmi;
                    destination = "\\\\" + machine + "\\admin$\\cdfmonitor";
                    int pid = 0;

                    if (Ping(machine)
                        && (wmi = new WMIServicesManager(machine, _creds)) != null
                        && wmi.Status
                        && (wmi.IsServiceInstalled(Resources.ServiceName) == WMIServicesManager.ReturnValue.True
                            && wmi.StopService(Resources.ServiceName) == WMIServicesManager.ReturnValue.Success
                            && wmi.UninstallService(Resources.ServiceName) == WMIServicesManager.ReturnValue.Success)
                        | (FileManager.CheckPath(destination, true)
                           &&
                           ((pid = wmi.RunProcess(string.Format("{0}\\cdfmonitor\\cdfmonitor.exe /clean", wmi.GetSYSTEMROOT()))) != 0
                            & wmi.CheckProcess(_processName, pid, true) == 0)
                            && FileManager.DeleteFolder(destination, false)))
                    {
                        RemoteList[machine] = RemoteStatus.UnDeployed;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("Fail:UnDeploy: unsuccessful.");
                        RemoteList[machine] = RemoteStatus.Error;
                        retVal = false;
                    }

                    if (!CheckProgress(false, machine, i)) return retVal;
                }
            }
            while ((RemoteList.Count(v => v.Value == RemoteStatus.UnDeployed)) != RemoteList.Count && CheckProgress(true, string.Empty, i));

            return retVal;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Checks the state of the service.
        /// </summary>
        /// <param name="wmi">The WMI.</param>
        /// <returns>RemoteStatus.</returns>
        private static RemoteStatus CheckServiceState(WMIServicesManager wmi)
        {
            RemoteStatus rs;
            WMIServicesManager.ServiceState ss = wmi.GetServiceState(Resources.ServiceName);
            switch (ss)
            {
                case WMIServicesManager.ServiceState.Unknown:
                    rs = RemoteStatus.Unknown;
                    break;

                case WMIServicesManager.ServiceState.StopPending:
                    rs = RemoteStatus.Stopping;
                    break;

                case WMIServicesManager.ServiceState.Stopped:
                    rs = RemoteStatus.Stopped;
                    break;

                case WMIServicesManager.ServiceState.StartPending:
                    rs = RemoteStatus.Starting;
                    break;

                case WMIServicesManager.ServiceState.Running:
                    rs = RemoteStatus.Started;
                    break;

                default:
                    rs = RemoteStatus.Error;
                    break;
            }
            return rs;
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        private static void Initialize()
        {
            if (RemoteList == null)
            {
                RemoteList = new Dictionary<string, RemoteStatus>();
            }
        }

        /// <summary>
        /// checks given timeout and returns false if timeout has elapsed. sleeps for SLEEP_TIME
        /// returns true if time still available.
        /// </summary>
        /// <param name="sleep">if set to <c>true</c> [sleep].</param>
        /// <param name="machine">The machine.</param>
        /// <param name="currentCount">The current count.</param>
        /// <param name="description">optional string description</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool CheckProgress(bool sleep, string machine, int currentCount, string description = null)
        {
            CDFMonitor.LogOutputHandler(string.Format("DEBUG:CheckTimer:enter:{0}:{1}:{2}", sleep, machine, currentCount));

            if (currentCount > _retry && _retry > 0)
            {
                CDFMonitor.LogOutputHandler("CheckTimer: remoteAction retry limit reached. exiting.");
                return false;
            }

            if (string.IsNullOrEmpty(machine))
            {
                // All machines have been processed so return false
                return false;
            }

            if (!CDFMonitor.ManageGuiWorker(100 * currentCount / RemoteList.Keys.Count, string.Format(" {0}: {1}: {2}", machine, RemoteList[machine], description)))
            {
                return false;
            }

            if (sleep && CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks the source.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool CheckSource(string source)
        {
            // todo: move to configurationverify
            if (!FileManager.CheckPath(source))
            {
                CDFMonitor.LogOutputHandler("CheckSource: invalid source path. exiting:" + source);
                return false;
            }

            if (!FileManager.FileExists(source + "\\cdfmonitor.exe.config"))
            {
                CDFMonitor.LogOutputHandler(
                    "CheckSource: path folder does not contain cdfmonitor.exe.config. exiting:" + source);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Converts to start mode.
        /// </summary>
        /// <param name="startMode">The start mode.</param>
        /// <param name="sm">The sm.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool ConvertToStartMode(string startMode, out WMIServicesManager.StartMode sm)
        {
            sm = new WMIServicesManager.StartMode();
            bool foundmode = false;

            if (string.IsNullOrEmpty(startMode))
            {
                return false;
            }

            foreach (WMIServicesManager.StartMode mode in Enum.GetValues(typeof(WMIServicesManager.StartMode)))
            {
                if (string.Compare(mode.ToString(), startMode, true) == 0)
                {
                    foundmode = true;
                    sm = mode;
                    break;
                }
            }

            if (!foundmode)
            {
                CDFMonitor.LogOutputHandler("Fail:ConvertToStartMode: unsuccessful.: " + startMode);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sets the se service logon right.
        /// </summary>
        /// <param name="wmi">The WMI.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool SetSeServiceLogonRight(WMIServicesManager wmi)
        {
            if (!CDFMonitor.Instance.Config.AppSettings.UseServiceCredentials)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(_creds.UserName)
                && _creds.Domain != Properties.Resources.SessionName)
            {
                string userName = _creds.UserName.ToString();
                if (!string.IsNullOrEmpty(_creds.Domain)
                    && !_creds.UserName.Contains(_creds.Domain))
                {
                    userName = string.Format("{0}\\{1}", _creds.Domain, _creds.UserName);
                }

                // get remote path string path = wmi.GetPath(Properties.Resources.ServiceName);

                int id = wmi.RunProcess(string.Format("{0}\\cdfmonitor\\cdfmonitor.exe /seservicelogonright: {1}", wmi.GetSYSTEMROOT(), userName));
                return (wmi.CheckProcess(_processName, id, true) == 0);
            }
            else
            {
                CDFMonitor.LogOutputHandler("skipping seservicelogonright");
            }

            return true;
        }

        #endregion Private Methods
    }
}