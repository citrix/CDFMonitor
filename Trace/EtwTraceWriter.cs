// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="EtwTraceWriter.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Trace
{
    using CDFM.Engine;
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Class EtwTraceWriter
    /// </summary>
    internal class EtwTraceWriter
    {
        #region Public Fields

        public static Guid EtwTraceGuid = new Guid(Properties.Resources.CDFMonitorTMFGuid);

        #endregion Public Fields

        #region Private Fields

        private long _traceHandle = 0;
        private bool _writerDisabled = true;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EtwTraceWriter" /> class.
        /// </summary>
        public EtwTraceWriter()
        {
            // EventRegister only works on vista+ no xp or 2k3
            if (CDFMonitor.Instance.Config.IsWinVistaOrHigher())
            {
                NativeMethods.publicGuid guid = new NativeMethods.publicGuid(EtwTraceGuid.ToByteArray());
                uint ret2 = EventRegister(ref guid, IntPtr.Zero, IntPtr.Zero, ref _traceHandle);
                Debug.Print("DEBUG:EtwTraceWriter:eventregister return:" + ret2.ToString());
                _writerDisabled = false;
            }
            else
            {
                Debug.Print("DEBUG:EtwTraceWriter:eventregister not supported.");
            }
        }

        #endregion Public Constructors

        #region Private Destructors

        /// <summary>
        /// Finalizes an instance of the <see cref="EtwTraceWriter" /> class.
        /// </summary>
        ~EtwTraceWriter()
        {
            EventUnregister(_traceHandle);
        }

        #endregion Private Destructors

        #region Public Methods

        /// <summary>
        /// Events the register.
        /// </summary>
        /// <param name="eventTraceProvider">The event trace provider.</param>
        /// <param name="enableCallBackNotUsed">The enable call back not used.</param>
        /// <param name="pcallbackContextNotUsed">The pcallback context not used.</param>
        /// <param name="traceHandle">The trace handle.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("advapi32.dll", EntryPoint = "EventRegister", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint EventRegister([In] ref NativeMethods.publicGuid eventTraceProvider, IntPtr enableCallBackNotUsed, IntPtr pcallbackContextNotUsed, ref long traceHandle);

        /// <summary>
        /// Events the unregister.
        /// </summary>
        /// <param name="regHandle">The reg handle.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("advapi32.dll", EntryPoint = "EventUnregister", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint EventUnregister([In] long regHandle);

        /// <summary>
        /// Events the write string.
        /// </summary>
        /// <param name="regHandle">The reg handle.</param>
        /// <param name="Level">The level.</param>
        /// <param name="Keyword">The keyword.</param>
        /// <param name="EventString">The event string.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("advapi32.dll", EntryPoint = "EventWriteString", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint EventWriteString([In] long regHandle, Byte Level, UInt64 Keyword, [MarshalAs(UnmanagedType.LPWStr)] string EventString);

        /// <summary>
        /// Writes the event.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool WriteEvent(string data)
        {
            if (!_writerDisabled)
            {
                uint ret = EventWriteString(_traceHandle, 0, 0, data);

                Debug.Print("DEBUG:EtwTraceWriter:eventwritestring return:" + ret.ToString());

                return true;
            }

            return false;
        }

        #endregion Public Methods
    }
}