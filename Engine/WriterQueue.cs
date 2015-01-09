// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="WriterQueue.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Engine
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Class LoggerQueue
    /// </summary>
    public class WriterQueue
    {
        #region Private Fields

        private readonly Queue<string> _buffer = new Queue<string>();
        private Int64 _bufferCounter;
        private bool _bufferEnabled;
        private Int32 _bufferLines = 1000;
        private volatile List<WriterJob> _writerJobs = new List<WriterJob>();

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="WriterQueue" /> class.
        /// </summary>
        public WriterQueue()
        {
            EnableBuffer(true);
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// Gets or sets the loggers.
        /// </summary>
        /// <value>The loggers.</value>
        public List<WriterJob> WriterJobs
        {
            get { return _writerJobs; }
            set { _writerJobs = value; }
        }

        public Int64 WriterQueueErrors { get; set; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Adds the logger.
        /// </summary>
        /// <param name="jobType">Type of the job.</param>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="param">The param.</param>
        /// <returns>LoggerJob.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public WriterJob AddJob(JobType jobType, string jobName, object param = null)
        {
            WriterJob writerJob = new WriterJob();

            // only setup one log of each type for now unless csv and no duplicate names
            Debug.Assert(!_writerJobs.Exists(x => x.JobType == jobType
                && jobType != JobType.Csv
                || x.JobName == jobName));

            switch (jobType)
            {
                case JobType.Console:
                case JobType.Etw:
                case JobType.Gui:
                    writerJob = NewWriterJob(jobType, jobName, param);
                    break;

                case JobType.Etl:
                case JobType.Csv:
                case JobType.Log:
                    writerJob = NewWriterJobFile(jobType, jobName, param);
                    break;

                case JobType.Network:
                    writerJob = NewWriterJobNet(jobType, jobName, param);
                    break;

                default:
                    break;
            }

            return writerJob;
        }

        /// <summary>
        /// Adds the trace to buffer.
        /// </summary>
        /// <param name="trace">The trace.</param>
        public void AddTraceToBuffer(string trace)
        {
            if (_bufferEnabled)
            {
                lock (_buffer)
                {
                    _buffer.Enqueue(trace);
                    while (_buffer.Count > _bufferLines)
                    {
                        _buffer.Dequeue();
                    }
                    _bufferCounter++;
                }
            }
        }

        /// <summary>
        /// Restarts the streams.
        /// </summary>
        public void ClearBuffer()
        {
            lock (_buffer)
            {
                _buffer.Clear();
            }
        }

        /// <summary>
        /// Clears the queues.
        /// </summary>
        public void ClearQueues()
        {
            for (int i = 0; i < _writerJobs.Count; i++)
            {
                if (_writerJobs[i].Writer.Queue != null)
                {
                    _writerJobs[i].Writer.Queue.ClearQueue();
                }
            }
        }

        /// <summary>
        /// Disables the queues.
        /// </summary>
        public void DisableQueues()
        {
            for (int i = 0; i < _writerJobs.Count; i++)
            {
                if (_writerJobs[i].Writer.Queue != null)
                {
                    _writerJobs[i].Writer.Queue.DisableQueue();
                }
            }
        }

        /// <summary>
        /// Dumps buffer but does not clear
        /// </summary>
        /// <param name="count">0 to return all records. any other number to return that many from
        /// tail. (not from beginning)</param>
        /// <returns>string[count]</returns>
        public string[] DumpBuffer(int count)
        {
            Debug.Print("DumpBuffer:enter:" + count.ToString());
            string[] retarray = new string[0];
            int indexcount = 0;
            lock (_buffer)
            {
                if (count > 0 && _buffer.Count - count > 0)
                {
                    indexcount = _buffer.Count - count;
                }
                else
                {
                    count = _buffer.Count;
                }

                // Debug.Print(string.Format("DEBUGDEBUG:DumpBuffer:count:{0}:indexcount:{1}",
                // count, indexcount));
                retarray = new string[count];
                _buffer.CopyTo(retarray, indexcount);
            }

            return (retarray);
        }

        /// <summary>
        /// Dumps the buffer to queue.
        /// </summary>
        /// <param name="writerJob">The logger job.</param>
        public void DumpBufferToQueue(WriterJob writerJob)
        {
            // catch up with queue
            if (writerJob != null && writerJob.Enabled)
            {
                foreach (string trace in DumpBuffer(_bufferLines))
                {
                    writerJob.Writer.Queue.Queue(trace);
                }
            }
        }

        /// <summary>
        /// Enables the buffer.
        /// </summary>
        /// <param name="enable">if set to <c>true</c> [enable].</param>
        public void EnableBuffer(bool enable)
        {
            _bufferEnabled = enable;

            if (!enable)
            {
                ClearBuffer();
            }
        }

        /// <summary>
        /// Enables the queues.
        /// </summary>
        public void EnableQueues()
        {
            for (int i = 0; i < _writerJobs.Count; i++)
            {
                if (_writerJobs[i].Writer.Queue != null)
                {
                    _writerJobs[i].Writer.Queue.EnableQueue();
                }
            }
        }

        /// <summary>
        /// Queues the lengths.
        /// </summary>
        /// <returns>Dictionary{System.StringSystem.Int32}.</returns>
        public Dictionary<string, int> QueueLengths()
        {
            Dictionary<string, int> dictionary = new Dictionary<string, int>();
            foreach (WriterJob writerJob in _writerJobs)
            {
                if (writerJob.Writer.Queue != null)
                {
                    dictionary.Add(writerJob.JobName, writerJob.Writer.Queue.QueueLength());
                }
            }
            return dictionary;
        }

        /// <summary>
        /// Queues the lengths total.
        /// </summary>
        /// <returns>Int64.</returns>
        public Int64 QueueLengthsTotal()
        {
            Int64 retval = 0;
            foreach (WriterJob writerJob in _writerJobs)
            {
                if (writerJob.Writer.Queue != null)
                {
                    retval += writerJob.Writer.Queue.QueueLength();
                }
            }
            return retval;
        }

        /// <summary>
        /// Flushes the streams.
        /// </summary>
        /// <param name="trace">The trace.</param>
        /// <param name="jobOutputType">Type of the job output.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        /// <exception cref="System.ArgumentException">QueueOutput:Invalid JobType:</exception>
        public bool QueueOutput(string trace, JobOutputType jobOutputType)
        {
            try
            {
                bool retval = false;
                if (_bufferEnabled)
                {
                    AddTraceToBuffer(trace);
                }

                for (int i = 0; i < _writerJobs.Count; i++)
                {
                    switch (_writerJobs[i].JobType)
                    {
                        // Always write to console
                        case JobType.Console:
                        case JobType.Gui:
                            retval = true;
                            break;

                        case JobType.Csv:
                        case JobType.Network:
                            if ((jobOutputType & JobOutputType.Trace) == JobOutputType.Trace)
                            {
                                retval = true;
                                break;
                            }

                            continue;

                        case JobType.Log:
                            if ((jobOutputType & JobOutputType.Log) == JobOutputType.Log)
                            {
                                retval = true;
                                break;
                            }

                            continue;

                        case JobType.Etw:
                            if ((jobOutputType & JobOutputType.Etw) == JobOutputType.Etw)
                            {
                                retval = true;
                                break;
                            }

                            continue;

                        case JobType.Etl:
                        case JobType.Unknown:
                        default:
                            Debug.Print("QueueOutput:Invalid JobType:{0}", _writerJobs[i].JobType);
                            throw new ArgumentException("QueueOutput:Invalid JobType:" + _writerJobs[i].JobType.ToString());
                    }

                    if (retval && _writerJobs[i].Writer.Queue.Queue(trace))
                    {
                        _writerJobs[i].Writer.QueuedEvents++;
                    }
                    else
                    {
                        Debug.Print("QueueOutput:Error:MissedQueueEvents:{0}", _writerJobs[i].JobType);
                        _writerJobs[i].Writer.MissedQueueEvents++;
                    }
                }

                return retval;
            }
            catch (Exception e)
            {
                Debug.Print("QueueOutput:Exception:{0}", e.ToString());
                WriterQueueErrors++;
                return false;
            }
        }

        /// <summary>
        /// Removes the job.
        /// </summary>
        /// <param name="writerJob">The logger job.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool RemoveJob(WriterJob writerJob)
        {
            if (writerJob == null)
            {
                return true;
            }

            writerJob.Enabled = false;
            if (writerJob.Writer == null)
            {
                Debug.Print("RemoveJob:error:writerJob == null. returning early.");
                return true;
            }

            if (_writerJobs.Contains(writerJob))
            {
                _writerJobs.Remove(writerJob);
            }

            if (writerJob.Writer.Queue != null)
            {
                writerJob.Writer.Queue.DisableQueue();
                writerJob.Writer.Queue.ClearQueue();

                writerJob.Writer.Queue.Shutdown();
            }

            if (writerJob.Writer.LogManager != null)
            {
                writerJob.Writer.LogManager.StopManagingLogFileServer();
                writerJob.Writer.LogManager.ShutDownLogStream();
            }

            return true;
        }

        /// <summary>
        /// Shuts down queues.
        /// </summary>
        public void ShutDownQueues()
        {
            for (int i = 0; i < _writerJobs.Count; i++)
            {
                _writerJobs[i].Writer.Queue.Shutdown();
            }
            _writerJobs.Clear();
        }

        #endregion Public Methods

        #region Internal Methods

        /// <summary>
        /// Sets the max queue lengths.
        /// </summary>
        /// <param name="queueLength">Length of the queue.</param>
        internal void SetMaxQueueLengths(int queueLength)
        {
            for (int i = 0; i < _writerJobs.Count; i++)
            {
                if (_writerJobs[i].Writer.Queue != null)
                {
                    _writerJobs[i].Writer.Queue.MaxQueueLength = queueLength;
                }
            }
        }

        #endregion Internal Methods

        #region Private Methods

        /// <summary>
        /// Adds the logger job.
        /// </summary>
        /// <param name="writerJob">The logger job.</param>
        private void AddWriterJob(WriterJob writerJob)
        {
            if (_writerJobs.Exists(x => x.JobName == writerJob.JobName))
            {
                _writerJobs.RemoveAll(x => x.JobName == writerJob.JobName);
            }

            _writerJobs.Add(writerJob);
        }

        /// <summary>
        /// News the logger job console.
        /// </summary>
        /// <param name="jobType">Type of the job.</param>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="instance">The param.</param>
        /// <returns>LoggerJob.</returns>
        private WriterJob NewWriterJob(JobType jobType, string jobName, object instance = null)
        {
            WriterJob writerJob = new WriterJob()
            {
                JobName = jobName,
                JobType = jobType,
                Instance = instance
            };

            writerJob.Writer = new Writer(writerJob);
            AddWriterJob(writerJob);
            DumpBufferToQueue(writerJob);
            return writerJob;
        }

        /// <summary>
        /// News the logger job file.
        /// </summary>
        /// <param name="jobType">Type of the job.</param>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>LoggerJob.</returns>
        private WriterJob NewWriterJobFile(JobType jobType, string jobName, object destination)
        {
            if (string.IsNullOrEmpty((string)destination))
            {
                // dont add if there isnt a file name
                return new WriterJob();
            }

            WriterJob writerJob = new WriterJob()
            {
                JobName = jobName,
                JobType = jobType,
                LogFileName = (string)destination,
                LogFileMaxCount = CDFMonitor.Instance.Config.AppSettings.LogFileMaxCount,
                LogFileMaxSize = CDFMonitor.Instance.Config.AppSettings.LogFileMaxSize,
                LogFileMaxSizeBytes = CDFMonitor.Instance.Config.AppSettings.LogFileMaxSize * 1024 * 1024,
                LogFileOverWrite = CDFMonitor.Instance.Config.AppSettings.LogFileOverWrite,
                LogFileAutoFlush = CDFMonitor.Instance.Config.AppSettings.LogFileAutoFlush
            };

            writerJob.Writer = new Writer(writerJob);

            if (jobType == JobType.Csv | jobType == JobType.Log)
            {
                AddWriterJob(writerJob);
                if (jobType == JobType.Log)
                {
                    DumpBufferToQueue(writerJob);
                }
            }

            return writerJob;
        }

        /// <summary>
        /// News the logger job net.
        /// </summary>
        /// <param name="jobType">Type of the job.</param>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="param">The param.</param>
        /// <returns>LoggerJob.</returns>
        private WriterJob NewWriterJobNet(JobType jobType, string jobName, object param)
        {
            WriterJob writerJob = new WriterJob()
            {
                JobName = jobName,
                JobType = jobType,
                Instance = param
            };

            writerJob.Writer = new Writer(writerJob);
            AddWriterJob(writerJob);

            return writerJob;
        }

        #endregion Private Methods
    }
}