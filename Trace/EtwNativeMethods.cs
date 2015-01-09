// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="EtwNativeMethods.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************

//++
//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//
// Module Name:
//
//  EtwNativeMethods.cs
//
// Abstract:
//
//  This module defines the native methods used by the EtwTraceController and EtwTraceConsumer classes.
//
//--

namespace CDFM.Trace
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Indicates which structure members are valid:
    /// </summary>
    [Flags]
    public enum EventTraceFlags : byte
    {
        /// <summary>
        /// EVENT_TRACE_USE_PROCTIME - ProcessorTime is valid.
        /// </summary>
        ProcessorTimeValid = 0x01,

        /// <summary>
        /// EVENT_TRACE_USE_NOCPUTIME - KernelTime, UserTime, and ProcessorTime are not used.
        /// </summary>
        NoTimes = 0x02,
    }

    /// <summary>
    /// Type of event. An event type can be user-defined or predefined. This enum identifies the
    /// general predefined event types.
    /// </summary>
    public enum EventTraceType : byte
    {
        /// <summary>
        /// EVENT_TRACE_TYPE_INFO - Informational event. This is the default event type.
        /// </summary>
        Info = 0x00,

        /// <summary>
        /// EVENT_TRACE_TYPE_START - Start event. Use to trace the initial state of a multi-step
        /// event.
        /// </summary>
        Start = 0x01,

        /// <summary>
        /// EVENT_TRACE_TYPE_END - End event. Use to trace the final state of a multi-step event.
        /// </summary>
        End = 0x02,

        /// <summary>
        /// EVENT_TRACE_TYPE_DC_START - Collection start event.
        /// </summary>
        CollectionStart = 0x03,

        /// <summary>
        /// EVENT_TRACE_TYPE_DC_END - Collection end event.
        /// </summary>
        CollectionEnd = 0x04,

        /// <summary>
        /// EVENT_TRACE_TYPE_EXTENSION - _extension event. Use for an event that is a continuation
        /// of a previous event. For example, use the _extension event type when an event trace
        /// records more data than can fit in a session buffer.
        /// </summary>
        Extension = 0x05,

        /// <summary>
        /// EVENT_TRACE_TYPE_REPLY - Reply event. Use when an application that requests resources c
        /// an receive multiple responses. For example, if a client application requests a URL, and
        /// the Web server reply is to send several files, each file received can be marked as a
        /// reply event.
        /// </summary>
        Reply = 0x06,

        /// <summary>
        /// EVENT_TRACE_TYPE_DEQUEUE - Dequeue event. Use when an activity is queued before it
        /// begins. Use EVENT_TRACE_TYPE_START to mark the time when a work item is queued. Use the
        /// dequeue event type to mark the time when work on the item actually begins. Use
        /// EVENT_TRACE_TYPE_END to mark the time when work on the item completes.
        /// </summary>
        Dequeue = 0x07,

        /// <summary>
        /// EVENT_TRACE_TYPE_CHECKPOINT - Checkpoint event. Use for an event that is not at the
        /// start or end of an activity.
        /// </summary>
        Checkpoint = 0x08,
    }

    /// <summary>
    /// An public class that contains all of the marshaling and structure definitions for native API
    /// calls.
    /// </summary>
    public static class NativeMethods
    {
        #region Public Fields

        public const int ERROR_ALREADY_EXISTS = 183;
        public const int ERROR_CANCELLED = 1223;
        public const int ERROR_CTX_CLOSE_PENDING = 7007;
        public const int ERROR_INVALID_HANDLE = 6;
        public const int ERROR_INVALID_PARAMETER = 87;
        public const int ERROR_SUCCESS = 0;
        public const int ERROR_WMI_INSTANCE_NOT_FOUND = 4201;
        public const uint EventTracePropertiesStringSize = 1024;
        public const uint EventTracePropertiesStructSize = 120;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        #endregion Public Fields

        #region Public Delegates

        /// <summary>
        /// Delegate EventCallback
        /// </summary>
        /// <param name="eventTrace">The event trace.</param>
        public delegate void EventCallback([In] ref EventTrace eventTrace);

        //    ULONG WINAPI <FunctionName> (PEVENT_TRACE_LOGFILE Logfile);
        /// <summary>
        /// Delegate EventTraceBufferCallback
        /// </summary>
        /// <param name="eventTraceLogfile">The event trace logfile.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public delegate bool EventTraceBufferCallback([In] ref EventTraceLogFile eventTraceLogfile);

        #endregion Public Delegates

        #region Public Enums

        /// <summary>
        /// Enum EventTraceFileMode
        /// </summary>
        public enum EventTraceFileMode : uint
        {
            None = 0x00000000, // Like sequential with no max
            Sequential = 0x00000001, // log sequentially stops at max
            Circular = 0x00000002, // log in circular manner
            Append = 0x00000004,
            NewFile = 0x00000008, // log sequentially until max then create new file
            RealTime = 0x00000100 // log in real time (no file)
        }

        /// <summary>
        /// Enum WNodeFlags
        /// </summary>
        [Flags]
        public enum WNodeFlags : uint
        {
            UseGuidPtr = 0x00080000, // Guid is actually a pointer
            TracedGuid = 0x00020000, // denotes a trace
            UseMofPtr = 0x00100000 // MOF data are dereferenced
        }

        #endregion Public Enums

        #region Public Methods

        //    ULONG CloseTrace(TRACEHANDLE TraceHandle);
        /// <summary>
        /// Closes the trace.
        /// </summary>
        /// <param name="traceHandle">The trace handle.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CloseTrace([In] ulong traceHandle);

        /// <summary>
        /// Enables the trace.
        /// </summary>
        /// <param name="enable">The enable.</param>
        /// <param name="enableFlag">The enable flag.</param>
        /// <param name="enableLevel">The enable level.</param>
        /// <param name="controlGuid">The control GUID.</param>
        /// <param name="traceHandle">The trace handle.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern uint EnableTrace([In] uint enable, [In] uint enableFlag, [In] uint enableLevel,
            [In] ref publicGuid controlGuid, [In] ulong traceHandle);

        // ULONG FlushTrace(TRACEHANDLE SessionHandle, LPCTSTR SessionName, PEVENT_TRACE_PROPERTIES Properties);
        /// <summary>
        /// Flushes the trace.
        /// </summary>
        /// <param name="traceHandle">The trace handle.</param>
        /// <param name="sessionName">Name of the session.</param>
        /// <param name="properties">The properties.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern uint FlushTrace([In] ulong traceHandle, [In] string sessionName,
            [In, Out] ref EventTraceProperties properties);

        /// <summary>
        /// Gets the trace logger handle.
        /// </summary>
        /// <param name="pWNODE_HEADER">The p WNOD e_ HEADER.</param>
        /// <returns>System.UInt32.</returns>
        /// Return Type: ULONG-&gt;unsigned int
        /// PropertyArray: ULONG*
        /// PropertyArrayCount: ULONG-&gt;unsigned int
        /// SessionCount: PULONG-&gt;ULONG*
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern uint GetTraceLoggerHandle([In] ulong pWNODE_HEADER);

        /// <summary>
        /// Determines whether [is valid handle] [the specified handle].
        /// </summary>
        /// <param name="handle">The handle.</param>
        /// <returns><c>true</c> if [is valid handle] [the specified handle]; otherwise,
        /// /c>.</returns>
        public static bool IsValidHandle(ulong handle)
        {
            IntPtr handleValue;

            unchecked
            {
                if (4 == IntPtr.Size)
                {
                    handleValue = new IntPtr((int)handle);
                }
                else
                {
                    handleValue = new IntPtr((long)handle);
                }
            }
            return INVALID_HANDLE_VALUE != handleValue;
        }

        //    TRACEHANDLE OpenTrace(PEVENT_TRACE_LOGFILE Logfile);
        /// <summary>
        /// Opens the trace.
        /// </summary>
        /// <param name="eventTraceLogfile">The event trace logfile.</param>
        /// <returns>System.UInt64.</returns>
        [DllImport("advapi32.dll", EntryPoint = "OpenTraceW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ulong OpenTrace([In] ref EventTraceLogFile eventTraceLogfile);

        /// <summary>
        /// Processes the trace.
        /// </summary>
        /// <param name="traceHandleArray">The trace handle array.</param>
        /// <param name="handleArrayLength">Length of the handle array.</param>
        /// <param name="startFileTime">The start file time.</param>
        /// <param name="endFileTime">The end file time.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern uint ProcessTrace(
            [In] ulong[] traceHandleArray,
            [In] int handleArrayLength,
            [In] ref long startFileTime,
            [In] ref long endFileTime);

        /// <summary>
        /// Queries the trace.
        /// </summary>
        /// <param name="traceHandle">The trace handle.</param>
        /// <param name="sessionName">Name of the session.</param>
        /// <param name="properties">The properties.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern uint QueryTrace([In] ulong traceHandle, [In] string sessionName,
            ref EventTraceProperties properties);

        /// <summary>
        /// Starts the trace.
        /// </summary>
        /// <param name="traceHandle">The trace handle.</param>
        /// <param name="sessionName">Name of the session.</param>
        /// <param name="properties">The properties.</param>
        /// <returns>System.UInt32.</returns>

        // todo: switch to Unicode but loggernameoffset and logfilenameoffset will have to be modified.
        [DllImport("advapi32.dll", CharSet = CharSet.Ansi)]
        public static extern uint StartTrace([Out] out ulong traceHandle, [In] string sessionName,
            [In] ref EventTraceProperties properties);

        /// <summary>
        /// Stops the trace.
        /// </summary>
        /// <param name="traceHandle">The trace handle.</param>
        /// <param name="sessionName">Name of the session.</param>
        /// <param name="properties">The properties.</param>
        /// <returns>System.UInt32.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern uint StopTrace([In] ulong traceHandle, [In] string sessionName,
            [In, Out] ref EventTraceProperties properties);

        #endregion Public Methods

        #region Public Structs

        /// <summary>
        /// Struct BufferUnion
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct BufferUnion
        {
            [FieldOffset(0)]
            public Guid LogInstanceGuid;

            [FieldOffset(0)]
            public uint StartBuffers;

            [FieldOffset(4)]
            public uint PointerSize;

            [FieldOffset(8)]
            public uint EventsLost;

            [FieldOffset(12)]
            public uint Reserved32;
        }

        /// <summary>
        /// Struct ContextVersionUnion
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct ContextVersionUnion
        {
            [FieldOffset(0)]
            public ulong HistoricalContext;

            [FieldOffset(0)]
            public uint Version;

            [FieldOffset(4)]
            public uint Linkage;
        }

        /// <summary>
        /// Struct EVENT_TRACE
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct EventTrace
        {
            public ushort Size;
            public EventTraceType HeaderType;
            public EventTraceFlags MarkerFlags;
            public byte Type;
            public byte Level;
            public ushort Version;
            public uint ThreadId;
            public uint ProcessId;
            public Int64 TimeStamp;
            public Guid Guid; // Int64 RegHandle;
            public uint InstanceId;
            public uint ParentInstanceId;
            public TimeUnion TimeUnion;
            public Guid ParentGuid;
            public IntPtr MofData;
            public uint MofLength;
            public uint ClientContext;
        }

        /// <summary>
        /// Struct EVENT_TRACE_LOGFILE
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct EventTraceLogFile
        {
            public string LogfileName;
            public string LoggerName;
            public long CurrentTime;
            public uint BuffersRead;
            public uint LogFileMode;
            public EventTrace CurrentEvent;
            public TraceLogfileHeader LogfileHeader;
            public EventTraceBufferCallback BufferCallback;
            public uint BufferSize;
            public uint Filled;
            public uint EventsLost;
            public EventCallback EventCallback;
            public uint IsKernelTrace;
            public long Context;
        }

        /// <summary>
        /// Struct EVENT_TRACE_PROPERTIES
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct EventTraceProperties
        {
            public WnodeHeader WNode;
            public uint BufferSize;
            public uint MinimumBuffers;
            public uint MaximumBuffers;
            public uint MaximumFileSize;
            public EventTraceFileMode LogFileMode;
            public uint FlushTimer;
            public uint EnableFlags;
            public int AgeLimit;
            public uint NumberOfBuffers;
            public uint FreeBuffers;
            public uint EventsLost;
            public uint BuffersWritten;
            public uint LogBuffersLost;
            public uint RealTimeBuffersLost;
            public unsafe void* LoggerThreadId;
            public uint LogFileNameOffset;
            public uint LoggerNameOffset;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)EventTracePropertiesStringSize)]
            public string LoggerName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)EventTracePropertiesStringSize)]
            public string LogFileName;
        }

        /// <summary>
        /// Struct KernalTimestampUnion
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct KernalTimestampUnion
        {
            [FieldOffset(0)]
            public uint CountLost;

            [FieldOffset(0)]
            public unsafe void* KernelHandle;

            [FieldOffset(0)]
            public long TimeStamp;
        }

        /// <summary>
        /// Struct publicGuid
        /// </summary>
        [StructLayout(LayoutKind.Sequential),
        Serializable]
        public struct publicGuid
        {
            public int _a;
            public short _b;
            public short _c;
            public byte _d;
            public byte _e;
            public byte _f;
            public byte _g;
            public byte _h;
            public byte _i;
            public byte _j;
            public byte _k;

            #region Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="publicGuid" /> struct.
            /// </summary>
            /// <param name="guidBytes">The GUID bytes.</param>
            /// <exception cref="System.ArgumentNullException">guidBytes</exception>
            /// <exception cref="System.ArgumentException">Wrong length;guidBytes</exception>
            public publicGuid(byte[] guidBytes)
            {
                if (guidBytes == null)
                {
                    throw new ArgumentNullException("guidBytes");
                }
                if (guidBytes.Length != 16)
                {
                    throw new ArgumentException("Wrong length", "guidBytes");
                }
                _a = BitConverter.ToInt32(guidBytes, 0);
                _b = BitConverter.ToInt16(guidBytes, 4);
                _c = BitConverter.ToInt16(guidBytes, 6);
                _d = guidBytes[8];
                _e = guidBytes[9];
                _f = guidBytes[10];
                _g = guidBytes[11];
                _h = guidBytes[12];
                _i = guidBytes[13];
                _j = guidBytes[14];
                _k = guidBytes[15];
            }

            #endregion Constructors

            #region Methods

            /// <summary>
            /// Implements the operator !=.
            /// </summary>
            /// <param name="a">A.</param>
            /// <param name="b">The b.</param>
            /// <returns>The result of the operator.</returns>
            public static bool operator !=(publicGuid a, publicGuid b)
            {
                return !a.Equals(b);
            }

            /// <summary>
            /// Implements the operator ==.
            /// </summary>
            /// <param name="a">A.</param>
            /// <param name="b">The b.</param>
            /// <returns>The result of the operator.</returns>
            public static bool operator ==(publicGuid a, publicGuid b)
            {
                return a.Equals(b);
            }

            /// <summary>
            /// Determines whether the specified <see cref="System.Object" /> is equal to this
            /// instance.
            /// </summary>
            /// <param name="o">The <see cref="System.Object" /> to compare with this
            /// instance.</param>
            /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this
            /// instance; otherwise, /c>.</returns>
            public override bool Equals(Object o)
            {
                bool isEqual = false;

                // Check that o is a Guid first
                if (o is publicGuid)
                {
                    var g = (publicGuid)o;

                    // Now compare each of the elements
                    isEqual = (g._a == _a &&
                               g._b == _b &&
                               g._c == _c &&
                               g._d == _d &&
                               g._e == _e &&
                               g._f == _f &&
                               g._g == _g &&
                               g._h == _h &&
                               g._i == _i &&
                               g._j == _j &&
                               g._k == _k);
                }
                return isEqual;
            }

            /// <summary>
            /// Returns a hash code for this instance.
            /// </summary>
            /// <returns>A hash code for this instance, suitable for use in hashing algorithms and
            /// data structures like a hash table.</returns>
            public override int GetHashCode()
            {
                return _a ^ ((_b << 16) | (ushort)_c) ^ ((_f << 24) | _k);
            }

            #endregion Methods
        }

        /// <summary>
        /// Struct SYSTEMTIME
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            public short wYear;
            public short wMonth;
            public short wDayOfWeek;
            public short wDay;
            public short wHour;
            public short wMinute;
            public short wSecond;
            public short wMilliseconds;
        }

        /// <summary>
        /// Struct TimeUnion
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct TimeUnion
        {
            [FieldOffset(0)]
            public uint KernelTime;

            [FieldOffset(4)]
            public uint UserTime;

            [FieldOffset(0)]
            public ulong ProcessorTime;
        }

        /// <summary>
        /// Struct TimeZoneInformation
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct TimeZoneInformation
        {
            public int Bias;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string StandardName;

            public SYSTEMTIME StandardDate;
            public int StandardBias;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DaylightName;

            public SYSTEMTIME DaylightDate;
            public int DaylightBias;
        }

        /// <summary>
        /// Struct TraceLogfileHeader
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct TraceLogfileHeader
        {
            public uint BufferSize;
            public VersionDetailUnion VersionDetailUnion;
            public uint ProviderVersion;
            public uint NumberOfProcessors;
            public long EndTime;
            public uint TimerResolution;
            public uint MaximumFileSize;
            public uint LogFileMode;
            public uint BuffersWritten;
            public BufferUnion BufferUnion;
            public IntPtr LoggerName;
            public IntPtr LogFileName;
            public TimeZoneInformation TimeZone;
            public long BootTime;
            public long PerfFreq;
            public long StartTime;
            public uint ReservedFlags;
            public uint BuffersLost;
        }

        /// <summary>
        /// Struct VersionDetailUnion
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct VersionDetailUnion
        {
            [FieldOffset(0)]
            public uint Version;

            [FieldOffset(0)]
            public byte VersionDetail_MajorVersion;

            [FieldOffset(1)]
            public byte VersionDetail_MinorVersion;

            [FieldOffset(2)]
            public byte VersionDetail_SubVersion;

            [FieldOffset(3)]
            public byte VersionDetail_SubMinorVersion;
        }

        /// <summary>
        /// Struct VersionUnion
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct VersionUnion
        {
            [FieldOffset(0)]
            public byte Type;

            [FieldOffset(1)]
            public byte Level;

            [FieldOffset(2)]
            public ushort Version;
        }

        /// <summary>
        /// Struct WNODE_HEADER
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct WnodeHeader
        {
            public uint BufferSize;
            public uint ProviderId;
            public ContextVersionUnion ContextVersion;
            public KernalTimestampUnion KernalTimestamp;
            public publicGuid Guid;
            public uint ClientContext;
            public WNodeFlags Flags;
        }

        #endregion Public Structs
    }
}