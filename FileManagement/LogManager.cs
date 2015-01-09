// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="LogManager.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.FileManagement
{
    using CDFM.Config;
    using CDFM.Engine;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Timers;

    /// <summary>
    /// Class LogManager
    /// </summary>
    public class LogManager
    {
        #region Public Fields

        public const int LOG_FILE_MAX_COUNT = 9999;

        #endregion Public Fields

        #region Private Fields

        private CDFMonitor _cdfMonitor;
        private int _currentIndex = 1;
        private WriterJob _job;
        private string _logFileBaseName = string.Empty;
        private string _logFileDirectory;
        private object LogFileLock;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FileManager" /> class.
        /// </summary>
        /// <param name="job">The job.</param>
        public LogManager(WriterJob job)
        {
            _job = job;
            _cdfMonitor = CDFMonitor.Instance;
            LogFileLock = new object();
            // TraceLogs = new string[0];
            Logs = new string[0];
            Init();
        }

        #endregion Public Constructors

        #region Public Enums

        /// <summary>
        /// Enum ManageSequentialTraceResults
        /// </summary>
        public enum ManageSequentialTraceResults
        {
            False,
            True,
            Restart
        }

        #endregion Public Enums

        #region Public Properties

        /// <summary>
        /// Gets or sets the size of the current log file.
        /// </summary>
        /// <value>The size of the current log file.</value>
        public Int64 CurrentLogFileSize
        {
            get;
            set;
        }

        //{
        //    get; set;
        //}
        /// <summary>
        /// Gets or sets the log file max size bytes.
        /// </summary>
        /// <value>The log file max size bytes.</value>
        public int LogFileMaxSizeBytes
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the log file.
        /// </summary>
        /// <value>The name of the log file.</value>
        public string LogFileName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the log file server.
        /// </summary>
        /// <value>The log file server.</value>
        public string LogFileServer
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the log file server evt.
        /// </summary>
        /// <value>The log file server evt.</value>
        public AutoResetEvent LogFileServerEvt
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the log file server MGR.
        /// </summary>
        /// <value>The log file server MGR.</value>
        public Thread LogFileServerMgr
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the log file stream.
        /// </summary>
        /// <value>The log file stream.</value>
        public StreamWriter LogFileStream
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether [logging is enabled].
        /// </summary>
        /// <value><c>true</c> if [logging is enabled]; otherwise, /c>.</value>
        public bool LoggingIsEnabled
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the logs.
        /// </summary>
        /// <value>The logs.</value>
        public string[] Logs
        {
            get;
            set;
        }

        #endregion Public Properties

        #region Private Properties

        /// <summary>
        /// Gets or sets a value indicating whether [log file server enabled].
        /// </summary>
        /// <value><c>true</c> if [log file server enabled]; otherwise, <c>false</c>.</value>
        private bool LogFileServerEnabled
        {
            get;
            set;
        }

        #endregion Private Properties

        #region Public Methods

        /// <summary>
        /// Disables the log stream.
        /// </summary>
        public void DisableLogStream()
        {
            // Close current log file and disable queue so it can be uploaded.
            LoggingIsEnabled = false;
            _job.Enabled = false;
            lock (LogFileLock)
            {
                if (LogFileStream != null)
                {
                    LogFileStream.Close();
                }
            }
        }

        /// <summary>
        /// Enables the log stream.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool EnableLogStream()
        {
            LoggingIsEnabled = true;
            _job.Enabled = true;
            if (!ManageSequentialLogs())
            {
                LoggingIsEnabled = false;
                _job.Enabled = false;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Inits this instance.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool Init()
        {
            if (string.IsNullOrEmpty(_job.LogFileName))
            {
                return false;
            }

            LogFileName = _logFileBaseName = FileManager.GetFullPath(_job.LogFileName);

            try
            {
                _logFileDirectory = Path.GetDirectoryName(FileManager.GetFullPath(_job.LogFileName));
                if (!string.IsNullOrEmpty(_logFileDirectory) && !FileManager.CheckPath(_logFileDirectory, true))
                {
                    return false;
                }
            }
            catch
            {
            }

            _logFileDirectory = string.IsNullOrEmpty(_logFileDirectory)
                                    ? AppDomain.CurrentDomain.BaseDirectory
                                    : _logFileDirectory;

            if (_job.LogFileMaxCount > LOG_FILE_MAX_COUNT |
                _job.LogFileMaxCount == 0)
            {
                _job.LogFileMaxCount = LOG_FILE_MAX_COUNT;
            }

            switch (_job.JobType)
            {
                case JobType.Csv:
                case JobType.Log:

                    EnableLogStream();
                    break;

                case JobType.Etl:

                    if (ManageSequentialTraces() == ManageSequentialTraceResults.False)
                    {
                        return false;
                    }
                    break;

                default:
                    return true;
            }

            return true;
        }

        /// <summary>
        /// determines if multiple files are needed trims files if out of range which file is
        /// current
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool ManageSequentialLogs()
        {
            lock (LogFileLock)
            {
                // Reformat logFileName to include version number
                // Search directory for existing files in format of Config.LogFileName.####.ext

                if (!LoggingIsEnabled)
                {
                    return true;
                }

                string logFile = LogFileName;
                List<string> tempList = PruneLogFiles(EnumerateLogFiles(logFile, "[0-9]{0,4}", true));

                // set current log
                if (tempList.Count > 0)
                {
                    logFile = tempList[tempList.Count - 1];
                }
                else
                {
                    // no log files so use default
                    logFile = GenerateNewLogName(logFile);
                    tempList.Add(logFile);
                }

                FileInfo logFileInfo = new FileInfo(logFile);

                // exit out if out of size or count
                if (!_job.LogFileOverWrite
                    && logFileInfo.Exists
                    && _job.LogFileMaxCount > 0
                    && tempList.Count >= _job.LogFileMaxCount
                    && _job.LogFileMaxSize > 0
                    && logFileInfo.Length >= _job.LogFileMaxSizeBytes)
                {
                    _cdfMonitor.EtwTraceStop();
                    _cdfMonitor.LogOutput("DEBUG:ManageSequentialLogs:reached max logfile size limit/count. quitting");
                    CDFMonitor.CloseCurrentSessionEvent.Set();
                    return false;
                }

                DisableLogStream();

                // We have complete logs list so now if logfilename doesnt exist, set and exit
                // File exists and has no more room
                if (FileManager.FileInUse(logFile)
                    || (_job.LogFileMaxSize > 0
                        && logFileInfo.Exists
                        && logFileInfo.Length >= _job.LogFileMaxSizeBytes))
                {
                    if (_job.LogFileMaxCount > 0
                        && tempList.Count >= _job.LogFileMaxCount)
                    {
                        // Delete oldest and generate new name
                        FileManager.DeleteFile(tempList[0]);
                        tempList.Remove(tempList[0]);
                    }

                    logFile = GenerateNewLogName(logFile);
                    tempList.Add(logFile);
                }

                CurrentLogFileSize = File.Exists(logFile) ? new FileInfo(logFile).Length : 0;

                Logs = tempList.ToArray();
                bool retval = SetLogFileName(logFile);
                LoggingIsEnabled = true;
                _job.Enabled = true;
                if (LogFileServerEvt != null)
                {
                    LogFileServerEvt.Set();
                }

                return retval;
            }
        }

        /// <summary>
        /// Manages the sequential traces.
        /// </summary>
        /// <returns>ManageSequentialTraceResults.</returns>
        public ManageSequentialTraceResults ManageSequentialTraces()
        {
            lock (LogFileLock)
            {
                ManageSequentialTraceResults results = ManageSequentialTraceResults.True;

                // get all trace files .etl and prune
                List<string> etwLogs = EnumerateLogFiles(_logFileBaseName, "[0-9]{0,4}", true);
                int currentIndex = 0;

                // get current etw index and restart etw if max is met
                foreach (string etwLog in etwLogs)
                {
                    string currentIndexString = Regex.Match(Path.GetFileName(etwLog), @".*?\.([0-9]{0,4})\.").Groups[1].Value;
                    if (_cdfMonitor.Controller != null
                        && !string.IsNullOrEmpty(currentIndexString)
                        && Int32.TryParse(currentIndexString, out currentIndex)
                        && currentIndex >= LOG_FILE_MAX_COUNT)
                    {
                        _cdfMonitor.LogOutput("ManageTraceFile: ETW log count has reached max count. restarting etw.");
                        results = ManageSequentialTraceResults.Restart;
                        break;
                    }
                }

                // get current list of cdfm files and index
                List<string> cdfmLogs = EnumerateLogFiles(_logFileBaseName, @"CDFM.[0-9]{4}", false);

                // todo fix sort for combined list. should be ok as long as cdfmlogs are added
                // before newLogs...
                List<string> allLogs = new List<string>();

                foreach (string log in cdfmLogs)
                {
                    if (!string.IsNullOrEmpty(log) & !allLogs.Contains(log))
                    {
                        allLogs.Add(log);
                    }
                }

                foreach (string log in etwLogs)
                {
                    if (!string.IsNullOrEmpty(log) & !allLogs.Contains(log))
                    {
                        allLogs.Add(log);
                    }
                }

                allLogs = PruneLogFiles(allLogs);

                Logs = allLogs.ToArray();

                if (Logs.Length < 1)
                {
                    _cdfMonitor.LogOutput("ManageTraceFile: no logs to process.");
                    return results;
                }

                if (etwLogs.Count > 0)
                {
                    LogFileName = etwLogs.Max();
                }

                // if only one newLog (log that has not been converted to *.CDFM.*) and trace is
                // running, return
                if (etwLogs.Count == 1
                    && _cdfMonitor.Controller != null
                    && _cdfMonitor.Controller.Running)
                {
                    _cdfMonitor.LogOutput("ManageTraceFile: controller running and only one file. returning.");
                    return results;
                }

                // exit out if limit reached
                if (Logs.Length >= _job.LogFileMaxCount && !_job.LogFileOverWrite)
                {
                    _cdfMonitor.LogOutput("ManageTraceFile: max file count reached. stopping trace:" +
                                            Logs.Length.ToString());
                    _cdfMonitor.EtwTraceStop();
                    CDFMonitor.CloseCurrentSessionEvent.Set();
                    return ManageSequentialTraceResults.False;
                }

                // get current list of cdfm files and index
                if (etwLogs.Count < 1)
                {
                    _cdfMonitor.LogOutput("ManageTraceFile: no new logs to process.");
                    return results;
                }

                // get next file name
                for (int i = 0; i < Logs.Length; i++)
                {
                    _cdfMonitor.LogOutput("ManageTraceFile: checking file:" + Logs[i]);

                    // if cdfm file then skip
                    if (cdfmLogs.Contains(Logs[i]))
                    {
                        continue;
                    }

                    string newFile = GenerateNewLogName(cdfmLogs.Count > 0 ? cdfmLogs.Last() :
                        Regex.Replace(_logFileBaseName, Path.GetExtension(_logFileBaseName), ".CDFM" + Path.GetExtension(_logFileBaseName)));
                    _cdfMonitor.LogOutput(string.Format("ManageSequentialTraces: renaming file {0} to {1}",
                                                          Logs[i], newFile));
                    if (!FileManager.FileInUse(Logs[i]))
                    {
                        FileManager.RenameFile(Logs[i], newFile);
                        Logs[i] = newFile;
                        cdfmLogs.Add(newFile);
                    }
                    else
                    {
                        _cdfMonitor.LogOutput(
                            string.Format("DEBUG:ManageSequentialTraces: file in use cannot rename file {0} to {1}",
                                          Logs[i], newFile));
                    }
                }

                Logs = cdfmLogs.ToArray();
                if (LogFileServerEvt != null)
                {
                    LogFileServerEvt.Set();
                }

                return results;
            }
        }

        /// <summary>
        /// Monitors the sequential traces.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ElapsedEventArgs"/> instance containing the event
        /// data.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public void MonitorSequentialTraces(object sender, ElapsedEventArgs e)
        {
            try
            {
                _cdfMonitor.LogOutput(string.Format("MonitorSequentialTraces:checking files for job:{0}", _job.JobName));
                ManageSequentialTraces();
            }
            catch (Exception ex)
            {
                _cdfMonitor.LogOutput("MonitorSequentialTraces:exception" + ex.ToString());
            }
        }

        /// <summary>
        /// Shuts down the log stream.
        /// </summary>
        public void ShutDownLogStream()
        {
            DisableLogStream();

            lock (LogFileLock)
            {
                if (LogFileStream != null)
                {
                    LogFileStream.Dispose();
                }
            }
        }

        /// <summary>
        /// Starts the managing log file server.
        /// </summary>
        public void StartManagingLogFileServer()
        {
            if (string.IsNullOrEmpty(LogFileServer))
            {
                CDFMonitor.LogOutputHandler("DEBUG:StartManagingLogFileServer:returning:no logfileserver specified.");
                return;
            }

            if (LogFileServerMgr != null)
            {
                CDFMonitor.LogOutputHandler("DEBUG:StartManagingLogFileServer:returning:thread already running.");
                return;
            }
            LogFileServerEnabled = true;
            LogFileServerEvt = new AutoResetEvent(false);
            LogFileServerMgr = new Thread(LogManager.ManageLogFileServerThreadProc);
            LogFileServerMgr.Name = "_logFileServer";
            LogFileServerMgr.IsBackground = true;
            LogFileServerMgr.Start(this);
        }

        /// <summary>
        /// Stops the managing log file server.
        /// </summary>
        public void StopManagingLogFileServer()
        {
            LogFileServerEnabled = false;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Manages the log file server.
        /// </summary>
        /// <param name="instance">The instance.</param>
        private static void ManageLogFileServerThreadProc(object instance)
        {
            LogManager fMInstance = (instance as LogManager);
            ResourceManagement rm = new ResourceManagement();
            Dictionary<string, int> logFileList = new Dictionary<string, int>();

            while (fMInstance.LogFileServerEnabled)
            {
                if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(0))
                {
                    return;
                }

                if (!fMInstance.LogFileServerEvt.WaitOne(100))
                {
                    continue;
                }

                CDFMonitor.LogOutputHandler("ManageLogFileServer:received event");
                if (rm.CheckResourceCredentials(fMInstance.LogFileServer) != ResourceManagement.CommandResults.Successful)
                {
                    CDFMonitor.LogOutputHandler("Fail:ManageLogFileServer:no access to server. exiting");
                    return;
                }

                lock (fMInstance.LogFileLock)
                {
                    foreach (string file in fMInstance.Logs)
                    {
                        if (!logFileList.ContainsKey(file) && String.Compare(file, fMInstance.LogFileName) != 0)
                        {
                            logFileList.Add(file, 0);
                        }
                    }

                    foreach (string file in fMInstance.Logs)
                    {
                        if (!logFileList.ContainsKey(file))
                        {
                            logFileList.Add(file, 0);
                        }
                    }

                    // see if any have been removed and check state
                    foreach (KeyValuePair<string, int> file in new Dictionary<string, int>(logFileList))
                    {
                        if (!fMInstance.Logs.Contains(file.Key) && !fMInstance.Logs.Contains(file.Key))
                        {
                            CDFMonitor.LogOutputHandler(string.Format("DEBUG:ManageLogFileServer:removing stale file:{0} with state:{1}", file.Key, file.Value));
                            logFileList.Remove(file.Key);
                        }
                    }
                }

                if (logFileList.Count < 1)
                {
                    continue;
                }

                // copy any files set to 0
                foreach (KeyValuePair<string, int> file in new Dictionary<string, int>(logFileList))
                {
                    if (file.Value != 0)
                    {
                        continue;
                    }

                    PackageManager pW = new PackageManager();
                    if (pW.CreatePackage(new string[] { file.Key }, AppDomain.CurrentDomain.BaseDirectory))
                    {
                        // Rename zip
                        string newFileName = string.Format("{0}\\{1}.zip", Path.GetDirectoryName(file.Key), Path.GetFileName(file.Key));

                        if (FileManager.RenameFile(pW.ZipFile, newFileName)
                            && FileManager.CopyFile(newFileName, string.Format("{0}\\{1}", fMInstance.LogFileServer, Environment.MachineName), true))
                        {
                            CDFMonitor.LogOutputHandler(string.Format("DEBUG:ManageLogFileServer:file copied:{0}", file.Key));
                            logFileList[file.Key] = 1;
                        }

                        FileManager.DeleteFile(newFileName);
                    }
                }
            }
        }

        /// <summary>
        /// enumerates base logfile name and logfilename with index ex. cdfmonitor.log and
        /// cdfmonitor.0000.log
        /// </summary>
        /// <param name="logFile">base log file name</param>
        /// <param name="pattern">The pattern.</param>
        /// <returns>list of files found in order of lastwrite</returns>
        private List<string> EnumerateLogFiles(string logFile, string pattern, bool includeBase)
        {
            logFile = FileManager.GetFullPath(logFile);
            string logFilePath = string.IsNullOrEmpty(Path.GetDirectoryName(logFile))
                || !Directory.Exists(Path.GetDirectoryName(logFile))
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetDirectoryName(logFile);

            if (string.IsNullOrEmpty(logFile))
            {
                return (new List<string>());
            }

            ResourceManagement rm = new ResourceManagement();
            if (rm.CheckResourceCredentials(logFile) != ResourceManagement.CommandResults.Successful)
            {
                return new List<string>();
            }

            if (rm.GetPathType(Path.GetDirectoryName(logFile)) == ResourceManagement.ResourceType.Unc)
            {
                rm.ConnectUncPath(Path.GetDirectoryName(logFile));
            }

            List<FileInfo> tempList = new List<FileInfo>();
            DirectoryInfo dI = new DirectoryInfo(logFilePath);

            string logFileBaseName = Regex.Replace(Path.GetFileName(logFile), @"\.([0-9]{1,4})\..{1,3}.{0,3}$", Path.GetExtension(logFile));

            pattern = string.Format(@"(?<beginning>{0})\.(?<index>{1})(?<end>{2})$", Path.GetFileNameWithoutExtension(logFileBaseName), pattern, Path.GetExtension(logFile));

            // Add all files with index in name
            tempList.AddRange(dI.GetFiles().Where(f => Regex.IsMatch(f.Name, pattern, RegexOptions.IgnoreCase)));

            // Add original name if it exists and different
            if (includeBase & !tempList.Any(i => i.FullName == logFile) & FileManager.FileExists(logFile))
            {
                // Add original name
                tempList.Add(new FileInfo(logFile));
            }
            else if (includeBase & !tempList.Any(i => i.FullName == string.Format("{0}\\{1}", logFilePath, logFileBaseName)) &
                FileManager.FileExists(string.Format("{0}\\{1}", logFilePath, logFileBaseName)))
            {
                // Add base name
                tempList.AddRange(dI.GetFiles(logFileBaseName, SearchOption.TopDirectoryOnly));
            }

            //tempList.Sort((x, y) => DateTime.Compare(x.CreationTime, y.CreationTime));
            tempList.Sort((x, y) => DateTime.Compare(x.LastWriteTime, y.LastWriteTime));

            _cdfMonitor.LogOutput("DEBUG:EnumerateLogFiles file list:");
            foreach (FileInfo path in tempList)
            {
                _cdfMonitor.LogOutput("DEBUG:EnumerateLogFiles file:" + path.Name);
            }

            if (rm.GetPathType(logFile) == ResourceManagement.ResourceType.Unc)
            {
                rm.DisconnectUncPath(logFile);
            }

            return (tempList.Select(x => x.FullName).ToList());
        }

        /// <summary>
        /// Generates the new name of the log.
        /// </summary>
        /// <param name="logFile">The log file.</param>
        /// <returns>System.String.</returns>
        private string GenerateNewLogName(string logFile)
        {
            _cdfMonitor.LogOutput("DEBUG:GenerateNewLogName:Enter:" + logFile);

            //130728 if logfilemaxcount = 0 then just keep same name and return
            // if count = 1 go ahead and increment so there will only be one file (same as count 0) but if overwrite, will see index on file incrementing.
            if (_cdfMonitor.Config.AppSettings.LogFileMaxCount == 0 && _cdfMonitor.Config.AppSettings.LogFileMaxSize == 0)
            {
                return logFile;
            }

            string directoryName = !string.IsNullOrEmpty(Path.GetDirectoryName(logFile)) ? Path.GetDirectoryName(logFile) + "\\" : AppDomain.CurrentDomain.BaseDirectory;
            logFile = Path.GetFileName(logFile);

            // string currentIndexString = Regex.Match(Path.GetFileName(logFile), @".*?\.([0-9]*)\.").Groups[1].Value;
            Group currentIndexMatch = Regex.Match(logFile, @"\.([0-9]{1,4})\..{1,3}.{0,3}$").Groups[1];
            string currentIndexString = currentIndexMatch.Value;

            // Read 4 digit number and increment
            if (!string.IsNullOrEmpty(currentIndexString))
            {
                _currentIndex = Convert.ToInt32(currentIndexString);

                // reset max counter if needed
                _currentIndex = _currentIndex > LOG_FILE_MAX_COUNT ? 1 : _currentIndex;
                _cdfMonitor.LogOutput("DEBUG:GenerateNewLogName _currentIndex from string: " + currentIndexString);
                // logFile = directoryName + Path.GetFileName(Regex.Replace(logFile, @"\.([0-9]*)\.", "." + _currentIndex.ToString("D4") + "."));
                logFile = logFile.Remove(currentIndexMatch.Index, currentIndexMatch.Length);
                logFile = logFile.Insert(currentIndexMatch.Index, _currentIndex.ToString("D4"));
            }
            else
            {
                // no digits in name. this only happens when moving from base file name with no
                // digits to 0001
                logFile = Path.GetFileNameWithoutExtension(logFile)
                          + "." + _currentIndex.ToString("D4") + Path.GetExtension(logFile);

                currentIndexMatch = Regex.Match(logFile, @"\.([0-9]{1,4})\..{1,3}.{0,3}$").Groups[1];
                currentIndexString = currentIndexMatch.Value;
                _cdfMonitor.LogOutput("DEBUG:GenerateNewLogName starting index: " + logFile);
            }

            int count = 0;
            while (count <= LOG_FILE_MAX_COUNT)
            {
                if (FileManager.FileExists(directoryName + logFile))
                {
                    _currentIndex = _currentIndex >= LOG_FILE_MAX_COUNT ? 1 : ++_currentIndex;
                    logFile = logFile.Remove(currentIndexMatch.Index, currentIndexMatch.Length);
                    logFile = logFile.Insert(currentIndexMatch.Index, _currentIndex.ToString("D4"));
                    count++;
                    continue;
                }

                break;
            }

            _cdfMonitor.LogOutput("DEBUG:GenerateNewLogName:return:" + logFile);
            return directoryName + Path.GetFileName(logFile);
        }

        /// <summary>
        /// Prunes the log files.
        /// </summary>
        /// <param name="fileList">The file list.</param>
        /// <returns>List{System.String}.</returns>
        private List<string> PruneLogFiles(List<string> fileList)
        {
            // Delete logs if there are too many and are overwriting
            string[] tempLogs = fileList.ToArray();
            try
            {
                int i = 0;

                while (fileList.Count > 1 && fileList.Count > _job.LogFileMaxCount)
                {
                    // Assuming list already sorted by creationtime
                    _cdfMonitor.LogOutput("PruneLogFiles:deleting logfile for circular:" + tempLogs[i]);
                    FileManager.DeleteFile(tempLogs[i]);
                    fileList.Remove(tempLogs[i]);
                    i++;
                }

                _cdfMonitor.LogOutput("DEBUG:PruneLogFiles file list:");
                foreach (string path in fileList)
                {
                    _cdfMonitor.LogOutput("DEBUG:" + path);
                }

                return fileList;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("DEBUG:PruneLogFiles:Exception:" + e.ToString());
                return (new List<string>());
            }
        }

        /// <summary>
        /// Sets the name of the log file.
        /// </summary>
        /// <param name="logFile">The log file.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool SetLogFileName(string logFile)
        {
            try
            {
                lock (LogFileLock)
                {
                    if (LogFileStream != null)
                    {
                        LogFileStream.Close();
                    }

                    LogFileStream = File.AppendText(logFile);
                    LogFileName = logFile;
                }
                return true;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("DEBUG:SetLogFileName:exception:" + e.ToString());
                try
                {
                    // try working directory if above fails
                    lock (LogFileLock)
                    {
                        logFile = Path.GetFileName(logFile);
                        if (LogFileStream != null)
                        {
                            LogFileStream.Close();
                        }

                        LogFileStream = File.AppendText(string.Format("{0}\\{1}", AppDomain.CurrentDomain.BaseDirectory, logFile));
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion Private Methods
    }
}