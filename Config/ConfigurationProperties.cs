// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="ConfigurationProperties.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************
namespace CDFM.Config
{
    using CDFM.Network;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Configuration;
    using System.Data;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Class Configuration
    /// </summary>
    partial class Configuration
    {
        #region Public Classes

        /// <summary>
        /// appsettings from config file that are used to configure CDFMonitor
        /// </summary>
        /// when adding properties, they have to be added in 3 places. in the Enum below in the
        /// Properties section below in VerifySetting() in ConfigurationVerify.cs if non-default
        /// initial
        public class ConfigurationProperties : INotifyPropertyChanged
        {
            #region Private Fields

            private string _Activity;
            private bool _AdvancedOptions;
            private bool _AllowSingleInstance;
            private bool _Annoyance;
            private bool _AutoScroll;
            private int _BufferLines;
            private int _BufferMax;
            private int _BufferMin;
            private int _BufferSize;
            private string _ConfigFile;
            private bool _Debug;
            private string _DeployPath;
            private string _DisplayFilter;
            private uint _EnableFlags;
            private string _EventCommand;
            private bool _EventCommandWait;
            private int _EventMaxCount;
            private int _EventThrottle;
            private string _GatherPath;
            private bool _LogBufferOnMatch;
            private bool _LogFileAutoFlush;
            private int _LogFileMaxCount;
            private int _LogFileMaxSize;
            private string _LogFileName;
            private bool _LogFileOverWrite;
            private string _LogFileServer;
            private int _LogLevel;
            private bool _LogMatchDetail;
            private bool _LogMatchOnly;
            private bool _LogToConsole;
            private bool _ModuleEnableByFilter;
            private string _ModuleFilter;
            private string _ModuleListViewItems;

            private string _ModulePath;

            private string _ModuleSource;

            private bool _MonitorProcesses;

            private bool _propertyNotificationsEnabled;

            private string _RegexPattern;

            private string _RemoteActivity;

            private string _RemoteMachines;

            private string _RemoteMachinesPath;

            private bool _RemoteUseMachinesCache;

            private int _Retries;

            private string _Runas;

            private string _ServiceStartMode;

            private string _ShutdownCommand;

            private bool _ShutdownCommandWait;

            private string _SmtpPassword;

            private int _SmtpPort;

            private string _SmtpSendFrom;

            private string _SmtpSendTo;

            private string _SmtpServer;

            private bool _SmtpSsl;

            private string _SmtpSubject;

            private string _SmtpUser;

            private bool _StartEventEnabledImmediately;

            private int _StartEventID;

            private string _StartEventSource;

            private string _StartupCommand;

            private bool _StartupCommandWait;

            private bool _StopEventDisabledPermanently;

            private bool _StopEventEnabled;

            private int _StopEventID;

            private string _StopEventSource;

            private string _TmfCacheDir;

            private string _TmfServers;

            private string _TraceFileInput;

            private string _TraceFileOutput;

            private bool _UdpClientEnabled;

            private int _UdpClientPort;

            private bool _UdpPingEnabled;

            private string _UdpPingServer;

            private int _UdpPingTimer;

            private string _UdpServer;

            private int _UdpServerPort;

            private string _UrlFiles;

            private string _UrlPassword;

            private string _UrlSite;

            private string _UrlUser;

            private bool _UseCredentials;

            private bool _UseServiceCredentials;

            private bool _UseTargetTime;

            private bool _UseTraceSourceForDestination;

            private string _Version;

            private bool _WriteEvent;

            #endregion Private Fields

            #region Public Constructors

            /// <summary>
            /// The Configuration Properties
            /// </summary>
            public ConfigurationProperties()
            {
                InitializeConfig();
            }

            #endregion Public Constructors

            #region Public Events

            /// <summary>
            /// Occurs when [property changed].
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;

            #endregion Public Events

            #region Public Enums

            /// <summary>
            /// Enum Enum
            /// </summary>
            public enum EnumProperties
            {
                Activity,
                AdvancedOptions,
                AllowSingleInstance,
                Annoyance,
                AutoScroll,
                BufferLines,
                BufferSize,
                BufferMin,
                BufferMax,
                ConfigFile,
                Debug,
                DeployPath,
                DisplayFilter,
                EnableFlags,
                EventCommand,
                EventCommandWait,
                EventMaxCount,
                EventThrottle,
                GatherPath,
                LogFileAutoFlush,
                LogFileMaxCount,
                LogFileMaxSize,
                LogFileName,
                LogFileOverWrite,
                LogFileServer,
                LogLevel,
                LogMatchDetail,
                LogMatchOnly,
                LogBufferOnMatch,
                LogToConsole,
                ModuleEnableByFilter,
                ModuleFilter,
                ModulePath,
                ModuleSource,
                ModuleListViewItems,
                MonitorProcesses,
                RegexPattern,
                RemoteMachines,
                RemoteMachinesPath,
                RemoteActivity,
                RemoteUseMachinesCache,
                Retries,
                RunAs,
                ServiceStartMode,
                ShutdownCommand,
                ShutdownCommandWait,
                SmtpPassword,
                SmtpPort,
                SmtpSendFrom,
                SmtpSendTo,
                SmtpServer,
                SmtpSsl,
                SmtpSubject,
                SmtpUser,
                StartupCommand,
                StartupCommandWait,
                StartEventSource,
                StartEventEnabled,
                StartEventEnabledImmediately,
                StartEventID,
                StopEventSource,
                StopEventDisabledPermanently,
                StopEventEnabled,
                StopEventID,
                TmfCacheDir,
                TmfServers,
                TraceFileInput,
                TraceFileOutput,
                UdpClientEnabled,
                UdpClientPort,
                UdpPingEnabled,
                UdpPingServer,
                UdpPingTimer,
                UdpServer,
                UdpServerPort,
                UrlFiles,
                UrlPassword,
                UrlSite,
                UrlUser,
                UseCredentials,
                UseServiceCredentials,
                UseTargetTime,
                UseTraceSourceForDestination,
                Version,
                WriteEvent
            }

            #endregion Public Enums

            #region Public Properties

            /// <summary>
            /// Gets or sets the config file.
            /// </summary>
            /// <value>The config file.</value>
            public string Activity
            {
                get { return _Activity; }
                set { _Activity = value; NotifyPropertyChanged("Activity"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [advanced options].
            /// </summary>
            /// <value><c>true</c> if [advanced options]; otherwise, /c>.</value>
            public bool AdvancedOptions
            {
                get { return _AdvancedOptions; }
                set { _AdvancedOptions = value; NotifyPropertyChanged("AdvancedOptions"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [allow single instance].
            /// </summary>
            /// <value><c>true</c> if [allow single instance]; otherwise, /c>.</value>
            public bool AllowSingleInstance
            {
                get { return _AllowSingleInstance; }
                set { _AllowSingleInstance = value; NotifyPropertyChanged("AllowSingleInstance"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether annoyance.
            /// </summary>
            /// <value><c>true</c> if annoyance; otherwise, /c>.</value>
            public bool Annoyance
            {
                get { return _Annoyance; }
                set { _Annoyance = value; NotifyPropertyChanged("Annoyance"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [auto scroll].
            /// </summary>
            /// <value><c>true</c> if [auto scroll]; otherwise, /c>.</value>
            public bool AutoScroll
            {
                get { return _AutoScroll; }
                set { _AutoScroll = value; NotifyPropertyChanged("AutoScroll"); }
            }

            //public bool BypassRegex { get; set; }
            /// <summary>
            /// Gets or sets the buffer lines.
            /// </summary>
            /// <value>The buffer lines.</value>
            public int BufferLines
            {
                get { return _BufferLines; }
                set { _BufferLines = value; NotifyPropertyChanged("BufferLines"); }
            }

            /// <summary>
            /// Gets or sets max number of etw buffers
            /// </summary>
            /// <value>The buffer max.</value>
            public int BufferMax
            {
                get { return _BufferMax; }
                set { _BufferMax = value; NotifyPropertyChanged("BufferMax"); }
            }

            /// <summary>
            /// Gets or sets min number of etw buffers
            /// </summary>
            /// <value>The buffer min.</value>
            public int BufferMin
            {
                get { return _BufferMin; }
                set { _BufferMin = value; NotifyPropertyChanged("BufferMin"); }
            }

            /// <summary>
            /// Gets or sets etw buffer size in kbytes
            /// </summary>
            /// <value>The size of the buffer.</value>
            public int BufferSize
            {
                get { return _BufferSize; }
                set { _BufferSize = value; NotifyPropertyChanged("BufferSize"); }
            }

            /// <summary>
            /// Gets or sets the config file.
            /// </summary>
            /// <value>The config file.</value>
            public string ConfigFile
            {
                get { return _ConfigFile; }
                set { _ConfigFile = value; NotifyPropertyChanged("ConfigFile"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="ConfigurationProperties" />
            /// is debug.
            /// </summary>
            /// <value><c>true</c> if debug; otherwise, /c>.</value>
            public bool Debug
            {
                get { return _Debug; }
                set { _Debug = value; NotifyPropertyChanged("Debug"); }
            }

            /// <summary>
            /// Gets or sets the deploy path.
            /// </summary>
            /// <value>The deploy path.</value>
            public string DeployPath
            {
                get { return _DeployPath; }
                set { _DeployPath = value; NotifyPropertyChanged("DeployPath"); }
            }

            /// <summary>
            /// Gets or sets the deploy path.
            /// </summary>
            /// <value>The deploy path.</value>
            public string DisplayFilter
            {
                get { return _DisplayFilter; }
                set { _DisplayFilter = value; NotifyPropertyChanged("DisplayFilter"); }
            }

            /// <summary>
            /// Gets or sets the enable flags.
            /// </summary>
            /// <value>The enable flags.</value>
            public uint EnableFlags
            {
                get { return _EnableFlags; }
                set { _EnableFlags = value; NotifyPropertyChanged("EnableFlags"); }
            }

            /// <summary>
            /// Gets or sets the event command.
            /// </summary>
            /// <value>The event command.</value>
            public string EventCommand
            {
                get { return _EventCommand; }
                set { _EventCommand = value; NotifyPropertyChanged("EventCommand"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [event command wait].
            /// </summary>
            /// <value><c>true</c> if [event command wait]; otherwise, /c>.</value>
            public bool EventCommandWait
            {
                get { return _EventCommandWait; }
                set { _EventCommandWait = value; NotifyPropertyChanged("EventCommandWait"); }
            }

            /// <summary>
            /// Gets or sets the event max count.
            /// </summary>
            /// <value>The event max count.</value>
            public int EventMaxCount
            {
                get { return _EventMaxCount; }
                set { _EventMaxCount = value; NotifyPropertyChanged("EventMaxCount"); }
            }

            /// <summary>
            /// Gets or sets the event throttle.
            /// </summary>
            /// <value>The event throttle.</value>
            public int EventThrottle
            {
                get { return _EventThrottle; }
                set { _EventThrottle = value; NotifyPropertyChanged("EventThrottle"); }
            }

            /// <summary>
            /// Gets or sets the gather path.
            /// </summary>
            /// <value>The gather path.</value>
            public string GatherPath
            {
                get { return _GatherPath; }
                set { _GatherPath = value; NotifyPropertyChanged("GatherPath"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [log buffer on match].
            /// </summary>
            /// <value><c>true</c> if [log buffer on match]; otherwise, /c>.</value>
            public bool LogBufferOnMatch
            {
                get { return _LogBufferOnMatch; }
                set { _LogBufferOnMatch = value; NotifyPropertyChanged("LogBufferOnMatch"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [log file auto flush].
            /// </summary>
            /// <value><c>true</c> if [log file auto flush]; otherwise, /c>.</value>
            public bool LogFileAutoFlush
            {
                get { return _LogFileAutoFlush; }
                set { _LogFileAutoFlush = value; NotifyPropertyChanged("LogFileAutoFlush"); }
            }

            /// <summary>
            /// Gets or sets the log file max count.
            /// </summary>
            /// <value>The log file max count.</value>
            public int LogFileMaxCount
            {
                get { return _LogFileMaxCount; }
                set { _LogFileMaxCount = value; NotifyPropertyChanged("LogFileMaxCount"); }
            }

            /// <summary>
            /// Gets or sets the size of the log file max.
            /// </summary>
            /// <value>The size of the log file max.</value>
            public int LogFileMaxSize
            {
                get { return _LogFileMaxSize; }
                set { _LogFileMaxSize = value; NotifyPropertyChanged("LogFileMaxSize"); }
            }

            /// <summary>
            /// Gets or sets the name of the log file.
            /// </summary>
            /// <value>The name of the log file.</value>
            public string LogFileName
            {
                get { return _LogFileName; }
                set { _LogFileName = value; NotifyPropertyChanged("LogFileName"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [log file over write].
            /// </summary>
            /// <value><c>true</c> if [log file over write]; otherwise, /c>.</value>
            public bool LogFileOverWrite
            {
                get { return _LogFileOverWrite; }
                set { _LogFileOverWrite = value; NotifyPropertyChanged("LogFileOverWrite"); }
            }

            /// <summary>
            /// Gets or sets the name of the log file.
            /// </summary>
            /// <value>The name of the log file.</value>
            public string LogFileServer
            {
                get { return _LogFileServer; }
                set { _LogFileServer = value; NotifyPropertyChanged("LogFileServer"); }
            }

            /// <summary>
            /// Gets or sets a value for ETW loglevel
            /// </summary>
            /// <value><c>true</c> if [log match detail]; otherwise, /c>.</value>
            public int LogLevel
            {
                get { return _LogLevel; }
                set { _LogLevel = value; NotifyPropertyChanged("LogLevel"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [log match detail].
            /// </summary>
            /// <value><c>true</c> if [log match detail]; otherwise, /c>.</value>
            public bool LogMatchDetail
            {
                get { return _LogMatchDetail; }
                set { _LogMatchDetail = value; NotifyPropertyChanged("LogMatchDetail"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [log match only].
            /// </summary>
            /// <value><c>true</c> if [log match only]; otherwise, /c>.</value>
            public bool LogMatchOnly
            {
                get { return _LogMatchOnly; }
                set { _LogMatchOnly = value; NotifyPropertyChanged("LogMatchOnly"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [log to console].
            /// </summary>
            /// <value><c>true</c> if [log to console]; otherwise, /c>.</value>
            public bool LogToConsole
            {
                get { return _LogToConsole; }
                set { _LogToConsole = value; NotifyPropertyChanged("LogToConsole"); }
            }

            /// <summary>
            /// Gets or sets the module enable by filter.
            /// </summary>
            /// <value>The module enable by filter.</value>
            public bool ModuleEnableByFilter
            {
                get { return _ModuleEnableByFilter; }
                set { _ModuleEnableByFilter = value; NotifyPropertyChanged("ModuleEnableByFilter"); }
            }

            /// <summary>
            /// Gets or sets the module filter.
            /// </summary>
            /// <value>The module filter.</value>
            public string ModuleFilter
            {
                get { return _ModuleFilter; }
                set { _ModuleFilter = value; NotifyPropertyChanged("ModuleFilter"); }
            }

            /// <summary>
            /// Gets or sets the modules.
            /// </summary>
            /// <value>The modules.</value>
            public string ModuleListViewItems
            {
                get
                {
                    return _ModuleListViewItems;
                }

                set
                {
                    _ModuleListViewItems = value; NotifyPropertyChanged("ModuleListViewItems");
                }
            }

            /// <summary>
            /// Gets or sets the modules path.
            /// </summary>
            /// <value>The modules.</value>
            public string ModulePath
            {
                get { return _ModulePath; }
                set { _ModulePath = value; NotifyPropertyChanged("ModulePath"); }
            }

            /// <summary>
            /// Gets or sets the module source.
            /// </summary>
            /// <value>The module source.</value>
            public string ModuleSource
            {
                get { return _ModuleSource; }
                set { _ModuleSource = value; NotifyPropertyChanged("ModuleSource"); }
            }

            /// <summary>
            /// Gets or sets the monitor processes.
            /// </summary>
            /// <value>The module source.</value>
            public bool MonitorProcesses
            {
                get { return _MonitorProcesses; }
                set { _MonitorProcesses = value; NotifyPropertyChanged("MonitorProcesses"); }
            }

            /// <summary>
            /// Gets or sets the regex pattern.
            /// </summary>
            /// <value>The regex pattern.</value>
            public string RegexPattern
            {
                get { return _RegexPattern; }
                set { _RegexPattern = value; NotifyPropertyChanged("RegexPattern"); }
            }

            /// <summary>
            /// Gets or sets the remote activity.
            /// </summary>
            /// <value>The remote activity.</value>
            public string RemoteActivity
            {
                get { return _RemoteActivity; }
                set { _RemoteActivity = value; NotifyPropertyChanged("RemoteActivity"); }
            }

            /// <summary>
            /// Gets or sets remotemachines.
            /// </summary>
            /// <value>The remote machines.</value>
            public string RemoteMachines
            {
                get { return _RemoteMachines; }
                set { _RemoteMachines = value; NotifyPropertyChanged("RemoteMachines"); }
            }

            /// <summary>
            /// Gets or sets remotemachines.
            /// </summary>
            /// <value>The remote machines.</value>
            public string RemoteMachinesPath
            {
                get { return _RemoteMachinesPath; }
                set { _RemoteMachinesPath = value; NotifyPropertyChanged("RemoteMachinesPath"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [remote use machines cache].
            /// </summary>
            /// <value><c>true</c> if [remote use machines cache]; otherwise, /c>.</value>
            public bool RemoteUseMachinesCache
            {
                get { return _RemoteUseMachinesCache; }
                set { _RemoteUseMachinesCache = value; NotifyPropertyChanged("RemoteUseMachinesCache"); }
            }

            /// <summary>
            /// Gets or sets the operation timeout.
            /// </summary>
            /// <value>The time in seconds.</value>
            public int Retries
            {
                get { return _Retries; }
                set { _Retries = value; NotifyPropertyChanged("Retries"); }
            }

            /// <summary>
            /// Gets or sets the run as.
            /// </summary>
            /// <value>The run as.</value>
            public string RunAs
            {
                get { return _Runas; }
                set { _Runas = value; NotifyPropertyChanged("Runas"); }
            }

            /// <summary>
            /// Gets or sets the SMTP password.
            /// </summary>
            /// <value>The SMTP password.</value>
            public string ServiceStartMode
            {
                get { return _ServiceStartMode; }
                set { _ServiceStartMode = value; NotifyPropertyChanged("ServiceStartMode"); }
            }

            /// <summary>
            /// Gets or sets the shutdown command.
            /// </summary>
            /// <value>The startup command.</value>
            public string ShutdownCommand
            {
                get { return _ShutdownCommand; }
                set { _ShutdownCommand = value; NotifyPropertyChanged("ShutdownCommand"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [shutdown command wait].
            /// </summary>
            /// <value><c>true</c> if [shutdown command wait]; otherwise, /c>.</value>
            public bool ShutdownCommandWait
            {
                get { return _ShutdownCommandWait; }
                set { _ShutdownCommandWait = value; NotifyPropertyChanged("ShutdownCommandWait"); }
            }

            /// <summary>
            /// Gets or sets the SMTP password.
            /// </summary>
            /// <value>The SMTP password.</value>
            public string SmtpPassword
            {
                get { return _SmtpPassword; }
                set { _SmtpPassword = value; NotifyPropertyChanged("SmtpPassword"); }
            }

            /// <summary>
            /// Gets or sets the SMTP port.
            /// </summary>
            /// <value>The SMTP port.</value>
            public int SmtpPort
            {
                get { return _SmtpPort; }
                set { _SmtpPort = value; NotifyPropertyChanged("SmtpPort"); }
            }

            /// <summary>
            /// Gets or sets the SMTP send from.
            /// </summary>
            /// <value>The SMTP send from.</value>
            public string SmtpSendFrom
            {
                get { return _SmtpSendFrom; }
                set { _SmtpSendFrom = value; NotifyPropertyChanged("SmtpSendFrom"); }
            }

            /// <summary>
            /// Gets or sets the SMTP send to.
            /// </summary>
            /// <value>The SMTP send to.</value>
            public string SmtpSendTo
            {
                get { return _SmtpSendTo; }
                set { _SmtpSendTo = value; NotifyPropertyChanged("SmtpSendTo"); }
            }

            /// <summary>
            /// Gets or sets the SMTP server.
            /// </summary>
            /// <value>The SMTP server.</value>
            public string SmtpServer
            {
                get { return _SmtpServer; }
                set { _SmtpServer = value; NotifyPropertyChanged("SmtpServer"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [SMTP SSL].
            /// </summary>
            /// <value><c>true</c> if [SMTP SSL]; otherwise, /c>.</value>
            public bool SmtpSsl
            {
                get { return _SmtpSsl; }
                set { _SmtpSsl = value; NotifyPropertyChanged("SmtpSsl"); }
            }

            /// <summary>
            /// Gets or sets the SMTP subject.
            /// </summary>
            /// <value>The SMTP subject.</value>
            public string SmtpSubject
            {
                get { return _SmtpSubject; }
                set { _SmtpSubject = value; NotifyPropertyChanged("SmtpSubject"); }
            }

            /// <summary>
            /// Gets or sets the SMTP user.
            /// </summary>
            /// <value>The SMTP user.</value>
            public string SmtpUser
            {
                get { return _SmtpUser; }
                set { _SmtpUser = value; NotifyPropertyChanged("SmtpUser"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [start event enabled].
            /// </summary>
            /// <value><c>true</c> if [start event enabled]; otherwise, /c>.</value>
            public bool StartEventEnabled
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [start event enabled immediately].
            /// </summary>
            /// <value><c>true</c> if [start event enabled immediately]; otherwise, /c>.</value>
            public bool StartEventEnabledImmediately
            {
                get { return _StartEventEnabledImmediately; }
                set { _StartEventEnabledImmediately = value; NotifyPropertyChanged("StartEventEnabledImmediately"); }
            }

            /// <summary>
            /// Gets or sets the start event ID.
            /// </summary>
            /// <value>The start event ID.</value>
            public int StartEventID
            {
                get { return _StartEventID; }
                set { _StartEventID = value; NotifyPropertyChanged("StartEventID"); }
            }

            /// <summary>
            /// Gets or sets the start event source.
            /// </summary>
            /// <value>The start event source.</value>
            public string StartEventSource
            {
                get { return _StartEventSource; }
                set { _StartEventSource = value; NotifyPropertyChanged("StartEventSource"); }
            }

            /// <summary>
            /// Gets or sets the startup command.
            /// </summary>
            /// <value>The startup command.</value>
            public string StartupCommand
            {
                get { return _StartupCommand; }
                set { _StartupCommand = value; NotifyPropertyChanged("StartupCommand"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [startup command wait].
            /// </summary>
            /// <value><c>true</c> if [startup command wait]; otherwise, /c>.</value>
            public bool StartupCommandWait
            {
                get { return _StartupCommandWait; }
                set { _StartupCommandWait = value; NotifyPropertyChanged("StartupCommandWait"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [stop event disabled permanently].
            /// </summary>
            /// <value><c>true</c> if [stop event disabled permanently]; otherwise, /c>.</value>
            public bool StopEventDisabledPermanently
            {
                get { return _StopEventDisabledPermanently; }
                set { _StopEventDisabledPermanently = value; NotifyPropertyChanged("StopEventDisabledPermanently"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [stop event enabled].
            /// </summary>
            /// <value><c>true</c> if [stop event enabled]; otherwise, /c>.</value>
            public bool StopEventEnabled
            {
                get { return _StopEventEnabled; }
                set { _StopEventEnabled = value; NotifyPropertyChanged("StopEventEnabled"); }
            }

            /// <summary>
            /// Gets or sets the stop event ID.
            /// </summary>
            /// <value>The stop event ID.</value>
            public int StopEventID
            {
                get { return _StopEventID; }
                set { _StopEventID = value; NotifyPropertyChanged("StopEventID"); }
            }

            /// <summary>
            /// Gets or sets the stop event source.
            /// </summary>
            /// <value>The stop event source.</value>
            public string StopEventSource
            {
                get { return _StopEventSource; }
                set { _StopEventSource = value; NotifyPropertyChanged("StopEventSource"); }
            }

            //string _startupCommand;
            /// <summary>
            /// Gets or sets the TMF cache dir.
            /// </summary>
            /// <value>The TMF cache dir.</value>
            public string TmfCacheDir
            {
                get { return _TmfCacheDir; }
                set { _TmfCacheDir = value; NotifyPropertyChanged("TmfCacheDir"); }
            }

            /// <summary>
            /// Gets or sets the TMF servers.
            /// </summary>
            /// <value>The TMF servers.</value>
            public string TmfServers
            {
                get { return _TmfServers; }
                set { _TmfServers = value; NotifyPropertyChanged("TmfServers"); }
            }

            /// <summary>
            /// Gets or sets the trace file.
            /// </summary>
            /// <value>The trace file.</value>
            public string TraceFileInput
            {
                get { return _TraceFileInput; }
                set { _TraceFileInput = value; NotifyPropertyChanged("TraceFileInput"); }
            }

            /// <summary>
            /// Gets or sets the trace file.
            /// </summary>
            /// <value>The trace file.</value>
            public string TraceFileOutput
            {
                get { return _TraceFileOutput; }
                set { _TraceFileOutput = value; NotifyPropertyChanged("TraceFileOutput"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [UDP client enabled].
            /// </summary>
            /// <value><c>true</c> if [UDP client enabled]; otherwise, /c>.</value>
            public bool UdpClientEnabled
            {
                get { return _UdpClientEnabled; }
                set { _UdpClientEnabled = value; NotifyPropertyChanged("UdpClientEnabled"); }
            }

            /// <summary>
            /// Gets or sets the UDP client port.
            /// </summary>
            /// <value>The UDP client port.</value>
            public int UdpClientPort
            {
                get { return _UdpClientPort; }
                set { _UdpClientPort = value; NotifyPropertyChanged("UdpClientPort"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [UDP client enabled].
            /// </summary>
            /// <value><c>true</c> if [UDP client enabled]; otherwise, /c>.</value>
            public bool UdpPingEnabled
            {
                get { return _UdpPingEnabled; }
                set { _UdpPingEnabled = value; NotifyPropertyChanged("UdpPingEnabled"); }
            }

            /// <summary>
            /// Gets or sets the UDP ping server.
            /// </summary>
            /// <value>The UDP ping server.</value>
            public string UdpPingServer
            {
                get { return _UdpPingServer; }
                set { _UdpPingServer = value; NotifyPropertyChanged("UdpPingServer"); }
            }

            /// <summary>
            /// Gets or sets the UDP ping timer.
            /// </summary>
            /// <value>The UDP ping timer.</value>
            public int UdpPingTimer
            {
                get { return _UdpPingTimer; }
                set { _UdpPingTimer = value; NotifyPropertyChanged("UdpPingTimer"); }
            }

            /// <summary>
            /// Gets or sets the UDP server.
            /// </summary>
            /// <value>The UDP server.</value>
            public string UdpServer
            {
                get { return _UdpServer; }
                set { _UdpServer = value; NotifyPropertyChanged("UdpServer"); }
            }

            /// <summary>
            /// Gets or sets the UDP server port.
            /// </summary>
            /// <value>The UDP server port.</value>
            public int UdpServerPort
            {
                get { return _UdpServerPort; }
                set { _UdpServerPort = value; NotifyPropertyChanged("UdpServerPort"); }
            }

            /// <summary>
            /// Gets or sets the URL files.
            /// </summary>
            /// <value>The URL files.</value>
            public string UrlFiles
            {
                get { return _UrlFiles; }
                set { _UrlFiles = value; NotifyPropertyChanged("UrlFiles"); }
            }

            /// <summary>
            /// Gets or sets the URL password.
            /// </summary>
            /// <value>The URL password.</value>
            public string UrlPassword
            {
                get { return _UrlPassword; }
                set { _UrlPassword = value; NotifyPropertyChanged("UrlPassword"); }
            }

            /// <summary>
            /// Gets or sets the URL site.
            /// </summary>
            /// <value>The URL site.</value>
            public string UrlSite
            {
                get { return _UrlSite; }
                set { _UrlSite = value; NotifyPropertyChanged("UrlSite"); }
            }

            /// <summary>
            /// Gets or sets the URL user.
            /// </summary>
            /// <value>The URL user.</value>
            public string UrlUser
            {
                get { return _UrlUser; }
                set { _UrlUser = value; NotifyPropertyChanged("UrlUser"); }
            }

            /// <summary>
            /// Gets or sets the UseCredentials
            /// </summary>
            /// <value>The Use Credentials.</value>
            public bool UseCredentials
            {
                get { return _UseCredentials; }
                set { _UseCredentials = value; NotifyPropertyChanged("UseCredentials"); }
            }

            /// <summary>
            /// Gets or sets the UseServiceCredentials
            /// </summary>
            /// <value>The Use Credentials.</value>
            public bool UseServiceCredentials
            {
                get { return _UseServiceCredentials; }
                set { _UseServiceCredentials = value; NotifyPropertyChanged("UseServiceCredentials"); }
            }

            /// <summary>
            /// Gets or sets the UseTargetTime
            /// </summary>
            /// <value>The Use Target Time.</value>
            public bool UseTargetTime
            {
                get { return _UseTargetTime; }
                set { _UseTargetTime = value; NotifyPropertyChanged("UseTargetTime"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [use trace source for destination].
            /// </summary>
            /// <value><c>true</c> if [use trace source for destination]; otherwise, /c>.</value>
            public bool UseTraceSourceForDestination
            {
                get { return _UseTraceSourceForDestination; }
                set { _UseTraceSourceForDestination = value; NotifyPropertyChanged("UseTraceSourceForDestination"); }
            }

            /// <summary>
            /// Gets or sets a value indicating utility version.
            /// </summary>
            /// <value>The utility version.</value>
            public string Version
            {
                get { return _Version; }
                set { _Version = value; } // NotifyPropertyChanged("Version"); }
            }

            /// <summary>
            /// Gets or sets a value indicating whether [write event].
            /// </summary>
            /// <value><c>true</c> if [write event]; otherwise, /c>.</value>
            public bool WriteEvent
            {
                get { return _WriteEvent; }
                set { _WriteEvent = value; NotifyPropertyChanged("WriteEvent"); }
            }

            #endregion Public Properties

            #region Public Methods

            /// <summary>
            /// Clears the config.
            /// </summary>
            public void ClearConfig()
            {
                InitializeConfig();
            }

            /// <summary>
            /// Returns whether PropertyNotifications are enabled
            /// </summary>
            /// <returns></returns>
            public bool GetPropertyNotifications()
            {
                return _propertyNotificationsEnabled;
            }

            /// <summary>
            /// Gets the value by property name string.
            /// </summary>
            /// <param name="propertyName">Name of the property.</param>
            /// <returns>System.Object.</returns>
            public object GetValueByPropertyNameString(string propertyName)
            {
                return this.GetType().GetProperty(propertyName).GetValue(this, null);
            }

            /// <summary>
            /// Initializes config for new config files. sets default strings like log file names
            /// and TMF server.
            /// </summary>
            public void InitializeNewConfig()
            {
                InitializeConfig();

                Annoyance = true;
                LogFileName = Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName) + ".log";
                TraceFileOutput = Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName) + ".etl";
                RunAs = ExecutionOptions.Gui.ToString();
                TmfCacheDir = "tmfs";
                TmfServers = "http://ctxsym.citrix.com/tmfs/xaxd/";
                UseTargetTime = true;
            }

            /// <summary>
            /// Notifies all properties changed.
            /// </summary>
            public void NotifyAllPropertiesChanged()
            {
                if (_propertyNotificationsEnabled)
                {
                    PropertyInfo[] properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (PropertyInfo info in properties)
                    {
                        NotifyPropertyChanged(info.Name);
                    }
                }
            }

            /// <summary>
            /// Notifies the property changed.
            /// </summary>
            /// <param name="property_name">The propery_name.</param>
            public void NotifyPropertyChanged(string property_name)
            {
                // WPF binds to PropertyChanged. call this when updating source property.
                if (_propertyNotificationsEnabled)
                {
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs(property_name));
                    }
                }
            }

            public void SetPropertyNotifications(bool enable)
            {
                _propertyNotificationsEnabled = enable;
            }

            /// <summary>
            /// Shallows the copy.
            /// </summary>
            /// <returns>ConfigurationProperties.</returns>
            public ConfigurationProperties ShallowCopy()
            {
                return (ConfigurationProperties)this.MemberwiseClone();
            }

            /// <summary>
            /// converts configuration properties to DataTable
            /// </summary>
            /// <returns>DataTable.</returns>
            public DataTable ToDataTable()
            {
                var table = new DataTable();
                table.Columns.Add("Property");
                table.Columns.Add("Value");
                foreach (
                    PropertyInfo property in
                        typeof(ConfigurationProperties).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    DataRow dR = table.NewRow();
                    dR["Property"] = property.Name;
                    dR["Value"] = property.GetValue(this, null);
                    table.Rows.Add(dR);
                }
                return table;
            }

            /// <summary>
            /// converts configuration properties into KeyValueCollection
            /// </summary>
            /// <returns>KeyValueConfigurationCollection.</returns>
            public KeyValueConfigurationCollection ToKeyValueConfigurationCollection()
            {
                var kvcc = new KeyValueConfigurationCollection();
                foreach (
                    PropertyInfo property in
                        typeof(ConfigurationProperties).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    System.Diagnostics.Debug.Print(string.Format("ToKeyValueConfigurationCollection {0}:{1}",
                        property.Name, property.GetValue(this, null)));
                    kvcc.Add(new KeyValueConfigurationElement(property.Name, property.GetValue(this, null).ToString()));
                }

                return kvcc;
            }

            /// <summary>
            /// To the key value pair.
            /// </summary>
            /// <param name="propertyName">Name of the property.</param>
            /// <returns>KeyValuePair{System.StringSystem.Object}.</returns>
            public KeyValuePair<string, object> ToKeyValuePair(string propertyName)
            {
                return new KeyValuePair<string, object>(propertyName, typeof(ConfigurationProperties)
                    .GetProperty(propertyName).GetValue(this, null).ToString());
            }

            #endregion Public Methods

            #region Private Methods

            /// <summary>
            /// Initializes the config.
            /// </summary>
            /// <exception cref="System.Exception">InitializeProperties: ERROR:undefined property
            /// type: + property.PropertyType.ToString()</exception>
            private void InitializeConfig()
            {
                foreach (PropertyInfo property in typeof(ConfigurationProperties).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(this, string.Empty, null);
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        property.SetValue(this, 0, null);
                    }
                    else if (property.PropertyType == typeof(uint))
                    {
                        property.SetValue(this, (uint)0, null);
                    }
                    else if (property.PropertyType == typeof(bool))
                    {
                        property.SetValue(this, false, null);
                    }
                    else if (property.PropertyType.IsEnum)
                    {
                        if (EnumProperties.IsDefined(property.PropertyType, "Unknown"))
                        {
                            property.SetValue(this, EnumProperties.Parse(property.PropertyType, "Unknown", true), null);
                        }
                    }
                    else if (property.PropertyType.IsArray)
                    {
                        property.SetValue(this, new string[0], property.GetIndexParameters());
                    }
                    else
                    {
                        throw new Exception("InitializeProperties: ERROR:undefined property type:" + property.PropertyType.ToString());
                    }
                }

                Activity = ActivityType.Unknown.ToString();
                ConfigFile = Process.GetCurrentProcess().MainModule.ModuleName + ".config";
                LogFileAutoFlush = true;
                LogToConsole = true;
                RunAs = ExecutionOptions.Unknown.ToString();
                SmtpPort = 25;
                LogLevel = 16;
                BufferLines = 99999;
                BufferMin = 40;
                BufferMax = 80;
                BufferSize = 100;
                Retries = 1;
                LogFileOverWrite = true;
                LogFileMaxCount = 10;
                LogFileMaxSize = 20;
                RemoteActivity = RemoteOperations.RemoteOperationMethods.Unknown.ToString();
                ServiceStartMode = Configuration.ServiceStartModeOptionEnum.Automatic.ToString();
                StartEventSource = "Application";
                StopEventSource = "Application";
                ModuleSource = ModuleSourceType.Configuration.ToString();
                UdpClientPort = 45000;
                UdpPingTimer = 60;
                UdpServerPort = 45001;
                AutoScroll = true;
            }

            #endregion Private Methods
        }

        #endregion Public Classes
    }
}