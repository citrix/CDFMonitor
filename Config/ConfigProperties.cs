// todo 1.166 deprecated

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CDFM.Config;
using CDFM.Engine;
using CDFM.WMI;
using Microsoft.Win32;

namespace CDFM.Config2
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks></remarks>
    public class ConfigProperties
    {
        #region Fields

        /// <summary>
        /// 
        /// </summary>
        public const string CONFIG_URL = "https://taas.citrix.com/tools/cdfmonitor/configs/"; //in configuration.cs

        /// <summary>
        /// 
        /// </summary>
        public const string MODULE_LIST_REGISTRY_HIVE_AND_KEY = "HKEY_LOCAL_MACHINE\\" + MODULE_LIST_REGISTRY_KEY + "\\";//in configuration.cs

        /// <summary>
        /// 
        /// </summary>
        public const string MODULE_LIST_REGISTRY_KEY = "SYSTEM\\CurrentControlSet\\Control\\Citrix\\Tracing\\Modules";//in configuration.cs

        /// <summary>
        /// 
        /// </summary>
        public const string UPDATE_URL = "https://taas.citrix.com/tools/cdfmonitor/update/version.xml";//in configuration.cs

        
        //public string EventCommand { get; set; }
        /// <summary>
        /// 
        /// </summary>
        private string _regexpattern = String.Empty; //in configurationproperties.cs

        //public string StartupCommand { get; set; }

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigProperties"/> class.
        /// </summary>
        /// <remarks></remarks>
        public ConfigProperties()  //in configurationproperties.cs
        {
            InitializeConfig();
        }

        #endregion Constructors

        #region Enumerations

        //public enum ActivityType
        //{
        //    ParseToCsv,
        //    RegexParseToCsv,
        //    RegexTraceToCsv,
        //    ProcessingArguments,
        //    Remote,
        //    TraceToEtl,
        //    TraceToCsv,
        //    Unknown,
        //}
        /// <summary>
        /// 
        /// </summary>
        /// <remarks></remarks>
        public enum ActivityType //in configuration.cs
        {
            /// <summary>
            /// 
            /// </summary>
            Unknown,

            /// <summary>
            /// 
            /// </summary>
            ProcessingArguments,

            /// <summary>
            /// 
            /// </summary>
            Remote,

            /// <summary>
            /// 
            /// </summary>
            TraceToEtl,

            /// <summary>
            /// 
            /// </summary>
            RegexTraceToCsv,

            /// <summary>
            /// 
            /// </summary>
            TraceToCsv,

            /// <summary>
            /// 
            /// </summary>
            ParseToCsv,

            /// <summary>
            /// 
            /// </summary>
            RegexParseToCsv,
        }

   

        #endregion Enumerations

        #region Properties

        /// <summary>
        /// Gets or sets the activity.
        /// </summary>
        /// <value>The activity.</value>
        /// <remarks></remarks>
        public ActivityType Activity { get; set; } //in configuration.cs

        /// <summary>
        /// Gets or sets a value indicating whether [allow single instance].
        /// </summary>
        /// <value><c>true</c> if [allow single instance]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool AllowSingleInstance { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [bypass regex].
        /// </summary>
        /// <value><c>true</c> if [bypass regex]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool BypassRegex { get; set; } //in configuration.cs

        /// <summary>
        /// Gets or sets the config file.
        /// </summary>
        /// <value>The config file.</value>
        /// <remarks></remarks>
        public string ConfigFile { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ConfigProperties"/> is debug.
        /// </summary>
        /// <value><c>true</c> if debug; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool Debug { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ConfigProperties"/> is editor.
        /// </summary>
        /// <value><c>true</c> if editor; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool Editor { get; set; } //todo: need to convert to runas

        /// <summary>
        /// Gets or sets the event command.
        /// </summary>
        /// <value>The event command.</value>
        /// <remarks></remarks>
        public string EventCommand { get; set; } //readded to configurationproperties.cs

        /// <summary>
        /// Gets or sets the event commands.
        /// </summary>
        /// <value>The event commands.</value>
        /// <remarks></remarks>
        public List<ProcessCommandResults> EventCommands { get; set; } //readded to configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [event command wait].
        /// </summary>
        /// <value><c>true</c> if [event command wait]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool EventCommandWait { get; set; } //readded to configurationproperties.cs

        /// <summary>
        /// Gets or sets the event max count.
        /// </summary>
        /// <value>The event max count.</value>
        /// <remarks></remarks>
        public int EventMaxCount { get; set; } //readded to configurationproperties.cs

        /// <summary>
        /// Gets or sets the event throttle.
        /// </summary>
        /// <value>The event throttle.</value>
        /// <remarks></remarks>
        public int EventThrottle { get; set; } //readded to configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ConfigProperties"/> is hidden.
        /// </summary>
        /// <value><c>true</c> if hidden; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool Hidden { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ConfigProperties"/> is initialized.
        /// </summary>
        /// <value><c>true</c> if initialized; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool Initialized { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [log file auto flush].
        /// </summary>
        /// <value><c>true</c> if [log file auto flush]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool LogFileAutoFlush { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets the log file max count.
        /// </summary>
        /// <value>The log file max count.</value>
        /// <remarks></remarks>
        public int LogFileMaxCount { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets the size of the log file max.
        /// </summary>
        /// <value>The size of the log file max.</value>
        /// <remarks></remarks>
        public int LogFileMaxSize { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets the name of the log file.
        /// </summary>
        /// <value>The name of the log file.</value>
        /// <remarks></remarks>
        public string LogFileName { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [log file over write].
        /// </summary>
        /// <value><c>true</c> if [log file over write]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool LogFileOverWrite { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [log match detail].
        /// </summary>
        /// <value><c>true</c> if [log match detail]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool LogMatchDetail { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [log match only].
        /// </summary>
        /// <value><c>true</c> if [log match only]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool LogMatchOnly { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [log to console].
        /// </summary>
        /// <value><c>true</c> if [log to console]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool LogToConsole { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [log to etl].
        /// </summary>
        /// <value><c>true</c> if [log to etl]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool LogToEtl { get; set; } //in configurationproperties.cs

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>not in config file</remarks>
        public Dictionary<string, string> ModuleList { get; set; }  //in configuration.cs

        /// <summary>
        /// Gets or sets the modules.
        /// </summary>
        /// <value>The modules.</value>
        /// <remarks></remarks>
        public string Modules { get; set; } //in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [processing arguments].
        /// </summary>
        /// <value><c>true</c> if [processing arguments]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool ProcessingArguments { get; set; }  //in configuration.cs

        /// <summary>
        /// Gets or sets the regex pattern.
        /// </summary>
        /// <value>The regex pattern.</value>
        /// <remarks></remarks>
        public string RegexPattern //copied to configurationproperties.cs
        {
            get { return (_regexpattern); }
            set
            {
                SetBypassRegex();
                _regexpattern = value;
            }
        }

        /// <summary>
        /// Gets or sets the regex variables.
        /// </summary>
        /// <value>The regex variables.</value>
        /// <remarks>not in config file.</remarks>
        public Dictionary<string, string> RegexVariables { get; set; } //in configuration.cs
        
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>not in config file</remarks>
        public Dictionary<string, string> RegistryModuleList { get; set; } //in configuration.cs

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>not in config file</remarks>
        public Dictionary<string, RemoteOperations.RemoteStatus> RemoteMachineList { get; set; } //copied to configuration.cs

        /// <summary>
        /// Gets or sets remotemachines.
        /// </summary>
        /// <remarks></remarks>
        public string RemoteMachines { get; set; } //copied to configurationproperties.cs
        

        /// <summary>
        /// Gets or sets the resource credentials.
        /// </summary>
        /// <value>The resource credentials.</value>
        /// <remarks></remarks>
        public ResourceManagement ResourceCredentials { get; set; } // in configuration.cs

        /// <summary>
        /// Gets a value indicating whether [send SMTP].
        /// </summary>
        /// <remarks></remarks>
        public bool SendSmtp { get; private set; } // in configuration.cs

        /// <summary>
        /// Gets a value indicating whether [send URL].
        /// </summary>
        /// <remarks></remarks>
        public bool SendUrl { get; private set; } // in configuration.cs

        //public string RegexPattern { get; set; }
        //private string _regexPattern;
        /// <summary>
        /// Gets or sets the SMTP password.
        /// </summary>
        /// <value>The SMTP password.</value>
        /// <remarks></remarks>
        public string SmtpPassword { get; set; } // todo put into credentials?  // in configurationproperties.cs

        /// <summary>
        /// Gets or sets the SMTP port.
        /// </summary>
        /// <value>The SMTP port.</value>
        /// <remarks></remarks>
        public int SmtpPort { get; set; } // in configurationproperties.cs

        /// <summary>
        /// Gets or sets the SMTP send from.
        /// </summary>
        /// <value>The SMTP send from.</value>
        /// <remarks></remarks>
        public string SmtpSendFrom { get; set; }// in configurationproperties.cs

        /// <summary>
        /// Gets or sets the SMTP send to.
        /// </summary>
        /// <value>The SMTP send to.</value>
        /// <remarks></remarks>
        public string SmtpSendTo { get; set; }// in configurationproperties.cs

        /// <summary>
        /// Gets or sets the SMTP server.
        /// </summary>
        /// <value>The SMTP server.</value>
        /// <remarks></remarks>
        public string SmtpServer { get; set; }// in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [SMTP SSL].
        /// </summary>
        /// <value><c>true</c> if [SMTP SSL]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool SmtpSsl { get; set; }// in configurationproperties.cs

        /// <summary>
        /// Gets or sets the SMTP subject.
        /// </summary>
        /// <value>The SMTP subject.</value>
        /// <remarks></remarks>
        public string SmtpSubject { get; set; }// in configurationproperties.cs

        /// <summary>
        /// Gets or sets the SMTP user.
        /// </summary>
        /// <value>The SMTP user.</value>
        /// <remarks></remarks>
        public string SmtpUser { get; set; }// in configurationproperties.cs

        /// <summary>
        /// Gets or sets the startup command.
        /// </summary>
        /// <value>The startup command.</value>
        /// <remarks></remarks>
        public string StartupCommand { get; set; }// in configurationproperties.cs

        /// <summary>
        /// Gets or sets the startup commands.
        /// </summary>
        /// <value>The startup commands.</value>
        /// <remarks></remarks>
        public List<ProcessCommandResults> StartupCommands { get; set; }// in configuration.cs

        /// <summary>
        /// Gets the status.
        /// </summary>
        /// <remarks></remarks>
        public string Status { get; private set; }// copied to configuration.cs

        /// <summary>
        /// Gets or sets the TMF cache dir.
        /// </summary>
        /// <value>The TMF cache dir.</value>
        /// <remarks></remarks>
        public string TmfCacheDir { get; set; }// in configurationproperties.cs

        /// <summary>
        /// Gets or sets the TMF server.
        /// </summary>
        /// <value>The TMF server.</value>
        /// <remarks></remarks>
        public string TmfServers { get; set; }// refactored to tmfservers in configurationproperties.cs

        /// <summary>
        /// Gets or sets the trace file.
        /// </summary>
        /// <value>The trace file.</value>
        /// <remarks></remarks>
        public string TraceFile { get; set; }// in configurationproperties.cs

        /// <summary>
        /// Gets or sets the URL files.
        /// </summary>
        /// <value>The URL files.</value>
        /// <remarks></remarks>
        public string UrlFiles { get; set; } // in configurationproperties.cs

        /// <summary>
        /// Gets or sets the URL password.
        /// </summary>
        /// <value>The URL password.</value>
        /// <remarks></remarks>
        public string UrlPassword { get; set; }// todo put into credentials?  // in configurationproperties.cs

        /// <summary>
        /// Gets or sets the URL site.
        /// </summary>
        /// <value>The URL site.</value>
        /// <remarks></remarks>
        public string UrlSite { get; set; } // in configurationproperties.cs

        /// <summary>
        /// Gets or sets the URL user.
        /// </summary>
        /// <value>The URL user.</value>
        /// <remarks></remarks>
        public string UrlUser { get; set; } // in configurationproperties.cs

        /// <summary>
        /// Gets or sets a value indicating whether [write event].
        /// </summary>
        /// <value><c>true</c> if [write event]; otherwise, <c>false</c>.</value>
        /// <remarks></remarks>
        public bool WriteEvent { get; set; } // in configurationproperties.cs

        #endregion Properties

        #region Methods

        /// <summary>
        /// Clears the config.
        /// </summary>
        /// <remarks></remarks>
        public void ClearConfig() // in configurationproperties.cs
        {
            InitializeConfig();
        }

        /// <summary>
        /// Determines the type of the activity.
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public ActivityType DetermineActivityType() // in configuration.cs
        {
            System.Diagnostics.Debug.Assert(Initialized);

            //SetBypassRegex();
            //SetSendSmtp();
            //SetSendUrl();
            //determine activity tab settings
            //if bypassregex then it has to be trace or parse
            //if not bypassregex then it has to be trace message or parse message
            // todo move this to config file but keep this for verification?

            if (ProcessingArguments)
            {
                Activity = ActivityType.ProcessingArguments;
            }
            else if (BypassRegex && !LogToEtl && TraceFile.Length < 1)
            {
                //then realtime etw trace message to csv or console
                Activity = ActivityType.TraceToCsv;
            }
            else if (!BypassRegex && !LogToEtl && TraceFile.Length < 1)
            {
                //then realtime etw trace message with regex to csv or console
                Activity = ActivityType.RegexTraceToCsv;
            }
            else if (!BypassRegex && !LogToEtl && TraceFile.Length > 0)
            {
                //then etl parse message with regex to csv or console
                Activity = ActivityType.RegexParseToCsv;
            }
            else if (TraceFile.Length > 0 && !LogToEtl)
            {
                //then etl parse to csv or console
                Activity = ActivityType.ParseToCsv;
            }
            else if (TraceFile.Length > 0 && LogToEtl)
            {
                //then realtime etw trace to etl
                Activity = ActivityType.TraceToEtl;
            }
            else
            {
                Activity = ActivityType.Unknown;
            }
            return Activity;
        }

        /// <summary>
        /// Displays the config settings.
        /// </summary>
        /// <remarks></remarks>
        public void DisplayConfigSettings(bool newActivity = false) // in configuration.cs
        {
            var sb = new StringBuilder();

            sb.AppendLine("--------------------------------------------------------------------------------------------");
            sb.AppendLine(String.Format("{0} CDFMonitor Activity:{1}", newActivity ? "Start" : "End", DateTime.Now.ToString()));
            sb.AppendLine("CDFMonitor version:" + Assembly.GetExecutingAssembly().GetName().Version);
            //write out configuration:
            sb.AppendLine("Command Line Arguments:" + String.Join(" ", CDFMonitor.Args));

            sb.AppendLine("Activity Type:" + Activity); 
            sb.AppendLine("modules:"); 
            if (Activity == ActivityType.TraceToEtl
                | Activity == ActivityType.TraceToCsv
                | Activity == ActivityType.RegexTraceToCsv)
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
            sb.AppendLine("bypassregex:" + BypassRegex);
            sb.AppendLine("regexpattern:" + RegexPattern);
            sb.AppendLine("writeevent:" + WriteEvent);
            sb.AppendLine("debug:" + Debug);
            sb.AppendLine("editor:" + Editor);
            sb.AppendLine("eventthrottle:" + EventThrottle);

            //parse command to try and determine arguments
            foreach (ProcessCommandResults eventCommand in EventCommands)
            {
                sb.AppendLine("eventcommand:" + eventCommand);
                sb.AppendLine("  command:" + eventCommand.Command);
                sb.AppendLine("  arguments:" + eventCommand.Arguments);
            }

            foreach (ProcessCommandResults startupCommand in StartupCommands)
            {
                sb.AppendLine("startupcommand:" + startupCommand);
                sb.AppendLine("  command:" + startupCommand.Command);
                sb.AppendLine("  arguments:" + startupCommand.Arguments);
            }

            sb.AppendLine("eventcommandwait:" + EventCommandWait);
            sb.AppendLine("eventmaxcount:" + EventMaxCount);
            sb.AppendLine("sessionname:" + CDFMonitor.SessionName);

            sb.AppendLine("logfilemaxsize:" + LogFileMaxSize/1024/1000);
            sb.AppendLine("logfilemaxcount:" + LogFileMaxCount);
            sb.AppendLine("logfileautoflush:" + LogFileAutoFlush);
            sb.AppendLine("logfileoverwrite:" + LogFileOverWrite);
            sb.AppendLine("logFileName:" + LogFileName);
            sb.AppendLine("logtoconsole:" + LogToConsole);
            sb.AppendLine("logmatchdetail:" + LogMatchDetail);
            sb.AppendLine("logmatchonly:" + LogMatchOnly);
            sb.AppendLine("logtoetl:" + LogToEtl);
            sb.AppendLine("hidden:" + Hidden);
            sb.AppendLine("allowsingleinstance:" + AllowSingleInstance);
            sb.AppendLine("tracefile:" + TraceFile);
            sb.AppendLine("smtpserver:" + SmtpServer);
            sb.AppendLine("smtpport:" + SmtpPort.ToString());
            sb.AppendLine("smtpsendto:" + SmtpSendTo);
            sb.AppendLine("smtpsendfrom:" + SmtpSendFrom);
            sb.AppendLine("smtpsubject:" + SmtpSubject);
            sb.AppendLine("smtpuser:" + SmtpUser);
            sb.AppendLine("sendsmtp:" + SendSmtp.ToString());
            sb.AppendLine("smtpssl:" + SmtpSsl);
            sb.AppendLine("urlfiles:" + UrlFiles); //String.Join(";", UrlFiles));
            sb.AppendLine("urlsite:" + UrlSite);
            sb.AppendLine("urluser:" + UrlUser);
            sb.AppendLine("sendURL:" + SendUrl.ToString());
            sb.AppendLine("tmfserver:" + TmfServers);
            sb.AppendLine("tmfcachedir:" + TmfCacheDir);

            CDFMonitor.WriteOutputHandler(sb.ToString());
        }

        /// <summary>
        /// Enumerates Citrix CDF key into Dictionary with guid key, guidString name value returned
        /// </summary>
        /// <returns>Dictionary</returns>
        /// <remarks></remarks>
        public Dictionary<string, string> EnumModulesFromReg() // in configuration.cs
        {
            var moduleList = new Dictionary<string, string>();
            try
            {
                RegistryKey moduleKey = Registry.LocalMachine;
                foreach (string regkey in moduleKey.OpenSubKey(MODULE_LIST_REGISTRY_KEY).GetSubKeyNames())
                {
                    //120612 making changes for local and remoteregistrymodules. removed braces here for consistency when writing file
                    //moduleList.Add(regkey, (string)Registry.GetValue(ConfigProperties.MODULE_LIST_REGISTRY_HIVE_AND_KEY
                    //   + regkey, "GUID", string.Empty));
                    moduleList.Add((string) Registry.GetValue(MODULE_LIST_REGISTRY_HIVE_AND_KEY
                                                              + regkey, "GUID", String.Empty), regkey);
                }
                return (moduleList);
            }
            catch
            {
                return (moduleList);
            }
        }

        /// <summary>
        /// Processes the commands.
        /// </summary>
        /// <param name="commands">The commands.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public List<ProcessCommandResults> ProcessCommand(string[] commands) // refactored from ProcessCommands. in configuration.cs
        {
            var pcrList = new List<ProcessCommandResults>();
            foreach (string command in commands)
            {
                if (String.IsNullOrEmpty(command)) continue;
                ProcessCommandResults pcr = ProcessCommand(command);

                System.Diagnostics.Debug.Print("configcommand:" + command);
                System.Diagnostics.Debug.Print("  command:" + pcr.Command);
                System.Diagnostics.Debug.Print("  arguments:" + pcr.Arguments);
                pcrList.Add(pcr);
            }
            return (pcrList);
        }

  
        /// <summary>
        /// Checks Module list in config for formatting. Processes wildcards and friendly names to guid
        /// </summary>
        /// <param name="modules">The modules.</param>
        /// <returns>bool false on failure/exception</returns>
        /// <remarks></remarks>
        public bool ProcessModules(string modules) // in configuration.cs
        {
            RegistryModuleList = EnumModulesFromReg();
            ModuleList = new Dictionary<string, string>();

            try
            {
                foreach (string omodule in modules.Split(';'))
                {
                    bool isGuid = false;

                    string module = omodule.Trim();
                    isGuid = IsGuid(module);

                    if (isGuid)
                    {
                        if (!ModuleList.Keys.Contains(module))
                        {
                            ModuleList.Add(module, module);
                        }
                    }

                        //else if(!isGuid && !isModule && IsFile(module))
                    else if (IsFile(module))
                    {
                        foreach (var kvp in ReadControlFile(module))
                        {
                            ModuleList.Add(kvp.Key, kvp.Value);
                        }
                    }
                    else
                    {
                        //convert friendly name to guid
                       
                        IEnumerable<KeyValuePair<string, string>> results = (from result in RegistryModuleList
                                                                             where
                                                                                 Regex.Match(result.Value, module,
                                                                                             RegexOptions.Singleline |
                                                                                             RegexOptions.IgnoreCase).
                                                                                 Success
                                                                             select result); 
                        foreach (var kvp in results)
                        {
                            if (!ModuleList.Keys.Contains(kvp.Key))
                            {
                                //isModule = true;
                                ModuleList.Add(kvp.Key, kvp.Value);
                                // ModuleList.Add(kvp.Value, kvp.Key);
                            }
                        }
                    }
                }
                if (ModuleList.Count == 0)
                {
                    CDFMonitor.WriteOutputHandler("ProcessModules error: no modules added.");
                    return (false);
                }
                return (true);
            }
            catch (Exception e)
            {
                CDFMonitor.WriteOutputHandler("ProcessModules: Exception" + e);
                return (false);
            }
        }

        /// <summary>
        /// Checks Module list in config for formatting. Processes wildcards and friendly names to guid
        /// </summary>
        /// <param name="modules">The modules.</param>
        /// <returns>bool false on failure/exception</returns>
        /// <remarks></remarks>
        public bool ProcessRemoteMachines(string machines) // copied to configuation.cs
        {
            
            RemoteMachineList = new Dictionary<string, RemoteOperations.RemoteStatus>();

            try
            {
                foreach (string machine in machines.Split(';'))
                {
                    if (string.IsNullOrEmpty(machine)) continue;

                    if (IsFile(machine))
                    {
                        foreach (var line in File.ReadAllText(machine).Split(new char[] { '\r', '\n' },StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (!RemoteMachineList.ContainsKey(line))
                            {
                                RemoteMachineList.Add(line, RemoteOperations.RemoteStatus.Unknown);
                            }
                        }
                    }
                    else
                    {
                        if (!RemoteMachineList.ContainsKey(machine))
                        {
                            RemoteMachineList.Add(machine, RemoteOperations.RemoteStatus.Unknown);
                        }
                    }
                }
                if (RemoteMachineList.Count == 0)
                {
                   // CDFMonitor.WriteOutputHandler("ProcessRemoteMachines error: no modules added.");
                    return (false);
                }
                return (true);
            }
            catch (Exception e)
            {
                CDFMonitor.WriteOutputHandler("ProcessRemoteMachines: Exception" + e);
                return (false);
            }
        }

        /// <summary>
        /// Reads .ctl file for module (guid) list.
        /// Returns Dictionary<string, string> list of modules and guids if successful.
        /// </summary>
        /// <param name="file">path and file name of file to read</param>
        /// <returns></returns>
        public Dictionary<string, string> ReadControlFile(string file) //in configuration.cs
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
                    string guid = String.Empty;
                    string name = String.Empty;
                    string line = stream.ReadLine();
                    if (String.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    MatchCollection mc = Regex.Matches(line, @"(?<guid>.*?)\s+(?<name>\w+)");
                        //this is how ctl files are generated %guid% %name%
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
                        // may just be a guid so try anyway
                        guid = IsGuid(line.Trim()) ? line.Trim() : String.Empty;
                        name = String.Empty;
                    }

                    if (!String.IsNullOrEmpty(guid))
                    {
                        if (!moduleList.Keys.Contains(guid))
                        {
                            moduleList.Add(guid, name);
                        }
                    }
                }
            }

            return moduleList;
        }

     
        public bool Validate() // in 1.166 processconfig. copying to configuration.cs
        {
            if (!ProcessRegex())
            {
                Status += "Invalid REGEX string\n";
                return false;
            }
            if (DetermineActivityType() == ActivityType.Unknown)
            {
                Status += "Unknown Activity Type\n";
                return false;
            }

            SetBypassRegex();
            SetSendSmtp();
            SetSendUrl();
            return true;
        }

        private static bool IsFile(string file)  //in configuration.cs
        {

            try
            {
                if (File.Exists(file))
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

        private static bool IsGuid(string guidString) //in configuration.cs
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

        private void InitializeConfig() //in configurationproperties.cs
        {
            #region configfileproperty initialization

            AllowSingleInstance = true;
            BypassRegex = false;
            ConfigFile = Process.GetCurrentProcess().MainModule.ModuleName + ".config";
            Debug = false;
            Editor = false;
            EventCommand = String.Empty;
            EventCommandWait = false;
            EventMaxCount = 0;
            EventThrottle = 0;
            Hidden = false;
            LogFileAutoFlush = true;
            LogFileMaxCount = 0;
            LogFileName = String.Empty;
            LogFileOverWrite = false;
            LogFileMaxSize = 0;
            LogMatchDetail = false;
            LogMatchOnly = false;
            LogToConsole = true;
            LogToEtl = false;
            Modules = String.Empty;
            RegexPattern = String.Empty;
            RegexVariables = new Dictionary<string, string>();
            SmtpPassword = String.Empty;
            SmtpPort = 25;
            SmtpSendFrom = String.Empty;
            SmtpSendTo = String.Empty;
            SmtpServer = String.Empty;
            SmtpSsl = false;
            SmtpSubject = String.Empty;
            SmtpUser = String.Empty;
            StartupCommand = String.Empty;
            TmfCacheDir = String.Empty;
            TmfServers = "http://ctxsym.citrix.com/tmfs/xaxd/";
            TraceFile = String.Empty;
            UrlFiles = String.Empty;
            UrlPassword = String.Empty;
            UrlSite = String.Empty;
            UrlUser = String.Empty;
            WriteEvent = false;
            ModuleListViewCollection = new ObservableCollection<ModuleListViewItem>();
            RemoteMachineListViewCollection = new ObservableCollection<RemoteMachinesListViewItem>();

            #endregion

            #region properties

            Activity = ActivityType.Unknown;
            // EventCommands = new List<ProcessCommandResults>();
            Initialized = false;
            ModuleList = new Dictionary<string, string>();
            RegistryModuleList = new Dictionary<string, string>();
            ProcessingArguments = false;
            // StartupCommands = new List<ProcessCommandResults>();
            ResourceCredentials = new ResourceManagement();
            SendSmtp = false;
            SendUrl = false;
            Status = String.Empty;

            #endregion
        }

        public ObservableCollection<RemoteMachinesListViewItem> RemoteMachineListViewCollection  {get;set; } //copied to configuration.cs

        public ObservableCollection<ModuleListViewItem> ModuleListViewCollection { get; set; } //copied to configuration.cs


        private ProcessCommandResults ProcessCommand(string cmd)//in configuration.cs
        {
            //parses command string to determine process and arguments for command
            var pcr = new ProcessCommandResults();

            try
            {
                string arguments = String.Empty;
                string command = String.Empty;
                string cleanarg = String.Empty;
                string cleancmd = String.Empty;

                //take off named variables if found
                var revar = new Regex("^\\?\\<(\\w*?)\\>command:");
                //only modify if argument is in eventcommand
                if (revar.IsMatch(cmd) && revar.Match(cmd).Groups.Count > 0)
                {
                    pcr.tag = revar.Match(cmd).Groups[1].Value;
                    cmd = revar.Replace(cmd, "");
                }

                if (cmd.Length < 1) return (pcr);

                //if file found set command to file and empty argument
                if (File.Exists(cmd))
                {
                    pcr.Command = cmd;
                    pcr.Arguments = String.Empty;
                    return (pcr);
                }
                //else walk path
                foreach (string path in (Environment.GetEnvironmentVariable("PATH").Split(new[] {';'})))
                {
                    if (File.Exists(String.Format("{0}\\{1}", path, cmd)))
                    {
                        pcr.Command = String.Format("{0}\\{1}", path, cmd);
                        pcr.Arguments = String.Empty;
                        return (pcr);
                    }
                }

                var args = new List<string>();
                args.AddRange(cmd.Split(new[] {' '}));

                string combinedArgs = String.Empty;
                foreach (string arg in args)
                {
                    combinedArgs += arg + " ";
                    if (File.Exists(combinedArgs))
                    {
                        pcr.Command = combinedArgs;
                        args.RemoveRange(0, args.IndexOf(arg) + 1);
                        pcr.Arguments = String.Join(" ", args.ToArray()); //string.Empty;
                        return (pcr);
                    }
                }

                //if nothing found then default to first arg
                if (String.IsNullOrEmpty(pcr.Command) && args.Count > 1)
                {
                    pcr.Command = args[0];
                    args.Remove(args[0]);
                    pcr.Arguments = String.Join(" ", args.ToArray());
                    return (pcr);
                }
                    //give up
                else if (String.IsNullOrEmpty(pcr.Command))
                {
                    pcr.Command = cmd;
                    pcr.Arguments = String.Empty;
                }
                return (pcr);
            }
            catch (Exception e)
            {
                CDFMonitor.WriteOutputHandler("Error processing command lines:" + e);
                return (pcr);
            }
        }


        private bool ProcessRegex() //copied to configuration.cs
        {
            try
            {
                //make sure regexpattern provided is valid
                var re = new Regex(RegexPattern);

                var revar = new Regex("\\?\\<(?<argName>\\w*?)\\>");
                foreach (Match m in revar.Matches(RegexPattern))
                {
                    RegexVariables.Add(m.Groups["argName"].Value, "");
                }

                return (true);
            }
            catch (Exception e)
            {
                CDFMonitor.WriteOutputHandler("Error processing regex:" + e);
                return (false);
            }
        }

        private void SetBypassRegex()//in configuration.cs
        {
            if (String.IsNullOrEmpty(RegexPattern)
                | RegexPattern == ".*" | RegexPattern == ".")
            {
                BypassRegex = true;
            }
            else
            {
                BypassRegex = false;
            }
        }

        private void SetSendSmtp()//in configuration.cs
        {
            if (SmtpServer.Length > 0
                && SmtpPort > 0
                && SmtpSendTo.Length > 0
                && !BypassRegex
                && !LogToEtl
                && !(TraceFile.Length > 0))
            {
                SendSmtp = true;
            }
            else
            {
                SendSmtp = false;
            }
        }

        private void SetSendUrl()//in configuration.cs
        {
            if (UrlSite.Length > 0
                && UrlUser.Length > 0
                && !BypassRegex
                && !LogToEtl
                && !(TraceFile.Length > 0))
            {
                SendUrl = true;
            }
            else
            {
                SendUrl = false;
            }
        }

        #endregion Methods

        /// <summary>
        /// 
        /// </summary>
        /// <remarks></remarks>
        public class ModuleListViewItem : INotifyPropertyChanged // copied to configuration.cs
        {
            #region Fields

            /// <summary>
            /// 
            /// </summary>
            private bool? _checked;
            /// <summary>
            /// 
            /// </summary>
            private string _moduleGuid = String.Empty;
            /// <summary>
            /// 
            /// </summary>
            private string _moduleName = String.Empty;

            #endregion Fields

            #region Events

            /// <summary>
            /// Occurs when delegate property value changes.
            /// </summary>
            /// <remarks></remarks>
            public event PropertyChangedEventHandler PropertyChanged;

            #endregion Events

            #region Properties

            /// <summary>
            /// Gets or sets the checked.
            /// </summary>
            /// <value>The checked.</value>
            /// <remarks></remarks>
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
            /// <remarks></remarks>
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
            /// <remarks></remarks>
            public string ModuleName
            {
                get { return (_moduleName); }
                set
                {
                    _moduleName = value;
                    RaisePropertyChanged("RemoteStatus");
                }
            }

            #endregion Properties

            #region Methods

            /// <summary>
            /// Raises the property changed.
            /// </summary>
            /// <param name="property">The property.</param>
            /// <remarks></remarks>
            private void RaisePropertyChanged(string property)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(property));
                }
            }

            #endregion Methods
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks></remarks>
        public class RemoteMachinesListViewItem : INotifyPropertyChanged // copied to configuration.cs
        {
            #region Fields

            /// <summary>
            /// 
            /// </summary>
            private string _machineName = String.Empty;
            /// <summary>
            /// 
            /// </summary>
            private string _remoteStatus = String.Empty;

            #endregion Fields

            #region Events

            /// <summary>
            /// Occurs when delegate property value changes.
            /// </summary>
            /// <remarks></remarks>
            public event PropertyChangedEventHandler PropertyChanged;

            #endregion Events

            #region Properties

            /// <summary>
            /// Gets or sets the module GUID.
            /// </summary>
            /// <value>The module GUID.</value>
            /// <remarks></remarks>
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
            /// <remarks></remarks>
            public string RemoteStatus
            {
                get { return (_remoteStatus); }
                set
                {
                    _remoteStatus = value;
                    RaisePropertyChanged("RemoteStatus");
                }
            }

            #endregion Properties

            #region Methods

            /// <summary>
            /// Raises the property changed.
            /// </summary>
            /// <param name="property">The property.</param>
            /// <remarks></remarks>
            private void RaisePropertyChanged(string property)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(property));
                }
            }

            #endregion Methods
        }

        #region Nested Types

        public class ProcessCommandResults // in configuration.cs
        {
            #region Fields

            public string Arguments = String.Empty;
            public string Command = String.Empty;
            public string tag = String.Empty;

            #endregion Fields
        }

        #endregion Nested Types
        
    }
}