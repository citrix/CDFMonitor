// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="WMI.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Network
{
    using CDFM.Config;
    using CDFM.Engine;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Management;
    using System.Security;

    /// <summary>
    /// Class WMI
    /// </summary>
    internal class WMI
    {
        #region Public Fields

        public bool Status;

        #endregion Public Fields

        #region Private Fields

        private const int CONNECT_TIMEOUT = 60000;
        private readonly string _remoteMachine;
        private ManagementScope _manScope;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// WMI ctor
        /// </summary>
        /// <param name="remoteMachine">The remote machine.</param>
        /// <param name="rootpath">The rootpath.</param>
        /// <param name="creds">The creds.</param>
        public WMI(string remoteMachine, string rootpath = null, ResourceCredential creds = null)
        {
            try
            {
                // ResourceCredential tempcreds = creds ?? new ResourceCredential();
                ResourceCredential tempcreds = creds.Clone() ?? new ResourceCredential();

                _remoteMachine = remoteMachine;

                if (string.IsNullOrEmpty(rootpath))
                {
                    rootpath = @"\\{0}\ROOT\CIMV2";
                }

                SecureString securePassword = null;
                if (!string.IsNullOrEmpty(tempcreds.UserName) && !string.IsNullOrEmpty(tempcreds.Password)
                    && CDFMonitor.Instance.Config.AppSettings.UseCredentials)
                {
                    unsafe
                    {
                        // Instantiate a new secure string.
                        fixed (char* pChars = tempcreds.Password.ToCharArray())
                        {
                            securePassword = new SecureString(pChars, tempcreds.Password.Length);
                        }
                    }
                }
                else
                {
                    tempcreds.UserName = null;
                    tempcreds.Password = null;
                    tempcreds.Domain = null;
                }

                CDFMonitor.LogOutputHandler(string.Format("WMI using credentials: user:{0} domain:{1}", tempcreds.UserName, tempcreds.Domain));

                var options =
                    new ConnectionOptions("MS_409",
                                          string.IsNullOrEmpty(tempcreds.UserName) ? null : tempcreds.UserName,
                                          securePassword,
                                          string.IsNullOrEmpty(tempcreds.Domain)
                                              ? null
                                              : "ntlmdomain:" + tempcreds.Domain,
                                          ImpersonationLevel.Impersonate,
                                          AuthenticationLevel.Default,
                                          true,
                                          null,
                                          new TimeSpan(0, 0, 0, 0, CONNECT_TIMEOUT));

                _manScope = new ManagementScope(String.Format(rootpath, _remoteMachine), options);
                _manScope.Connect();
                Status = true;
                CDFMonitor.LogOutputHandler("DEBUG:WMI initialization: " + Status);
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("WMI exception: " + e.ToString());
                Status = false;
            }
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// Gets or sets the man scope.
        /// </summary>
        /// <value>The man scope.</value>
        public ManagementScope ManScope
        {
            get { return _manScope; }
            set { _manScope = value; }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Checks remote machine process list through wmi for passed processName. returns int count
        /// of processes matching processName. returns -1 if 30 second timeout is hit.
        /// </summary>
        /// <param name="processName">string name of process to check notepad.exe</param>
        /// <param name="id">The id.</param>
        /// <param name="wait">bool default false to wait for process to terminate up to 30
        /// <param name="searchOnlyByName">bool to search only by name and not also with id</param>
        /// seconds</param>
        /// <returns>number of processes matchin processName</returns>
        public int CheckProcess(string processName, int id, bool wait = false, bool searchOnlyByName = false)
        {
            CDFMonitor.LogOutputHandler("DEBUG:CheckProcess:enter:" + processName);
            int count = 0;
            int procCount = 0;
            string processIdFilter;

            // search by id as well as name
            if (searchOnlyByName)
            {
                processIdFilter = string.Empty;
            }
            else if (id == 0)
            {
                CDFMonitor.LogOutputHandler("CheckProcess:exiting: invalid id:" + id.ToString());
                return -1;
            }
            else
            {
                processIdFilter = string.Format(" AND ProcessId = '{0}'", id);
            }

            try
            {
                while (count < 600)
                {
                    var msQuery = new SelectQuery(string.Format("SELECT * FROM Win32_Process Where Name = '{0}'{1}", processName, processIdFilter));

                    var searchProcedure = new ManagementObjectSearcher(_manScope, msQuery);
                    procCount = searchProcedure.Get().Count;

                    if (procCount == 0 | !wait)
                    {
                        if (searchOnlyByName)
                        {
                            CDFMonitor.LogOutputHandler(string.Format("CheckProcess:exit:{0} loop:{1} process count:{2}", procCount, count, procCount));
                        }
                        else
                        {
                            CDFMonitor.LogOutputHandler(string.Format("CheckProcess:exit:{0} loop:{1} pid:{2}", procCount, count, id));
                        }
                        return (procCount);
                    }

                    if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000))
                    {
                        return 0;
                    }

                    CDFMonitor.LogOutputHandler("DEBUG:CheckProcess:count:" + count);
                    count++;
                }

                return -1;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("Fail:CheckProcess:exception:" + e.ToString());
                return (-1);
            }
        }

        /// <summary>
        /// Gets the SYSTEMROOT.
        /// </summary>
        /// <returns>System.String.</returns>
        public string GetSYSTEMROOT()
        {
            try
            {
                string retval = string.Empty;
                var objectGetOptions = new ObjectGetOptions();
                var managementPath = new ManagementPath("Win32_OperatingSystem");
                var processClass = new ManagementClass(_manScope, managementPath, objectGetOptions);
                ManagementObjectCollection objs = processClass.GetInstances();
                foreach (ManagementObject obj in objs)
                {
                    retval = obj["WindowsDirectory"].ToString();
                    break;
                }
                objs.Dispose();
                CDFMonitor.LogOutputHandler("DEBUG:GetSYSTEMROOT returned: " + retval);

                return retval;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("GetSYSTEMROOT exception: " + e.ToString());
                return (string.Empty);
            }
        }

        /// <summary>
        /// Kills the process.
        /// </summary>
        /// <param name="processName">Name of the process.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool KillProcess(string processName)
        {
            try
            {
                CDFMonitor.LogOutputHandler("DEBUG:KillProcess:enter:" + processName);
                var msQuery = new SelectQuery("SELECT * FROM Win32_Process "
                                              + "Where Name = '" + processName + "'"
                                              + "AND Handle != '" + Process.GetCurrentProcess().Id + "'");
                var searchProcedure = new ManagementObjectSearcher(_manScope, msQuery);
                foreach (ManagementObject item in searchProcedure.Get())
                {
                    try
                    {
                        item.InvokeMethod("Terminate", null);
                    }
                    catch (SystemException e)
                    {
                        CDFMonitor.LogOutputHandler("DEBUG:KillProcess:exception:" + e.ToString());
                        return false;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("Fail:KillProcess:exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Reads the registry modules.
        /// </summary>
        /// <param name="registryPath">The registry path.</param>
        /// <returns>Dictionary{System.StringSystem.String}.</returns>
        public Dictionary<string, string> ReadRegistryModules(string registryPath)
        {
            var registryValues = new Dictionary<string, string>();
            bool hasAccess = false;
            ManagementBaseObject outVals;

            try
            {
                CDFMonitor.LogOutputHandler("ReadRegistry:enter:" + registryPath);

                var objectGetOptions = new ObjectGetOptions();
                var managementPath = new ManagementPath("StdRegProv");
                var processClass = new ManagementClass(_manScope, managementPath, objectGetOptions);
                ManagementBaseObject inParams = processClass.GetMethodParameters("CheckAccess");

                // Add the input parameters.
                // HKLM
                inParams["hDefKey"] = 2147483650;
                inParams["sSubKeyName"] = registryPath;

                // Enumerate subkeys
                inParams["uRequired"] = 8;

                // Execute the method and obtain the return values.
                ManagementBaseObject outParams = processClass.InvokeMethod("CheckAccess", inParams, null);

                hasAccess = (bool)outParams["bGranted"];

                CDFMonitor.LogOutputHandler("bGranted: " + hasAccess);
                CDFMonitor.LogOutputHandler("ReturnValue: " + outParams["ReturnValue"]);

                if (hasAccess)
                {
                    // Read key
                    outVals = processClass.InvokeMethod("EnumKey", inParams, null);
                    var subkeys = (string[])outVals["sNames"];

                    inParams = processClass.GetMethodParameters("GetStringValue");
                    inParams["hDefKey"] = 0x80000002; // 2147483650;
                    inParams["sValueName"] = "GUID";

                    foreach (string key in subkeys)
                    {
                        inParams["sSubKeyName"] = registryPath + "\\" + key;
                        outVals = processClass.InvokeMethod("GetStringValue", inParams, null);
                        registryValues.Add((string)outVals["sValue"], key);
                    }
                }

                return (registryValues);
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("Fail:ReadRegistry:exception:" + e.ToString());
                return (registryValues);
            }
        }

        /// <summary>
        /// Runs the process.
        /// </summary>
        /// <param name="remoteCommand">The remote command.</param>
        /// <returns>System.Int32.</returns>
        public int RunProcess(string remoteCommand)
        {
            try
            {
                CDFMonitor.LogOutputHandler("Runprocess:enter:" + remoteCommand);

                var objectGetOptions = new ObjectGetOptions();
                var managementPath = new ManagementPath("Win32_Process");
                var processClass = new ManagementClass(_manScope, managementPath, objectGetOptions);
                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");
                inParams["CommandLine"] = remoteCommand;
                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);

                CDFMonitor.LogOutputHandler("DEBUG:RunProcess:Creation of the process returned: " +
                                              outParams["returnValue"]);
                CDFMonitor.LogOutputHandler("DEBUG:RunProcess:Process ID: " + outParams["processId"]);

                return (Convert.ToInt32(outParams["processId"]));
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("Fail:Runprocess:exception:" + e.ToString());
                return (0);
            }
        }

        #endregion Public Methods
    }
}