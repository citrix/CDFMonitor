// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="ConfigurationVerify.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************

// -----------------------------------------------------------------------
// <copyright file="ConfigurationVerify.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace CDFM.Config
{
    using CDFM.Engine;
    using CDFM.FileManagement;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Text.RegularExpressions;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class ConfigurationVerify
    {
        #region Private Fields

        private Configuration _config;
        private Configuration.ConfigurationProperties _referenceConfig;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationVerify" /> class.
        /// </summary>
        /// <param name="config">The config.</param>
        public ConfigurationVerify(Configuration config)
        {
            _config = config;
            _referenceConfig = new Configuration.ConfigurationProperties();
        }

        #endregion Public Constructors

        #region Public Methods

        /// <summary>
        /// Verifies all settings.
        /// </summary>
        /// <param name="repair">if set to <c>true</c> [repair].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool VerifyAllSettings(bool repair)
        {
            bool tempVal = true;

            try
            {
                foreach (KeyValueConfigurationElement setting in _config.AppSettings.ToKeyValueConfigurationCollection())
                {
                    tempVal &= VerifySetting(
                        new KeyValuePair<string, object>(setting.Key, setting.Value), repair);
                }

                CDFMonitor.LogOutputHandler("VerifyAllSettings Results:" + tempVal.ToString());
                return tempVal;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("VerifyAllSettings:Exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Verifies the setting.
        /// </summary>
        /// <param name="setting">The setting.</param>
        /// <param name="repair">if set to <c>true</c> [repair].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool VerifySetting(string setting, bool repair)
        {
            return VerifySetting(_config.AppSettings.ToKeyValuePair(setting), repair);
        }

        /// <summary>
        /// Verifies the setting.
        /// </summary>
        /// <param name="setting">The setting.</param>
        /// <param name="repair">if set to <c>true</c> [repair].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool VerifySetting(KeyValuePair<string, object> setting, bool repair)
        {
            bool retval = true;
            Debug.Print("DEBUG:VerifySetting:Enter:" + setting.Key);

            // Check against default reference and only verify if not default.
            if (setting.Key == typeof(Configuration.ConfigurationProperties)
                .GetProperty(setting.Key).GetValue(_referenceConfig, null).ToString())
            {
                Debug.Print("DEBUG:VerifySetting:value same as default:" + setting.Key);
                return retval;
            }

            try
            {
                switch ((Configuration.ConfigurationProperties.EnumProperties)Enum
                    .Parse(typeof(Configuration.ConfigurationProperties.EnumProperties), setting.Key))
                {
                    case Configuration.ConfigurationProperties.EnumProperties.Activity:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.AdvancedOptions:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.AllowSingleInstance:

                        // make sure only one instance per session
                        if (_config.AppSettings.AllowSingleInstance && !_config.IsFirstInstance())
                        {
                            CDFMonitor.LogOutputHandler("Failure: another instance running:exiting");
                            retval = false;
                            break;
                        }

                        retval = true;
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.Annoyance:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.AutoScroll:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.BufferLines:

                        // set up to max regardless of repair
                        CDFMonitor.LogOutputHandler(string.Format("BufferLines should be less than 1,000,000:{0}",
                        retval = Convert.ToInt32(setting.Value) < 1000000));
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.BufferMax:

                        // At least size of buffermin
                        CDFMonitor.LogOutputHandler(string.Format("BufferMax should be greater than BufferMin:{0}",
                        retval = Convert.ToInt32(setting.Value) > _config.AppSettings.BufferMin));
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.BufferMin:

                        // At least 2 per proc
                        CDFMonitor.LogOutputHandler(string.Format("BufferMin should be 2 x #processors:{0}",
                        retval = Environment.ProcessorCount * 2 <= Convert.ToInt32(_config.AppSettings.BufferMin)));
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.BufferSize:

                        // 1024 (in kb) is limit
                        CDFMonitor.LogOutputHandler(string.Format("BufferSize should be less than 1024 and greater than 0:{0}",
                        retval = Convert.ToInt32(setting.Value) < 1024 && Convert.ToInt32(setting.Value) > 0));
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.ConfigFile:

                        // should exist
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.Debug:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.DeployPath:

                        retval = VerifyDeployPath(repair);
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.DisplayFilter:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.EnableFlags:

                        // should really support uint but it currently doesnt
                        // uint 4294967295
                        // int 2147483647
                        // 0xffffff max for user trace

                        // CDFMonitor.LogOutputHandler(string.Format("EnableFlags should be uint.MaxValue (0xffffff) or 0:{0}",
                        CDFMonitor.LogOutputHandler(string.Format("EnableFlags should be {0} or 0:{1}", uint.MaxValue,
                        retval = _config.AppSettings.EnableFlags >= 0
                            & _config.AppSettings.EnableFlags <= uint.MaxValue));

                        // if enable flags > 0 then it is a kernel trace and max log count has to be 0
                        if (_config.AppSettings.EnableFlags > 0 & _config.AppSettings.EnableFlags < uint.MaxValue & _config.Activity == Configuration.ActivityType.TraceToEtl)
                        {
                            CDFMonitor.LogOutputHandler(string.Format("LogFileMaxCount has to 0 or 1 for kernel tracing:{0}",
                                retval = _config.AppSettings.LogFileMaxCount == 0 | _config.AppSettings.LogFileMaxCount == 1));
                        }

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.EventCommand:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.EventCommandWait:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.EventMaxCount:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.EventThrottle:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.GatherPath:

                        retval = VerifyGatherPath(repair);
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogFileAutoFlush:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogFileMaxCount:

                        CDFMonitor.LogOutputHandler(string.Format("LogFileMaxcount should be less than 9999:{0}",
                            retval = Convert.ToInt32(setting.Value) < 9999));
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogFileMaxSize:

                        CDFMonitor.LogOutputHandler(string.Format("LogFileMaxSize should be less than 1024:{0}",
                        retval = Convert.ToInt32(setting.Value) < 1024));
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogFileName:

                        if (string.IsNullOrEmpty(_config.AppSettings.LogFileName))
                        {
                            if (repair)
                            {
                                CDFMonitor.LogOutputHandler(string.Format("Fail:Log File Name is not configured but should for this activity:{0}", _config.Activity));
                                retval = false;
                                break;
                            }
                            else
                            {
                                CDFMonitor.LogOutputHandler(string.Format("Warning:Log File Name is not configured for this activity:{0}", _config.Activity));
                            }
                        }

                        retval = AllLogsUnique();

                        // path should exist?
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogFileOverWrite:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogFileServer:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogLevel:

                        CDFMonitor.LogOutputHandler(string.Format("LogLevel should be between 1 and 16:{0}",
                        retval = Convert.ToInt32(setting.Value) <= 16 && Convert.ToInt32(setting.Value) > 0));
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogMatchDetail:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogMatchOnly:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogBufferOnMatch:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.LogToConsole:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.ModuleEnableByFilter:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.ModuleFilter:

                        // process and make sure at least one module comes back
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.ModuleListViewItems:

                        // process
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.ModulePath:
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.ModuleSource:
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.MonitorProcesses:
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.RegexPattern:

                        // process
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.RemoteMachines:
                        if (_config.RemoteMachineList == null || _config.RemoteMachineList.Keys.Count < 1
                            && _config.Activity == Configuration.ActivityType.Remote)
                        {
                            CDFMonitor.LogOutputHandler("Warning:RemoteMachines should have at least one item:");
                        }
                        else
                        {
                            CDFMonitor.LogOutputHandler(string.Format("DEBUG:RemoteMachines items:{0}", _config.RemoteMachineList.Keys.Count));
                            retval = true;
                        }

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.RemoteMachinesPath:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.RemoteActivity:

                        if (_config.Activity == Configuration.ActivityType.Remote)
                        {
                            try
                            {
                                Enum.Parse(typeof(Configuration.ConfigurationOperators.AppOperators), _config.AppSettings.RemoteActivity, true);
                            }
                            catch (Exception e)
                            {
                                CDFMonitor.LogOutputHandler(string.Format("{0}:RemoteOperation:{1}", repair ? "Fail" : "Warning", e.Message));
                                retval = repair ? false : true;
                            }
                        }

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.RemoteUseMachinesCache:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.Retries:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.RunAs:

                        // check against activity type
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.ServiceStartMode:

                        // should be one of type WMI.WMIServicesManager.StartMode
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.ShutdownCommand:

                        // process
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.ShutdownCommandWait:

                        // process
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.SmtpPassword:
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.SmtpPort:
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.SmtpSendFrom:

                        // process
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.SmtpSendTo:

                        // process
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.SmtpServer:

                        // ping
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.SmtpSsl:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.SmtpSubject:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.SmtpUser:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.StartEventEnabled:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.StartEventEnabledImmediately:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.StartEventID:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.StartEventSource:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.StartupCommand:

                        // process
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.StartupCommandWait:

                        // process
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.StopEventDisabledPermanently:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.StopEventEnabled:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.StopEventID:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.StopEventSource:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.TmfCacheDir:

                        if (string.IsNullOrEmpty(_config.AppSettings.TmfCacheDir))
                        {
                            break;
                        }

                        retval = FileManager.CheckPath(_config.AppSettings.TmfCacheDir, true);
                        if (retval)
                        {
                            CDFMonitor.LogOutputHandler(string.Format("TmfCacheDir:success:{0}", _config.AppSettings.TmfCacheDir));
                        }
                        else
                        {
                            CDFMonitor.LogOutputHandler(string.Format("{0}:TmfCacheDir:{1}", repair ? "Fail" : "Warning", retval));
                        }
                        retval = repair ? false : true;

                        // verify
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.TmfServers:

                        // process
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.TraceFileInput:

                        if (_config.Activity == Configuration.ActivityType.RegexParseToCsv)
                        {
                            string[] files = new string[0];
                            if ((files = FileManager.GetFiles(_config.AppSettings.TraceFileInput, null, SearchOption.AllDirectories)).Length < 1)
                            {
                                CDFMonitor.LogOutputHandler(string.Format("Trace File Input does not exist but should for this activity type:{0}", _config.AppSettings.TraceFileInput));
                                retval = false;
                                break;
                            }

                            foreach (string file in files)
                            {
                                // make sure its csv,txt,log, or etl
                                if (Regex.IsMatch(Path.GetExtension(file), "csv|txt|log|etl|zip"))
                                {
                                    retval &= true;
                                }
                                else
                                {
                                    CDFMonitor.LogOutputHandler(string.Format("Fail:Trace File Input does not match supported extensions: csv|txt|log|etl|zip", file));
                                    retval &= false;
                                }
                            }

                            retval &= AllLogsUnique();
                            CDFMonitor.LogOutputHandler(string.Format("Trace File Input exists:{0}", _config.AppSettings.TraceFileInput));
                        }
                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.TraceFileOutput:

                        // use repair as force check
                        string traceFile = _config.AppSettings.TraceFileOutput;
                        retval &= VerifyTraceFileOutput(ref traceFile, repair);
                        if (String.Compare(_config.AppSettings.TraceFileOutput, traceFile, true) != 0)
                        {
                            // force update
                            _config.AppSettings.TraceFileOutput = traceFile;
                            _config.AppSettings.NotifyPropertyChanged(Configuration.ConfigurationProperties.EnumProperties.TraceFileOutput.ToString());
                        }

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UdpClientPort:

                        if (_config.AppSettings.UdpClientEnabled | _config.AppSettings.UdpPingEnabled)
                        {
                            CDFMonitor.LogOutputHandler(string.Format("Udp Client Port range valid  (1-65536):{0}",
                                retval = (_config.AppSettings.UdpClientPort > 0 && _config.AppSettings.UdpClientPort < 65535)));
                            CDFMonitor.LogOutputHandler(string.Format("Udp Client Port not conflicting with server port:{0}",
                                retval &= _config.AppSettings.UdpClientPort != _config.AppSettings.UdpServerPort));
                        }

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UdpClientEnabled:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UdpPingEnabled:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UdpPingServer:

                        // if (_config.AppSettings.UdpClientEnabled)
                        if (_config.AppSettings.UdpPingEnabled)
                        {
                            // try either ip as it will default to udp server if ping server empty
                            if (!string.IsNullOrEmpty(_config.AppSettings.UdpPingServer))
                            {
                                retval = Network.RemoteOperations.Ping(_config.AppSettings.UdpPingServer);
                            }
                            else if (!string.IsNullOrEmpty(_config.AppSettings.UdpServer))
                            {
                                retval = Network.RemoteOperations.Ping(_config.AppSettings.UdpServer);
                            }
                            else
                            {
                                retval = false;
                            }

                            CDFMonitor.LogOutputHandler(string.Format("Udp Ping Server Ping results:{0}", retval));
                            // dont fail on ping fail just warn as this would stop tracing all together.
                            // some ping servers may not be pingable or always online.
                            if (!retval)
                            {
                                CDFMonitor.LogOutputHandler("Warning:Udp client is enabled but Udp Server is not pingable!");
                                retval = true;
                            }
                        }

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UdpPingTimer:

                        if (_config.AppSettings.UdpPingEnabled)
                        {
                            CDFMonitor.LogOutputHandler(string.Format("Udp Ping Timer seconds 0 (disabled) or greater than 60:{0}",
                              retval = _config.AppSettings.UdpPingTimer == 0 | _config.AppSettings.UdpPingTimer >= 60));
                        }

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UdpServer:

                        if (_config.AppSettings.UdpClientEnabled)
                        {
                            if (!string.IsNullOrEmpty(_config.AppSettings.UdpServer))
                            {
                                CDFMonitor.LogOutputHandler(string.Format("Udp Server Ping results:{0}",
                                    retval = Network.RemoteOperations.Ping(_config.AppSettings.UdpServer)));

                                // dont fail on ping fail just warn as this would stop tracing all together.
                                // some ping servers may not be pingable or always online.
                                if (!retval)
                                {
                                    CDFMonitor.LogOutputHandler("Warning:Udp client is enabled but Udp Server is not pingable!");
                                    retval = true;
                                }
                            }
                            else
                            {
                                retval = false;
                            }
                        }

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UdpServerPort:

                        if (_config.Activity == Configuration.ActivityType.Server)
                        {
                            CDFMonitor.LogOutputHandler(string.Format("Udp Server Port range valid (1-65536):{0}",
                                retval = (_config.AppSettings.UdpServerPort > 0 && _config.AppSettings.UdpServerPort < 65535)));
                            CDFMonitor.LogOutputHandler(string.Format("Udp Server Port not conflicting with client port:{0}",
                                retval &= _config.AppSettings.UdpClientPort != _config.AppSettings.UdpServerPort
                                && _config.AppSettings.UdpClientPort != 0));
                        }

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UrlFiles:

                        foreach (string file in _config.AppSettings.UrlFiles.Split(';'))
                        {
                            if (string.IsNullOrEmpty(file))
                            {
                                continue;
                            }

                            if (FileManager.GetFiles(file).Length < 1)
                            {
                                CDFMonitor.LogOutputHandler(string.Format("UrlFiles:Warning:{0} does not exist.", file));
                            }
                        }

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UrlPassword:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UrlSite:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UrlUser:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UseCredentials:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UseServiceCredentials:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UseTargetTime:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.UseTraceSourceForDestination:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.Version:

                        break;

                    case Configuration.ConfigurationProperties.EnumProperties.WriteEvent:

                        break;

                    default:

                        CDFMonitor.LogOutputHandler(string.Format("Fail:VerifySetting:Unknown setting:{0}", setting.Key));
                        retval = false;
                        return retval;
                }

                if (retval)
                {
                    Debug.Print(string.Format("VerifySetting:Exit:{0}:{1}", setting.Key, retval));
                }
                else
                {
                    CDFMonitor.LogOutputHandler(string.Format("Fail:VerifySetting:Exit:{0}:{1}", setting.Key, retval));
                }
                return retval;
            }
            catch (Exception e)
            {
                string strval = string.Format("VerifySetting:Exception:{0}:{1}", setting, e.ToString());
                CDFMonitor.LogOutputHandler(strval);
                CDFMonitor.LogOutputHandler(strval);
                retval = false;
                return retval;
            }
        }

        /// <summary>
        /// Verifies the trace file output.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="repair">if set to <c>true</c> [repair].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool VerifyTraceFileOutput(ref string file, bool repair)
        {
            bool retval = true;
            if (_config.Activity == Configuration.ActivityType.TraceToEtl)
            {
                if (string.IsNullOrEmpty(file))
                {
                    CDFMonitor.LogOutputHandler(string.Format("Fail:Trace File Output does not exist but should for this activity type (etl):{0}", _config.AppSettings.Activity));
                    return false;
                }
            }

            if (_config.Activity == Configuration.ActivityType.TraceToCsv
                || _config.Activity == Configuration.ActivityType.RegexParseToCsv)
            {
                if (string.IsNullOrEmpty(file))
                {
                    if (repair)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Fail:Trace File Output is not configured but should for this activity:{0}", _config.Activity));
                        return false;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Warning:Trace File Output is not configured for this activity:{0}", _config.Activity));
                    }
                }
            }

            if (_config.Activity == Configuration.ActivityType.TraceToEtl)
            {
                // fix extension regardless of repair
                if (Path.GetExtension(file).ToLower() != ".etl")
                {
                    CDFMonitor.LogOutputHandler(string.Format("Trace File Output does does not have correct extension. fixing:{0}", file));
                    file = Regex.Replace(file, Path.GetExtension(file), ".etl", RegexOptions.IgnoreCase);
                }
            }
            else
            {
                // fix extension regardless of repair
                if (Path.GetExtension(file).ToLower() == ".etl")
                {
                    CDFMonitor.LogOutputHandler(string.Format("Trace File Output does does not have correct extension. fixing:{0}", file));
                    file = Regex.Replace(file, Path.GetExtension(file), ".csv", RegexOptions.IgnoreCase);
                }
            }

            if ((string.IsNullOrEmpty(file) && repair)
                || (!string.IsNullOrEmpty(file)
                    && !FileManager.CheckPath(file, true)))
            {
                retval = false;
            }

            retval &= AllLogsUnique();

            return retval;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Alls the logs unique.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool AllLogsUnique()
        {
            // todo: not currently checking for kernel file
            List<string> logs = new List<string>();
            try
            {
                if (!string.IsNullOrEmpty(_config.AppSettings.LogFileName))
                {
                    logs.Add(_config.AppSettings.LogFileName);

                    // spaces will cause zipping to fail Part URI is not valid per rules defined in
                    // the Open Packaging Conventions specification

                    if (_config.AppSettings.LogFileName.Contains(" ") && !string.IsNullOrEmpty(_config.AppSettings.LogFileServer))
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Warning:log file name shouldnt contain spaces if using Log File Server"));
                    }
                }

                if (!string.IsNullOrEmpty(_config.AppSettings.TraceFileInput))
                {
                    logs.Add(_config.AppSettings.TraceFileInput);
                }

                if (!string.IsNullOrEmpty(_config.AppSettings.TraceFileOutput))
                {
                    logs.Add(_config.AppSettings.TraceFileOutput);
                    if (_config.AppSettings.LogFileName.Contains(" ") && !string.IsNullOrEmpty(_config.AppSettings.LogFileServer))
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Warning:trace file output shouldnt contain spaces if using Log File Server"));
                    }
                }

                if (_config.IsKernelTraceConfigured())
                {
                    logs.Add(_config.KernelTraceFile());
                    if (_config.AppSettings.LogFileName.Contains(" ") && !string.IsNullOrEmpty(_config.AppSettings.LogFileServer))
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Warning:trace file output shouldnt contain spaces if using Log File Server"));
                    }
                }

                return true;
            }
            catch
            {
                CDFMonitor.LogOutputHandler(string.Format("Verify all log names and paths for uniqueness."));
                return false;
            }
        }

        /// <summary>
        /// Verifies the deploy path.
        /// </summary>
        /// <param name="repair">if set to <c>true</c> [repair].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool VerifyDeployPath(bool repair)
        {
            if (_config.Activity != Configuration.ActivityType.Remote)
            {
                return true;
            }

            if (string.IsNullOrEmpty(_config.AppSettings.DeployPath))
            {
                if (!repair)
                {
                    CDFMonitor.LogOutputHandler("Specify 'Deploy Source Path' location.");
                    return _config.RemoteActivity != Network.RemoteOperations.RemoteOperationMethods.Deploy;
                }

                string newPath = string.Format("{0}deploy", AppDomain.CurrentDomain.BaseDirectory);
                if (FileManager.CreateFolder(newPath))
                {
                    _config.AppSettings.DeployPath = newPath;
                }
                else
                {
                    CDFMonitor.LogOutputHandler("Specify 'Deploy Source Path' location.");
                }
            }

            if (!FileManager.CheckPath(_config.AppSettings.DeployPath))
            {
                if (!repair)
                {
                    CDFMonitor.LogOutputHandler("'Deploy Source Path' does not exist.");
                    return _config.RemoteActivity != Network.RemoteOperations.RemoteOperationMethods.Deploy;
                }

                if (!FileManager.CreateFolder(_config.AppSettings.DeployPath))
                {
                    CDFMonitor.LogOutputHandler("Specify 'Deploy Source Path' location. Could not create folder.");
                }
            }

            string newConfig = string.Format("{0}\\{1}.config", _config.AppSettings.DeployPath, System.AppDomain.CurrentDomain.FriendlyName);

            if (string.Compare(FileManager.GetFullPath(newConfig), FileManager.GetFullPath(_config.AppSettings.ConfigFile)) == 0)
            {
                CDFMonitor.LogOutputHandler(string.Format("'Deploy Source Path' remote config cannot be same as current config."));
                return _config.RemoteActivity != Network.RemoteOperations.RemoteOperationMethods.Deploy;
            }

            if (!FileManager.FileExists(newConfig))
            {
                if (!repair)
                {
                    CDFMonitor.LogOutputHandler(string.Format("'Deploy Source Path' does not contain:{0}.",
                    newConfig));
                    return _config.RemoteActivity != Network.RemoteOperations.RemoteOperationMethods.Deploy;
                }

                CDFMonitor.LogOutputHandler(string.Format("Generating new config for 'Deploy Source Path':{0}", newConfig));
                return _config.CreateConfigFile(newConfig);
            }

            return true;
        }

        /// <summary>
        /// Verifies the gather path.
        /// </summary>
        /// <param name="repair">if set to <c>true</c> [repair].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool VerifyGatherPath(bool repair)
        {
            if (_config.Activity != Configuration.ActivityType.Remote)
            {
                return true;
            }

            if (string.IsNullOrEmpty(_config.AppSettings.GatherPath))
            {
                CDFMonitor.LogOutputHandler("Specify 'Gather Destination Path' location.");
                if (!repair)
                {
                    return _config.RemoteActivity != Network.RemoteOperations.RemoteOperationMethods.Gather;
                }

                string newPath = string.Format("{0}gather", AppDomain.CurrentDomain.BaseDirectory);
                if (FileManager.CreateFolder(newPath))
                {
                    _config.AppSettings.GatherPath = newPath;
                }
            }

            if (!string.IsNullOrEmpty(_config.AppSettings.DeployPath)
                && (String.Compare(_config.AppSettings.DeployPath, _config.AppSettings.GatherPath) == 0
                || FileManager.GetFullPath(_config.AppSettings.GatherPath).Contains(FileManager.GetFullPath(_config.AppSettings.DeployPath))))
            {
                CDFMonitor.LogOutputHandler("'Gather Destination Path' should not be the same as or child of 'Deploy Source Path'.");
                if (!repair)
                {
                    return _config.RemoteActivity != Network.RemoteOperations.RemoteOperationMethods.Gather;
                }

                string newPath = string.Format("{0}deploy", AppDomain.CurrentDomain.BaseDirectory);

                if (String.Compare(_config.AppSettings.DeployPath, newPath) == 0)
                {
                    // fix gather path
                    newPath = string.Format("{0}gather", AppDomain.CurrentDomain.BaseDirectory);
                    if (FileManager.CreateFolder(newPath))
                    {
                        _config.AppSettings.GatherPath = newPath;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler(string.Format("could not create folder:", newPath));
                        return false;
                    }
                }
                else
                {
                    // fix deploy path
                    if (FileManager.CreateFolder(newPath))
                    {
                        _config.AppSettings.DeployPath = newPath;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler(string.Format("'Setup Remote Config' could not create folder:", newPath));
                        return false;
                    }
                }
            }

            if (!FileManager.CreateFolder(_config.AppSettings.GatherPath))
            {
                CDFMonitor.LogOutputHandler(string.Format("'Setup Remote Config' could not create folder:", _config.AppSettings.GatherPath));
                return false;
            }

            return true;
        }

        #endregion Private Methods
    }
}