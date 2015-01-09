// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="Configuration.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Config
{
    using CDFM.Engine;
    using CDFM.FileManagement;
    using CDFM.Network;
    using CDFM.Properties;
    using CDFM.Service;
    using CDFM.Trace;
    using Microsoft.Win32;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Security.Principal;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Xml;

    /// <summary>
    /// Class Configuration
    /// </summary>
    public partial class Configuration
    {
        #region Private Methods

        /// <summary>
        /// Configures the log file server.
        /// </summary>
        /// <param name="writerJob">The writer job.</param>
        private void ConfigureLogFileServer(WriterJob writerJob)
        {
            if (!string.IsNullOrEmpty(AppSettings.LogFileServer)
                && writerJob != null
                && writerJob.Enabled
                && string.IsNullOrEmpty(writerJob.Writer.LogManager.LogFileServer))
            {
                writerJob.Writer.LogManager.LogFileServer = AppSettings.LogFileServer;
                writerJob.Writer.LogManager.StartManagingLogFileServer();
            }
            else
            {
                writerJob.Writer.LogManager.StopManagingLogFileServer();
            }
        }

        /// <summary>
        /// Enables the tracing.
        /// </summary>
        /// <param name="loggerJob">The logger job.</param>
        /// <param name="traceFileOutputPath">The path.</param>
        /// <param name="name">The name.</param>
        /// <param name="enable">if set to <c>true</c> [enable].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool ConfigureTracingLog(ref WriterJob loggerJob, string traceFileOutputPath, string name, bool enable)
        {
            JobType jobType;

            DisableTracingLog(loggerJob);

            if (!enable)
            {
                return true;
            }

            switch (Activity)
            {
                // case ActivityType.ParseToCsv:
                case ActivityType.RegexParseToCsv:

                // case ActivityType.RegexTraceToCsv:
                case ActivityType.TraceToCsv:
                    jobType = JobType.Csv;
                    break;

                case ActivityType.TraceToEtl:
                    jobType = JobType.Etl;
                    break;

                case ActivityType.Remote:
                    return true;

                case ActivityType.Server:
                    jobType = JobType.Csv;
                    break;

                case ActivityType.Unknown:
                default:
                    return false;
            }

            if (!Verify.VerifyTraceFileOutput(ref traceFileOutputPath, jobType == JobType.Etl))
            {
                return false;
            }

            if (string.IsNullOrEmpty(traceFileOutputPath))
            {
                // its ok to not have output file unless tracing to etl.
                return true;
            }

            loggerJob = LoggerQueue.AddJob(jobType, name, traceFileOutputPath);

            if (jobType != JobType.Etl)
            {
                loggerJob.Writer.Queue.MaxQueueLength = ThreadQueueLength;
            }

            if (!loggerJob.Enabled)
            {
                return false;
            }

            ConfigureLogFileServer(loggerJob);

            return true;
        }

        /// <summary>
        /// reads commandline arguments and populates corresponding properties
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool ReadArguments()
        {
            // Process command line arguments
            string propertyValue = string.Empty;
            int propertyValueIdx = -1;
            bool retVal = true;

            try
            {
                _cdfMonitor.LogOutput("DEBUG:ReadArguments enter");

                // Look for properties first
                foreach (
                    PropertyInfo property in
                        typeof(ConfigurationProperties).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    Debug.Print("DEBUG:ReadArguments appProperty:" + property);

                    if (_Args.Any(a => Regex.IsMatch(a, string.Format("/{0}:", property.Name), RegexOptions.IgnoreCase)))
                    {
                        propertyValue = _Args.First(
                            arg => Regex.IsMatch(
                                arg,
                                string.Format("/{0}:", property.Name),
                                RegexOptions.IgnoreCase))
                                .Split(new char[] { ':' }, 2)[1];

                        if (property.PropertyType == typeof(string))
                        {
                            if (!string.IsNullOrEmpty(propertyValue))
                            {
                                propertyValue = Environment.ExpandEnvironmentVariables(propertyValue);
                            }

                            property.SetValue(_ConfigurationProperties, propertyValue, null);
                        }
                        else if (property.PropertyType == typeof(int))
                        {
                            property.SetValue(_ConfigurationProperties, Convert.ToInt32(propertyValue), null);
                        }
                        else if (property.PropertyType == typeof(uint))
                        {
                            property.SetValue(_ConfigurationProperties, Convert.ToUInt32(propertyValue), null);
                        }
                        else if (property.PropertyType == typeof(bool))
                        {
                            property.SetValue(_ConfigurationProperties, Convert.ToBoolean(propertyValue), null);
                        }
                    }
                }

                // Check all operators with values
                foreach (PropertyInfo property in
                        typeof(ConfigurationOperators).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    _cdfMonitor.LogOutput("DEBUG:ReadArguments appOperator property:" + property);

                    if (_Args.ToList().Exists(arg => arg.Substring(0).Contains(string.Format("/{0}:", property.Name.ToLower()))))
                    {
                        propertyValueIdx = (Array.IndexOf(_Args,
                            string.Format("/{0}:", property.Name.ToLower())));
                        if (propertyValueIdx > -1 && _Args.Length > propertyValueIdx)
                        {
                            propertyValue = _Args[++propertyValueIdx].Trim();
                        }

                        if (property.PropertyType == typeof(string))
                        {
                            if (!string.IsNullOrEmpty(propertyValue))
                            {
                                propertyValue = Environment.ExpandEnvironmentVariables(propertyValue);
                            }

                            property.SetValue(_ConfigurationOperators, propertyValue, null);
                        }
                        else if (property.PropertyType == typeof(int))
                        {
                            property.SetValue(_ConfigurationOperators, Convert.ToInt32(propertyValue), null);
                        }
                        else if (property.PropertyType == typeof(uint))
                        {
                            property.SetValue(_ConfigurationOperators, Convert.ToUInt32(propertyValue), null);
                        }
                        else if (property.PropertyType == typeof(bool))
                        {
                            property.SetValue(_ConfigurationOperators, Convert.ToBoolean(propertyValue), null);
                        }
                    }
                }

                // Check all app operators without values (bool flags)
                foreach (
                    PropertyInfo property in
                        typeof(ConfigurationOperators).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    Debug.Print("DEBUG:ReadArguments appOperator:" + property);
                    if (_Args.Any(a => Regex.IsMatch(
                        a,
                        string.Format("/{0}$", property.Name),
                        RegexOptions.IgnoreCase)))
                    {
                        // Treat as boolean
                        property.SetValue(_ConfigurationOperators, true, null);

                        retVal = false;
                    }
                    else if (_Args.Contains("/?"))
                    {
                        AppOperators.DisplayHelp = true;
                        retVal = false;
                    }
                }

                // This is the way filetype association passes arguments
                if (_Args.Length == 1
                    && !_Args[0].StartsWith("/")
                    && Path.GetExtension(_Args[0]).ToLower() == ".etl"
                    && FileManager.FileExists(_Args[0]))
                {
                    AppOperators.Fta = true;
                }

                return retVal;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("ReadArguments Exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Reads the online version XML.
        /// </summary>
        /// <returns>ReadOnlineVersionResults.</returns>
        private ReadOnlineVersionResults ReadOnlineVersionXml()
        {
            var cOVR = new ReadOnlineVersionResults();
            try
            {
                cOVR.CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                // Check for version update
                var doc = new XmlDocument();
                doc.LoadXml(new HttpGet().GetRequest(UPDATE_URL));
                if (doc != null
                    && doc.DocumentElement.SelectSingleNode("Version").InnerText.Length > 0)
                {
                    cOVR.IsPopulated = true;
                    cOVR.PackageVersion = new Version(doc.DocumentElement.SelectSingleNode("Version").InnerText);

                    if (cOVR.CurrentVersion < cOVR.PackageVersion)
                        cOVR.IsNewer = true;
                    else
                        return (cOVR);

                    cOVR.PackageUrl = doc.DocumentElement.SelectSingleNode("PackageUrl").InnerText;
                    cOVR.ArticleUrl = doc.DocumentElement.SelectSingleNode("ArticleUrl").InnerText;
                    cOVR.Date = doc.DocumentElement.SelectSingleNode("Date").InnerText;

                    _cdfMonitor.LogOutput("package url:" + cOVR.PackageUrl);
                    _cdfMonitor.LogOutput("article url:" + cOVR.ArticleUrl);
                    _cdfMonitor.LogOutput("publised date:" + cOVR.Date);
                    _cdfMonitor.LogOutput("published version:" + cOVR.PackageVersion); //<--not working
                    _cdfMonitor.LogOutput("current version:" + cOVR.CurrentVersion);
                }

                return (cOVR);
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("DEBUG:Unable to check version" + e.ToString());
                return (cOVR);
            }
        }

        /// <summary>
        /// Sets the send SMTP.
        /// </summary>
        /// <param name="modules">Dictionary of module names and guids</param>
        /// <param name="path">string path and file name of ctl</param>
        private void WriteModulesList(Dictionary<string, string> modules, string path)
        {
            StreamWriter sw = null;
            StringBuilder sb = new StringBuilder("Modules:\n");

            foreach (var kvp in modules)
            {
                sb.AppendLine(string.Format("{0}    {1}", kvp.Value, kvp.Key));
            }

            _cdfMonitor.LogOutput(sb.ToString());

            if (!String.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                sw = File.AppendText(path);
                foreach (var kvp in modules)
                {
                    sw.WriteLine(string.Format("{0}    {1}", kvp.Value, kvp.Key));
                }

                sw.Close();
            }
        }

        #endregion Private Methods

        #region Public Classes

        /// <summary>
        /// Class ModuleListViewItem
        /// </summary>
        public class ModuleListViewItem : INotifyPropertyChanged
        {
            #region Private Fields

            /// <summary>
            /// The _checked
            /// </summary>
            private bool? _checked;

            /// <summary>
            /// The _module GUID
            /// </summary>
            private string _moduleGuid = String.Empty;

            /// <summary>
            /// The _module name
            /// </summary>
            private string _moduleName = String.Empty;

            #endregion Private Fields

            #region Public Events

            /// <summary>
            /// Occurs when delegate property value changes.
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;

            #endregion Public Events

            #region Public Properties

            /// <summary>
            /// Gets or sets the checked.
            /// </summary>
            /// <value>The checked.</value>
            public bool? Checked
            {
                get { return (_checked); }
                set
                {
                    _checked = value;
                    RaisePropertyChanged("Checked");
                }
            }

            /// <summary>
            /// Gets or sets the module GUID.
            /// </summary>
            /// <value>The module GUID.</value>
            public string ModuleGuid
            {
                get { return (_moduleGuid); }
                set
                {
                    _moduleGuid = value;
                    RaisePropertyChanged("ModuleGuid");
                }
            }

            /// <summary>
            /// Gets or sets the name of the module.
            /// </summary>
            /// <value>The name of the module.</value>
            public string ModuleName
            {
                get { return (_moduleName); }
                set
                {
                    _moduleName = value;
                    RaisePropertyChanged("RemoteStatus");
                }
            }

            #endregion Public Properties

            #region Public Methods

            /// <summary>
            /// Raises the property changed.
            /// </summary>
            /// <param name="property">The property.</param>
            public void RaisePropertyChanged(string property)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(property));
                }
            }

            #endregion Public Methods
        }

        /// <summary>
        /// Class to hold individual EventCommands broken down into commands and arguments
        /// </summary>
        public class ProcessCommandResults
        {
            #region Public Fields

            public string Arguments = string.Empty;
            public string Command = string.Empty;
            public string CommandString = string.Empty;
            public string tag = string.Empty;

            #endregion Public Fields
        }

        /// <summary>
        /// Class to hold individual EventCommands broken down into commands and arguments
        /// </summary>
        public class ProcessRegexResults
        {
            #region Public Fields

            public bool Populated;
            public string RegexPattern = string.Empty;
            public Dictionary<string, string> RegexVariables = new Dictionary<string, string>();

            #endregion Public Fields
        }

        /// <summary>
        /// Class to hold utility update information results
        /// </summary>
        public class ReadOnlineVersionResults
        {
            #region Public Fields

            public string ArticleUrl = string.Empty;
            public Version CurrentVersion = new Version();
            public string Date = string.Empty;
            public bool IsNewer;
            public bool IsPopulated;
            public string PackageUrl = string.Empty;
            public Version PackageVersion = new Version();

            #endregion Public Fields
        }

        /// <summary>
        /// Class RemoteMachinesListViewItem
        /// </summary>
        public class RemoteMachinesListViewItem : INotifyPropertyChanged
        {
            #region Private Fields

            private string _machineName = String.Empty;
            private string _remoteStatus = String.Empty;

            #endregion Private Fields

            #region Public Events

            public event PropertyChangedEventHandler PropertyChanged;

            #endregion Public Events

            #region Public Properties

            public string MachineName
            {
                get { return (_machineName); }
                set
                {
                    _machineName = value;
                    RaisePropertyChanged("MachineName");
                }
            }

            /// <summary>
            /// Gets or sets the name of the module.
            /// </summary>
            /// <value>The name of the module.</value>
            public string RemoteStatus
            {
                get { return (_remoteStatus); }
                set
                {
                    _remoteStatus = value;
                    RaisePropertyChanged("RemoteStatus");
                }
            }

            #endregion Public Properties

            #region Private Methods

            private void RaisePropertyChanged(string property)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(property));
                }
            }

            #endregion Private Methods
        }

        /// <summary>
        /// Class ModuleListViewItem
        /// </summary>
        public class UdpClientsListViewItem : UdpPingPacket, INotifyPropertyChanged
        {
            #region Public Constructors

            public UdpClientsListViewItem(UdpPingPacket packet = null)
                : base()
            {
                if (packet != null)
                {
                    base.AvgProcessCpu = packet.AvgProcessCpu;
                    base.ClientActivity = packet.ClientActivity;
                    base.ClientName = packet.ClientName;
                    base.ClientPingTime = packet.ClientPingTime;
                    base.CurrentMachineCpu = packet.CurrentMachineCpu;
                    base.Duration = packet.Duration;
                    base.MatchedEvents = packet.MatchedEvents;
                    base.MissedMatchedEvents = packet.MissedMatchedEvents;
                    base.MaxQueueMissedEvents = packet.MaxQueueMissedEvents;
                    base.ParserQueue = packet.ParserQueue;
                    base.ProcessedEvents = packet.ProcessedEvents;
                    base.CurrentProcessCpu = packet.CurrentProcessCpu;
                    base.TracesPerSecond = packet.TracesPerSecond;
                    base.UdpCounter = packet.UdpCounter;
                    base.UdpTraceType = packet.UdpTraceType;
                }
            }

            #endregion Public Constructors

            #region Public Events

            /// <summary>
            /// Occurs when delegate property value changes.
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;

            #endregion Public Events

            #region Public Methods

            /// <summary>
            /// Raises the property changed.
            /// </summary>
            /// <param name="property">The property.</param>
            public void OnPropertyChanged(string property)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(property));
                }
            }

            #endregion Public Methods
        }

        #endregion Public Classes

        #region Public Fields

        public const string CONFIG_URL = "http://ctxsym.citrix.com/tmfs/cdfmonitor/configs/";

        public const string MODULE_LIST_REGISTRY_HIVE_AND_KEY = "HKEY_LOCAL_MACHINE\\" + MODULE_LIST_REGISTRY_KEY + "\\";

        public const string MODULE_LIST_REGISTRY_KEY = "SYSTEM\\CurrentControlSet\\Control\\Citrix\\Tracing\\Modules";

        public const string UPDATE_URL = "http://ctxsym.citrix.com/tmfs/cdfmonitor/update/version.xml";

        public static int MaxThreadCount = 100;

        public static int MinThreadCount = 1;

        public WriterJob LoggerJobConsole;

        public WriterJob LoggerJobEtw;

        public WriterJob LoggerJobKernelTrace;

        public WriterJob LoggerJobTrace;

        public WriterJob LoggerJobUdp;

        public WriterJob LoggerJobUtility;

        public Dictionary<string, string> RegexVariables = new Dictionary<string, string>();

        public Guid SessionGuid = new Guid(Properties.Resources.SessionGuid);

        #endregion Public Fields

        #region Private Fields

        private const string JOBS_SECTION = "Jobs";

        private readonly string[] _Args;

        private readonly CDFMonitor _cdfMonitor;

        private System.Configuration.Configuration _Config;

        private ExeConfigurationFileMap _ConfigFileMap;

        private ConfigurationOperators _ConfigurationOperators = new ConfigurationOperators();

        private ConfigurationProperties _ConfigurationProperties = new ConfigurationProperties();

        private List<Configuration.ModuleListViewItem> _moduleListCurrentConfigCache = new List<Configuration.ModuleListViewItem>();

        private Mutex _mutex;

        private string _previousConfig;

        private ResourceManagement _ResourceCredentials = new ResourceManagement();

        private bool _sendSmtp;

        private string _sessionName;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Configuration constructor
        /// </summary>
        /// <param name="args">The args.</param>
        public Configuration(string[] args)
        {
            // Initializing values for appProperties that are not default false and string.empty
            // Create logger queue
            LoggerQueue = new Engine.WriterQueue();
            CDFMonitorProgramFiles = string.Format("{0}\\Citrix\\{1}", Environment.GetEnvironmentVariable("ProgramFiles"), Properties.Resources.SessionName);
            LoggerJobUtility = new WriterJob();
            LoggerJobTrace = new WriterJob();
            LoggerJobKernelTrace = new WriterJob();
            LoggerJobUdp = new WriterJob();
            LoggerJobConsole = new WriterJob();
            LoggerJobEtw = new WriterJob();
            SessionName = Resources.SessionName;
            StartupCommands = new List<ProcessCommandResults>();
            ShutdownCommands = new List<ProcessCommandResults>();
            ModuleListViewCollection = new ObservableCollection<ModuleListViewItem>();
            ModuleListViewCollection.CollectionChanged += ModuleListViewCollection_CollectionChanged;
            UdpClientsListViewCollection = new ObservableCollection<UdpClientsListViewItem>();
            RemoteMachineList = new Dictionary<string, RemoteOperations.RemoteStatus>();
            EventCommands = new List<ProcessCommandResults>();
            Verify = new ConfigurationVerify(this);
            _cdfMonitor = CDFMonitor.Instance;
            _Args = args;
            AppSettings.ConfigFile = Process.GetCurrentProcess().ProcessName + ".exe.config";
            FTAFolder = Directory.GetCurrentDirectory();

            ModuleList = new Dictionary<string, string>();
            ThreadQueueLength = 100000;
        }

        #endregion Public Constructors

        #region Public Enums

        /// <summary>
        /// Enum ActivityType
        /// </summary>
        [Flags]
        public enum ActivityType
        {
            RegexParseToCsv = 2,
            Remote = 8,
            TraceToEtl = 16,
            TraceToCsv = 32,
            Unknown = 64,
            Server = 128
        }

        /// <summary>
        /// Enum ExecutionOptions
        /// </summary>
        public enum ExecutionOptions
        {
            Unknown,
            Console,
            Gui,
            Hidden,
            Service
        }

        /// <summary>
        /// Enum ModuleSourceType
        /// </summary>
        public enum ModuleSourceType
        {
            Unknown,
            Configuration,
            LocalMachine,
            RemoteMachine,
            File
        }

        /// <summary>
        /// Enum ServiceReturn
        /// </summary>
        public enum ServiceReturn
        {
            Unknown,
            AlreadyInstalled,
            AlreadyUninstalled,
            Error,
            InstallSuccessful,
            UninstallSuccessful
        }

        /// <summary>
        /// Enum ServiceStartModeOptionEnum
        /// </summary>
        public enum ServiceStartModeOptionEnum
        {
            Automatic = 2,
            Manual = 3,
            Disabled = 4
        }

        #endregion Public Enums

        #region Public Properties

        /// <summary>
        /// Gets or sets the name of the session.
        /// </summary>
        /// <value>The name of the session.</value>
        public static Configuration.ExecutionOptions StartupAs
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the activity.
        /// </summary>
        /// <value>The activity.</value>
        public ActivityType Activity
        {
            get
            {
                try
                {
                    return (ActivityType)Enum.Parse(typeof(ActivityType), AppSettings.Activity, true);
                }
                catch (Exception e)
                {
                    _cdfMonitor.LogOutput("DEBUG:Activity:exception: " + e.ToString());
                    return ActivityType.Unknown;
                }
            }

            set { AppSettings.Activity = value.ToString(); }
        }

        /// <summary>
        /// Gets or sets the operators.
        /// </summary>
        /// <value>The operators.</value>
        public ConfigurationOperators AppOperators
        {
            get { return _ConfigurationOperators; }

            set { _ConfigurationOperators = value; }
        }

        /// <summary>
        /// Gets or sets the app settings.
        /// </summary>
        /// <value>The app settings.</value>
        public ConfigurationProperties AppSettings
        {
            get { return _ConfigurationProperties; }

            set { _ConfigurationProperties = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [bypass regex].
        /// </summary>
        /// <value><c>true</c> if [bypass regex]; otherwise, /c>.</value>
        public bool BypassRegexPattern
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets value for root program files path for CDFMonitor (including CDFMonitor in path)
        /// </summary>
        public string CDFMonitorProgramFiles
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the console reader thread.
        /// </summary>
        /// <value>The console reader thread.</value>
        public Thread ConsoleReaderThread
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the startup commands.
        /// </summary>
        /// <value>The startup commands.</value>
        public List<ProcessCommandResults> EventCommands
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the startup as.
        /// </summary>
        /// <value>The startup as.</value>
        public List<string> EventLogSourceOptions
        {
            get
            {
                List<string> returnList = new List<string>();

                // TODO: can pass machine name in geteventlogs(machine) for remote
                foreach (var log in System.Diagnostics.EventLog.GetEventLogs())
                {
                    returnList.Add(log.LogDisplayName);
                }

                return returnList;
            }
        }

        /// <summary>
        /// folder for file type association
        /// </summary>
        public string FTAFolder
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is populated.
        /// </summary>
        /// <value><c>true</c> if this instance is populated; otherwise, /c>.</value>
        public bool IsPopulated
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the logger queue.
        /// </summary>
        /// <value>The logger queue.</value>
        public WriterQueue LoggerQueue
        {
            get;
            set;
        }

        /// <summary>
        /// missed events from ETW Controller for session
        /// </summary>
        public Int64 MissedControllerEvents
        {
            get
            {
                Int64 retVal = 0;
                if (CDFMonitor.Instance.Controller != null)
                {
                    CDFMonitor.Instance.Controller.QueryTrace(SessionName);
                    retVal = CDFMonitor.Instance.Controller.MissedControllerEvents;
                }

                if (CDFMonitor.Instance.Consumer != null)
                {
                    retVal += CDFMonitor.Instance.Consumer.MissedControllerEvents;
                }

                return retVal;
            }
        }

        /// <summary>
        /// Gets or sets the jobs list.
        /// </summary>
        /// <value>The jobs list.</value>
        public Dictionary<string, string> ModuleList
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the module list current config cache.
        /// </summary>
        /// <value>The module list current config cache.</value>
        public List<Configuration.ModuleListViewItem> ModuleListCurrentConfigCache
        {
            get
            {
                _cdfMonitor.LogOutput(string.Format("DEBUG:ModuleListCurrentConfigCache:get: "
                    + "_moduleListCurrentConfigCache.Count:{0} "
                    + "_cdfMonitor.Config.ModuleListViewItems.Count:{1} "
                    + "_cdfMonitor.Config.ModuleList.Count:{2}",
                    _moduleListCurrentConfigCache.Count,
                    _cdfMonitor.Config.ModuleListViewItems.Count,
                    _cdfMonitor.Config.ModuleList.Count));

                return _moduleListCurrentConfigCache;
            }
            set
            {
                _cdfMonitor.LogOutput(string.Format("DEBUG:ModuleListCurrentConfigCache:set: "
                    + "_moduleListCurrentConfigCache.Count:{0} "
                    + "_cdfMonitor.Config.ModuleListViewItems.Count:{1} "
                    + "_cdfMonitor.Config.ModuleList.Count:{2}",
                    value.Count,
                    _cdfMonitor.Config.ModuleListViewItems.Count,
                    _cdfMonitor.Config.ModuleList.Count));

                _moduleListCurrentConfigCache = value;
            }
        }

        /// <summary>
        /// Gets or sets the module list view collection.
        /// </summary>
        /// <value>The module list view collection.</value>
        public ObservableCollection<ModuleListViewItem> ModuleListViewCollection
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the module list view items.
        /// </summary>
        /// <value>The module list view items.</value>
        public List<ModuleListViewItem> ModuleListViewItems
        {
            get
            {
                string[] items = AppSettings.ModuleListViewItems.Split(';');
                Debug.Print(string.Format("DEBUG:Config.ModuleListViewItems:get:count:{0}", items.Length));

                var lvc = new List<ModuleListViewItem>();

                foreach (string line in items)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    bool check;
                    var tmpString = new string[3];
                    tmpString = line.Split(':');

                    // To support using module filter as module selection like in ver 1.x
                    if (AppSettings.ModuleEnableByFilter)
                    {
                        check = Regex.IsMatch(string.Format("{0} {1}", tmpString[1], tmpString[2]), AppSettings.ModuleFilter);
                    }
                    else
                    {
                        check = Convert.ToBoolean(tmpString[0]);
                    }

                    ModuleListViewItem lvi = new ModuleListViewItem()
                    {
                        Checked = check,
                        ModuleGuid = tmpString[1],
                        ModuleName = tmpString[2]
                    };

                    lvc.Add(lvi);
                }
                return lvc;
            }

            set
            {
                Debug.Print(string.Format("DEBUG:Config.ModuleListViewItems:set:count:{0}", value.Count));
                StringBuilder sb = new StringBuilder();
                foreach (ModuleListViewItem lvi in value)
                {
                    sb.Append(string.Format("{0}:{1}:{2};", lvi.Checked, lvi.ModuleGuid, lvi.ModuleName));
                }

                AppSettings.ModuleListViewItems = sb.ToString();
            }
        }

        /// <summary>
        /// Gets or sets the module source.
        /// </summary>
        /// <value>The module source.</value>
        public ModuleSourceType ModuleSource
        {
            get { return (ModuleSourceType)Enum.Parse(typeof(ModuleSourceType), AppSettings.ModuleSource, true); }

            set { AppSettings.ModuleSource = value.ToString(); }
        }

        /// <summary>
        /// Gets or sets the color of the original console.
        /// </summary>
        /// <value>The color of the original console.</value>
        public ConsoleColor OriginalConsoleColor
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the activity.
        /// </summary>
        /// <value>The activity.</value>
        public RemoteOperations.RemoteOperationMethods RemoteActivity
        {
            get { return (RemoteOperations.RemoteOperationMethods)Enum.Parse(typeof(RemoteOperations.RemoteOperationMethods), AppSettings.RemoteActivity, true); }

            set { AppSettings.RemoteActivity = value.ToString(); }
        }

        /// <summary>
        /// Gets or sets the remote machine list.
        /// </summary>
        /// <value>The remote machine list.</value>
        /// <remarks>
        /// not in config file
        /// </remarks>
        public Dictionary<string, RemoteOperations.RemoteStatus> RemoteMachineList
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the remote machine list view collection.
        /// </summary>
        /// <value>The remote machine list view collection.</value>
        public ObservableCollection<RemoteMachinesListViewItem> RemoteMachineListViewCollection
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the remote path.
        /// </summary>
        /// <value>The remote path.</value>
        public string RemotePath
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the resource credentials.
        /// </summary>
        /// <value>The resource credentials.</value>
        public ResourceManagement ResourceCredentials
        {
            get { return _ResourceCredentials; }

            set { _ResourceCredentials = value; }
        }

        /// <summary>
        /// Gets or sets the startup as.
        /// </summary>
        /// <value>The startup as.</value>
        public ExecutionOptions RunAs
        {
            get { return (ExecutionOptions)Enum.Parse(typeof(ExecutionOptions), AppSettings.RunAs, true); }

            set { AppSettings.RunAs = value.ToString(); }
        }

        /// <summary>
        /// Gets or sets the startup as.
        /// </summary>
        /// <value>The startup as.</value>
        public List<ExecutionOptions> RunAsOptions
        {
            get
            {
                List<ExecutionOptions> returnList = new List<ExecutionOptions>();
                foreach (ExecutionOptions executionOption in Enum.GetValues(typeof(ExecutionOptions)))
                {
                    returnList.Add(executionOption);
                }

                return returnList;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [send SMTP].
        /// </summary>
        /// <value><c>true</c> if [send SMTP]; otherwise, /c>.</value>
        public bool SendSmtp
        {
            get
            {
                return _sendSmtp;
            }

            set
            {
                CDFMonitor.LogOutputHandler(string.Format("DEBUG:SendSmtp:{0}", value));
                _sendSmtp = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [send URL].
        /// </summary>
        /// <value><c>true</c> if [send URL]; otherwise, /c>.</value>
        public bool SendUrl
        {
            get
            {
                bool retval;
                if (AppSettings.UrlSite.Length > 0
                                && AppSettings.UrlUser.Length > 0
                                && !BypassRegexPattern
                                    && (Activity == ActivityType.TraceToCsv
                                        | Activity == ActivityType.Server))
                {
                    retval = true;
                }
                else
                {
                    retval = false;
                }

                CDFMonitor.LogOutputHandler(string.Format("DEBUG:SendUrl:{0}", retval));
                return retval;
            }
        }

        /// <summary>
        /// Gets or sets the startup as.
        /// </summary>
        /// <value>The startup as.</value>
        public ServiceStartModeOptionEnum ServiceStartMode
        {
            get { return (ServiceStartModeOptionEnum)Enum.Parse(typeof(ServiceStartModeOptionEnum), AppSettings.ServiceStartMode, true); }

            set { AppSettings.ServiceStartMode = value.ToString(); }
        }

        /// <summary>
        /// Gets or sets the startup as.
        /// </summary>
        /// <value>The startup as.</value>
        public List<ServiceStartModeOptionEnum> ServiceStartModeOptions
        {
            get
            {
                List<ServiceStartModeOptionEnum> returnList = new List<ServiceStartModeOptionEnum>();
                foreach (ServiceStartModeOptionEnum executionOption in Enum.GetValues(typeof(ServiceStartModeOptionEnum)))
                {
                    returnList.Add(executionOption);
                }

                return returnList;
            }
        }

        /// <summary>
        /// Gets or sets the name of the session.
        /// </summary>
        /// <value>The name of the session.</value>
        public string SessionName
        {
            get
            {
                return _sessionName;
            }
            set
            {
                _sessionName = value;
            }
        }

        /// <summary>
        /// Gets or sets the startup commands.
        /// </summary>
        /// <value>The startup commands.</value>
        public List<ProcessCommandResults> ShutdownCommands
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the startup commands.
        /// </summary>
        /// <value>The startup commands.</value>
        public List<ProcessCommandResults> StartupCommands
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the status.
        /// </summary>
        /// <value>The status.</value>
        public string Status
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the length of the thread queue.
        /// </summary>
        /// <value>The length of the thread queue.</value>
        public int ThreadQueueLength
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the TMF servers list.
        /// </summary>
        /// <value>The TMF servers list.</value>
        public string[] TMFServersList
        {
            get
            {
                return AppSettings.TmfServers.Split(';');
            }

            set
            {
                AppSettings.TmfServers = String.Join(";", value);
            }
        }

        public ObservableCollection<UdpClientsListViewItem> UdpClientsListViewCollection
        {
            get;

            set;
        }

        /// <summary>
        /// Gets or sets the verify.
        /// </summary>
        /// <value>The verify.</value>
        public ConfigurationVerify Verify
        {
            get;
            set;
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Determines whether this instance is administrator.
        /// </summary>
        /// <returns><c>true</c> if this instance is administrator; otherwise, /c>.</returns>
        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// parses command string to determine process and arguments for command.
        /// </summary>
        /// <param name="cmds">The CMDS.</param>
        /// <returns>List{ProcessCommandResults}.</returns>
        public static List<ProcessCommandResults> ProcessCommand(string cmds)
        {
            // TODO: needs to be reworked. not currently handling arguments that are quoted or
            //       arguments with semicolons embedded

            var pcrs = new List<ProcessCommandResults>();
            bool innerbreak = false;
            try
            {
                foreach (string fullcmd in cmds.Split(';'))
                {
                    var pcr = new ProcessCommandResults();
                    innerbreak = false;

                    // TODO: strip out regex variables?
                    string cmd = fullcmd;
                    string arguments = String.Empty;
                    string command = String.Empty;
                    string cleanarg = String.Empty;
                    string cleancmd = String.Empty;

                    pcr.CommandString = cmd;

                    // Take off named variables if found
                    var revar = new Regex("^\\?\\<(\\w*?)\\>command:");

                    // Only modify if argument is in eventcommand
                    if (revar.IsMatch(cmd) && revar.Match(cmd).Groups.Count > 0)
                    {
                        pcr.tag = revar.Match(cmd).Groups[1].Value;
                        cmd = revar.Replace(cmd, "");
                    }

                    if (cmd.Length < 1)
                    {
                        continue;
                    }

                    // If file found set command to file and empty argument
                    if (File.Exists(cmd))
                    {
                        pcr.Command = cmd;
                        pcr.Arguments = string.Empty;

                        pcrs.Add(pcr);
                        continue;
                    }

                    // Search all paths
                    foreach (string path in (Environment.GetEnvironmentVariable("PATH").Split(';')))
                    {
                        if (File.Exists(string.Format("{0}\\{1}", path, cmd)))
                        {
                            pcr.Command = string.Format("{0}\\{1}", path, cmd);
                            pcr.Arguments = string.Empty;

                            pcrs.Add(pcr);
                            innerbreak = true;
                            break;
                        }
                    }
                    if (innerbreak)
                    {
                        continue;
                    }

                    var args = new List<string>();

                    args.AddRange(cmd.Split(' '));
                    if (args.Count > 1)
                    {
                        // If file found set command to file and arguments
                        if (File.Exists(args[0]))
                        {
                            pcr.Command = args[0];
                            args.Remove(args[0]);
                            pcr.Arguments = String.Join(" ", args.ToArray()); //string.Empty;

                            pcrs.Add(pcr);
                            continue;
                        }

                        foreach (string path in (Environment.GetEnvironmentVariable("PATH").Split(';')))
                        {
                            if (File.Exists(string.Format("{0}\\{1}", path, args[0])))
                            {
                                pcr.Command = string.Format("{0}\\{1}", path, args[0]);
                                args.Remove(args[0]);
                                pcr.Arguments = String.Join(" ", args.ToArray());

                                innerbreak = true;
                                pcrs.Add(pcr);
                                break;
                            }
                        }
                        if (innerbreak)
                        {
                            continue;
                        }
                    }

                    // If nothing found then default to first arg
                    if (string.IsNullOrEmpty(pcr.Command) && args.Count > 1)
                    {
                        pcr.Command = args[0];
                        args.Remove(args[0]);
                        pcr.Arguments = String.Join(" ", args.ToArray());

                        pcrs.Add(pcr);
                        continue;
                    }

                    // Give up
                    else if (string.IsNullOrEmpty(pcr.Command))
                    {
                        pcr.Command = cmd;
                        pcr.Arguments = string.Empty;
                    }

                    pcrs.Add(pcr);
                    continue;
                }

                string output = SerializeCommands(pcrs);
                if (!string.IsNullOrEmpty(output))
                {
                    CDFMonitor.LogOutputHandler(output);
                }

                return (pcrs);
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("Error processing command lines:" + e.ToString());
                return (pcrs);
            }
        }

        /// <summary>
        /// Serializes the commands.
        /// </summary>
        /// <param name="commands">The commands.</param>
        /// <returns>System.String.</returns>
        public static string SerializeCommands(List<ProcessCommandResults> commands)
        {
            var sb = new StringBuilder();

            foreach (var pcrListResult in commands)
            {
                sb.AppendLine("fullcommand:" + pcrListResult.CommandString);
                sb.AppendLine("     command:" + pcrListResult.Command);
                sb.AppendLine("     arguments:" + pcrListResult.Arguments);

                if (!File.Exists(pcrListResult.Command))
                {
                    sb.AppendLine("Warning:file does not exist. depending on command this may be ok:" + pcrListResult.CommandString);
                }
                else
                {
                    sb.AppendLine("Command file exists:" + pcrListResult.CommandString);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Determines whether regex pattern should be bypassed.
        /// </summary>
        /// <param name="pattern">The pattern.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool BypassRegexFilter(string pattern)
        {
            if (string.IsNullOrEmpty(pattern) | pattern == ".*" | pattern == ".")
            {
                return true;
            }
            else if (IsValidRegexPattern(pattern))
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Checks for new version.
        /// </summary>
        public void CheckForNewVersion()
        {
            ReadOnlineVersionResults cOVR = ReadOnlineVersionXml();

            if (cOVR.IsPopulated && cOVR.IsNewer)
            {
                string downloadPath = AppDomain.CurrentDomain.BaseDirectory + Path.GetFileName(cOVR.PackageUrl);
                new WebClient().DownloadFile(cOVR.PackageUrl, downloadPath);
                CDFMonitor.LogOutputHandler("package downloaded to: " + downloadPath);
            }
            else if (cOVR.IsPopulated && !cOVR.IsNewer)
            {
                CDFMonitor.LogOutputHandler("No update");
            }
            else
            {
                CDFMonitor.LogOutputHandler("Error:unable to connect to:" + UPDATE_URL);
            }
        }

        /// <summary>
        /// Configures the console output.
        /// </summary>
        /// <param name="enable">if set to <c>true</c> [enable].</param>
        public void ConfigureConsoleOutput(bool enable, bool enableReader = false)
        {
            if (enableReader & RunAs == ExecutionOptions.Console)
            {
                // Start console reader thread
                if (ConsoleReaderThread == null)
                {
                    ConsoleReaderThread = new Thread(ConsoleManager.ConsoleReadInput);
                }

                if (ConsoleReaderThread.ThreadState != System.Threading.ThreadState.Running)
                {
                    ConsoleReaderThread.Start();
                }
            }

            if (enable & AppSettings.LogToConsole)
            {
                Debug.Print("ConfigureConsoleOutput:enabling.");
                if (!LoggerQueue.WriterJobs.Exists(j => j.JobName == "console"))
                {
                    LoggerJobConsole = LoggerQueue.AddJob(JobType.Console, "console");
                }
            }
            else
            {
                if (RunAs != ExecutionOptions.Console
                    && ConsoleReaderThread != null
                    && ConsoleReaderThread.ThreadState == System.Threading.ThreadState.Running)
                {
                    ConsoleReaderThread.Abort();
                }
                Debug.Print("ConfigureConsoleOutput:disabling.");
                LoggerQueue.RemoveJob(LoggerJobConsole);
            }
        }

        /// <summary>
        /// Configures the name of the controller session.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool ConfigureControllerSessionName()
        {
            // Not used for kernel session
            if (!AppSettings.AllowSingleInstance)
            {
                SessionGuid = Guid.NewGuid();

                // Since processconfig gets called multiple times. Reset SessionName to base name
                // before finding available name
                SessionName = Resources.SessionName;
                EtwTraceController controller = new EtwTraceController(SessionName, SessionGuid);
                int i = 0;
                for (i = 0; i < EtwTraceController.ETW_MAX_SESSIONS; i++)
                {
                    if (controller.QueryTrace(SessionName + i))
                    {
                        continue;
                    }
                    else
                    {
                        SessionName += i;
                        _cdfMonitor.LogOutput("DEBUG:ConfigureControllerSession: SessionName=" + SessionName);
                        break;
                    }
                }
                if (i >= EtwTraceController.ETW_MAX_SESSIONS)
                {
                    _cdfMonitor.LogOutput("ConfigureControllerSession: Max ETW sessions reached. exiting");
                    _cdfMonitor.LogOutput("ConfigureControllerSession: run cdfmonitor /clean if needed.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Configures the etw output.
        /// </summary>
        /// <param name="enable">if set to <c>true</c> [enable].</param>
        public void ConfigureEtwOutput(bool enable)
        {
            // Etwtracewriter only supports Vista+
            if (!IsWinVistaOrHigher())
            {
                return;
            }

            if (enable)
            {
                LoggerJobEtw = LoggerQueue.AddJob(JobType.Etw, "etw", new EtwTraceWriter());
            }
            else
            {
                LoggerQueue.RemoveJob(LoggerJobEtw);
            }
        }

        /// <summary>
        /// Configures the tracing.
        /// </summary>
        /// <param name="enable">if set to <c>true</c> [enable].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool ConfigureTracing(bool enable, string outputFile = null)
        {
            if (string.IsNullOrEmpty(outputFile))
            {
                outputFile = AppSettings.TraceFileOutput;
            }
            _cdfMonitor.LogOutput("DEBUG:ConfigureTracing:enter");
            bool retval = true;

            // If logfileoverwrite and max size and count = 0 then delete file.
            if (AppSettings.LogFileOverWrite
                && AppSettings.LogFileMaxCount == 0
                && AppSettings.LogFileMaxSize == 0
                && FileManager.FileExists(outputFile))
            {
                _cdfMonitor.LogOutput("ConfigureTracing: deleting trace file based on log file settings of: logfileoverwrite = true, logfilemax = 0, logfilecount = 0: " + outputFile);
                FileManager.DeleteFile(outputFile);
            }

            retval = ConfigureTracingLog(ref LoggerJobTrace, outputFile, "traceFile", enable);
            _cdfMonitor.LogOutput(string.Format("{0}ConfigureTracing:loggerJobTrace:return:{1}", retval ? "DEBUG:" : "Fail:", retval));

            if (IsKernelTraceConfigured() & Activity == ActivityType.TraceToEtl)
            {
                retval &= ConfigureTracingLog(ref LoggerJobKernelTrace, KernelTraceFile(), Properties.Resources.KernelSessionName, enable);
                _cdfMonitor.LogOutput(string.Format("{0}ConfigureTracing:loggerJobKernelTrace:return:{1}", retval ? "DEBUG:" : "Fail:", retval));
            }

            _cdfMonitor.LogOutput(string.Format("{0}ConfigureTracing:exit:{1}", retval ? "DEBUG:" : "Fail:", retval));
            return retval;
        }

        /// <summary>
        /// Enables the utility log.
        /// </summary>
        /// <param name="reset">if set to <c>true</c> [reset].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool ConfigureUtilityLog(bool reset)
        {
            // Set up logging
            DisableUtilityLog();

            if (reset
                && File.Exists(LoggerJobUtility.LogFileName)
                && AppSettings.LogFileOverWrite
                && AppSettings.LogFileMaxSize == 0
                && AppSettings.LogFileMaxCount <= 1)
            {
                FileManager.DeleteFile(LoggerJobUtility.Writer.LogManager.LogFileName);
                _cdfMonitor.LogOutput("Deleted Log file for startup:" + LoggerJobUtility.Writer.LogManager.LogFileName);
            }

            if (!string.IsNullOrEmpty(AppSettings.LogFileName)
                && FileManager.CheckPath(AppSettings.LogFileName, true))
            {
                LoggerJobUtility = LoggerQueue.AddJob(JobType.Log, "logFile", AppSettings.LogFileName);
                LoggerJobUtility.Writer.Queue.MaxQueueLength = ThreadQueueLength;
                if (!LoggerJobUtility.Enabled)
                {
                    return false;
                }

                // TODO: needs to be tested
                ConfigureLogFileServer(LoggerJobUtility);
            }

            return true;
        }

        /// <summary>
        /// Creates the config file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool CreateConfigFile(string file = null)
        {
            try
            {
                ConfigurationXml xml = new ConfigurationXml(file);
                ConfigurationProperties configProps = new ConfigurationProperties();
                configProps.InitializeNewConfig();
                xml.AddXmlNodes(configProps.ToKeyValueConfigurationCollection());
                return xml.Save();
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("CreateConfigFile:exception" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Currents the config file.
        /// </summary>
        /// <returns>System.String.</returns>
        public string CurrentConfigFile()
        {
            try
            {
                return _ConfigFileMap.ExeConfigFilename;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("Failure:could not open config file. regenerating default config.:" + e.ToString());

                if (CreateConfigFile() && ReadConfigFile(AppSettings.ConfigFile))
                {
                    _cdfMonitor.LogOutput("Reading default config.");
                    return _ConfigFileMap.ExeConfigFilename;
                }
                else
                {
                    _cdfMonitor.LogOutput("Failure:could not create default config file.");
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Disables the tracing.
        /// </summary>
        /// <param name="loggerJob">The logger job.</param>
        public void DisableTracingLog(WriterJob loggerJob)
        {
            if (loggerJob != null)
            {
                //          LoggerJobTrace.Writer.LogManager.ManageSequentialTraces();
                LoggerQueue.RemoveJob(loggerJob);
                return;
            }
        }

        /// <summary>
        /// Disables the utility log.
        /// </summary>
        public void DisableUtilityLog()
        {
            if (LoggerJobUtility != null)
            {
                LoggerQueue.RemoveJob(LoggerJobUtility);
            }
        }

        /// <summary>
        /// Displays the config settings.
        /// </summary>
        /// <param name="newActivity">if set to <c>true</c> [new activity].</param>
        public void DisplayConfigSettings(bool newActivity = false)
        {
            // Only display if gathering a trace (not processing one post)
            if (Activity != Configuration.ActivityType.TraceToCsv
                & Activity != Configuration.ActivityType.TraceToEtl)
            {
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine(
                "--------------------------------------------------------------------------------------------");
            sb.AppendLine(String.Format("{0} CDFMonitor Activity:{1}", newActivity ? "Start" : "End", DateTime.Now.ToString()));

            sb.AppendLine("Current Utility Log:" + LoggerJobUtilityPath());
            sb.AppendLine("Current Trace Log:" + LoggerJobTracePath());

            sb.AppendLine("CDFMonitor version:" + Assembly.GetExecutingAssembly().GetName().Version);
            sb.AppendLine("IsAdministrator:" + IsAdministrator());
            sb.AppendLine("ModuleList:");
            if (Activity == ActivityType.TraceToEtl
                | Activity == ActivityType.TraceToCsv)
            {
                foreach (var kvp in ModuleList)
                {
                    sb.AppendLine(String.Format("   {0}:({1})", kvp.Key, kvp.Value));
                }
            }
            else
            {
                sb.AppendLine("(module list not used for this activity)");
            }

            sb.AppendLine("EventCommands:");
            sb.AppendLine(SerializeCommands(EventCommands));
            sb.AppendLine("StartupCommands:");
            sb.AppendLine(SerializeCommands(StartupCommands));
            sb.AppendLine("ShutdownCommands:");
            sb.AppendLine(SerializeCommands(ShutdownCommands));

            foreach (
                PropertyInfo property in
                    typeof(ConfigurationProperties).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property != null)
                {
                    if (property.PropertyType.IsArray)
                    {
                        string values = String.Join(";",
                                                    (string[])
                                                    property.GetValue(_ConfigurationProperties, property.GetIndexParameters()));
                        sb.AppendLine(string.Format("{0}: {1}", property.Name.ToLower(), values));
                    }
                    else
                    {
                        sb.AppendLine(string.Format("{0}: {1}", property.Name.ToLower(),
                                                              property.GetValue(_ConfigurationProperties, null)));
                    }
                }
            }

            foreach (
                PropertyInfo property in
                    typeof(ConfigurationOperators).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property != null)
                {
                    if (property.PropertyType.IsArray)
                    {
                        string values = String.Join(";",
                                                    (string[])
                                                    property.GetValue(_ConfigurationOperators,
                                                                      property.GetIndexParameters()));
                        sb.AppendLine(string.Format("{0}: {1}", property.Name.ToLower(), values));
                    }
                    else
                    {
                        sb.AppendLine(string.Format("{0}: {1}", property.Name.ToLower(),
                                                              property.GetValue(_ConfigurationOperators, null)));
                    }
                }
            }

            if (Activity == ActivityType.TraceToEtl)
            {
                // Moving to etw
                CDFMonitor.LogOutputHandler(sb.ToString(), JobOutputType.Etw);
            }
            else if (Activity == ActivityType.TraceToCsv
                | Activity == ActivityType.RegexParseToCsv)
            {
                CDFMonitor.LogOutputHandler(sb.ToString());
            }
        }

        /// <summary>
        /// Enumerates Citrix CDF key into Dictionary with guid key, guidString name value returned
        /// </summary>
        /// <returns>Dictionary</returns>
        public Dictionary<string, string> EnumModulesFromReg()
        {
            var moduleList = new Dictionary<string, string>();
            try
            {
                RegistryKey moduleKey = Registry.LocalMachine;
                foreach (string regkey in moduleKey.OpenSubKey(MODULE_LIST_REGISTRY_KEY).GetSubKeyNames())
                {
                    moduleList.Add("{" + (string)Registry.GetValue(MODULE_LIST_REGISTRY_HIVE_AND_KEY
                                                                    + regkey, "GUID", string.Empty) + "}",
                                   regkey.ToLower());
                }
                return (moduleList);
            }
            catch
            {
                return (moduleList);
            }
        }

        /// <summary>
        /// Enums the remote modules from reg.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>Dictionary{System.StringSystem.String}.</returns>
        public Dictionary<string, string> EnumRemoteModulesFromReg()
        {
            RemoteOperations.RemoteStatus rs;
            Dictionary<string, string> retval = new Dictionary<string, string>();
            int timeout = 1;
            RemoteOperations remoteOp = new RemoteOperations(_cdfMonitor.Config.ProcessRemoteMachines(AppSettings.ModulePath),
                    string.Empty,
                    timeout,
                    _cdfMonitor.Config.ResourceCredentials.GetCredentials(Properties.Resources.SessionName));
            retval = remoteOp.ReadRegistryModules(Config.Configuration.MODULE_LIST_REGISTRY_KEY, out rs);
            if (rs == RemoteOperations.RemoteStatus.Success)
            {
                return retval;
            }
            else
            {
                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Determines whether [is first instance].
        /// </summary>
        /// <returns><c>true</c> if [is first instance]; otherwise, /c>.</returns>
        public bool IsFirstInstance()
        {
            bool firstInstance = true;
            if (_mutex == null)
            {
                _mutex = new Mutex(false, "Global\\" + Properties.Resources.SessionName.ToUpper(), out firstInstance);
                if (!firstInstance) _mutex = null;
            }

            _cdfMonitor.LogOutput("DEBUG:IsFirstInstance:" + firstInstance.ToString());
            return firstInstance;
        }

        /// <summary>
        /// Determines whether [is kernel trace configured].
        /// </summary>
        /// <returns><c>true</c> if [is kernel trace configured]; otherwise, /c>.</returns>
        public bool IsKernelTraceConfigured()
        {
            // Only enable kernel trace if doing live trace and trace flags are set
            if (Activity == ActivityType.TraceToCsv
                || Activity == ActivityType.TraceToEtl)
            {
                return AppSettings.EnableFlags > 0 & AppSettings.EnableFlags < 2147483647;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether [is valid mail address] [the specified emailaddress].
        /// </summary>
        /// <param name="emailaddress">The emailaddress.</param>
        /// <returns><c>true</c> if [is valid mail address] [the specified emailaddress]; otherwise,
        /// /c>.</returns>
        public bool IsValidMailAddress(string emailaddress)
        {
            if (string.IsNullOrEmpty(emailaddress))
            {
                return false;
            }

            try
            {
                new System.Net.Mail.MailAddress(emailaddress);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether [is valid regex pattern] [the specified pattern].
        /// </summary>
        /// <param name="pattern">The pattern.</param>
        /// <returns><c>true</c> if [is valid regex pattern] [the specified pattern]; otherwise,
        /// /c>.</returns>
        public bool IsValidRegexPattern(string pattern)
        {
            try
            {
                Regex d = new Regex(pattern);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsWinVistaOrHigher()
        {
            OperatingSystem OS = Environment.OSVersion;
            return (OS.Platform == PlatformID.Win32NT) && (OS.Version.Major >= 6);
        }

        /// <summary>
        /// Kernels the trace file.
        /// </summary>
        /// <returns>System.String.</returns>
        public string KernelTraceFile()
        {
            return string.Format("{0}\\{1}", Path.GetDirectoryName(FileManager.GetFullPath(AppSettings.TraceFileOutput)), Properties.Resources.KernelSessionFileName);
        }

        /// <summary>
        /// Displays Output File Names and Paths
        /// </summary>
        /// <returns></returns>
        public string LoggerJobTracePath()
        {
            try
            {
                if (LoggerJobTrace != null && LoggerJobTrace.Enabled)
                {
                    return LoggerJobTrace.Writer.LogManager.LogFileName;
                }

                return string.Empty;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("LoggerJobTracePath:exception:" + e.ToString());
                return string.Empty;
            }
        }

        /// <summary>
        /// Displays Output File Names and Paths
        /// </summary>
        /// <returns></returns>
        public string LoggerJobUtilityPath()
        {
            try
            {
                // Output file info
                if (LoggerJobUtility != null && LoggerJobUtility.Enabled)
                {
                    return LoggerJobUtility.Writer.LogManager.LogFileName;
                }

                return string.Empty;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("LoggerJobUtilityPath:exception:" + e.ToString());
                return string.Empty;
            }
        }

        /// <summary>
        /// Handles the CollectionChanged event of the ModuleListViewCollection control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The
        /// <see cref="System.Collections.Specialized.NotifyCollectionChangedEventArgs" /> instance
        /// containing the event data.</param>
        /// <exception cref="System.Exception"></exception>
        public void ModuleListViewCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ModuleListViewItems = new List<Configuration.ModuleListViewItem>(ModuleListViewCollection);
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (ModuleListViewItem item in e.NewItems)
                    {
                        ModuleListViewItem_PropertyChanged(item, null);
                        item.PropertyChanged += ModuleListViewItem_PropertyChanged;
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    ModuleList.Clear();
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (ModuleListViewItem item in e.OldItems)
                    {
                        item.PropertyChanged -= ModuleListViewItem_PropertyChanged;
                        ModuleListViewItem_PropertyChanged(item, null);
                    }
                    break;

                default:
                    throw new Exception();
            }
        }

        /// <summary>
        /// Handles the PropertyChanged event of the ModuleListViewItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs" /> instance containing the
        /// event data.</param>
        public void ModuleListViewItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ModuleListViewItems = new List<Configuration.ModuleListViewItem>(ModuleListViewCollection);

            var lvi = (ModuleListViewItem)sender;
            if (lvi.Checked == true)
            {
                if (!ModuleList.ContainsKey(lvi.ModuleGuid))
                {
                    ModuleList.Add(lvi.ModuleGuid, lvi.ModuleName);
                }
            }
            else
            {
                if (ModuleList.ContainsKey(lvi.ModuleGuid))
                {
                    ModuleList.Remove(lvi.ModuleGuid);
                }
            }
        }

        /// <summary>
        /// run this after all configuration properties are populated this processes/verifies
        /// configuration properties for use with utility
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool ProcessConfiguration()
        {
            bool ret = true;
            try
            {
                if (!IsPopulated)
                {
                    _cdfMonitor.LogOutput("ProcessConfiguration:IsPopulated set to false. exiting");
                    return false;
                }

                // Check for credentials if configured
                if (AppSettings.UseCredentials)
                {
                    ResourceCredentials.CheckResourceCredentials(Resources.SessionName);
                }

                if (!ConfigureUtilityLog(false))
                {
                    ret = false;
                }

                RemoteMachineList = ProcessRemoteMachines();

                if (!ProcessRegexPattern(AppSettings.RegexPattern))
                {
                    _cdfMonitor.LogOutput("Invalid REGEX string\n");
                    ret = false;
                }

                if (Activity == ActivityType.Unknown)
                {
                    _cdfMonitor.LogOutput("Unknown Activity Type\n");
                    ret = false;
                }

                BypassRegexPattern = BypassRegexFilter(AppSettings.RegexPattern);

                if (Activity == ActivityType.RegexParseToCsv
                    || Activity == ActivityType.TraceToCsv)
                {
                    foreach (string tmfServer in TMFServersList)
                    {
                        if (!VerifyTMFServer(tmfServer))
                        {
                            _cdfMonitor.LogOutput("ERROR:ProcessConfiguration:cannot verify TMF server. ");
                        }
                    }
                }

                if (AppSettings.SmtpServer.Length > 0
                    && AppSettings.SmtpPort > 0
                    && AppSettings.SmtpSendTo.Length > 0
                    && !BypassRegexPattern
                    && (Activity == ActivityType.Server
                    | Activity == ActivityType.TraceToCsv))
                {
                    SendSmtp = true;
                }
                else
                {
                    SendSmtp = false;
                }

                EventCommands = ProcessCommand(AppSettings.EventCommand);
                ShutdownCommands = ProcessCommand(AppSettings.ShutdownCommand);
                StartupCommands = ProcessCommand(AppSettings.StartupCommand);

                if (!Verify.VerifyAllSettings(false))
                {
                    _cdfMonitor.LogOutput("ProcessConfiguration:VerifyAllSettings failed. exiting");
                    ret = false;
                }

                return ret;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("Error processing config file:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Checks Module list in config for formatting. Processes wildcards and friendly names to
        /// guid. called in non gui scenarios
        /// </summary>
        /// <returns>bool false on failure/exception</returns>
        public bool ProcessModules()
        {
            try
            {
                switch (ModuleSource)
                {
                    case ModuleSourceType.Configuration:
                        ModuleList.Clear();

                        foreach (ModuleListViewItem mlvi in ModuleListViewItems)
                        {
                            if ((bool)mlvi.Checked)
                            {
                                ModuleList.Add(mlvi.ModuleGuid, mlvi.ModuleName);
                            }
                        }

                        break;

                    case ModuleSourceType.File:
                        if (!string.IsNullOrEmpty(AppSettings.ModulePath) && File.Exists(AppSettings.ModulePath))
                        {
                            _cdfMonitor.LogOutput("ProcessModules: reading from file:" + AppSettings.ModulePath);

                            ModuleList.Clear();
                            foreach (var kvp in ReadControlFile(AppSettings.ModulePath))
                            {
                                if (!ModuleList.Keys.Contains(kvp.Key))
                                {
                                    ModuleList.Add(kvp.Key, kvp.Value);
                                }
                            }
                        }
                        break;

                    case ModuleSourceType.LocalMachine:
                        ModuleList = EnumModulesFromReg();
                        break;

                    case ModuleSourceType.RemoteMachine:
                        ModuleList = EnumRemoteModulesFromReg();
                        break;

                    case ModuleSourceType.Unknown:
                    default:
                        _cdfMonitor.LogOutput("Fail:ProcessModules: invalid module source:" + ModuleSource.ToString());
                        return false;
                }

                // If filter in place then filter
                if (!BypassRegexFilter(AppSettings.ModuleFilter))
                {
                    ModuleList = ModuleList.Where(e => Regex.IsMatch(string.Format("{0} {1}", e.Key, e.Value),
                                                  AppSettings.ModuleFilter, RegexOptions.IgnoreCase)).ToDictionary(v => v.Key, v => v.Value);
                }
                if (ModuleList.Count == 0
                    & (Activity == ActivityType.TraceToCsv
                    | Activity == ActivityType.TraceToEtl))
                {
                    _cdfMonitor.LogOutput("ProcessModules warning: no modules added.");
                }

                return true;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("ProcessModules: Exception" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Process specified operator
        /// </summary>
        /// <param name="appOperator">The app operator.</param>
        /// <param name="overrideValueToTrue">if set to <c>true</c> [override value to
        /// true].</param>
        /// <param name="sender">The sender.</param>
        /// <returns>true if appOperator was processed and utility should keep running. returns
        /// false if error or utility should not keep running</returns>
        public bool ProcessOperator(ConfigurationOperators.AppOperators appOperator, bool overrideValueToTrue = false)
        {
            Debug.Print("ProcessOperator enter:" + appOperator.ToString());
            bool retVal = false;

            try
            {
                if (!overrideValueToTrue)
                {
                    object appOperatorValue = null;
                    PropertyInfo propertyInfo = _ConfigurationOperators.GetType().GetProperty(appOperator.ToString());
                    appOperatorValue = propertyInfo.GetValue(_ConfigurationOperators, null);

                    if (appOperatorValue == null)
                    {
                        retVal = true;
                        return retVal;
                    }
                    if ((appOperatorValue.GetType()) == typeof(bool)
                        && ((bool)appOperatorValue == false))
                    {
                        retVal = true;
                        return retVal;
                    }
                    if ((appOperatorValue.GetType()) == typeof(string)
                        && (String.IsNullOrEmpty((string)appOperatorValue)))
                    {
                        retVal = true;
                        return retVal;
                    }
                    if ((appOperatorValue.GetType()) == typeof(int)
                        && ((int)appOperatorValue == 0))
                    {
                        retVal = true;
                        return retVal;
                    }
                }

                ResourceCredential creds = new ResourceCredential();

                // Adding service crendentials
                if (AppSettings.UseCredentials | AppSettings.UseServiceCredentials)
                {
                    creds = ResourceCredentials.GetCredentials(Resources.SessionName);
                }

                switch (appOperator)
                {
                    case ConfigurationOperators.AppOperators.Check:

                        RemoteOperations check = new RemoteOperations(ProcessRemoteMachines(),
                                                         string.Empty, AppSettings.Retries,
                                                         creds);
                        return check.Check();

                    case ConfigurationOperators.AppOperators.CheckService:

                        _cdfMonitor.LogOutput("check service");
                        if (retVal = CDFServiceInstaller.ServiceIsInstalled(Resources.ServiceName))
                        {
                            _cdfMonitor.LogOutput(string.Format("service state: {0}", CDFServiceInstaller.GetServiceStatus(Resources.ServiceName)));
                        }
                        else
                        {
                            _cdfMonitor.LogOutput("service state: not installed");
                        }
                        return retVal;

                    case ConfigurationOperators.AppOperators.Clean:

                        Console.ResetColor();
                        _cdfMonitor.EtwTraceClean(true);

                        if (!string.IsNullOrEmpty(AppSettings.LogFileName)
                            && LoggerJobUtility.Writer.LogManager.Logs != null
                            && LoggerJobUtility.Writer.LogManager.Logs.Length > 0)
                        {
                            LoggerJobUtility.Writer.LogManager.DisableLogStream();

                            foreach (string log in LoggerJobUtility.Writer.LogManager.Logs)
                            {
                                if (FileManager.FileExists(log) && !FileManager.FileInUse(log))
                                {
                                    Debug.Print("Clean:deleting log:" + log);
                                    FileManager.DeleteFile(log);
                                }
                            }
                        }
                        if (string.IsNullOrEmpty(AppSettings.TraceFileOutput))
                        {
                            return retVal;
                        }

                        // This causes a temporary 0 byte file to be created but cleanup cleans it
                        if (ConfigureTracing(true)
                            && LoggerJobTrace.Writer.LogManager.Logs.Length > 0)
                        {
                            string[] logs = LoggerJobTrace.Writer.LogManager.Logs;
                            ConfigureTracing(false);

                            foreach (string log in logs)
                            {
                                if (FileManager.FileExists(log) && !FileManager.FileInUse(log))
                                {
                                    _cdfMonitor.LogOutput("Clean:deleting tracelog:" + log);
                                    FileManager.DeleteFile(log);
                                }
                            }
                        }

                        // Delete TMF cache dir
                        if (!string.IsNullOrEmpty(AppSettings.TmfCacheDir))
                        {
                            FileManager.DeleteFolder(AppSettings.TmfCacheDir);
                        }

                        ConfigureTracing(false);
                        return retVal;

                    case ConfigurationOperators.AppOperators.Deploy:

                        RemoteOperations deploy = new RemoteOperations(ProcessRemoteMachines(),
                                                          AppSettings.DeployPath, AppSettings.Retries,
                                                          creds);
                        return deploy.Deploy(AppSettings.ServiceStartMode);

                    case ConfigurationOperators.AppOperators.DownloadConfigs:

                        // Downloads all config files from server
                        var httpGet = new HttpGet();
                        string configDir = AppDomain.CurrentDomain.BaseDirectory + "configs";
                        Directory.CreateDirectory(configDir);

                        foreach (string config in Regex.Split(httpGet.GetRequest(CONFIG_URL + "configs.txt"), "\r\n"))
                        {
                            if (config.Trim().Length < 1) continue;
                            string downloadDir = configDir + "\\" + config.Replace("/", "\\");
                            _cdfMonitor.LogOutput("Copying config:" + downloadDir);
                            if (!Directory.Exists(Path.GetDirectoryName(downloadDir)))
                                Directory.CreateDirectory(Path.GetDirectoryName(downloadDir));
                            File.WriteAllText(configDir + "\\" + config,
                                              httpGet.GetRequest(CONFIG_URL + config));
                        }

                        return retVal;

                    case ConfigurationOperators.AppOperators.DownloadTMFs:

                        _cdfMonitor.LogOutput("downloading tmfs");
                        httpGet = new HttpGet();
                        string cacheDir = string.Empty;

                        // Use cachedir
                        if (AppSettings.TmfCacheDir.Length > 0)
                        {
                            _cdfMonitor.LogOutput("checking directory: " + AppSettings.TmfCacheDir);
                            Directory.CreateDirectory(AppSettings.TmfCacheDir);
                            cacheDir = AppSettings.TmfCacheDir;
                        }
                        else
                        {
                            Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "tmfs");
                            cacheDir = AppDomain.CurrentDomain.BaseDirectory + "tmfs";
                            _cdfMonitor.LogOutput("downloading tmfs to directory: " + cacheDir);
                        }

                        _cdfMonitor.LogOutput("connecting to server: " + AppSettings.TmfServers);
                        string[] tmfs = Regex.Split(httpGet.GetRequest(AppSettings.TmfServers + "guids.txt"), "\r\n");
                        _cdfMonitor.LogOutput("tmf _id: " + tmfs.Length.ToString());

                        foreach (string tmf in tmfs)
                        {
                            if (tmf.Trim().Length < 1) continue;

                            _cdfMonitor.LogOutput("Copying tmf:" + tmf);
                            File.WriteAllText(cacheDir + "\\" + tmf,
                                              httpGet.GetRequest(AppSettings.TmfServers + tmf));
                            if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(0))
                            {
                                return false;
                            }
                        }

                        return retVal;

                    case ConfigurationOperators.AppOperators.EnumModules:

                        ProcessModules();
                        WriteModulesList(ModuleList, _ConfigurationOperators.Path);
                        return retVal;

                    case ConfigurationOperators.AppOperators.Fta:

                        // This is the way file type association passes arg
                        Activity = ActivityType.RegexParseToCsv;
                        AppSettings.UseTraceSourceForDestination = true;
                        AppSettings.TraceFileInput = _Args[0].ToLower();
                        return true;

                    case ConfigurationOperators.AppOperators.Gather:

                        RemoteOperations gather = new RemoteOperations(ProcessRemoteMachines(),
                                                          AppSettings.GatherPath, AppSettings.Retries,
                                                          creds);
                        bool ret = gather.Gather();
                        _cdfMonitor.LogOutput("Check Gather path for files: " + _cdfMonitor.Config.AppSettings.GatherPath);
                        return ret;

                    case ConfigurationOperators.AppOperators.InstallService:

                        _cdfMonitor.LogOutput("installing service");
                        string dest = string.Format("{0}\\{1}", Environment.ExpandEnvironmentVariables("%systemroot%"), Properties.Resources.SessionName);

                        if (!CDFServiceInstaller.ServiceIsInstalled(Resources.ServiceName))
                        {
                            if (!string.IsNullOrEmpty(AppOperators.Path))
                            {
                                dest = AppOperators.Path;
                            }

                            _cdfMonitor.LogOutput("/installservice: using path:" + dest);
                            AppSettings.RunAs = "service";

                            // Set allowsingleinstance in case of gui
                            AppSettings.AllowSingleInstance = false;

                            WriteConfigFile(CurrentConfigFile());

                            _cdfMonitor.LogOutput(string.Format("DEBUG:ProcessOperator:InstallService: comparing paths:{0} : {1}",
                                string.Format("{0}\\{1}", dest, AppDomain.CurrentDomain.FriendlyName),
                                Process.GetCurrentProcess().MainModule.FileName));

                            // Make sure this instance not the destination path
                            if (String.Compare(string.Format("{0}\\{1}", dest, AppDomain.CurrentDomain.FriendlyName),
                                Process.GetCurrentProcess().MainModule.FileName, true) != 0)
                            {
                                if (!FileManager.CopyFiles(new string[] { AppDomain.CurrentDomain.FriendlyName, CurrentConfigFile() }, dest, true))
                                {
                                    _cdfMonitor.LogOutput("Fail:/installservice: unable to copy files. exiting");
                                    return false;
                                }
                            }

                            ReadConfigFile(string.Format("{0}\\{1}", dest, Path.GetFileName(CurrentConfigFile())));
                            string args = String.Join(" ", _Args).ToLower();
                            if (Regex.IsMatch(args, "/installservice"))
                            {
                                args = Regex.Replace(args, "/installservice", "");
                            }

                            args += " /runningasservice";

                            CDFServiceInstaller.InstallAndStart(Resources.ServiceName, Resources.FriendlyName,
                                                                string.Format("{0}\\{1} {2}", dest, AppDomain.CurrentDomain.FriendlyName, args));
                        }
                        else
                        {
                            _cdfMonitor.LogOutput("installing service:service already exists");
                        }

                        return retVal;

                    case ConfigurationOperators.AppOperators.Kill:

                        // Kills all CDFMonitor instances
                        _cdfMonitor.EtwTraceClean(true);

                        Process[] processes = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
                        foreach (Process p in processes)
                        {
                            if (p.Id != Process.GetCurrentProcess().Id)
                            {
                                _cdfMonitor.LogOutput("killing process:" + p.ProcessName + ":" + p.Id);
                                p.Kill(); // Only thing that works
                            }
                        }

                        return retVal;

                    case ConfigurationOperators.AppOperators.Modify:

                        RemoteOperations modify = new RemoteOperations(ProcessRemoteMachines(),
                                                          AppSettings.GatherPath, AppSettings.Retries,
                                                          creds);
                        return modify.Modify(AppSettings.ServiceStartMode);

                    //case ConfigurationOperators.AppOperators.Path:
                    //    return true;

                    case ConfigurationOperators.AppOperators.RegisterFta:

                        ConfigureFTA(true);
                        return retVal;

                    case ConfigurationOperators.AppOperators.ResetConfig:

                        string configFilePath = CurrentConfigFile();
                        AppSettings.InitializeNewConfig();
                        ProcessConfiguration();
                        WriteConfigFile(configFilePath);
                        return retVal;

                    case ConfigurationOperators.AppOperators.RunningAsService:

                        retVal = true;
                        return retVal;

                    case ConfigurationOperators.AppOperators.SeServiceLogonRight:

                        LsaUtility.SetRight(AppOperators.SeServiceLogonRight, "SeServiceLogonRight");

                        return false;

                    case ConfigurationOperators.AppOperators.Start:

                        // Start existing session
                        var etwController = new EtwTraceController(SessionName, SessionGuid);
                        if (etwController.QueryTrace(SessionName))
                        {
                            _cdfMonitor.LogOutput("Session exists.");
                            etwController.StartTrace();
                            _cdfMonitor.LogOutput(string.Format("ETW Session started"));
                            return retVal;
                        }
                        _cdfMonitor.LogOutput(string.Format("ETW Session {0} does not exist", SessionName));
                        return retVal;

                    case ConfigurationOperators.AppOperators.StartRemote:

                        RemoteOperations startRemote = new RemoteOperations(ProcessRemoteMachines(),
                                                          AppSettings.GatherPath, AppSettings.Retries,
                                                          creds);
                        return startRemote.StartRemote();

                    case ConfigurationOperators.AppOperators.StartService:

                        _cdfMonitor.LogOutput("starting service");
                        if (CDFServiceInstaller.ServiceIsInstalled(Resources.ServiceName))
                        {
                            CDFServiceInstaller.StopService(Resources.ServiceName);
                            CDFServiceInstaller.ChangeServiceConfig(Resources.ServiceName, ServiceBootFlag.AutoStart);
                            CDFServiceInstaller.StartService(Resources.ServiceName);
                        }
                        else
                        {
                            _cdfMonitor.LogOutput("starting service:service does not exist");
                        }
                        return retVal;

                    case ConfigurationOperators.AppOperators.Stop:

                        // Stop existing session
                        var etwController2 = new EtwTraceController(SessionName, SessionGuid);
                        if (etwController2.QueryTrace(SessionName))
                        {
                            _cdfMonitor.LogOutput("Session exists.");
                            etwController2.StopTrace();
                            _cdfMonitor.LogOutput(string.Format("ETW Session stopped."));
                            return retVal;
                        }
                        _cdfMonitor.LogOutput(string.Format("ETW Session {0} does not exist", SessionName));
                        return retVal;

                    case ConfigurationOperators.AppOperators.StopRemote:

                        RemoteOperations stopRemote = new RemoteOperations(ProcessRemoteMachines(),
                                                          AppSettings.GatherPath, AppSettings.Retries,
                                                          creds);
                        return stopRemote.StopRemote();

                    case ConfigurationOperators.AppOperators.StopService:

                        _cdfMonitor.LogOutput("stopping service");
                        if (CDFServiceInstaller.ServiceIsInstalled(Resources.ServiceName))
                        {
                            CDFServiceInstaller.ChangeServiceConfig(Resources.ServiceName, ServiceBootFlag.DemandStart);
                            CDFServiceInstaller.StopService(Resources.ServiceName);
                            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
                            {
                                _cdfMonitor.LogOutput("Fail:stopping service: service still running");
                            }
                            {
                                _cdfMonitor.LogOutput("stopping service: service stopped");
                            }
                        }
                        else
                        {
                            _cdfMonitor.LogOutput("stopping service: service does not exist");
                        }
                        return retVal;

                    case ConfigurationOperators.AppOperators.Undeploy:

                        RemoteOperations unDeploy = new RemoteOperations(ProcessRemoteMachines(),
                                                          string.Empty, AppSettings.Retries,
                                                          creds);
                        return unDeploy.UnDeploy();

                    case ConfigurationOperators.AppOperators.UninstallService:
                        _cdfMonitor.LogOutput("uninstalling service");
                        QueryServiceConfig serviceConfig = new QueryServiceConfig();

                        if (CDFServiceInstaller.ServiceIsInstalled(Resources.ServiceName))
                        {
                            serviceConfig = CDFServiceInstaller.GetServiceConfig(Properties.Resources.ServiceName);

                            string installPath = Path.GetDirectoryName(serviceConfig.lpBinaryPathName.Split()[0]);
                            CDFServiceInstaller.StopService(Resources.ServiceName);
                            CDFServiceInstaller.UninstallService(Resources.ServiceName);
                            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
                            {
                                _cdfMonitor.LogOutput("Fail:uninstall service: service still running");
                            }
                            {
                                _cdfMonitor.LogOutput("uninstall service: service stopped");
                            }

                            _cdfMonitor.LogOutput(string.Format("DEBUG:ProcessOperator:UnInstallService: comparing paths:{0} : {1}",
                               installPath,
                               Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)));

                            // Make sure this instance not the installPath
                            if (String.Compare(installPath, Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), true) != 0)
                            {
                                // Remove dir
                                FileManager.DeleteFolder(installPath, false);
                            }
                        }
                        else
                        {
                            _cdfMonitor.LogOutput("uninstalling service:service does not exist");
                        }

                        // Try to revert back to previous config if pointing to service config that is no longer there
                        if (!string.IsNullOrEmpty(serviceConfig.lpBinaryPathName) &&
                            String.Compare(serviceConfig.lpBinaryPathName.Split()[0] + ".config",
                                FileManager.GetFullPath(_cdfMonitor.Config.CurrentConfigFile()), true) == 0
                            && !string.IsNullOrEmpty(_previousConfig))
                        {
                            ReadConfigFile(_previousConfig);
                        }

                        goto case ConfigurationOperators.AppOperators.Clean;

                    case ConfigurationOperators.AppOperators.UnregisterFta:

                        ConfigureFTA(false);
                        return retVal;

                    case ConfigurationOperators.AppOperators.Update:

                        CheckForNewVersion();
                        return retVal;

                    case ConfigurationOperators.AppOperators.Upload:

                        _cdfMonitor.LogOutput("upload");
                        _cdfMonitor.UrlUploadThread =
                            new ThreadQueue<string>((data) => { _cdfMonitor.UploadPackage(data); }, "_urlUploadThread");

                        _cdfMonitor.UrlUploadThread.Queue();

                        while (_cdfMonitor.UrlUploadThread.ProcessedCounter != _cdfMonitor.UrlUploadThread.QueuedCounter)
                        {
                            if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000)) return false;
                        }
                        return retVal;

                    case ConfigurationOperators.AppOperators.Zip:

                        _cdfMonitor.LogOutput("zip");
                        _cdfMonitor.BuildPackage(_ConfigurationOperators.Path);
                        return retVal;

                    case ConfigurationOperators.AppOperators.DisplayHelp:
                    default:
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("This utility manages CDF (ETW) tracing for Citrix components.");
                        sb.AppendLine("All properties are in the 'cdfMonitor.exe.config' file.");
                        sb.AppendLine("All properties can also be passed via commandline using format /%property%:%value%");
                        sb.AppendLine("     Example: cdfmonitor.exe /configfile:c:\\temp\\new.config");
                        sb.AppendLine("Optional operators:");
                        sb.AppendLine("  '/check' checks CDFMonitor service state and status of a remote machine.");
                        sb.AppendLine("  '/checkservice' checks CDFMonitor service state and status on local machine.");
                        sb.AppendLine("  '/clean' to cleanup ETW sessions and files if utility terminated unexpectedly.");
                        sb.AppendLine("  '/configfile:%configFile%' used to specify alternate utility configuration file to read from.");
                        sb.AppendLine("  '/deploy' deploys CDFMonitor service to a remote machine.");
                        sb.AppendLine("  '/downloadconfigs' to download configuration files from server");
                        sb.AppendLine("  '/downloadtmfs' to download TMF files from server");
                        sb.AppendLine("  '/enummodules' to enumerate guidString list from registry");
                        sb.AppendLine("  '/gather' gathers CDFMonitor service traces from a remote machine.");
                        sb.AppendLine("  '/installservice' installs CDFMonitor as a service on local machine.");
                        sb.AppendLine("  '/kill' kills all CDFMonitor processes on local machine.");
                        sb.AppendLine("  '/modify' modifies CDFMonitor service state on a remote machine.");

                        // sb.AppendLine(" '/path' specifies folder path for other remote
                        // machine operations.");
                        sb.AppendLine("  '/registerfta' registers .etl file type association with this utility.");
                        sb.AppendLine("  '/resetconfig' resets config file values to default.");
                        sb.AppendLine("  '/start' starts CDFMonitor tracing on local machine if etw session exists and is stopped.");
                        sb.AppendLine("  '/startremote' starts CDFMonitor service on remote machine.");
                        sb.AppendLine("  '/startservice' starts CDFMonitor service on local machine.");
                        sb.AppendLine("  '/stop' stops CDFMonitor tracing on local machine if etw session exists and is started.");
                        sb.AppendLine("  '/stopremote' stops CDFMonitor service on remote machine.");
                        sb.AppendLine("  '/stopservice' stops CDFMonitor service on local machine.");
                        sb.AppendLine("  '/undeploy' uninstalls CDFMonitor service from remote machine.");
                        sb.AppendLine("  '/uninstallservice' uninstalls CDFMonitor service from local machine.");
                        sb.AppendLine("  '/unregisterfta' unregisters .etl file type association from this utility.");
                        sb.AppendLine("  '/update' downloads latest version of utility");
                        sb.AppendLine("  '/upload' uploads urlfiles to urlserver.");
                        sb.AppendLine("  '/zip' zips log files and url files into zip file.");
                        sb.AppendLine("For additional information, search support.citrix.com for 'CDFMonitor'");
                        sb.AppendLine(" or search for CTX129537 and CTX139593.");
                        sb.AppendLine("current version:" + Assembly.GetExecutingAssembly().GetName().Version);
                        _cdfMonitor.LogOutput(sb.ToString());
                        CheckForNewVersion();
                        return retVal;
                }
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput(string.Format("ProcessOperator exception: bad operator. exiting:{0}", e));
                return false;
            }
            finally
            {
                Debug.Print(string.Format("ProcessOperator {0} exit:{1}", appOperator, retVal));
            }
        }

        /// <summary>
        /// Process all operators specified
        /// </summary>
        /// <returns>returns true if utility should continue</returns>
        public bool ProcessOperators()
        {
            bool ret = true;
            foreach (
                ConfigurationOperators.AppOperators appOperator in
                    Enum.GetValues(typeof(ConfigurationOperators.AppOperators)))
            {
                ret &= ProcessOperator(appOperator);
            }

            return (ret);
        }

        /// <summary>
        /// Processes the regex.
        /// </summary>
        /// <param name="regexPattern">The regex pattern.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool ProcessRegexPattern(string regexPattern)
        {
            try
            {
                // Make sure regexpattern provided is valid
                var re = new Regex(regexPattern);

                var revar = new Regex("\\?\\<(?<argName>\\w*?)\\>");
                foreach (Match m in revar.Matches(regexPattern))
                {
                    if (string.IsNullOrEmpty(m.Groups["argName"].Value))
                    {
                        continue;
                    }

                    if (RegexVariables.ContainsKey(m.Groups["argName"].Value))
                    {
                        RegexVariables.Remove(m.Groups["argName"].Value);
                    }

                    RegexVariables.Add(m.Groups["argName"].Value, "");
                }

                return true;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("Error processing regex:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Verifies and processes Remote Actiivty
        /// </summary>
        /// <param name="remoteActivity">RemoteOperations.RemoteOperationMethods</param>
        /// <returns></returns>
        public bool ProcessRemoteActivity(string remoteActivity = null)
        {
            bool retval = false;
            try
            {
                if (string.IsNullOrEmpty(remoteActivity = remoteActivity ?? AppSettings.RemoteActivity))
                {
                    _cdfMonitor.LogOutput(string.Format("ProcessRemoteActivity:Invalid remoteActivity:{0}", remoteActivity));
                    return retval = false;
                }
                switch ((RemoteOperations.RemoteOperationMethods)Enum.Parse(
                 typeof(RemoteOperations.RemoteOperationMethods), remoteActivity, true))
                {
                    case RemoteOperations.RemoteOperationMethods.Check:
                        return retval = _cdfMonitor.Config.ProcessOperator(ConfigurationOperators.AppOperators.Check, true);

                    case RemoteOperations.RemoteOperationMethods.Deploy:
                        return retval = _cdfMonitor.Config.ProcessOperator(ConfigurationOperators.AppOperators.Deploy, true);

                    case RemoteOperations.RemoteOperationMethods.Gather:
                        return retval = _cdfMonitor.Config.ProcessOperator(ConfigurationOperators.AppOperators.Gather, true);

                    case RemoteOperations.RemoteOperationMethods.Modify:
                        return retval = _cdfMonitor.Config.ProcessOperator(ConfigurationOperators.AppOperators.Modify, true);

                    case RemoteOperations.RemoteOperationMethods.Start:
                        return retval = _cdfMonitor.Config.ProcessOperator(ConfigurationOperators.AppOperators.StartRemote, true);

                    case RemoteOperations.RemoteOperationMethods.Stop:
                        return retval = _cdfMonitor.Config.ProcessOperator(ConfigurationOperators.AppOperators.StopRemote, true);

                    case RemoteOperations.RemoteOperationMethods.UnDeploy:
                        return retval = _cdfMonitor.Config.ProcessOperator(ConfigurationOperators.AppOperators.Undeploy, true);

                    default:
                        return retval = false;
                }
            }
            finally
            {
                _cdfMonitor.LogOutput("DEBUG:ProcessRemoteActivity:exit:" + retval);
            }
        }

        /// <summary>
        /// Processes the remote machines.
        /// </summary>
        /// <param name="machines">The machines.</param>
        /// <returns>Dictionary{System.StringRemoteOperations.RemoteStatus}.</returns>
        public Dictionary<string, RemoteOperations.RemoteStatus> ProcessRemoteMachines(string machines = null)
        {
            List<string> tempMachineList = new List<string>();

            if (!AppSettings.RemoteUseMachinesCache)
            {
                RemoteMachineList.Clear();
            }

            try
            {
                if (string.IsNullOrEmpty(machines))
                {
                    machines = string.Empty;
                    if (!string.IsNullOrEmpty(AppSettings.RemoteMachines))
                    {
                        machines = AppSettings.RemoteMachines;
                    }
                    else if (File.Exists(AppSettings.RemoteMachinesPath))
                    {
                        machines = AppSettings.RemoteMachinesPath;
                    }
                }

                foreach (string machine in machines.Split(';'))
                {
                    if (string.IsNullOrEmpty(machine)) continue;

                    if (IsFile(machine))
                    {
                        foreach (var line in File.ReadAllText(machine).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (!RemoteMachineList.ContainsKey(line))
                            {
                                CDFMonitor.LogOutputHandler(string.Format("ProcessRemoteMachines adding :{0}", line));
                                RemoteMachineList.Add(line, RemoteOperations.RemoteStatus.Unknown);
                            }

                            tempMachineList.Add(line);
                        }
                    }
                    else if (machine.Contains("\r\n"))
                    {
                        // It's coming from text box (gui)
                        foreach (var item in machine.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (!RemoteMachineList.ContainsKey(item))
                            {
                                CDFMonitor.LogOutputHandler(string.Format("ProcessRemoteMachines adding :{0}", item));
                                RemoteMachineList.Add(item, RemoteOperations.RemoteStatus.Unknown);
                            }

                            tempMachineList.Add(item);
                        }
                    }
                    else
                    {
                        if (!RemoteMachineList.ContainsKey(machine))
                        {
                            CDFMonitor.LogOutputHandler(string.Format("ProcessRemoteMachines adding :{0}", machine));
                            RemoteMachineList.Add(machine, RemoteOperations.RemoteStatus.Unknown);
                        }

                        tempMachineList.Add(machine);
                    }
                }

                // Clean up cache
                foreach (string item in new List<string>(RemoteMachineList.Keys))
                {
                    if (!tempMachineList.Contains(item))
                    {
                        RemoteMachineList.Remove(item);
                    }
                }

                CDFMonitor.LogOutputHandler("DEBUG:ProcessRemoteMachines:exit:count:" + RemoteMachineList.Count);
                return (RemoteMachineList);
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("ProcessRemoteMachines: Exception" + e.ToString());
                return (new Dictionary<string, RemoteOperations.RemoteStatus>());
            }
        }

        // Copied from configproperties.cs
        /// <summary>
        /// Checks Module list in config for formatting. Processes wildcards and friendly names to guid
        /// </summary>
        /// <returns>bool false on failure/exception</returns>
        public bool ProcessStartup()
        {
            // Read arguments to see if configfilepath populated
            ReadArguments();

            string initialConfigFile = AppSettings.ConfigFile;
            if (!ReadConfigFile(initialConfigFile))
            {
                return false;
            }

            // cCheck again in case configfile has been redirected
            if (string.Compare(initialConfigFile, AppSettings.ConfigFile, true) != 0
                && !ReadConfigFile(AppSettings.ConfigFile))
            {
                return false;
            }

            ConfigureUtilityLog(true);

            ReadArguments();

            StartupAs = RunAs;

            if (!ProcessOperators() | !ProcessModules())
            {
                return false;
            }

            AppSettings.Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            return true;
        }

        /// <summary>
        /// Reads the config file.
        /// </summary>
        /// <param name="configFilePath">The config file path.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        /// <exception cref="System.Exception">ReadConfigFile: ERROR:undefined property type: +
        /// property.PropertyType</exception>
        public bool ReadConfigFile(string configFilePath)
        {
            try
            {
                _cdfMonitor.LogOutput("DEBUG:ReadConfigFile enter");
                string workingDirFilePath = string.Format("{0}\\{1}", AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(configFilePath));

                if (!UpdateConfigFile(configFilePath, workingDirFilePath))
                {
                    workingDirFilePath = File.Exists(configFilePath)
                                          ? configFilePath
                                          : workingDirFilePath;
                }

                KeyValueConfigurationCollection appSettings;

                // Read app config file for variables
                if (!File.Exists(workingDirFilePath))
                {
                    _cdfMonitor.LogOutput("ReadConfigFile: Invalid config file. exiting:" + configFilePath);

                    return false;
                }
                else
                {
                    // Keep previous config file name for when switching between service and program (gui)
                    if (string.Compare(_previousConfig, AppSettings.ConfigFile, true) != 0)
                    {
                        _previousConfig = AppSettings.ConfigFile;
                    }

                    _ConfigFileMap = new ExeConfigurationFileMap();
                    _ConfigFileMap.ExeConfigFilename = workingDirFilePath;
                    AppSettings.ClearConfig();

                    // Get the mapped configuration file.
                    _Config = ConfigurationManager.OpenMappedExeConfiguration(_ConfigFileMap,
                                                                              ConfigurationUserLevel.None);
                    appSettings = _Config.AppSettings.Settings;
                    _cdfMonitor.LogOutput("DEBUG:ReadConfigFile: config file successfully loaded:" +
                                            configFilePath);
                }

                // Iterate Settings enum and set all properties from config file
                foreach (
                    PropertyInfo property in
                        typeof(ConfigurationProperties).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    Debug.Print("DEBUG:ReadConfigFile: processing appProperty:" + property.Name);

                    string propertyValue = appSettings[property.Name] != null
                                               ? appSettings[property.Name].Value
                                               : string.Empty;
                    if (string.IsNullOrEmpty(propertyValue))
                    {
                        Debug.Print("DEBUG:ReadConfigFile: property value is empty:" + property.Name);
                        continue;
                    }

                    _cdfMonitor.LogOutput("DEBUG:ReadConfigFile: property value:" + propertyValue);

                    if (property.PropertyType == typeof(string) && propertyValue is string)
                    {
                        propertyValue = Environment.ExpandEnvironmentVariables(propertyValue);
                        property.SetValue(_ConfigurationProperties, propertyValue, null);
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        int val = 0;
                        if (Int32.TryParse(propertyValue, out val))
                        {
                            property.SetValue(_ConfigurationProperties, val, null);
                        }
                    }
                    else if (property.PropertyType == typeof(uint))
                    {
                        uint uval = 0;
                        if (UInt32.TryParse(propertyValue, out uval))
                        {
                            property.SetValue(_ConfigurationProperties, uval, null);
                        }
                    }
                    else if (property.PropertyType == typeof(bool))
                    {
                        bool val = false;
                        if (bool.TryParse(propertyValue, out val))
                        {
                            property.SetValue(_ConfigurationProperties, val, null);
                        }
                    }
                    else if (property.PropertyType.IsEnum)
                    {
                        if (Enum.IsDefined(property.PropertyType, propertyValue))
                        {
                            property.SetValue(_ConfigurationProperties,
                                              Enum.Parse(property.PropertyType, propertyValue, true), null);
                        }
                    }
                    else if (property.PropertyType.IsArray)
                    {
                        string[] values = Environment.ExpandEnvironmentVariables(propertyValue).Split(';');
                        if (values.Length > 0)
                        {
                            property.SetValue(_ConfigurationProperties, values, property.GetIndexParameters());
                        }
                    }
                    else
                    {
                        throw new Exception("ReadConfigFile: ERROR:undefined property type:" + property.PropertyType);
                    }
                }

                ModuleListCurrentConfigCache = ModuleListViewItems;
                AppSettings.NotifyAllPropertiesChanged();

                IsPopulated = true;
                return true;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("ReadConfigFile Exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Reads the control file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns>Dictionary{System.StringSystem.String}.</returns>
        public Dictionary<string, string> ReadControlFile(string file)
        {
            var moduleList = new Dictionary<string, string>();

            if (!IsFile(file))
            {
                return moduleList;
            }

            using (StreamReader stream = File.OpenText(file))
            {
                while (!stream.EndOfStream)
                {
                    string guid = string.Empty;
                    string name = string.Empty;
                    string line = stream.ReadLine();
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    MatchCollection mc = Regex.Matches(line, @"(?<guid>.*?)\s+(?<name>\w+)");

                    // This is how ctl files are generated %guid% %name%
                    if (mc.Count > 0)
                    {
                        foreach (Match m in mc)
                        {
                            if (m.Groups["guid"].Success && IsGuid(m.Groups["guid"].Value))
                            {
                                guid = m.Groups["guid"].Value;
                            }
                            if (m.Groups["name"].Success)
                            {
                                name = m.Groups["name"].Value;
                            }
                        }
                    }
                    else
                    {
                        // May just be a guid so try anyway
                        guid = IsGuid(line.Trim()) ? line.Trim() : string.Empty;
                        name = string.Empty;
                    }

                    if (!string.IsNullOrEmpty(guid))
                    {
                        if (!moduleList.Keys.Contains(FormatGuid(guid)))
                        {
                            // isModule = true;
                            moduleList.Add(FormatGuid(guid), name);

                            // moduleList.Add(name, FormatGuid(guid));
                        }
                    }
                }
            }

            return moduleList;
        }

        /// <summary>
        /// download config files from url if specified configfilepath is url
        /// </summary>
        /// <param name="configFileSourcePath">path and name of file</param>
        /// <param name="configFileDestPath">The config file dest path.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool UpdateConfigFile(string configFileSourcePath, string configFileDestPath)
        {
            CDFMonitor.LogOutputHandler(string.Format("DEBUG:UpdateConfigFile:enter:{0}:{1}", configFileSourcePath, configFileDestPath));
            string configData = string.Empty;
            try
            {
                switch (new ResourceManagement().GetPathType(configFileSourcePath))
                {
                    // Only download if remote like url or unc and copy into dest config
                    case ResourceManagement.ResourceType.Url:
                        configData = new HttpGet().GetRequest(configFileSourcePath);
                        break;

                    case ResourceManagement.ResourceType.Unc:
                        configData = File.ReadAllText(configFileSourcePath);
                        break;

                    default:
                        CDFMonitor.LogOutputHandler("DEBUG:UpdateConfigFile:returning.");
                        return false;
                }

                if (!string.IsNullOrEmpty(configData))
                {
                    CDFMonitor.LogOutputHandler(string.Format("UpdateConfigFile:config file downloaded:{0}", AppSettings.Debug ? configData : string.Empty));
                    if (File.Exists(configFileDestPath) && String.Compare(File.ReadAllText(configFileDestPath), configData) == 0)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("DEBUG:UpdateConfigFile:config file downloaded is same as cache:{0}", AppSettings.Debug ? configData : string.Empty));
                        return true;
                    }

                    File.WriteAllText(configFileDestPath, configData);
                    return true;
                }

                string eventString = "UpdateConfigFile:Error:config file not downloaded. verify configuration.";
                CDFMonitor.LogOutputHandler(eventString);
                EventLog.WriteEntry(Process.GetCurrentProcess().MainModule.ModuleName, eventString,
                                        EventLogEntryType.Information, 100);

                if (File.Exists(configFileDestPath))
                {
                    CDFMonitor.LogOutputHandler("DEBUG:UpdateConfigFile:using cache.");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("UpdateConfigFile:exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Verifies File Path and returns full file string
        /// </summary>
        /// <param name="file">string file name</param>
        /// <param name="mBytesNeeded">optional int for expected size of logs in mega bytes
        /// needed.</param>
        /// <returns>returns true if path available</returns>
        public bool VerifyPathSpace(string file, Int64 mBytesNeeded = 0)
        {
            try
            {
                _cdfMonitor.LogOutput("VerifyPathSpace: enter:" + file);
                _cdfMonitor.LogOutput(string.Format("VerifyPathSpace: file exists:{0}:{1}",
                    AppSettings.LogFileName,
                    File.Exists(AppSettings.LogFileName)));

                var fileInfo = new FileInfo(file);
                _cdfMonitor.LogOutput(string.Format("VerifyPathSpace: directory exists:{0}:{1}",
                    fileInfo.FullName,
                    fileInfo.Directory.Exists));

                // TODO: this info needs to be in status.text
                // Make sure there is enough available space on drive plus 1% of total drive space
                var driveInfo = new DriveInfo(Path.GetPathRoot(fileInfo.FullName));
                if (!(driveInfo.AvailableFreeSpace > ((mBytesNeeded * 1024 * 1024) + (driveInfo.TotalSize * .01))))
                {
                    _cdfMonitor.LogOutput("VerifyPathSpace:Fail:directory does not have enough available space");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("VerifyPathSpace: Exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Verifies the TMF server.
        /// </summary>
        /// <param name="tmfServer">The TMF server.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool VerifyTMFServer(string tmfServer)
        {
            // Only verify for certain activities and if not running as a service. During service
            // startup, checking tmfserver url path may cause web proxy service to start (its set to
            // manual) web proxy service cant start until CDFMonitor completes startup due to
            // service control manager being locked

            if (Activity == ActivityType.Unknown
                | Activity == ActivityType.Remote
                | Activity == ActivityType.TraceToEtl
                | AppOperators.RunningAsService)
            {
                return true;
            }

            if (ResourceCredentials.GetPathType(tmfServer) == ResourceManagement.ResourceType.Url)
            {
                return FileManager.CheckPath(tmfServer + Properties.Resources.TMFServerGuidList);
            }

            return FileManager.CheckPath(tmfServer);
        }

        /// <summary>
        /// writes config file with populated properties
        /// </summary>
        /// <param name="configFilePath">The config file path.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool WriteConfigFile(string configFilePath)
        {
            try
            {
                AppSettings.Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                _cdfMonitor.LogOutput("DEBUG:WriteConfigFile enter:" + configFilePath);

                KeyValueConfigurationElement[] kvcea = new KeyValueConfigurationElement[AppSettings.ToKeyValueConfigurationCollection().Count];
                AppSettings.ToKeyValueConfigurationCollection().CopyTo(kvcea, 0);
                List<KeyValueConfigurationElement> kList = kvcea.ToList();
                kList.Sort((a, b) => { return (a.Key.CompareTo(b.Key)); });

                foreach (KeyValueConfigurationElement kvce in kList)
                {
                    _Config.AppSettings.Settings.Remove(kvce.Key);
                    _Config.AppSettings.Settings.Add(kvce);
                }

                if (string.Compare(FileManager.GetFullPath(_ConfigFileMap.ExeConfigFilename), FileManager.GetFullPath(configFilePath), true) == 0)
                {
                    _Config.Save(ConfigurationSaveMode.Full);
                }
                else
                {
                    _Config.SaveAs(configFilePath, ConfigurationSaveMode.Full);
                    ReadConfigFile(configFilePath);
                }

                return true;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("WriteConfigFile Exception:" + e.ToString());
                return false;
            }
        }

        #endregion Public Methods

        /// <summary>
        /// Formats the GUID.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <returns>System.String.</returns>
        private static string FormatGuid(string module)
        {
            if (!module.StartsWith("{"))
            {
                module = "{" + module;
            }
            if (!module.EndsWith("{"))
            {
                module = module + "}";
            }
            return module;
        }

        /// <summary>
        /// Determines whether the specified module is file.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <returns><c>true</c> if the specified module is file; otherwise, /c>.</returns>
        private static bool IsFile(string module)
        {
            // See if its a file with modules
            try
            {
                if (File.Exists(module))
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether the specified GUID string is GUID.
        /// </summary>
        /// <param name="guidString">The GUID string.</param>
        /// <returns><c>true</c> if the specified GUID string is GUID; otherwise, /c>.</returns>
        private static bool IsGuid(string guidString)
        {
            bool isGuid = false;
            try
            {
                new Guid(guidString);
                isGuid = true;
            }
            catch
            {
                isGuid = false;
            }
            return isGuid;
        }

        /// <summary>
        /// Verifies the configurefta path.
        /// </summary>
        /// <param name="set">if set to <c>true</c> [repair].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool ConfigureFTA(bool set)
        {
            // Set path to programfiles
            string ftaFolder = FTAFolder;
            string newConfig = string.Format("{0}\\{1}.config", ftaFolder, System.AppDomain.CurrentDomain.FriendlyName);
            string newExe = string.Format("{0}\\{1}", ftaFolder, System.AppDomain.CurrentDomain.FriendlyName);

            if (set)
            {
                // Add

                if (!FileManager.CheckPath(ftaFolder, true))
                {
                    CDFMonitor.LogOutputHandler("ConfigureFTA:Fail: unable to create folder:" + ftaFolder);
                    return false;
                }

                if (!FileManager.CopyFile(Process.GetCurrentProcess().MainModule.FileName, ftaFolder, true))
                {
                    CDFMonitor.LogOutputHandler(string.Format("Fail:ConfigureFTA:Fail:could not copy exe into fta path:{0}",
                    System.AppDomain.CurrentDomain.FriendlyName));
                    return false;
                }

                if (WriteConfigFile(newConfig))
                {
                    // Set back to current
                    ReadConfigFile(AppSettings.ConfigFile);
                    FileAssociation.SetAssociation(string.Format("{0}\\{1}", ftaFolder, System.AppDomain.CurrentDomain.FriendlyName));
                    CDFMonitor.LogOutputHandler("CDFMonitor registered using current configuration. Edit config file in this location to modify configuration for FTA:" + ftaFolder);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                // Remove
                if (String.Compare(Path.GetDirectoryName(FTAFolder), Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), true) == 0)
                {
                    // Don't remove directory if it's the current working directory
                    return true;
                }

                if (FileManager.CheckPath(ftaFolder) && !FileManager.DeleteFolder(Path.GetDirectoryName(FileManager.GetFullPath(ftaFolder))))
                {
                    CDFMonitor.LogOutputHandler("ConfigureFTA:Fail: could not delete folder:" + ftaFolder);
                    return false;
                }
                else
                {
                    CDFMonitor.LogOutputHandler("ConfigureFTA:folder removed:" + ftaFolder);
                    FileAssociation.UnSetAssociation();
                    return true;
                }
            }
        }
    }
}