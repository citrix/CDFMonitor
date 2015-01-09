// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="EtwTraceConsumer.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************

//++
//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//
// Module Name:
//
//  EtwTraceConsumer.cs
//
// Abstract:
//
//  This module implements the EtwTraceConsumer class.
//
//--

namespace CDFM.Trace
{
    using CDFM.Engine;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    /// Implementation of a real-time ETW tracing session for the benefit of the LoggerTest unit
    /// tests.
    /// </summary>
    public sealed class EtwTraceConsumer
    {
        #region Private Fields

        private readonly NativeMethods.EventCallback _eventCallback;
        private readonly NativeMethods.EventTraceBufferCallback _eventTraceBufferCallback;
        private readonly ulong[] _handle = new ulong[1];
        private readonly string _sessionName;
        private NativeMethods.EventTraceLogFile _bufferCallback;
        private uint _missedControllerEvents;
        private Int64 _onBufferReadCount;
        private Int64 _onRaiseEventReadCount;
        private int _processTraceRetry = 100;
        private bool _running;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// ctor.
        /// </summary>
        /// <param name="sessionName">Name of the session.</param>
        public EtwTraceConsumer(string sessionName)
        {
            Debug.Assert(!String.IsNullOrEmpty(sessionName), "!String.IsNullOrEmpty(sessionName)");
            CDFMonitor.LogOutputHandler("DEBUG:EtwTraceConsumer.ctor: new trace:" + sessionName);
            _sessionName = sessionName;
            _eventTraceBufferCallback = OnBufferRead;
            _eventCallback = OnTraceEvent;
        }

        #endregion Public Constructors

        #region Public Events

        /// <summary>
        /// Raised when an ETW event is consumed.
        /// </summary>
        public event EventHandler<EventReadEventArgs> EventRead;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Gets the buffer callback.
        /// </summary>
        /// <value>The buffer callback.</value>
        public NativeMethods.EventTraceLogFile BufferCallback
        {
            get { return _bufferCallback; }
        }

        /// <summary>
        /// Gets the missed controller events.
        /// </summary>
        /// <value>The missed controller events.</value>
        public uint MissedControllerEvents
        {
            get { return _missedControllerEvents; }

            //set { _missedControllerEvents = value; }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="EtwTraceConsumer" /> is running.
        /// </summary>
        /// <value><c>true</c> if running; otherwise, /c>.</value>
        public bool Running
        {
            get { return _running; }

            //set { _running = value; }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Releases unmanaged resources.
        /// </summary>
        public void CloseTrace()
        {
            CDFMonitor.LogOutputHandler("DEBUG:CloseTrace: entry");
            if (_running)
            {
                _running = false;
            }
            else
            {
                return;
            }

            uint processResult = 0;
            int count = 100;

            while (count > 0)
            {
                if (_handle[0] != 0 && NativeMethods.IsValidHandle(_handle[0]))
                {
                    CDFMonitor.LogOutputHandler("DEBUG:Consumer.CloseTrace: closing trace:" + processResult.ToString());
                    processResult = NativeMethods.CloseTrace(_handle[0]);

                    if (processResult == NativeMethods.ERROR_SUCCESS
                        || processResult == NativeMethods.ERROR_CANCELLED
                        || processResult == NativeMethods.ERROR_CTX_CLOSE_PENDING
                        || processResult == NativeMethods.ERROR_INVALID_HANDLE)
                    {
                        _handle[0] = 0;
                        return;
                    }
                }

                Thread.Sleep(100);
                count--;
            }

            _handle[0] = 0;
            CDFMonitor.LogOutputHandler("CloseTrace:Failed to close trace:" + processResult.ToString());
        }

        /// <summary>
        /// Opens an real-time ETW session for reading.
        /// </summary>
        /// <param name="logFileName">Name of the log file.</param>
        public void OpenTrace(string logFileName = null)
        {
            Debug.Assert(_handle[0] == 0, "_handle == 0");
            _onBufferReadCount = 0;

            var logfile = new NativeMethods.EventTraceLogFile();
            logfile.BufferCallback = _eventTraceBufferCallback;
            logfile.EventCallback = _eventCallback;
            if (string.IsNullOrEmpty(logFileName))
            {
                logfile.LoggerName = _sessionName;
                logfile.LogFileMode = (uint)NativeMethods.EventTraceFileMode.RealTime;
            }
            else
            {
                logfile.LogfileName = logFileName;
            }
            CDFMonitor.LogOutputHandler("DEBUG:Consumer.OpenTrace: opening trace");

            // Open sessions with Etw OpenTrace method
            _handle[0] = NativeMethods.OpenTrace(ref logfile);

            int lastError = Marshal.GetLastWin32Error();

            if (!NativeMethods.IsValidHandle(_handle[0]))
            {
                //throw new Win32Exception(lastError);
                CDFMonitor.LogOutputHandler("OpenTrace:Failed to open trace:" + lastError.ToString());
                _running = false;
            }
            else
            {
                _running = true;
            }
        }

        /// <summary>
        /// Starts reading the ETW session events.
        /// </summary>
        /// <param name="state">The state.</param>
        public void ProcessTrace(object state)
        {
            Debug.Assert(_handle[0] != 0 && NativeMethods.IsValidHandle(_handle[0]),
                         "_handle[0] != 0 && NativeMethods.IsValidHandle(_handle[0])");

            long startFileTime = 0;
            long stopFileTime = 0;
            uint ret = 0;

            // Sometimes processtrace will return early presumably due to network issues or latency so keep retrying
            while (_running && _processTraceRetry > 0 && _handle[0] != 0)
            {
                if (_bufferCallback.LogfileHeader.BuffersWritten > 0
                    && (_onBufferReadCount == _bufferCallback.BuffersRead))
                {
                    break;
                }

                startFileTime = _bufferCallback.LogfileHeader.StartTime;
                CDFMonitor.LogOutputHandler(string.Format("ProcessTrace (re)starting ProcessTrace:{0}:{1}", startFileTime, _sessionName));
                ret = NativeMethods.ProcessTrace(_handle, _handle.Length, ref startFileTime, ref stopFileTime);
                _processTraceRetry--;

                if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000)) return;
            }

            // Only call CloseTrace after ProcessTrace completes
            CloseTrace();
            CDFMonitor.LogOutputHandler("ProcessTrace return: " + ret);
        }

        /// <summary>
        /// Initiates reading of the ETW session events.
        /// </summary>
        public void ProcessTraceAsync()
        {
            ThreadPool.QueueUserWorkItem(ProcessTrace);
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Returns true to ensure that events continue to be processed.
        /// </summary>
        /// <param name="eventTraceLogfile">The event trace logfile.</param>
        /// <returns>true</returns>
        /// <remarks>
        /// This method is called by ETW for each buffer in this session.
        /// </remarks>
        private bool OnBufferRead(ref NativeMethods.EventTraceLogFile eventTraceLogfile)
        {
            _missedControllerEvents = eventTraceLogfile.LogfileHeader.BufferUnion.EventsLost;

            // Always return true to continue processing events
            _bufferCallback = eventTraceLogfile;
            _onBufferReadCount++;
            return _running;
        }

        /// <summary>
        /// De-serializes the user data associated with each ETW event in this session.
        /// </summary>
        /// <param name="eventTrace">ETW native representation of the event</param>
        /// <remarks>
        /// This method is called by ETW for each event in this session.
        /// </remarks>
        private unsafe void OnTraceEvent(ref NativeMethods.EventTrace eventTrace)
        {
            var pData = (byte*)eventTrace.MofData;
            var length = (int)eventTrace.MofLength;

            if (pData != null)
            {
                using (var stream = new UnmanagedMemoryStream(pData, length, length, FileAccess.Read))
                {
                    var breader = new BinaryReader(stream);

                    int traceFunc;
                    breader.BaseStream.Position = 0;

                    // 130623 not sure if this always the case but citrix user traces have func id
                    // in first two bytes and appear to have 'Type' 255
                    if (eventTrace.Type == 255)
                    {
                        // this works for user traces
                        traceFunc = BitConverter.ToInt16(breader.ReadBytes(2), 0); //.ReadByte();
                    }
                    else
                    {
                        // this works for kernel traces
                        traceFunc = eventTrace.Type;
                    }

                    byte[] eventStringBytes = breader.ReadBytes((int)(breader.BaseStream.Length)); // / 2);

                    RaiseEventRead(eventTrace.Guid, eventTrace, eventStringBytes, traceFunc);
                    breader.Close();
                }
            }
        }

        /// <summary>
        /// Raises the EventRead event.
        /// </summary>
        /// <param name="eventGuid">Guid associated with the event</param>
        /// <param name="eventTrace">The event trace.</param>
        /// <param name="eventStringBytes">The event string bytes.</param>
        /// <param name="traceFunc">The trace func.</param>
        private void RaiseEventRead(Guid eventGuid, NativeMethods.EventTrace eventTrace, byte[] eventStringBytes,
            int traceFunc)
        {
            _onRaiseEventReadCount++;
            EventHandler<EventReadEventArgs> eventRead = EventRead;
            if (eventRead != null)
            {
                eventRead(this, new EventReadEventArgs(eventGuid, eventTrace, eventStringBytes, traceFunc));
            }
        }

        #endregion Private Methods
    }

    /// <summary>
    /// Encapsulates the information associated with an ETW event.
    /// </summary>
    public sealed class EventReadEventArgs : EventArgs
    {
        #region Private Fields

        private readonly Guid _eventGuid;
        private readonly byte[] _eventStringBytes;
        private readonly NativeMethods.EventTrace _eventTrace;
        private readonly int _traceFunc;
        private byte _level = 16;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// ctor.
        /// </summary>
        /// <param name="eventGuid">The event GUID.</param>
        /// <param name="eventTrace">The event trace.</param>
        /// <param name="eventStringBytes">The event string bytes.</param>
        /// <param name="traceFunc">The trace func.</param>
        public EventReadEventArgs(Guid eventGuid,
            NativeMethods.EventTrace eventTrace, byte[] eventStringBytes, int traceFunc)
        {
            _eventGuid = eventGuid;
            _eventStringBytes = eventStringBytes;
            _eventTrace = eventTrace;
            _traceFunc = traceFunc;
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// Guid associated with the event.
        /// </summary>
        /// <value>The event GUID.</value>
        public Guid EventGuid
        {
            get { return _eventGuid; }
        }

        /// <summary>
        /// Gets the event trace.
        /// </summary>
        /// <value>The event trace.</value>
        public NativeMethods.EventTrace EventTrace
        {
            get { return _eventTrace; }
        }

        /// <summary>
        /// Level associated with the event.
        /// </summary>
        /// <value>The level.</value>
        public byte Level
        {
            get { return _level; }
        }

        /// <summary>
        /// Gets the trace func.
        /// </summary>
        /// <value>The trace func.</value>
        public int TraceFunc
        {
            get { return _traceFunc; }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Events the string bytes.
        /// </summary>
        /// <returns>System.Byte[][].</returns>
        public byte[] EventStringBytes()
        {
            return _eventStringBytes;
        }

        #endregion Public Methods
    }
}