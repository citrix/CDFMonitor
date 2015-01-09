// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="Writer.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Engine
{
    using CDFM.FileManagement;
    using CDFM.Gui;
    using CDFM.Network;
    using System;
    using System.Diagnostics;
    using System.Windows.Media;

    /// <summary>
    /// Enum JobOutputType
    /// </summary>
    [Flags]
    public enum JobOutputType
    {
        Unknown = 0,
        Log = 1,
        Trace = 2,
        Etw = 4
    }

    /// <summary>
    /// Enum JobType
    /// </summary>
    [Flags]
    public enum JobType
    {
        Unknown = 1,
        Console = 2,
        Csv = 4,
        Gui = 8,
        Etl = 16,
        Etw = 32,
        Network = 64,
        Log = 128
    }

    /// <summary>
    /// Class Writer
    /// </summary>
    public class Writer
    {
        #region Public Constructors

        //public writer(Job job)
        /// <summary>
        /// Initializes a new instance of the <see cref="Writer" /> class.
        /// </summary>
        /// <param name="job">The job.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public Writer(WriterJob job)
        {
            _writerJob = job;

            if (string.IsNullOrEmpty(job.JobName))
            {
                CDFMonitor.LogOutputHandler("Fail:writer: logfilename empty. returning.");
                return;
            }

            switch (job.JobType)
            {
                case JobType.Csv:
                case JobType.Log:
                    LogManager = new LogManager(job);
                    Queue = new ThreadQueue<string>((data) => { ThreadQueueProcCsv(data); }, job.JobName + "_csvwriter");
                    break;

                case JobType.Etl:
                    LogManager = new LogManager(job);
                    break;

                case JobType.Etw:
                    Queue = new ThreadQueue<string>((data) => { ThreadQueueProcEtw(data); }, job.JobName + "_etwwriter");
                    break;

                case JobType.Console:
                    Queue = new ThreadQueue<string>((data) => { ThreadQueueProcConsole(data); }, job.JobName + "_consolewriter");
                    break;

                case JobType.Gui:
                    Queue = new ThreadQueue<string>((data) => { ThreadQueueProcGui(data); }, job.JobName + "_guiwriter");
                    break;

                case JobType.Network:
                    Queue = new ThreadQueue<string>((data) => { ThreadQueueProcUdp(data); }, job.JobName + "_netwriter");
                    break;

                case JobType.Unknown:
                default:
                    return;
            }

            _writerJob.Enabled = true;

            return;
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// Gets or sets the _ewf events.
        /// </summary>
        /// <value>The _ewf events.</value>
        public int ErrorWarningFailEvents
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the log manager.
        /// </summary>
        /// <value>The log manager.</value>
        public LogManager LogManager
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the missed queue events.
        /// </summary>
        /// <value>The missed queue events.</value>
        public int MissedQueueEvents
        {
            get;
            set;
        }

        /// <summary>
        /// The _missed writer events
        /// </summary>
        public int MissedWriterEvents
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the log queue.
        /// </summary>
        /// <value>The log queue.</value>
        public ThreadQueue<string> Queue
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the queued events.
        /// </summary>
        /// <value>The queued events.</value>
        public int QueuedEvents
        {
            get;
            set;
        }

        #endregion Public Properties

        #region Private Properties

        /// <summary>
        /// Gets or sets the writer job.
        /// </summary>
        /// <value>The writer job.</value>
        private WriterJob _writerJob
        {
            get;
            set;
        }

        #endregion Private Properties

        #region Private Methods

        /// <summary>
        /// Determines the color.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>Brush.</returns>
        private Brush DetermineColor(string data)
        {
            SolidColorBrush color = Brushes.White;

            if (data.StartsWith("MATCH,"))
            {
                color = Brushes.Cyan;
            }

            if (data.Contains("DEBUG:"))
            {
                color = Brushes.Gray;
            }
            else if (data.Contains("SUCCESS:"))
            {
                color = Brushes.Green;
            }

            if (data.ToLower().Contains("fail") | data.ToLower().Contains("error"))
            {
                color = Brushes.Red;
                ErrorWarningFailEvents++;
            }

            if (data.ToLower().Contains("warning") | data.ToLower().Contains("exception"))
            {
                ErrorWarningFailEvents++;
                color = Brushes.Yellow;
            }

            return color;
        }

        /// <summary>
        /// Writes the console Q.
        /// </summary>
        /// <param name="data">The data.</param>
        private void ThreadQueueProcConsole(string data)
        {
            // sets console color filters output
            Console.ForegroundColor = ConsoleManager.GetConsoleColor(DetermineColor(data));

            try
            {
                Console.WriteLine(data);
            }
            catch (Exception e)
            {
                MissedWriterEvents++;
                Debug.Print("ThreadQueueProcConsole:exception:" + e.ToString());
            }
        }

        /// <summary>
        /// ThreadQueueProc
        /// </summary>
        /// <param name="data">string message to write</param>
        private void ThreadQueueProcCsv(string data)
        {
            try
            {
                // Log file management additions
                LogManager.CurrentLogFileSize += data.Length;
                if (_writerJob.LogFileMaxSize > 0 &&
                    (_writerJob.LogFileMaxSizeBytes - LogManager.CurrentLogFileSize < 0))
                {
                    LogManager.CurrentLogFileSize = 0;
                    LogManager.LogFileStream.Flush();
                    LogManager.ManageSequentialLogs();
                }

                Debug.Print("ThreadQueueProcCsv:" + data);
                LogManager.LogFileStream.WriteLine(data);
                if (_writerJob.LogFileAutoFlush)
                {
                    LogManager.LogFileStream.Flush();
                }
            }
            catch (Exception e)
            {
                Debug.Print("ThreadQueueProcCsv:exception:" + e.ToString());
                MissedWriterEvents++;
            }
        }

        /// <summary>
        /// Threads the queue proc etw.
        /// </summary>
        /// <param name="data">The data.</param>
        private void ThreadQueueProcEtw(string data)
        {
            try
            {
                ((CDFM.Trace.EtwTraceWriter)_writerJob.Instance).WriteEvent(data);
            }
            catch (Exception e)
            {
                MissedWriterEvents++;
                Debug.Print("ThreadQueueProcEtw:exception:" + e.ToString());
            }
        }

        /// <summary>
        /// Threads the queue proc GUI.
        /// </summary>
        /// <param name="data">The data.</param>
        private void ThreadQueueProcGui(string data)
        {
            CDFMonitorGui guiInstance = ((CDFMonitorGui)_writerJob.Instance);

            foreach (string splitLine in data.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                CDFMonitorGui.WriteListObj wListobj = new CDFM.Gui.CDFMonitorGui.WriteListObj()
                {
                    color = DetermineColor(data),
                    line = splitLine
                };

                lock (guiInstance.WriteList)
                {
                    guiInstance.WriteList.Enqueue(wListobj);
                }
            }

            while (true)
            {
                if (guiInstance.WriteList.Count > 5000)
                {
                    if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(10))
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Threads the queue proc UDP.
        /// </summary>
        /// <param name="data">The data.</param>
        private void ThreadQueueProcUdp(string data)
        {
            try
            {
                Debug.Print("ThreadQueueProcUdp:enter:" + data);
                ((Udp)_writerJob.Instance).UDPWriterTrace(data);
            }
            catch (Exception e)
            {
                MissedWriterEvents++;
                Debug.Print("ThreadQueueProcUdp:exception:" + e.ToString());
            }
        }

        #endregion Private Methods
    }

    /// <summary>
    /// Class WriterJob
    /// </summary>
    public class WriterJob
    {
        #region Public Fields

        public string JobName;
        public JobType JobType;
        public int LogFileMaxCount;
        public int LogFileMaxSize;
        public string LogFileName;
        public bool LogFileOverWrite;
        public Writer Writer;

        #endregion Public Fields

        #region Private Fields

        private bool _Enabled;

        #endregion Private Fields

        #region Public Properties

        /// <summary>
        /// Gets or Sets Writer Enabled.
        /// </summary>
        /// <value><c>true</c> if enabled; otherwise, /c>.</value>
        public bool Enabled
        {
            get
            {
                return _Enabled;
            }
            set
            {
                _Enabled = value;
            }
        }

        /// <summary>
        /// Gets or sets the instance.
        /// </summary>
        /// <value>The instance.</value>
        public object Instance
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether [log file auto flush].
        /// </summary>
        /// <value><c>true</c> if [log file auto flush]; otherwise, /c>.</value>
        public bool LogFileAutoFlush
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the log file max size bytes.
        /// </summary>
        /// <value>The log file max size bytes.</value>
        public long LogFileMaxSizeBytes
        {
            get;
            set;
        }

        #endregion Public Properties
    }
}