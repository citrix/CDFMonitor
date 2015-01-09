// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="EtwTraceController.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************

//++
//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//
// Module Name:
//
//  EtwTraceController.cs
//
// Abstract:
//
//  This module implements the EtwTraceController class.
//
//--

namespace CDFM.Trace
{
    using CDFM.Engine;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Implementation of controller.
    /// </summary>
    public class EtwTraceController
    {
        #region Public Fields

        public const int ETW_MAX_SESSIONS = 8;

        #endregion Public Fields

        #region Private Fields

        private NativeMethods.publicGuid _guid;
        private ulong _handle;
        private bool _isKernelController;
        private string _sessionName;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// ctor.
        /// </summary>
        /// <param name="sessionName">Name of the session.</param>
        /// <param name="guid">The GUID.</param>
        public EtwTraceController(string sessionName, Guid guid)
        {
            Debug.Assert(!String.IsNullOrEmpty(sessionName), "!String.IsNullOrEmpty(sessionName)");
            CDFMonitor.LogOutputHandler("DEBUG:EtwTraceController.ctor: new trace:" + sessionName);
            _sessionName = sessionName;
            if (String.Compare(_sessionName, Properties.Resources.KernelSessionName, true) == 0)
            {
                // then its a controller for kernel
                _isKernelController = true;
            }

            //_strguid = strGUID;
            _guid = new NativeMethods.publicGuid(guid.ToByteArray());
        }

        #endregion Public Constructors

        #region Public Properties

        public Int64 MissedControllerEvents { get; set; }

        public bool Running { get; set; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Cleans the trace.
        /// </summary>
        /// <param name="sessionName">Name of the session.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool CleanTrace(string sessionName)
        {
            try
            {
                EtwTraceConsumer consumer;
                if (QueryTrace(sessionName))
                {
                    CDFMonitor.LogOutputHandler("DEBUG:ETWController.CleanTrace:stopping trace:" + sessionName);
                    consumer = new EtwTraceConsumer(sessionName);

                    // Clean up old session if something went wrong previous
                    consumer.CloseTrace();
                    StopTrace(sessionName);
                }
                return true;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("CleanTrace:exception " + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Enable/Disable the ETW session.
        /// </summary>
        /// <param name="enable">Indicates if the session should be enabled or disabled</param>
        /// <param name="strguid">The strguid.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool EnableTrace(bool enable, string strguid)
        {
            Debug.Assert(!_isKernelController, "!_isKernelController");
            NativeMethods.publicGuid guid = new NativeMethods.publicGuid();
            try
            {
                CDFMonitor.LogOutputHandler(string.Format("DEBUG:EnableTrace:enabling trace:strguid:{0}", strguid));
                guid = new NativeMethods.publicGuid(new Guid(strguid).ToByteArray());
                Debug.Assert(_handle != 0 && NativeMethods.IsValidHandle(_handle),
                             "_handle != 0 && NativeMethods.IsValidHandle(_handle)");

                NativeMethods.EventTraceProperties properties = CommonEventTraceProperties();

                if (properties.EnableFlags != 0)
                {
                    CDFMonitor.LogOutputHandler(string.Format("EnableTrace:KernelFlags configured/enabled. overriding EnableFlags and modules. returning:{0}", strguid));
                    return true;
                }

                uint flags = 0xffffffff;
                uint processResult = NativeMethods.EnableTrace(enable ? 1U : 0U, flags /* enableFlag */, (uint)CDFMonitor.Instance.Config.AppSettings.LogLevel /* enableLevel */, ref guid, _handle);

                if (processResult != NativeMethods.ERROR_SUCCESS
                    && processResult != NativeMethods.ERROR_CANCELLED)
                {
                    CDFMonitor.LogOutputHandler("DEBUG:EnableTrace:enabling trace error:" + processResult.ToString());
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler(string.Format("EnableTrace: exception enabling trace error:{0} guid:{1}", e, guid.ToString()));
                return false;
            }
        }

        /// <summary>
        /// Flush the ETW session.
        /// </summary>
        /// <exception cref="System.ComponentModel.Win32Exception"></exception>
        public void FlushTrace()
        {
            Debug.Assert(0 != _handle && NativeMethods.IsValidHandle(_handle),
                         "0 != _handle && NativeMethods.IsValidHandle(_handle)");

            NativeMethods.EventTraceProperties properties = CommonEventTraceProperties();
            CDFMonitor.LogOutputHandler("DEBUG:FlushTrace:Flushing trace");
            uint processResult = NativeMethods.FlushTrace(_handle, _sessionName, ref properties);

            if (processResult != NativeMethods.ERROR_SUCCESS && processResult != NativeMethods.ERROR_CANCELLED)
            {
                throw new Win32Exception((int)processResult);
            }
        }

        /// <summary>
        /// Queries the trace.
        /// </summary>
        /// <param name="sessionName">Name of the session.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool QueryTrace(string sessionName)
        {
            Debug.Assert(!String.IsNullOrEmpty(sessionName), "!String.IsNullOrEmpty(sessionName)");

            return (QueryTrace(0, sessionName));
        }

        /// <summary>
        /// Start the ETW session.
        /// </summary>
        /// <param name="logFileMode">The log file mode.</param>
        /// <param name="logFileMaxSize">Size of the log file max.</param>
        /// <param name="logFileName">Name of the log file.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool StartTrace(NativeMethods.EventTraceFileMode logFileMode = NativeMethods.EventTraceFileMode.RealTime,
            uint logFileMaxSize = 0, string logFileName = null)
        {
            Debug.Assert(_handle == 0, "_handle == 0");

            NativeMethods.EventTraceProperties properties = CommonEventTraceProperties();
            properties.WNode.Flags = NativeMethods.WNodeFlags.TracedGuid;
            properties.WNode.ClientContext = 1;
            properties.LogFileMode = logFileMode;
            properties.FlushTimer = 1;

            if (logFileMode != NativeMethods.EventTraceFileMode.RealTime)
            {
                properties.MaximumFileSize = logFileMaxSize;

                //32 bit different
                uint offset = 4;
                if (IntPtr.Size == 8) //64 bit
                {
                    offset = 0;
                }
                properties.LogFileNameOffset = NativeMethods.EventTracePropertiesStructSize +
                                               (NativeMethods.EventTracePropertiesStringSize) - offset;
                properties.LogFileName = logFileName;
                properties.FlushTimer = 0;
            }
            CDFMonitor.LogOutputHandler("DEBUG:ETWController.StartTrace:starting trace.");
            uint processResult = NativeMethods.StartTrace(out _handle, _sessionName, ref properties);

            int lastError = Marshal.GetLastWin32Error();

            if (!NativeMethods.IsValidHandle(_handle))
            {
                CDFMonitor.LogOutputHandler("DEBUG:StartTrace: exception. lastError:" + lastError.ToString());
                return false;
            }

            if (processResult != NativeMethods.ERROR_SUCCESS
                && processResult != NativeMethods.ERROR_CANCELLED
                && processResult != NativeMethods.ERROR_ALREADY_EXISTS)
            {
                CDFMonitor.LogOutputHandler("StartTrace: exception. process result:" + processResult.ToString());

                return false;
            }

            return true;
        }

        /// <summary>
        /// Starts the trace safe.
        /// </summary>
        /// <param name="logFileMode">The log file mode.</param>
        /// <param name="logFileMaxSize">Size of the log file max.</param>
        /// <param name="logFileName">Name of the log file.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool StartTraceSafe(
            NativeMethods.EventTraceFileMode logFileMode = NativeMethods.EventTraceFileMode.RealTime,
            uint logFileMaxSize = 0, string logFileName = null)
        {
            if (QueryTrace(_sessionName))
            {
                StopTrace(_sessionName);
            }

            if (!StartTrace(logFileMode, logFileMaxSize, logFileName))
            {
                CleanTrace(_sessionName);

                if (!StartTrace(logFileMode, logFileMaxSize, logFileName))
                {
                    CDFMonitor.Instance.EtwTraceClean(true);

                    if (!StartTrace(logFileMode, logFileMaxSize, logFileName))
                    {
                        return false;
                    }
                }
            }

            return Running = true;
        }

        /// <summary>
        /// Stop the ETW session (if one exists).
        /// </summary>
        public void StopTrace()
        {
            if (_handle != 0 && NativeMethods.IsValidHandle(_handle))
            {
                StopTrace(_handle, null);
                _handle = 0;
            }
        }

        /// <summary>
        /// Stop the named ETW session (if it exists).
        /// </summary>
        /// <param name="sessionName">Name of the ETW session</param>
        /// <remarks>
        /// This method is called by the unit tests prior to creating an instance of this controller
        /// to ensure that the specified ETW session doesn't already exist.
        /// </remarks>
        public void StopTrace(string sessionName)
        {
            Debug.Assert(!String.IsNullOrEmpty(sessionName), "!String.IsNullOrEmpty(sessionName)");

            StopTrace(0, sessionName);
            _handle = 0;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Initializes some of the fields of the NativeMethods.EVENT_TRACE_PROPERTIES structure
        /// with values that are common to all the ETW methods used in this class.
        /// </summary>
        /// <returns>The initialized NativeMethods.EVENT_TRACE_PROPERTIES structure.</returns>
        private NativeMethods.EventTraceProperties CommonEventTraceProperties()
        {
            var properties = new NativeMethods.EventTraceProperties();
            properties.WNode.BufferSize = NativeMethods.EventTracePropertiesStructSize +
                                          (NativeMethods.EventTracePropertiesStringSize * 2);
            properties.WNode.Guid = _guid;

            properties.LoggerNameOffset = NativeMethods.EventTracePropertiesStructSize;
            properties.LogFileNameOffset = 0;
            properties.FlushTimer = 0;
            properties.BufferSize = CDFMonitor.Instance.Config.AppSettings.BufferSize != 0 ? (uint)CDFMonitor.Instance.Config.AppSettings.BufferSize : 10;
            properties.MinimumBuffers = CDFMonitor.Instance.Config.AppSettings.BufferMin != 0 ? (uint)CDFMonitor.Instance.Config.AppSettings.BufferMin : 40;
            properties.MaximumBuffers = CDFMonitor.Instance.Config.AppSettings.BufferMax != 0 ? (uint)CDFMonitor.Instance.Config.AppSettings.BufferMax : 80;

            properties.EnableFlags = 0;
            if (_isKernelController)
            {
                // http: //msdn.microsoft.com/en-us/library/windows/desktop/aa364085(v=vs.85).aspx
                properties.WNode.Guid = new NativeMethods.publicGuid(new Guid("9e814aad-3204-11d2-9a82-006008a86939").ToByteArray());

                properties.EnableFlags = CDFMonitor.Instance.Config.IsKernelTraceConfigured() ? (uint)CDFMonitor.Instance.Config.AppSettings.EnableFlags : 0;
            }

            return properties;
        }

        /// <summary>
        /// Queries the trace.
        /// </summary>
        /// <param name="handle">The handle.</param>
        /// <param name="sessionName">Name of the session.</param>
        /// <returns>true if trace exists, otherwise false</returns>
        private bool QueryTrace(ulong handle, string sessionName)
        {
            Debug.Assert(!String.IsNullOrEmpty(sessionName) || handle != 0,
                         "!String.IsNullOrEmpty(sessionName) || handle != 0");

            NativeMethods.EventTraceProperties properties = CommonEventTraceProperties();
            CDFMonitor.LogOutputHandler("DEBUG:QueryTrace: querying trace");
            uint processResult = NativeMethods.QueryTrace(handle, sessionName, ref properties);

            if (processResult == NativeMethods.ERROR_WMI_INSTANCE_NOT_FOUND)
            {
                CDFMonitor.LogOutputHandler("DEBUG:QueryTrace: trace does not exist:" + sessionName);
                return false;
            }

            MissedControllerEvents = properties.EventsLost;
            CDFMonitor.LogOutputHandler("DEBUG:QueryTrace: trace exists:" + sessionName);
            return true;
        }

        /// <summary>
        /// Stop the named ETW session (if it exists).
        /// </summary>
        /// <param name="handle">Handle to the ETW session</param>
        /// <param name="sessionName">Name of the ETW session</param>
        /// <exception cref="System.ComponentModel.Win32Exception"></exception>
        /// <remarks>
        /// The session should be specified by either the handle or the session name.
        /// </remarks>
        private void StopTrace(ulong handle, string sessionName)
        {
            Debug.Assert(!String.IsNullOrEmpty(sessionName) || handle != 0,
                         "!String.IsNullOrEmpty(sessionName) || handle != 0");
            Running = false;

            if (!QueryTrace(handle, sessionName))
            {
                return;
            }

            NativeMethods.EventTraceProperties properties = CommonEventTraceProperties();
            CDFMonitor.LogOutputHandler("DEBUG:ETWController.StopTrace:stopping trace:" + sessionName);
            uint processResult = NativeMethods.StopTrace(handle, sessionName, ref properties);

            if (processResult != NativeMethods.ERROR_SUCCESS &&
                processResult != NativeMethods.ERROR_CANCELLED &&
                processResult != NativeMethods.ERROR_WMI_INSTANCE_NOT_FOUND)
            {
                throw new Win32Exception((int)processResult);
            }
        }

        #endregion Private Methods
    }
}