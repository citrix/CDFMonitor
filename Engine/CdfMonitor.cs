// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="CdfMonitor.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************

namespace CDFM.Engine
{
    using CDFM.Config;
    using CDFM.FileManagement;
    using CDFM.Gui;
    using CDFM.Network;
    using CDFM.Properties;
    using CDFM.Service;
    using CDFM.Trace;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Mail;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Principal;
    using System.ServiceProcess;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Timers;
    using System.Windows;
    using System.Xml;
    using Configuration = CDFM.Config.Configuration;

    /// <summary>
    /// Enum ConfigureEventLogEventListenerResult
    /// </summary>
    internal enum ConfigureEventLogEventListenerResult
    {
        EnableTracing,
        EventTracing,
        Exception
    }

    /// <summary>
    /// Struct TraceEventThreadInfo
    /// </summary>
    public struct TraceEventThreadInfo
    {
        #region Public Properties

        /// <summary>
        /// The event trace
        /// </summary>
        /// <value>The event trace.</value>
        public NativeMethods.EventTrace EventTrace
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the formatted event string.
        /// </summary>
        /// <value>The formatted event string.</value>
        public string FormattedEventString
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the formatted trace string.
        /// </summary>
        /// <value>The formatted trace string.</value>
        public string FormattedTraceString
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether [test mode].
        /// </summary>
        /// <value><c>true</c> if [test mode]; otherwise, /c>.</value>
        public bool TestMode
        {
            get;
            set;
        }

        #endregion Public Properties
    }

    /// <summary>
    /// Class CDFMonitor
    /// </summary>
    public class CDFMonitor
    {
        #region Public Fields

        public static ManualResetEvent CloseCurrentSessionEvent;
        public static WriteOutputDelegate LogOutputHandler;
        public Configuration Config;
        public Thread GuiThread;
        public ThreadQueue<EventReadEventArgs> RegexParserThread;
        public Udp Udp;

        #endregion Public Fields

        #region Private Fields

        private EtwTraceConsumer _consumer;
        private Int64 _consumerEventReadCount;
        private EtwTraceConsumer _consumerKernel;
        private EtwTraceController _controller;
        private EtwTraceController _controllerKernel;
        private ManualResetEvent _eventListenerMatch = new ManualResetEvent(false);
        private DateTime _lasteventtime = DateTime.MinValue;
        private DateTime _lastGetStatTime;
        private TimeSpan _lastProcessorTime;
        private Queue<string> _logMatchOnlyQueue = new Queue<string>(1000);
        private Int64 _missedLoggerEvents;
        private string _path = string.Empty;
        private ProcessListMonitor _processMonitor = ProcessListMonitor.Instance;
        private EventLog _startEventLog = new EventLog();
        private EventLog _stopEventLog = new EventLog();
        private Int64 _throttledEvents;
        private int _timeZoneBias = -1;
        private Int64 _tmfParseErrors;
        private TMF _tmfParser;

        #endregion Private Fields

        #region Private Constructors

        /// <summary>
        /// Prevents a default instance of the <see cref="CDFMonitor" /> class from being created.
        /// </summary>
        private CDFMonitor()
        {
        }

        #endregion Private Constructors

        #region Public Delegates

        /// <summary>
        /// Delegate WriteOutputDelegate
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="jobOutputType">Type of the job output.</param>
        public delegate void WriteOutputDelegate(string input, JobOutputType jobOutputType = JobOutputType.Log);

        #endregion Public Delegates

        #region Private Delegates

        /// <summary>
        /// Delegate EventHandler
        /// </summary>
        /// <param name="sig">The sig.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private delegate bool EventHandler(CtrlType sig);

        #endregion Private Delegates

        #region Private Events

        /// <summary>
        /// Occurs when [_handler].
        /// </summary>
        private event EventHandler _handler;

        #endregion Private Events

        #region Public Enums

        /// <summary>
        /// Enum ConsoleModes
        /// </summary>
        public enum ConsoleModes
        {
            ENABLE_PROCESSED_INPUT = 0x1,
            ENABLE_LINE_INPUT = 0x2,
            ENABLE_ECHO_INPUT = 0x4,
            ENABLE_WINDOW_INPUT = 0x8,
            ENABLE_MOUSE_INPUT = 0x10,
            ENABLE_INSERT_MODE = 0x20,
            ENABLE_QUICK_EDIT_MODE = 0x40,
            ENABLE_EXTENDED_FLAGS = 0x80,
            ENABLE_AUTO_POSITION = 0x100,
            ENABLE_PROCESSED_OUTPUT = 0x1,
            ENABLE_WRAP_AT_EOL_OUTPUT = 0x2
        }

        /// <summary>
        /// Enum CtrlType
        /// </summary>
        public enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        #endregion Public Enums

        #region Public Properties

        /// <summary>
        /// Gets the args.
        /// </summary>
        /// <value>The args.</value>
        public static string[] Args
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the instance.
        /// </summary>
        /// <value>The instance.</value>
        public static CDFMonitor Instance
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the session.
        /// </summary>
        /// <value>The name of the session.</value>
        public ThreadQueue<EntryWrittenEventArgs> _eventListenerThread
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the console writer thread.
        /// </summary>
        /// <value>The console writer thread.</value>
        public EtwTraceConsumer Consumer
        {
            get { return _consumer; }
            set { _consumer = value; }
        }

        /// <summary>
        /// Gets or sets the controller.
        /// </summary>
        /// <value>The controller.</value>
        public EtwTraceController Controller
        {
            get { return _controller; }
            set { _controller = value; }
        }

        /// <summary>
        /// Gets or sets the GUI.
        /// </summary>
        /// <value>The GUI.</value>
        public CDFMonitorGui Gui
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the marker events.
        /// </summary>
        /// <value>The marker events.</value>
        public int MarkerEvents
        {
            get;
            set;
        }

        /// <summary>
        /// The _matched events
        /// </summary>
        /// <value>The matched events.</value>
        public Int64 MatchedEvents
        {
            get;
            set;
        }

        /// <summary>
        /// The _missed events
        /// </summary>
        /// <value>The missed events.</value>
        public Int64 MissedMatchedEvents
        {
            get;
            set;
        }

        /// <summary>
        /// The _processed events
        /// </summary>
        /// <value>The processed events.</value>
        public Int64 ProcessedEvents
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the process running.
        /// </summary>
        /// <value>The process running.</value>
        public int ProcessRunning
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the log manager.
        /// </summary>
        /// <value>The log manager.</value>
        public Regex RegexTracePatternObj
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the server regex parser thread.
        /// </summary>
        /// <value>The server regex parser thread.</value>
        public ThreadQueue<string> ServerRegexParserThread
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the SMTP thread.
        /// </summary>
        /// <value>The SMTP thread.</value>
        public ThreadQueue<string> SmtpThread
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>The start time.</value>
        public DateTime StartTime
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the UDP reader thread.
        /// </summary>
        /// <value>The UDP reader thread.</value>
        public Thread UdpReaderThread
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the URL upload thread.
        /// </summary>
        /// <value>The URL upload thread.</value>
        public ThreadQueue<string> UrlUploadThread
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether [write GUI status].
        /// </summary>
        /// <value><c>true</c> if [write GUI status]; otherwise, /c>.</value>
        public bool WriteGuiStatus { get; set; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Checks for gui instance and worker instance. checks for pending cancellation.
        /// </summary>
        /// <param name="count"></param>
        /// <param name="progress">string progress</param>
        /// <returns>false if error or cancellation pending, otherwise true</returns>
        public static bool ManageGuiWorker(int count, string progress)
        {
            if (CDFMonitorGui.Instance == null)
            {
                return true;
            }

            if (CDFMonitorGui.Instance.CancellationPending())
            {
                CDFMonitor.LogOutputHandler("ManageGuiWorker: pending operation cancelled.");
                return false;
            }
            else
            {
                CDFMonitorGui.Instance.ReportProgress(count, progress);
                return true;
            }
        }

        /// <summary>
        /// Builds the package.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>System.String.</returns>
        public string BuildPackage(string path)
        {
            // zips current Logs based on config
            bool retval = false;
            PackageManager pW;

            List<string> zipList = new List<string>();
            LogOutput("BuildPackage");

            if (string.IsNullOrEmpty(path))
            {
                LogOutput("BuildPackage:gathering files from config.");
                if (Config.LoggerJobUtility.Enabled)
                {
                    Config.LoggerJobUtility.Writer.LogManager.ManageSequentialLogs();
                    zipList.AddRange(Config.LoggerJobUtility.Writer.LogManager.Logs);
                }

                // get for both etl and non etl
                // only if not currently tracing

                if (_consumer == null || (_consumer != null && !_consumer.Running))
                {
                    zipList.AddRange(EnumeratePackageFiles());
                }
                else
                {
                    // pause writer and get list of files to zip
                    Config.LoggerQueue.DisableQueues();
                    zipList.AddRange(EnumeratePackageFiles());
                    Config.LoggerQueue.EnableQueues();
                }

                // if urlfiles exist, get those too.
                foreach (string item in Config.AppSettings.UrlFiles.Split(';'))
                {
                    string[] list = FileManager.GetFiles(item);
                    if (list.Length > 0)
                    {
                        zipList.AddRange(list);
                    }
                }
            }
            else if (File.Exists(path))
            {
                LogOutput("BuildPackage: single file");

                // Dont zip a zip
                if (Path.GetExtension(path).ToLower().Equals(".zip"))
                {
                    return path;
                }
                zipList.Add(path);
            }
            else if (Directory.Exists(path))
            {
                LogOutput("BuildPackage: single directory");
                zipList.AddRange(Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly));
            }

            foreach (string file in zipList)
            {
                LogOutput("BuildPackage:" + file + " added to upload list");
            }

            if (!string.IsNullOrEmpty(Config.LoggerJobUtilityPath()))
            {
                Config.LoggerJobUtility.Writer.LogManager.DisableLogStream();
            }

            if (!string.IsNullOrEmpty(Config.LoggerJobTracePath())
                && (_consumer != null && _consumer.Running))
            {
                Config.LoggerJobTrace.Writer.LogManager.DisableLogStream();
            }

            pW = new PackageManager();
            retval = pW.CreatePackage(zipList.ToArray(), AppDomain.CurrentDomain.BaseDirectory);

            if (!string.IsNullOrEmpty(Config.LoggerJobUtilityPath()))
            {
                Config.LoggerJobUtility.Writer.LogManager.EnableLogStream();
            }

            if (!string.IsNullOrEmpty(Config.LoggerJobTracePath())
                && (_consumer != null && _consumer.Running))
            {
                Config.LoggerJobTrace.Writer.LogManager.EnableLogStream();
            }

            if (retval)
            {
                LogOutput("BuildPackage: built zip:" + pW.ZipFile);
                // delete packaged files on success
                FileManager.DeleteFiles(zipList.ToArray(), false);
                return (pW.ZipFile);
            }
            else
            {
                LogOutput("BuildPackage: Warning:zip: possible error in building zip");
                return (string.Empty);
            }
        }

        /// <summary>
        /// Etws the trace clean.
        /// </summary>
        /// <param name="all">if set to <c>true</c> [force].</param>
        public void EtwTraceClean(bool all = false)
        {
            LogOutput(string.Format("DEBUG:ETWTraceClean:enter:{0}", all));
            EtwTraceController controller = new EtwTraceController(Config.SessionName, Config.SessionGuid);
            string debugstr = "DEBUG:";
            if (all)
            {
                debugstr = "";
            }
            if (controller.QueryTrace(Config.SessionName))
            {
                LogOutput(string.Format("{0}EtwTraceClean: session exists.", debugstr));
            }
            else
            {
                LogOutput(string.Format("{0}EtwTraceClean: session does not exist.", debugstr));
                if (!all) return;
            }

            if (controller.QueryTrace(Properties.Resources.KernelSessionName))
            {
                LogOutput(string.Format("{0}EtwTraceClean: kernel session exists.", debugstr));
                controller.CleanTrace(Properties.Resources.KernelSessionName);
            }

            LogOutput(string.Format("{0}ETWTraceClean: calling CleanTrace", debugstr));
            controller.CleanTrace(Config.SessionName);

            if (all)
            {
                Config.SessionName = Resources.SessionName;

                for (int i = 0; i < EtwTraceController.ETW_MAX_SESSIONS; i++)
                {
                    if (controller.QueryTrace(Config.SessionName + i))
                    {
                        controller.CleanTrace(Config.SessionName + i);
                        LogOutput(string.Format("{0}EtwTraceClean: session exists:{1}", debugstr, Config.SessionName + i));
                    }
                }
            }
            else
            {
                LogOutput(string.Format("{0}EtwTraceClean: bypassing clean.", debugstr));
            }
        }

        /// <summary>
        /// Etws the trace start.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        /// <exception cref="System.ArgumentException">EtwTraceStart: +
        /// Config.Activity.ToString()</exception>
        public bool EtwTraceStart()
        {
            try
            {
                // reset timezone config info
                _timeZoneBias = -1;

                // start trace determine if logfile an .etl
                switch (Config.Activity)
                {
                    case Configuration.ActivityType.TraceToEtl:
                        if (Config.ConfigureTracing(true) & EtwInitialize())
                        {
                            EtwTraceToEtl();
                        }
                        else
                        {
                            return false;
                        }

                        break;

                    case Configuration.ActivityType.TraceToCsv:
                        if (Config.ConfigureTracing(true) & EtwInitialize())
                        {
                            EtwTraceToCsv();
                        }
                        else
                        {
                            return false;
                        }

                        break;

                    // case Configuration.ActivityType.ParseToCsv:
                    case Configuration.ActivityType.RegexParseToCsv:

                        string[] files;
                        int count = 0;
                        string traceFileInput = Config.AppSettings.TraceFileInput;

                        // 130811 to support *.zip extraction of CDFMonitor .zip files because of
                        // content xml file requirement
                        if (traceFileInput.EndsWith(".zip"))
                        {
                            PackageManager pM = new PackageManager();
                            files = FileManager.GetFiles(Config.AppSettings.TraceFileInput, null, SearchOption.AllDirectories);
                            count = 0;

                            // extract zips before looking for .etl files
                            foreach (string file in files)
                            {
                                if (!ManageGuiWorker(count,
                                    string.Format("EtwTraceStart:parsing zip {0} of {1}: {2}", ++count, files.Length, FileManager.GetFullPath(file))))
                                {
                                    return false;
                                }

                                LogOutput(string.Format("EtwTraceStart:parsing zip {0} of {1}", count, files.Length));
                                pM.ExtractPackage(file, Path.GetDirectoryName(FileManager.GetFullPath(file)));
                            }

                            // remove zip extension as the zip name will be the default extraction
                            // dir
                            traceFileInput = traceFileInput.ToLower().Replace("*", "").Replace(".zip", "\\*.etl");
                        }

                        files = FileManager.GetFiles(traceFileInput, null, SearchOption.AllDirectories);
                        count = 0;

                        foreach (string file in files)
                        {
                            if (!ManageGuiWorker(count,
                                string.Format("EtwTraceStart:parsing file {0} of {1}: {2}", ++count, files.Length, FileManager.GetFullPath(file))))
                            {
                                return false;
                            }

                            LogOutput(string.Format("EtwTraceStart:parsing file {0} of {1}", count, files.Length));
                            string traceFileOutput = Config.AppSettings.TraceFileOutput;
                            LogOutput("EtwTraceStart:tracing file:" + file);

                            if (!Config.ConfigureTracing(false))
                            {
                                return false;
                            }

                            if (Config.AppSettings.UseTraceSourceForDestination)
                            {
                                traceFileOutput = file + ".csv";
                            }

                            if (!Config.ConfigureTracing(true, traceFileOutput))
                            {
                                return false;
                            }

                            if (EtwInitialize())
                            {
                                // todo: fix this override
                                WriteGuiStatus = true;
                                LogOutput("Output File:" + Config.LoggerJobTracePath());
                                WriteGuiStatus = false;

                                EtwParseToCsv(file);
                            }

                            if (CloseCurrentSessionEvent.WaitOne(100))
                            {
                                break;
                            }
                        }

                        break;

                    default:
                        throw new ArgumentException("EtwTraceStart:" + Config.Activity.ToString());
                }

                return true;
            }
            catch (Exception e)
            {
                // write error message to console
                LogOutput("EtwTraceStart:exception:" + e.ToString());
                EtwTraceStop();
                CloseCurrentSessionEvent.Set();
                return false;
            }
        }

        /// <summary>
        /// Etws the trace stop.
        /// </summary>
        public void EtwTraceStop()
        {
            DisplayProcessList();

            Config.DisplayConfigSettings();

            if (Udp.PingEnabled)
            {
                Udp.SendPing(null, null);
            }

            LogOutput(GetStats());
            LogOutput("CDFMonitor Stopping.");
            if (_consumer != null)
            {
                LogOutput("DEBUG:EtwTraceStop: enter");
                _consumer.EventRead -= Consumer_EventRead;
                _consumer.CloseTrace();
                LogOutput("DEBUG:EtwTraceStop: consumer closed");
            }

            if (_controller != null)
            {
                // Stop with session name because of multiple sessions
                _controller.StopTrace(Config.SessionName);
                LogOutput("DEBUG:EtwTraceStop: controller closed");
                EtwTraceClean();
            }

            if (_consumerKernel != null)
            {
                LogOutput("DEBUG:EtwTraceStop:kernel enter");
                _consumerKernel.EventRead -= Consumer_EventRead;
                _consumerKernel.CloseTrace();
                LogOutput("DEBUG:EtwTraceStop:kernel consumer closed");
            }

            if (_controllerKernel != null)
            {
                // Stop with session name because of multiple sessions
                _controllerKernel.StopTrace(Properties.Resources.KernelSessionName);
                LogOutput("DEBUG:EtwTraceStop:kernel controller closed");
                EtwTraceClean();
            }

            // Clean out queues to be responsive to closedown
            Thread.Sleep(100);

            if (RegexParserThread != null)
            {
                RegexParserThread.ClearQueue();
            }

            Config.ConfigureTracing(false);
        }

        /// <summary>
        /// Events the log event listener proc.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EntryWrittenEventArgs" /> instance containing the event
        /// data.</param>
        public void EventLogEventListenerProc(object sender, EntryWrittenEventArgs e)
        {
            LogOutput("DEBUG:Windows Event received:" + e.Entry.Message);
            _eventListenerThread.Queue(e);
        }

        /// <summary>
        /// Gets the stats.
        /// </summary>
        /// <returns>System.String.</returns>
        public string GetStats()
        {
            StringBuilder sb = new StringBuilder();

            if (Config.RemoteMachineList != null && Config.RemoteMachineList.Count > 0)
            {
                sb.AppendLine("\n---------------------------------------------------------");
                sb.AppendLine("RemoteOperations List:");
                foreach (KeyValuePair<string, RemoteOperations.RemoteStatus> kvp in Config.RemoteMachineList)
                {
                    sb.AppendLine(kvp.Key + ":" + kvp.Value);
                }
            }

            if (Config.Activity != Configuration.ActivityType.TraceToEtl && !String.IsNullOrEmpty(Config.AppSettings.TraceFileOutput) && _consumer != null)
            {
                sb.AppendLine("---------------------------------------------------------");
                sb.AppendLine(string.Format("Traced OS processor count: {0}",
                                            _consumer.BufferCallback.LogfileHeader.NumberOfProcessors));
                sb.AppendLine(string.Format("Traced OS log file mode: {0}",
                                            _consumer.BufferCallback.LogfileHeader.LogFileMode));
                sb.AppendLine(string.Format("Traced OS max trace size: {0}",
                                            _consumer.BufferCallback.LogfileHeader.MaximumFileSize));
                sb.AppendLine(string.Format("Traced OS time zone bias: {0}",
                                            _consumer.BufferCallback.LogfileHeader.TimeZone.Bias));
                sb.AppendLine(string.Format("Traced OS last boot: {0}",
                                            DateTime.FromFileTime(_consumer.BufferCallback.LogfileHeader.BootTime)));
                sb.AppendLine(string.Format("Traced OS type: {0}",
                                            _consumer.BufferCallback.LogfileHeader.BufferUnion.PointerSize == 8
                                                ? "x64"
                                                : "x86"));
                sb.AppendLine(string.Format("Traced OS version: {0}",
                                            _consumer.BufferCallback.LogfileHeader.VersionDetailUnion.
                                                VersionDetail_MajorVersion
                                            + "." +
                                            _consumer.BufferCallback.LogfileHeader.VersionDetailUnion.
                                                VersionDetail_MinorVersion
                                            + "." + _consumer.BufferCallback.LogfileHeader.ProviderVersion));
            }

            if (_tmfParser != null && _consumer != null)
            {
                sb.AppendLine("---------------------------------------------------------");
                sb.AppendLine(string.Format("{0} TMF server missed events", _tmfParser.TMFServerMissedCount));
                sb.AppendLine(string.Format("{0} TMF server alternate hit events", _tmfParser.TMFServerAlternateHitCount));
                sb.AppendLine(string.Format("{0} TMF server hit events", _tmfParser.TMFServerHitCount));
                sb.AppendLine(string.Format("{0} TMF cache missed events", _tmfParser.TMFCacheMissedCount));
                sb.AppendLine(string.Format("{0} TMF cache hit events", _tmfParser.TMFCacheHitCount));
                sb.AppendLine(string.Format("{0} TMF parse errors", _tmfParseErrors));
                sb.AppendLine("---------------------------------------------------------");
                sb.AppendLine(string.Format("{0} error|warning|fail|exception events", Config.LoggerJobConsole.Writer.ErrorWarningFailEvents));
                sb.AppendLine(string.Format("{0} processed events", ProcessedEvents));
                sb.AppendLine(string.Format("{0} matched events", MatchedEvents));
                sb.AppendLine(string.Format("{0} consumer read events", _consumerEventReadCount));
                sb.AppendLine(string.Format("{0} throttled events", _throttledEvents));
                sb.AppendLine(string.Format("{0} missed CDFMonitor match events", MissedMatchedEvents));
                sb.AppendLine(string.Format("{0} missed logger events", _missedLoggerEvents));
                sb.AppendLine(string.Format("logger jobs:"));
                foreach (WriterJob lj in Config.LoggerQueue.WriterJobs)
                {
                    sb.AppendLine(string.Format("    {0}", lj.JobName));
                    sb.AppendLine(string.Format("        {0} missed events", lj.Writer.MissedQueueEvents));
                    sb.AppendLine(string.Format("        {0} total events", lj.Writer.QueuedEvents + lj.Writer.MissedQueueEvents));
                    sb.AppendLine(string.Format("        {0} currently queued events", lj.Writer.Queue.QueueLength()));

                    if (Config.AppSettings.Debug)
                    {
                        sb.AppendLine(string.Format("        {0} missed max queue events", lj.Writer.Queue.MaxQueueMissedEvents));
                        sb.AppendLine(string.Format("        {0} queued counter", lj.Writer.Queue.QueuedCounter));
                        sb.AppendLine(string.Format("        {0} processed counter", lj.Writer.Queue.ProcessedCounter));
                        sb.AppendLine(string.Format("        {0} missed writer counter", lj.Writer.MissedWriterEvents));
                    }
                }

                sb.AppendLine(string.Format("{0} missed controller events", Config.MissedControllerEvents));
                sb.AppendLine(string.Format("{0} markers", MarkerEvents));
                sb.AppendLine(string.Format("{0} parser queue length", RegexParserThread.QueueLength()));
                sb.AppendLine(string.Format("{0} writer queue errors", Config.LoggerQueue.WriterQueueErrors));
                sb.AppendLine(string.Format("{0} parser max queue missed events", ThreadQueue<long>.ThreadQueues.Sum(p => p.MaxQueueMissedEvents)));
                sb.AppendLine(string.Format("{0} buffers read", _consumer.BufferCallback.BuffersRead));
                sb.AppendLine(string.Format("{0} buffers total", _consumer.BufferCallback.LogfileHeader.BuffersWritten));

                // returns 12:00 midnight, January 1, 1601 A.D. (C.E.) Coordinated Universal Time
                // (UTC) if empty so dont display.
                DateTime emptyDate = new DateTime(1601, 1, 1, 0, 0, 0);
                if (DateTime.Compare(emptyDate, DateTime.FromFileTime(_consumer.BufferCallback.LogfileHeader.StartTime).ToUniversalTime()) != 0)
                {
                    sb.AppendLine(string.Format("{0} trace start time",
                                                          DateTime.FromFileTime(
                                                              _consumer.BufferCallback.LogfileHeader.StartTime)));
                    sb.AppendLine(string.Format("{0} trace stop time",
                                                          DateTime.FromFileTime(
                                                              _consumer.BufferCallback.LogfileHeader.EndTime)));
                }
            }

            TimeSpan duration = DateTime.Now.Subtract(StartTime);
            TimeSpan processorTime = Process.GetCurrentProcess().TotalProcessorTime;
            TimeSpan currentProcessorTimeSpan = processorTime - _lastProcessorTime;
            TimeSpan currentDuration = DateTime.Now - _lastGetStatTime;

            sb.AppendLine("---------------------------------------------------------");
            sb.AppendLine(string.Format("{0} CDFMonitor activity start time", StartTime));
            sb.AppendLine(string.Format("{0} CDFMonitor activity stop time", DateTime.Now));
            sb.AppendLine(string.Format("{0} CDFMonitor duration", duration));
            sb.AppendLine(string.Format("{0} CDFMonitor processor time",
                                                  new TimeSpan((processorTime.Ticks / Environment.ProcessorCount))));
            sb.AppendLine(string.Format("{0} CDFMonitor average processor utilization %",
                                                  (((processorTime.TotalMilliseconds / Environment.ProcessorCount)
                                                    / duration.TotalMilliseconds) * 100).ToString("F")));
            sb.AppendLine(string.Format("{0} CDFMonitor current processor utilization %",
                                                  (((currentProcessorTimeSpan.TotalMilliseconds /
                                                     Environment.ProcessorCount)
                                                    / currentDuration.TotalMilliseconds) * 100).ToString("F")));
            sb.AppendLine(string.Format("{0} CDFMonitor traces per second",
                                                  (ProcessedEvents / duration.TotalSeconds).ToString("F")));

            sb.AppendLine("---------------------------------------------------------");

            _lastProcessorTime = processorTime;
            _lastGetStatTime = DateTime.Now;

            return (sb.ToString());
        }

        /// <summary>
        /// Writes the output.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="outputTypes">The output types.</param>
        public void LogOutput(string data, JobOutputType outputTypes = JobOutputType.Log)//Unknown)
        {
            if (!Config.AppSettings.Debug && data.StartsWith("DEBUG:"))
            {
                Debug.Print("WriteOutputDated:" + data);
                return;
            }

            Config.LoggerQueue.QueueOutput(string.Format("{0}:{1}", DateTime.Now, data), outputTypes);

            // todo: move to writer
            if (WriteGuiStatus && CDFMonitorGui.WriteStatusHandler != null)
            {
                CDFMonitorGui.WriteStatusHandler(data, false, null);
            }
        }

        /// <summary>
        /// Processes the event thread proc.
        /// </summary>
        /// <param name="traceEvent">The trace event.</param>
        public void ProcessEventThreadProc(Object traceEvent)
        {
            try
            {
                // only run command max number of times
                if (!CheckEventMaxCount())
                {
                    return;
                }

                // increment command count counter for max count comparison
                MatchedEvents++;

                // proceses event string by comparing to regular expression provided in app config
                // file read in thread specific variable instance
                TraceEventThreadInfo ti = (TraceEventThreadInfo)traceEvent;

                // check for throttle value
                if (Config.AppSettings.EventThrottle > 0)
                {
                    LogOutput("DEBUG: Checking throttle");

                    // only run command once until value in seconds is reached
                    if (DateTime.Now.Subtract(_lasteventtime).TotalSeconds > Config.AppSettings.EventThrottle)
                    {
                        _lasteventtime = DateTime.Now;
                    }
                    else
                    {
                        LogOutput("bypassing event due to throttle!");
                        _throttledEvents++;
                        return;
                    }
                }

                // write to event log if specified in app config
                if (Config.AppSettings.WriteEvent)
                {
                    LogOutput("DEBUG:Writing event to event log");
                    EventLog.WriteEntry(Process.GetCurrentProcess().MainModule.ModuleName, ti.FormattedEventString,
                                        EventLogEntryType.Information, 100);
                }

                // call commands specified in app config
                if (Config.EventCommands.Count > 0)
                {
                    // search all eventcommands for matching argument variables and replace
                    if (Config.RegexVariables.Count > 0)
                    {
                        // get match out of eventstring
                        Match eventStringMatch = RegexTracePatternObj.Match(ti.FormattedEventString);
                        LogOutput("DEBUG:ProcessEventThreadProc: ti.FormattedEventString:" + ti.FormattedEventString);

                        // loop through each eventcommand
                        foreach (Configuration.ProcessCommandResults eventCommandPCR in Config.EventCommands)
                        {
                            string newCommand = eventCommandPCR.Command;
                            string newArguments = eventCommandPCR.Arguments;
                            bool runcmd = false;

                            // loop through all arguments in regexvariables and compare
                            foreach (string revarArg in new List<string>(Config.RegexVariables.Keys))
                            {
                                LogOutput("DEBUG:ProcessEventThreadProc: checking revarArg:" + revarArg);

                                // make sure there is a match in eventString
                                if (eventStringMatch.Groups[revarArg].Success)
                                {
                                    // Add value to RegexVariables for later use
                                    LogOutput("DEBUG:ProcessEventThreadProc: RegexVariables.Value:" +
                                                eventStringMatch.Groups[revarArg].Value);
                                    Monitor.Enter(Config.RegexVariables);
                                    Config.RegexVariables[revarArg] = eventStringMatch.Groups[revarArg].Value;
                                    Monitor.Exit(Config.RegexVariables);
                                }

                                string rePattern = "\\?\\<" + revarArg + "\\>";

                                // only modify if argument is in eventcommand
                                if (Regex.IsMatch(eventCommandPCR.Command, rePattern)
                                    || (String.Compare(eventCommandPCR.tag, revarArg, true) == 0
                                        && eventStringMatch.Groups[revarArg].Success))
                                {
                                    newCommand = Regex.Replace(newCommand, rePattern, Config.RegexVariables[revarArg]);
                                    LogOutput("DEBUG:ProcessEventThreadProc:newCommand:" + newCommand);
                                }

                                // only modify if argument is in eventcommand argument
                                if (Regex.IsMatch(eventCommandPCR.Arguments, rePattern)
                                    || (String.Compare(eventCommandPCR.tag, revarArg, true) == 0
                                        && eventStringMatch.Groups[revarArg].Success))
                                {
                                    newArguments = Regex.Replace(newArguments, rePattern,
                                                                 Config.RegexVariables[revarArg]);
                                    LogOutput("DEBUG:ProcessEventThreadProc:newArguments:" + newArguments);
                                }

                                if (String.Compare(eventCommandPCR.tag, revarArg, true) == 0 &&
                                    eventStringMatch.Groups[revarArg].Success)
                                {
                                    runcmd = true;
                                }
                            }

                            if (newCommand != string.Empty && runcmd)
                            {
                                RunProcess(newCommand, newArguments, Config.AppSettings.EventCommandWait, ti.TestMode);
                            }
                        }
                    }
                    else
                    {
                        // just run all commands
                        foreach (Configuration.ProcessCommandResults eventCommandPCR in Config.EventCommands)
                        {
                            RunProcess(eventCommandPCR.Command, eventCommandPCR.Arguments, Config.AppSettings.EventCommandWait, ti.TestMode);
                        }
                    }
                }

                if (Config.SendSmtp)
                {
                    SmtpThread.Queue(ti.FormattedTraceString);
                }

                // url
                if (Config.SendUrl)
                {
                    UrlUploadThread.Queue(ti.FormattedTraceString);
                }
            }
            catch (Exception e)
            {
                LogOutput("Failure:Exception processing event:" + e.ToString());
            }
        }

        /// <summary>
        /// Sends the SMTP.
        /// </summary>
        /// <param name="smtpData">The SMTP data.</param>
        public void SendSMTP(string smtpData)
        {
            if (!Config.SendSmtp) return;
            LogOutput("sending smtp:" + smtpData);

            try
            {
                // To
                MailMessage mailMsg = new MailMessage();
                mailMsg.To.Add(Config.AppSettings.SmtpSendTo);

                // From
                MailAddress mailAddress = new MailAddress(Config.AppSettings.SmtpSendFrom);
                mailMsg.From = mailAddress;

                // Subject and Body
                mailMsg.Subject = string.Format("{0} {1}", Config.AppSettings.SmtpSubject, Dns.GetHostEntry("localhost").HostName);
                mailMsg.Body = smtpData;

                // Init SmtpClient and send
                SmtpClient smtpClient = new SmtpClient(Config.AppSettings.SmtpServer, Config.AppSettings.SmtpPort);
                smtpClient.Credentials = new NetworkCredential(Config.AppSettings.SmtpUser, Config.AppSettings.SmtpPassword);

                // get password if empty in config put here on purpose for customer to optionally
                // control uploads by populating site, user, but not password
                if (!string.IsNullOrEmpty(Config.AppSettings.SmtpUser)
                    && string.IsNullOrEmpty(Config.AppSettings.SmtpPassword))
                {
                    smtpClient.Credentials = Credentials.PromptForCredentials(Config.AppSettings.SmtpUser, Config.AppSettings.SmtpPassword,
                                                                            Config.AppSettings.SmtpServer);
                }

                smtpClient.EnableSsl = Config.AppSettings.SmtpSsl;
                smtpClient.Send(mailMsg);
            }
            catch (Exception e)
            {
                LogOutput("SendSMTP error:" + e.ToString());
            }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void ServiceStart()
        {
            Start();
        }

        /// <summary>
        /// Starts the configured activity.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool Start()
        {
            try
            {
                ResetStats();

                // clear outputs

                if (!Config.ProcessConfiguration())
                {
                    return false;
                }

                // run action activity startup commands
                foreach (Config.Configuration.ProcessCommandResults startupCommandPCR in Config.StartupCommands)
                {
                    RunProcess(startupCommandPCR.Command, startupCommandPCR.Arguments, Config.AppSettings.StartupCommandWait);
                }

                // 130505 currently only using buffer now for startup of utility. make sure its
                // disabled before starting session.
                Config.LoggerQueue.EnableBuffer(false);

                _logMatchOnlyQueue.Clear();

                // enable process listener if realtime tracing
                if ((Config.Activity == Configuration.ActivityType.TraceToCsv
                    | Config.Activity == Configuration.ActivityType.TraceToEtl)
                    & Config.AppSettings.MonitorProcesses)
                {
                    _processMonitor.Enable();
                }

                // initialize TMF parser
                _tmfParser = new TMF();

                // setup regex with pattern
                RegexTracePatternObj = new Regex(Config.AppSettings.RegexPattern, RegexOptions.IgnoreCase);

                // setup udp if enabled
                if ((Config.Activity == Configuration.ActivityType.Server
                    | Config.Activity == Configuration.ActivityType.TraceToCsv
                    | Config.Activity == Configuration.ActivityType.TraceToEtl)
                    & (Config.AppSettings.UdpClientEnabled | Config.AppSettings.UdpPingEnabled))
                {
                    Udp.EnableWriter();

                    // dont enable for UdpPingEnabled
                    if (Config.AppSettings.UdpClientEnabled)
                    {
                        Config.LoggerJobUdp = Config.LoggerQueue.AddJob(JobType.Network, "udp", Udp);
                    }
                }
                else
                {
                    Config.LoggerQueue.RemoveJob(Config.LoggerJobUdp);
                }

                // setup server for udp if that is current activity
                if (Config.Activity == Configuration.ActivityType.Server)
                {
                    if (!Config.ConfigureTracing(true))
                    {
                        return false;
                    }

                    UdpReaderThread = new Thread(Udp.StartListener);
                    UdpReaderThread.Name = "_udpReader";
                    UdpReaderThread.Start();
                    CloseCurrentSessionEvent.WaitOne();
                }
                //130922 console comes through here if using only properties and not operators /remoteactivity=deploy vs /deploy
                else if (Config.Activity == Configuration.ActivityType.Remote)
                {
                    return Config.ProcessRemoteActivity();
                }
                else
                {
                    // Activity other than server

                    Udp.StopListener();

                    // check if eventlog listeners should be enabled if no error and not delayed
                    // trace, start trace
                    ConfigureEventLogEventListenerResult ret = ConfigureEventLogEventListener(true);
                    if (ret == ConfigureEventLogEventListenerResult.Exception
                        | (ret == ConfigureEventLogEventListenerResult.EnableTracing && !EtwTraceStart()))
                    {
                        return false;
                    }
                    else if (ret == ConfigureEventLogEventListenerResult.EventTracing)
                    {
                        // using event log events to start / stop trace. loop until forced stop
                        if (Config.AppSettings.StartEventEnabledImmediately
                            | !Config.AppSettings.StartEventEnabled & Config.AppSettings.StopEventEnabled)
                        {
                            _eventListenerMatch.Set();
                        }
                        else
                        {
                            LogOutput("CDFMonitor waiting for startup event");
                            _eventListenerMatch.Reset();
                        }

                        while (true)
                        {
                            // matching event start id
                            while (!_eventListenerMatch.WaitOne(0))
                            {
                                if (CloseCurrentSessionEvent.WaitOne(100))
                                {
                                    return false;
                                }
                            }

                            if (!EtwTraceStart())
                            {
                                return false;
                            }

                            _eventListenerMatch.Reset();
                        }
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                LogOutput("Error Starting:" + e.ToString());
                return false;
            }
            finally
            {
                CloseCurrentSessionEvent.Set();
            }
        }

        /// <summary>
        /// Stops the configured activity
        /// </summary>
        public void Stop()
        {
            try
            {
                // run shutdown processes and do not wait.
                foreach (Config.Configuration.ProcessCommandResults shutdownCommandPCR in Config.ShutdownCommands)
                {
                    RunProcess(shutdownCommandPCR.Command, shutdownCommandPCR.Arguments, Config.AppSettings.ShutdownCommandWait);
                }

                _processMonitor.Disable();

                Config.LoggerQueue.ClearQueues();
                Config.LoggerQueue.ClearBuffer();

                if (Config.AppSettings.UdpClientEnabled | Udp.WriterEnabled)
                {
                    Udp.DisableWriter();
                }

                if (Config.Activity == Configuration.ActivityType.Server)
                {
                    Udp.StopListener();
                }

                EtwTraceStop();
                ConfigureEventLogEventListener(false);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Stopping:" + e.ToString());
                CloseCurrentSessionEvent.Set();
            }
        }

        /// <summary>
        /// Writes the output.
        /// </summary>
        /// <param name="data">The data.</param>
        public void TraceOutput(string data)
        {
            Debug.Print("TraceOutput:" + data);

            if (!Config.LoggerQueue.QueueOutput(data, JobOutputType.Trace))
            {
                _missedLoggerEvents++;
            }
        }

        /// <summary>
        /// Uploads the package.
        /// </summary>
        /// <param name="traceString">The check.</param>
        public void UploadPackage(string traceString)
        {
            bool checkOnly = string.Compare(traceString, "verify") == 0;
            string zipFile = string.Empty;

            if (!checkOnly)
            {
                zipFile = BuildPackage(_path);
            }

            if (!String.IsNullOrEmpty(zipFile) || checkOnly)
            {
                NetworkCredential nc = new NetworkCredential(Config.AppSettings.UrlUser, Config.AppSettings.UrlPassword);

                // get password if empty in config put here on purpose for customer to optionally
                // control uploads by populating site, user, but not password
                if (!string.IsNullOrEmpty(Config.AppSettings.UrlUser)
                    && string.IsNullOrEmpty(Config.AppSettings.UrlPassword))
                {
                    nc = Credentials.PromptForCredentials(Config.AppSettings.UrlUser, Config.AppSettings.UrlPassword,
                                                                            Config.AppSettings.UrlSite);
                }

                // ftp or http
                if (Config.AppSettings.UrlSite.ToLower().Contains("ftp://"))
                {
                    LogOutput(string.Format("UploadPackage:Sending zip to FTP:{0}",
                                              Config.AppSettings.UrlSite + "/" + Path.GetFileName(zipFile)));
                    AsynchronousFtpUpLoader ftp = new AsynchronousFtpUpLoader(
                        Config.AppSettings.UrlSite + "/" + Path.GetFileName(zipFile), nc.UserName, nc.Password, zipFile);
                    LogOutput(string.Format("UploadPackage:FTP result:{0}", ftp.Status));
                }
                else
                {
                    LogOutput(string.Format("UploadPackage:Sending zip to URL:{0}", Config.AppSettings.UrlSite + "/" + zipFile));
                    AsynchronousHttpUpLoader ftp = new AsynchronousHttpUpLoader(
                        Config.AppSettings.UrlSite + "/" + Path.GetFileName(zipFile), nc.UserName, nc.Password, zipFile);
                    LogOutput(string.Format("UploadPackage:URL result:{0}", ftp.Status));
                }

                if (Config.SendSmtp)
                {
                    SmtpThread.Queue(traceString);
                }
            }

            LogOutput("UploadPackage:exit");
        }

        /// <summary>
        /// Handlers the specified sig.
        /// </summary>
        /// <param name="sig">The sig.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool WndMsgHandler(CtrlType sig)
        {
            // listens for window events
            bool handled = false;
            LogOutput("DEBUG:received handler event:" + sig.ToString());
            switch (sig)
            {
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_BREAK_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    {
                        if (Config.AppOperators.RunningAsService && sig == CtrlType.CTRL_LOGOFF_EVENT)
                        {
                            // let scm handle
                            Debug.Print("bypassing handler");
                            handled = true;
                            break;
                        }
                        else
                        {
                            // let shutdown processes finish
                            for (int i = 0; i <= 300; i++)
                            {
                                if (ProcessRunning > 0)
                                {
                                    Thread.Sleep(1000);
                                }
                                else
                                {
                                    break;
                                }
                            }

                            // clean up trace processing thread
                            CloseCurrentSessionEvent.Set();

                            lock (this)
                            {
                                LogOutput("finalizing");
                                Cleanup();
                            }
                        }
                    }

                    // set to false for application to exit
                    handled = false;
                    break;

                default:
                    return handled;
            }
            return handled;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Gets the console window.
        /// </summary>
        /// <returns>IntPtr.</returns>
        [DllImport("kernel32")]
        private static extern IntPtr GetConsoleWindow();

        /// <summary>
        /// Main function
        /// </summary>
        /// <param name="args">The args.</param>
        [STAThread]
        private static void Main(string[] args)
        {
            //#if DEBUG
            //            System.Diagnostics.Debugger.Break();
            //#endif
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            AppDomain.CurrentDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);

            Thread.CurrentThread.Name = "main";

            // set min and max thread counts to for running commands
            ThreadPool.SetMinThreads(Configuration.MinThreadCount, Configuration.MinThreadCount);
            ThreadPool.SetMaxThreads(Configuration.MaxThreadCount, Configuration.MaxThreadCount);
            Args = args;

            CDFMonitor cdfM = new CDFMonitor();
            CDFMonitor.Instance = cdfM;
            bool initialized = cdfM.Initialize();
            if (!initialized && args.Length > 0)
            {
                // could be processing argument or error in config
                CloseCurrentSessionEvent.Set();
                cdfM.WndMsgHandler(CtrlType.CTRL_CLOSE_EVENT);
                return;
            }
            else if (!initialized)
            {
                // set to gui so config can be fixed
                Configuration.StartupAs = Configuration.ExecutionOptions.Gui;
            }

            // though config may say service, its really only running as a service with argument
            // runningasaservice
            if (cdfM.Config.AppOperators.RunningAsService)
            {
                cdfM.Config.ConfigureConsoleOutput(false);

                ConsoleManager.Hide();
                ServiceBase[] ServicesToRun = new ServiceBase[] { new ServiceCDFMonitor(cdfM) };
                ServiceBase.Run(ServicesToRun);
            }
            else
            {
                switch (Configuration.StartupAs)
                {
                    // case Configuration.ExecutionOptions.server:
                    case Configuration.ExecutionOptions.Console:
                        cdfM.Config.ConfigureConsoleOutput(true, true);
                        cdfM.Start();
                        break;

                    case Configuration.ExecutionOptions.Unknown:
                    case Configuration.ExecutionOptions.Service:
                    case Configuration.ExecutionOptions.Gui:
                        cdfM.Config.ConfigureConsoleOutput(false);

                        // start gui thread
                        ConsoleManager.Hide();
                        cdfM.Gui = new CDFMonitorGui();
                        break;

                    case Configuration.ExecutionOptions.Hidden:
                        ConsoleManager.Hide();
                        cdfM.Start();
                        break;

                    default:

                        cdfM.LogOutput("Main: Configuration.ExecutionOptions switch error:" +
                                         Configuration.StartupAs.ToString());
                        break;
                }
            }

            CloseCurrentSessionEvent.Set();
            cdfM.WndMsgHandler(CtrlType.CTRL_CLOSE_EVENT);
        }

        /// <summary>
        /// Sets the console CTRL handler.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <param name="add">if set to <c>true</c> [add].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        /// <summary>
        /// Shows the window.
        /// </summary>
        /// <param name="hWnd">The h WND.</param>
        /// <param name="nCmdShow">The n CMD show.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Checks the event max count.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool CheckEventMaxCount()
        {
            // exit application if maxcount reached
            if (Config.AppSettings.EventMaxCount > 0 && MatchedEvents >= Config.AppSettings.EventMaxCount)
            {
                LogOutput("max command count reached, exiting session.");
                Stop();
                CloseCurrentSessionEvent.Set();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks for new version.
        /// </summary>
        private void CheckForNewVersion()
        {
            CheckOnlineVersionResults cOVR = CheckOnlineVersion();

            if (cOVR.IsPopulated && cOVR.IsNewer)
            {
                string downloadPath = AppDomain.CurrentDomain.BaseDirectory + Path.GetFileName(cOVR.PackageUrl);
                new WebClient().DownloadFile(cOVR.PackageUrl, downloadPath);
                LogOutput("package downloaded to: " + downloadPath);
            }
            else if (cOVR.IsPopulated && !cOVR.IsNewer)
            {
                LogOutput("No update");
            }
            else
            {
                LogOutput("Error:unable to connect to:" + Configuration.UPDATE_URL);
            }
        }

        /// <summary>
        /// Checks the online version.
        /// </summary>
        /// <returns>CheckOnlineVersionResults.</returns>
        private CheckOnlineVersionResults CheckOnlineVersion()
        {
            CheckOnlineVersionResults cOVR = new CheckOnlineVersionResults();
            try
            {
                cOVR.CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                // check for version update
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(new HttpGet().GetRequest(Configuration.UPDATE_URL));
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

                    LogOutput("package url:" + cOVR.PackageUrl);
                    LogOutput("article url:" + cOVR.ArticleUrl);
                    LogOutput("published date:" + cOVR.Date);
                    LogOutput("published version:" + cOVR.PackageVersion); //<--not working
                    LogOutput("current version:" + cOVR.CurrentVersion);
                }

                return (cOVR);
            }
            catch (Exception e)
            {
                LogOutput("DEBUG:Unable to check version" + e.ToString());
                return (cOVR);
            }
        }

        /// <summary>
        /// Cleans up this instance.
        /// </summary>
        private void Cleanup()
        {
            try
            {
                Stop();

                // dispose process listener
                _processMonitor.Dispose();

                // upload to logfileserver
                // todo: fix this
                try
                {
                    if (Config.LoggerJobUtility.Writer.LogManager.LogFileServerEvt != null)
                    {
                        Config.LoggerJobUtility.Writer.LogManager.LogFileServerEvt.Set();
                        Config.LoggerJobUtility.Writer.LogManager.LogFileServerMgr.Join(10000);
                    }
                }
                catch { }

                // _regexthread only one not string todo fix foreach to cover all threads
                if (RegexParserThread != null) RegexParserThread.Shutdown();
                if (_eventListenerThread != null) _eventListenerThread.Shutdown();

                // sleep here to output last traces if queue not behind
                Thread.Sleep(100);

                // clear and disable all queues
                foreach (ThreadQueue<string> q in ThreadQueue<string>.ThreadQueues)
                {
                    q.ClearQueue();
                }

                // make sure last command ran like zip/upload/smtp
                int count = 0;
                while (count < 30)
                {
                    count++;
                    bool sleep = false;
                    foreach (ThreadQueue<string> q in ThreadQueue<string>.ThreadQueues)
                    {
                        if (q.IsActive)
                        {
                            sleep = true;

                            LogOutput("DEBUG:Waiting on q:" + q.Name);
                        }
                    }
                    if (!sleep)
                    {
                        break;
                    }
                    Thread.Sleep(1000);
                }

                LogOutput("finished");

                // sleep here to output last traces from closedown
                Thread.Sleep(100);

                foreach (ThreadQueue<string> q in ThreadQueue<string>.ThreadQueues)
                {
                    q.Shutdown();
                }

                // todo: fix this
                try
                {
                    Config.LoggerJobUtility.Writer.LogManager.LogFileStream.Close();
                }
                catch { }

                Console.ResetColor();
                Thread.Sleep(0);
            }
            catch (Exception e)
            {
                LogOutput("Error closing:" + e.ToString());
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Setups the event log event listener.
        /// </summary>
        /// <param name="enable">if set to <c>true</c> [enable].</param>
        /// <returns><c>1 to start tracing</c> -1 to not start tracing, <c>0 exception/fail</c>
        /// otherwise</returns>
        private ConfigureEventLogEventListenerResult ConfigureEventLogEventListener(bool enable)
        {
            try
            {
                // dont use when parsing files or remote
                if (Config.Activity != Configuration.ActivityType.TraceToCsv
                    & Config.Activity != Configuration.ActivityType.TraceToEtl
                    & Config.Activity != Configuration.ActivityType.Server)
                {
                    LogOutput("DEBUG:SetupEventLogEventListener:unsupported activity:returning true:" + Config.Activity.ToString());
                    return ConfigureEventLogEventListenerResult.EnableTracing;
                }

                if (Config.AppSettings.StartEventEnabled)
                {
                    if (enable)
                    {
                        _startEventLog = new EventLog(Config.AppSettings.StartEventSource, Environment.MachineName);
                        _startEventLog.EnableRaisingEvents = enable;
                        _startEventLog.EntryWritten += new EntryWrittenEventHandler(EventLogEventListenerProc);
                        return ConfigureEventLogEventListenerResult.EventTracing;
                    }
                    else
                    {
                        if (_startEventLog != null)
                        {
                            _startEventLog.EntryWritten -= new EntryWrittenEventHandler(EventLogEventListenerProc);
                            _startEventLog.Dispose();
                        }
                    }
                }

                if (Config.AppSettings.StopEventEnabled)
                {
                    // only setup if different than start
                    if (string.Compare(Config.AppSettings.StopEventSource, Config.AppSettings.StartEventSource) != 0 | !Config.AppSettings.StartEventEnabled)
                    {
                        if (enable)
                        {
                            _stopEventLog = new EventLog(Config.AppSettings.StopEventSource, Environment.MachineName);
                            _stopEventLog.EnableRaisingEvents = enable;
                            _stopEventLog.EntryWritten += new EntryWrittenEventHandler(EventLogEventListenerProc);
                            return ConfigureEventLogEventListenerResult.EventTracing;
                        }
                        else
                        {
                            if (_stopEventLog != null)
                            {
                                _stopEventLog.EntryWritten -= new EntryWrittenEventHandler(EventLogEventListenerProc);
                                _stopEventLog.Dispose();
                            }
                        }
                    }
                }

                return ConfigureEventLogEventListenerResult.EnableTracing;
            }
            catch (Exception e)
            {
                LogOutput("SetupEventLogEventListener:Exception:" + e.ToString());
                return ConfigureEventLogEventListenerResult.Exception;
            }
        }

        /// <summary>
        /// Handles the EventRead event of the Consumer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CDFMonitor.Engine.Trace.EventReadEventArgs" /> instance
        /// containing the event data.</param>
        private void Consumer_EventRead(object sender, EventReadEventArgs e)
        {
            _consumerEventReadCount++;
            RegexParserThread.Queue(e);
        }

        /// <summary>
        /// Converts Process ID to process name and id string
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private string ConvertToProcessName(uint id)
        {
            // only convert if realtime tracing
            if (Config.AppSettings.MonitorProcesses
                && (Config.Activity == Configuration.ActivityType.TraceToCsv
                || Config.Activity == Configuration.ActivityType.TraceToEtl))
            {
                return _processMonitor.GetProcessNameFromId(id);
            }
            else
            {
                return id.ToString();
            }
        }

        /// <summary>
        /// converts local time to remote time (time on device that was running trace). it does this
        /// with bias time provided in etl. returns string DateTime.
        /// </summary>
        /// <param name="fileTime">EventTrace.TimeStamp</param>
        /// <returns>string DateTime</returns>
        private string ConvertToRemoteTime(long fileTime)
        {
            string dateTime = string.Empty;
            bool discardTime = false;
            try
            {
                if (fileTime == 0)
                {
                    fileTime = DateTime.Now.ToFileTime();
                    discardTime = true;
                    _timeZoneBias = (int)TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalMinutes;
                }
                else if (_timeZoneBias == -1 && !Config.AppSettings.UseTargetTime)
                {
                    _timeZoneBias = (int)TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalMinutes;
                }
                else if (_timeZoneBias == -1)
                {
                    // Try kernel consumer and user consumer as kernel always the first trace message in realtime
                    EtwTraceConsumer etc = Config.IsKernelTraceConfigured() ? _consumerKernel : _consumer;

                    _timeZoneBias = etc.BufferCallback.LogfileHeader.TimeZone.Bias * -1;

                    // dst
                    var dateTimeDaylightDay = etc.BufferCallback.LogfileHeader.TimeZone.DaylightDate;
                    var dateTimeStandardDay = etc.BufferCallback.LogfileHeader.TimeZone.StandardDate;
                    DateTime traceStartTime = DateTime.FromFileTime(etc.BufferCallback.LogfileHeader.StartTime);
                    DateTime dateTimeDaylightDate = new DateTime(traceStartTime.Year, dateTimeDaylightDay.wMonth, dateTimeDaylightDay.wDay);
                    DateTime dateTimeStandardDate = new DateTime(traceStartTime.Year, dateTimeStandardDay.wMonth, dateTimeStandardDay.wDay);

                    if (dateTimeDaylightDate < traceStartTime && traceStartTime < dateTimeStandardDate)
                    {
                        _timeZoneBias -= etc.BufferCallback.LogfileHeader.TimeZone.DaylightBias;
                    }
                }

                dateTime = DateTime.FromFileTime(fileTime)
                               .AddMinutes(_timeZoneBias)
                               .ToUniversalTime().ToString("o") + _timeZoneBias;

                // remove 'T' and 'Z' and convert to csv
                dateTime = dateTime.Replace("Z", ",");
                dateTime = dateTime.Replace("T", ",");
                if (discardTime)
                {
                    // reset until we get a good time in
                    _timeZoneBias = -1;
                }
            }
            catch (Exception e)
            {
                Debug.Print("ConvertToRemoteTime:exception getting timestamp for event" + e.ToString());
                _timeZoneBias = -1;
            }

            return dateTime;
        }

        /// <summary>
        /// Displays the process list.
        /// </summary>
        private void DisplayProcessList()
        {
            // only display if gathering a trace (not processing one post)
            if ((Config.Activity != Configuration.ActivityType.TraceToCsv
                & Config.Activity != Configuration.ActivityType.TraceToEtl)
                | !Config.AppSettings.LogMatchDetail)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Running Process list:");
            sb.AppendLine("--------------------------------------------------------------------------------------------");
            sb.AppendLine("[process_name],[process_id],[process_session_id],[start_time],[thread_count],[processor_time],[memory_size],[file_version],[file_name]");
            foreach (Process process in Process.GetProcesses().OrderBy(p => p.ProcessName))
            {
                try
                {
                    sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                        process.ProcessName,
                        process.Id,
                        process.SessionId,
                        process.StartTime,
                        process.Threads.Count,
                        process.TotalProcessorTime,
                        process.VirtualMemorySize64,
                        process.MainModule.FileVersionInfo.FileVersion,
                        process.MainModule.FileName));
                }
                catch { }
            }

            LogOutput(sb.ToString(), JobOutputType.Etw);
        }

        private List<string> EnumeratePackageFiles()
        {
            List<string> zipList = new List<string>();
            bool disableTracing = false;

            if (string.IsNullOrEmpty(Config.AppSettings.TraceFileOutput) ||
                !Config.Verify.VerifySetting(Configuration.ConfigurationProperties.EnumProperties.TraceFileOutput.ToString(), true))
            {
                return zipList;
            }

            if (!Config.LoggerJobTrace.Enabled && Config.LoggerJobTrace.Instance == null)
            {
                disableTracing = true;
                Config.ConfigureTracing(true);
            }

            if (Config.LoggerJobTrace.Enabled)
            {
                if (Config.LoggerJobTrace.JobType == JobType.Etl)
                {
                    Config.LoggerJobTrace.Writer.LogManager.ManageSequentialTraces();
                }
                else
                {
                    Config.LoggerJobTrace.Writer.LogManager.ManageSequentialLogs();
                }

                foreach (string log in Config.LoggerJobTrace.Writer.LogManager.Logs)
                {
                    if (!zipList.Contains(log))
                    {
                        zipList.Add(log);
                    }
                }
            }

            if (Config.IsKernelTraceConfigured())
            {
                if (Config.LoggerJobKernelTrace.Enabled)
                {
                    if (Config.LoggerJobKernelTrace.JobType == JobType.Etl)
                    {
                        Config.LoggerJobKernelTrace.Writer.LogManager.ManageSequentialTraces();
                    }
                    else
                    {
                        Config.LoggerJobKernelTrace.Writer.LogManager.ManageSequentialLogs();
                    }

                    foreach (string log in Config.LoggerJobKernelTrace.Writer.LogManager.Logs)
                    {
                        if (!zipList.Contains(log))
                        {
                            zipList.Add(log);
                        }
                    }
                }
            }

            if (disableTracing)
            {
                Config.ConfigureTracing(false);
            }

            return zipList;
        }

        /// <summary>
        /// Etws the add modules.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool EtwAddModules()
        {
            // Add utility guid so utility can send messages to etl
            if (!Config.ModuleList.ContainsKey(Properties.Resources.CDFMonitorTMFGuid))
            {
                Config.ModuleList.Add(Properties.Resources.CDFMonitorTMFGuid, Properties.Resources.SessionName);
            }

            foreach (KeyValuePair<string, string> kvp in Config.ModuleList)
            {
                if (!_controller.EnableTrace(true, kvp.Key))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Etws the initialize.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool EtwInitialize()
        {
            try
            {
                EtwTraceClean();
                if (Config.ConfigureControllerSessionName())
                {
                    // create new controller session
                    _controller = new EtwTraceController(Config.SessionName, Config.SessionGuid);

                    // set up event consumer
                    _consumer = new EtwTraceConsumer(Config.SessionName);

                    if (Config.IsKernelTraceConfigured())
                    {
                        // create new controller session
                        _controllerKernel = new EtwTraceController(Properties.Resources.KernelSessionName, new Guid(Properties.Resources.KernelSessionGuid));

                        // set up event consumer
                        _consumerKernel = new EtwTraceConsumer(Properties.Resources.KernelSessionName);
                    }

                    return true;
                }
                else
                {
                    LogOutput("Fail:EtwInitialize:Error initializing ETW");
                    return false;
                }
            }
            catch (Exception e)
            {
                LogOutput("exception:EtwInitialize:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Etws the parse to CSV.
        /// <param name="traceInput">pass string.empty for 'realtime' trace else pass input file for
        /// parsing.</param>
        private void EtwParseToCsv(string traceInput)
        {
            LogOutput("DEBUG:EtwTraceStart: logtoetl = false");

            // determine if etl or csv before starting controller
            if (!string.IsNullOrEmpty(traceInput)
                && Path.GetExtension(traceInput).ToLower() != ".etl")
            {
                // csv parse to csv
                FindMatchTextFile(traceInput);
            }
            else
            {
                // etl parse to csv
                if (!_controller.StartTraceSafe())
                {
                    CloseCurrentSessionEvent.Set();
                    return;
                }

                EtwStartConsumer(traceInput);

                if (Config.Activity == Configuration.ActivityType.RegexParseToCsv)
                {
                    TraceOutput(GetStats());
                    LogOutput("file parsing complete");
                    EtwTraceStop();
                }
            }
        }

        /// <summary>
        /// Etws the process trace async.
        /// </summary>
        /// <param name="traceInput">pass string.empty for 'realtime' trace else pass input file for
        /// parsing.</param>
        private void EtwStartConsumer(string traceInput)
        {
            // string empty for traceInput means realtime trace
            _consumer.OpenTrace(traceInput);

            if (string.IsNullOrEmpty(traceInput) && Config.IsKernelTraceConfigured())
            {
                _consumerKernel.OpenTrace(string.Empty);
                _consumerKernel.ProcessTraceAsync();
                _consumerKernel.EventRead += Consumer_EventRead;
            }

            _consumer.ProcessTraceAsync();
            _consumer.EventRead += Consumer_EventRead;

            LogOutput("CDFMonitor Running.");
            DisplayProcessList();
            string match = !string.IsNullOrEmpty(PrependMatchDesignator(false)) ? "[Match]," : string.Empty;

            if (Config.AppSettings.LogMatchDetail)
            {
                LogOutput(
                    string.Format("{0}[Date],[Time],[TmfGuid],[ThreadID],[ProcessID],[Module],[Source],[Function],[Level],[Class],[TraceMessage]", match));
            }
            else
            {
                LogOutput(
                    string.Format("{0}[Date],[Time],[ThreadID],[ProcessID],[Level],[TraceMessage]", match));
            }

            while (_consumer.Running)
            {
                if (CloseCurrentSessionEvent.WaitOne(100))
                {
                    break;
                }
            }

            bool sleep = true;

            while (sleep)
            {
                sleep = false;

                foreach (ThreadQueue<EventReadEventArgs> q in ThreadQueue<EventReadEventArgs>.ThreadQueues)
                {
                    Debug.Print("checking queue:" + q.Name);

                    if (q.IsActive | q.QueueLength() > 0)
                    {
                        sleep = true;
                    }
                }

                if (Config.LoggerQueue.QueueLengthsTotal() > 0)
                {
                    sleep = true;
                }

                if (CloseCurrentSessionEvent.WaitOne(100))
                {
                    return;
                }
            }

            return;
        }

        /// <summary>
        /// Etws the trace to CSV.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool EtwTraceToCsv()
        {
            // etl parse
            if (!_controller.StartTraceSafe())
            {
                CloseCurrentSessionEvent.Set();
                return false;
            }
            if (Config.IsKernelTraceConfigured()
                && !_controllerKernel.StartTraceSafe())
            {
                CloseCurrentSessionEvent.Set();
                return false;
            }

            if (!EtwAddModules())
            {
                return false;
            }

            // real time trace
            EtwStartConsumer(string.Empty);

            return true;
        }

        /// <summary>
        /// Etws the trace to etl.
        /// </summary>
        private void EtwTraceToEtl()
        {
            LogOutput("DEBUG:EtwTraceToEtl: logtoetl = true");
            NativeMethods.EventTraceFileMode filemode = NativeMethods.EventTraceFileMode.None;
            bool monitorEtlFiles = false;
            string tempTraceFile = Config.AppSettings.TraceFileOutput;
            if (!Config.Verify.VerifySetting(Configuration.ConfigurationProperties.EnumProperties.TraceFileOutput.ToString(), true))
            {
                return;
            }

            if (Config.AppSettings.LogFileOverWrite && Config.AppSettings.LogFileMaxSize > 0 && Config.AppSettings.LogFileMaxCount <= 1)
            {
                filemode = NativeMethods.EventTraceFileMode.Circular;
            }
            else if (!Config.AppSettings.LogFileOverWrite && Config.AppSettings.LogFileMaxSize > 0 && Config.AppSettings.LogFileMaxCount <= 1)
            {
                filemode = NativeMethods.EventTraceFileMode.Sequential;
            }
            else if (Config.AppSettings.LogFileMaxSize == 0 && Config.AppSettings.LogFileMaxCount <= 1)
            {
                filemode = NativeMethods.EventTraceFileMode.None;
            }
            else if (Config.AppSettings.LogFileMaxCount > 1 && Config.AppSettings.LogFileMaxSize > 0)
            {
                filemode = NativeMethods.EventTraceFileMode.NewFile;
                tempTraceFile = string.Format(@"{0}\{1}.%d{2}",
                                              Directory.GetParent(Config.AppSettings.TraceFileOutput),
                                              Path.GetFileNameWithoutExtension(Config.AppSettings.TraceFileOutput),
                                              Path.GetExtension(Config.AppSettings.TraceFileOutput));
                monitorEtlFiles = true;
            }
            else
            {
                LogOutput(
                    string.Format(
                        "EtwTraceToEtl: invalid arguments for etl tracing. quitting logfileoverwrite:{0} logfilemaxcount:{1} logfilemaxsize:{2}",
                        Config.AppSettings.LogFileOverWrite, Config.AppSettings.LogFileMaxCount, Config.AppSettings.LogFileMaxSize));
                CloseCurrentSessionEvent.Set();
                return;
            }

            try
            {
                LogOutput(string.Format("EtwTraceToEtl: starting .etl file session:{0} : {1}", filemode.ToString(),
                                            tempTraceFile));
                if (!_controller.StartTraceSafe(filemode, (uint)(Config.AppSettings.LogFileMaxSize), tempTraceFile))
                {
                    CloseCurrentSessionEvent.Set();
                    return;
                }

                if (!EtwAddModules())
                {
                    return;
                }

                if (Config.IsKernelTraceConfigured())
                {
                    LogOutput(string.Format("EtwTraceToEtl: starting kernel .etl file session:{0} : {1}", filemode.ToString(),
                                            Config.KernelTraceFile()));
                    if (!_controllerKernel.StartTraceSafe(filemode, (uint)(Config.AppSettings.LogFileMaxSize), Config.KernelTraceFile()))
                    {
                        CloseCurrentSessionEvent.Set();
                        return;
                    }
                }

                System.Timers.Timer userTimer = new System.Timers.Timer(60000);
                System.Timers.Timer kernelTimer = new System.Timers.Timer(60000);
                if (monitorEtlFiles)
                {
                    LogOutput(string.Format("EtwTraceToEtl: monitoring .etl file count:{0} : {1} : {2}",
                                                filemode.ToString(), tempTraceFile, Config.AppSettings.LogFileMaxCount));

                    userTimer.AutoReset = true;
                    userTimer.Elapsed += new ElapsedEventHandler(Config.LoggerJobTrace.Writer.LogManager.MonitorSequentialTraces);
                    userTimer.Start();

                    // todo: kernel multifile to etl not working
                    if (Config.IsKernelTraceConfigured())
                    {
                        kernelTimer.AutoReset = true;
                        kernelTimer.Elapsed += new ElapsedEventHandler(Config.LoggerJobKernelTrace.Writer.LogManager.MonitorSequentialTraces);
                        kernelTimer.Start();
                    }
                }

                LogOutput("ETWTraceToEtl: running.");

                DisplayProcessList();

                if (CloseCurrentSessionEvent.WaitOne())
                {
                    userTimer.Stop();
                    _controller.StopTrace();
                    if (Config.IsKernelTraceConfigured())
                    {
                        kernelTimer.Stop();
                        _controllerKernel.StopTrace();
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                LogOutput("DEBUG:EtwTraceToEtl: monitor caught exception:" + e.ToString());
            }
        }

        /// <summary>
        /// Finds the match.
        /// </summary>
        /// <param name="e">The <see cref="CDFMonitor.Engine.Trace.EventReadEventArgs" /> instance
        /// containing the event data.</param>
        private void FindMatch(EventReadEventArgs e)
        {
            // event called when new trace is written, setup by consumer
            TraceEventThreadInfo eventInfo = new TraceEventThreadInfo();
            eventInfo.EventTrace = e.EventTrace;

            ProcessedEvents++;

            // get info from TMF file
            TMFTrace tmf = _tmfParser.ProcessTMFTrace(e);
            eventInfo.FormattedEventString = tmf.TMFParsedString;

            if (!tmf.IsPopulated || eventInfo.FormattedEventString.Length == 0)
            {
                _tmfParseErrors++;
            }

            if (!CheckEventMaxCount())
            {
                return;
            }

            if (Config.AppSettings.Debug)
            {
                // display bytes as a comma separated string
                StringBuilder evtStringBytes = new StringBuilder();
                byte[] evtBytes = new byte[e.EventStringBytes().Length];
                System.Buffer.BlockCopy(e.EventStringBytes(), 0, evtBytes, 0, evtBytes.Length);
                foreach (byte b in evtBytes)
                {
                    evtStringBytes.Append(b + ",");
                }

                TraceOutput(string.Format(@"DEBUG:{0},{1},{2},{3},{4}:{5}",
                                          eventInfo.EventTrace.Guid,
                                          eventInfo.EventTrace.ThreadId,
                                          ConvertToProcessName(eventInfo.EventTrace.ProcessId),
                                          e.TraceFunc,
                                          Encoding.ASCII.GetString(e.EventStringBytes()).Replace("\0", ""),
                                          evtStringBytes));
            }

            string matchstr = string.Empty;
            bool match = Config.BypassRegexPattern;
            if (!match)
            {
                match = RegexTracePatternObj.IsMatch(eventInfo.FormattedEventString);
                matchstr = PrependMatchDesignator(match);
            }

            string dateTime = ConvertToRemoteTime(e.EventTrace.TimeStamp);

            // format supplied event string
            if (Config.AppSettings.LogMatchDetail)
            {
                matchstr = (string.Format(@"{0}{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                                            matchstr,
                                            dateTime,
                                            eventInfo.EventTrace.Guid,
                                            eventInfo.EventTrace.ThreadId,
                                            ConvertToProcessName(eventInfo.EventTrace.ProcessId),
                                            tmf.Module,
                                            tmf.Source,
                                            tmf.Function,
                                            eventInfo.EventTrace.HeaderType,
                                            tmf.Class,
                                            eventInfo.FormattedEventString));
            }
            else
            {
                matchstr = (string.Format(
                    "{0}{1},{2},{3},{4},{5}",
                    matchstr,
                    dateTime,
                    eventInfo.EventTrace.ThreadId,
                    ConvertToProcessName(eventInfo.EventTrace.ProcessId),
                    eventInfo.EventTrace.HeaderType,
                    eventInfo.FormattedEventString));
            }

            if (!match && Config.AppSettings.LogMatchOnly)
            {
                // dont trace out to console and logs, just store in buffer in case match is found
                // so it can be sent
                if (Config.AppSettings.LogBufferOnMatch)
                {
                    // Config.LoggerQueue.AddTraceToBuffer(matchstr);
                    _logMatchOnlyQueue.Enqueue(matchstr);
                }
                while (_logMatchOnlyQueue.Count > 1000)
                {
                    _logMatchOnlyQueue.Dequeue();
                }
            }
            else if (match && Config.AppSettings.LogMatchOnly && Config.AppSettings.LogBufferOnMatch)
            {
                // found match so dump buffer contents to output
                foreach (string str in _logMatchOnlyQueue)
                {
                    TraceOutput(str);
                }

                _logMatchOnlyQueue.Clear();
            }
            else
            {
                // just normal trace out
                TraceOutput(matchstr);
            }

            if (match && !Config.BypassRegexPattern)
            {
                eventInfo.FormattedTraceString = matchstr;

                // pass event to threadpool
                int availThreads = 0;
                int availIOThreads = 0;
                ThreadPool.GetAvailableThreads(out availThreads, out availIOThreads);
                if (availThreads > 0)
                {
                    ThreadPool.QueueUserWorkItem(ProcessEventThreadProc, eventInfo);
                }
                else
                {
                    MissedMatchedEvents++;
                }
            }
        }

        /// <summary>
        /// Finds the match text.
        /// </summary>
        /// <param name="str">The STR.</param>
        private void FindMatchText(string str)
        {
            // event called when new trace is written, setup by consumer
            TraceEventThreadInfo eventInfo = new TraceEventThreadInfo();

            ProcessedEvents++;

            eventInfo.FormattedEventString = str;
            if (eventInfo.FormattedEventString.Length == 0)
            {
                _tmfParseErrors++;
            }

            bool match = Config.BypassRegexPattern;
            if ((!Config.BypassRegexPattern
                 && !(match = RegexTracePatternObj.IsMatch(eventInfo.FormattedEventString))
                 && Config.AppSettings.LogMatchOnly)
                || !CheckEventMaxCount())
            {
                return;
            }

            if (match | !Config.AppSettings.LogMatchOnly)
            {
                string matchstr = PrependMatchDesignator(match);

                matchstr = (string.Format(@"{0}{1}",
                                            matchstr,
                                            eventInfo.FormattedEventString));
                TraceOutput(matchstr);
                eventInfo.FormattedTraceString = matchstr;
            }

            if (match && !Config.BypassRegexPattern)
            {
                int availThreads = 0;
                int availIOThreads = 0;
                ThreadPool.GetAvailableThreads(out availThreads, out availIOThreads);
                if (availThreads > 0)
                {
                    ThreadPool.QueueUserWorkItem(ProcessEventThreadProc, eventInfo);
                }
                else
                {
                    MissedMatchedEvents++;
                }
            }
            return;
        }

        /// <summary>
        /// Finds the match text file.
        /// </summary>
        /// <param name="path">The path.</param>
        private void FindMatchTextFile(string path)
        {
            using (StreamReader sr = new StreamReader(path))
            {
                while (sr.Peek() >= 0)
                {
                    FindMatchText(sr.ReadLine());
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CDFMonitor" /> class.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool Initialize()
        {
            // setup global event for processingthread to wait on
            CloseCurrentSessionEvent = new ManualResetEvent(false);

            Config = new Configuration(Args);

            Config.SessionName = Resources.SessionName;

            Udp = new Network.Udp(this);

            // start console write and read threadq
            Config.ConfigureConsoleOutput(true);
            Config.ConfigureEtwOutput(true);

            // setup write callback
            LogOutputHandler = LogOutput;

            // console window handler
            _handler = WndMsgHandler;
            SetConsoleCtrlHandler(_handler, true);

            // start regex parser thread so that main thread can queue up traces in order for parse
            RegexParserThread = new ThreadQueue<EventReadEventArgs>((data) => { FindMatch(data); }, "_regexParser");
            RegexParserThread.WaitForQueue = true;
            RegexParserThread.MaxQueueLength = Config.ThreadQueueLength;

            // start regex parser thread so that main thread can queue up traces in order for parse
            ServerRegexParserThread = new ThreadQueue<string>((data) => { FindMatchText(data); }, "_serverRegexParser");
            ServerRegexParserThread.WaitForQueue = true;
            ServerRegexParserThread.MaxQueueLength = Config.ThreadQueueLength;

            // start smtp thread
            SmtpThread = new ThreadQueue<string>((data) => { SendSMTP(data); }, "_smtpThread");

            // start urlupload thread
            UrlUploadThread = new ThreadQueue<string>((data) => { UploadPackage(data); }, "_urlUploadThread");

            // start eventlistener
            _eventListenerThread = new ThreadQueue<EntryWrittenEventArgs>((data) => { ProcessEventLogEventListenerProc(data); }, "_eventListenerThread");

            if (Config.ProcessStartup() && Config.ProcessConfiguration())
            {
                EtwTraceClean();
                Config.DisplayConfigSettings(true);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Logs the trace.
        /// </summary>
        /// <param name="match">if set to <c>true</c> [match].</param>
        /// <returns>System.String.</returns>
        private string PrependMatchDesignator(bool match)
        {
            string matchstr = string.Empty;
            if (!Config.BypassRegexPattern)
            {
                matchstr = match ? "MATCH," : "NOMATCH,";
            }
            return matchstr;
        }

        /// <summary>
        /// Processes the event log event listener proc.
        /// </summary>
        /// <param name="e">The <see cref="EntryWrittenEventArgs" /> instance containing the event
        /// data.</param>
        private void ProcessEventLogEventListenerProc(EntryWrittenEventArgs e)
        {
            // check if it matches start or stop event ids'
            LogOutput("DEBUG:ProcessEventLogEventListenerProc:entry:eventid:" + (e.Entry.InstanceId & 0xffff).ToString());
            if (Config.AppSettings.StartEventEnabled
                && Config.AppSettings.StartEventID.ToString().Split(';').ToList()
                .Contains((e.Entry.InstanceId & 0xffff).ToString()))
            {
                MatchedEvents++;
                LogOutput("StartEvent received:" + e.Entry.Message);
                if (!_eventListenerMatch.WaitOne(0))
                {
                    _eventListenerMatch.Set();
                    return;
                }
            }

            if (Config.AppSettings.StopEventEnabled
                && Config.AppSettings.StopEventID.ToString().Split(';').ToList()
                .Contains((e.Entry.InstanceId & 0xffff).ToString()))
            {
                // stop tracing
                if (_eventListenerMatch.WaitOne(0))
                {
                    MatchedEvents++;
                    LogOutput("StopEvent received:" + e.Entry.Message);
                    EtwTraceStop();
                }

                // return false so trace does not start until start event is written.
                if (Config.AppSettings.StopEventDisabledPermanently)
                {
                    LogOutput("CDFMonitor stopping for event permanently");
                    Stop();
                    CDFMonitor.CloseCurrentSessionEvent.Set();
                }
            }
        }

        /// <summary>
        /// Resets the stats.
        /// </summary>
        private void ResetStats()
        {
            StartTime = DateTime.Now;
            _missedLoggerEvents = 0;
            MissedMatchedEvents = 0;
            MatchedEvents = 0;
            ProcessedEvents = 0;
            _throttledEvents = 0;
            _consumerEventReadCount = 0;
            _tmfParseErrors = 0;

            if (_tmfParser != null)
            {
                _tmfParser.TMFServerMissedCount = 0;
                _tmfParser.TMFServerAlternateHitCount = 0;
                _tmfParser.TMFServerHitCount = 0;
                _tmfParser.TMFCacheMissedCount = 0;
                _tmfParser.TMFCacheHitCount = 0;
            }
        }

        /// <summary>
        /// Runs the process.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="wait">if set to <c>true</c> [wait].</param>
        /// <param name="testMode">if set to <c>true</c> [test mode].</param>
        private void RunProcess(string command, string arguments, bool wait, bool testMode = false)
        {
            ProcessRunning++;

            try
            {
                LogOutput(string.Format("starting command with wait={0}:{1} {2}", wait, command, arguments));

                ProcessStartInfo psi = new ProcessStartInfo(command, arguments);
                if (testMode)
                {
                    return;
                }

                // wait for command to return if specified in app config
                if (wait)
                {
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;

                    Process objProcess = Process.Start(psi);
                    StreamReader processOutput = objProcess.StandardOutput;

                    // wait for command to return if its still running
                    if (!objProcess.HasExited)
                    {
                        LogOutput("DEBUG:process ID:" + objProcess.Id);
                        string outputString = processOutput.ReadToEnd();

                        // todo set a max waitforexit timer in config file? wait for process to
                        // finish
                        objProcess.WaitForExit();

                        // write process results to event if specified in app config
                        if (Config.AppSettings.WriteEvent)
                            EventLog.WriteEntry(Config.SessionName, "eventcommand process result:" + outputString,
                                                EventLogEntryType.Information, 101);
                        LogOutput("SUCCESS: eventcommand process result:" + outputString);

                        if (Config.SendSmtp)
                        {
                            SendSMTP(outputString);
                        }
                    }
                    else
                    {
                        // process exited before setup for return object could be setup
                        LogOutput("Warning:Process exited before result could be captured");
                    }
                }
                else
                {
                    // just start process and dont wait for return
                    Process objProcess = Process.Start(command, arguments);

                    // check return of process start for errors
                    if (objProcess == null)
                    {
                        LogOutput("Fail: error starting eventcommand with no wait");
                    }
                    else
                    {
                        LogOutput("DEBUG:process ID:" + objProcess.Id);
                    }
                }
            }
            catch (Exception e)
            {
                LogOutput("RunProcess:exception:" + e.ToString());
            }
            finally
            {
                ProcessRunning--;
            }
        }

        /// <summary>
        /// WriteModulesList creates a CTL file of module names and guids if valid file and path are
        /// specified
        /// </summary>
        /// <param name="modules">Dictionary of module names and guids</param>
        /// <param name="path">string path and file name of ctl</param>
        private void WriteModulesList(Dictionary<string, string> modules, string path)
        {
            StreamWriter sw = null;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n");

            foreach (KeyValuePair<string, string> kvp in modules)
            {
                sb.AppendLine(string.Format("{0}    {1}", kvp.Value, kvp.Key));
            }

            LogOutput(sb.ToString());

            if (!String.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                sw = File.AppendText(path);
                foreach (KeyValuePair<string, string> kvp in modules)
                {
                    sw.WriteLine(string.Format("{0}    {1}", kvp.Value, kvp.Key));
                }

                sw.Close();
            }
        }

        #endregion Private Methods
    }

    /// <summary>
    /// Class CheckOnlineVersionResults
    /// </summary>
    public class CheckOnlineVersionResults
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
}