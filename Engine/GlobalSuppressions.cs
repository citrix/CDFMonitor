// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project. Project-level
// suppressions either have no target or are given a specific target
// and scoped to a namespace, type, member, etc.
//
// To add a suppression to this file, right-click the message in the
// Error List, point to "Suppress Message(s)", and click "In Project
// Suppression File". You do not need to add suppressions to this
// file manually.
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests")]

// The fields are defined and used by unmanaged code.
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+BufferUnion.#EventsLost")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+BufferUnion.#LogInstanceGuid")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+BufferUnion.#PointerSize")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+BufferUnion.#Reserved32")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+BufferUnion.#StartBuffers")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+ContextVersionUnion.#HistoricalContext")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+ContextVersionUnion.#Linkage")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+ContextVersionUnion.#Version")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+KernalTimestampUnion.#CountLost")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+KernalTimestampUnion.#KernelHandle")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+KernalTimestampUnion.#TimeStamp")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+TimeUnion.#KernelTime")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+TimeUnion.#ProcessorTime")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+TimeUnion.#UserTime")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+VersionDetailUnion.#Version")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+VersionDetailUnion.#VersionDetail_MajorVersion")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+VersionDetailUnion.#VersionDetail_MinorVersion")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+VersionDetailUnion.#VersionDetail_SubMinorVersion")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods+VersionDetailUnion.#VersionDetail_SubVersion")]

// Unit tests run with full trust
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.EtwTraceConsumer.#OpenTrace()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.EtwTraceController.#StartTrace()")]

// The return value of ProcessTrace does not affect the outcome of the unit tests
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.NativeMethods.ProcessTrace(System.UInt64[],System.Int32,System.Int64@,System.Int64@)", Scope = "member", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.EtwTraceConsumer.#ProcessTrace(System.Object)")]

// Resources are correctly disposed when each test comletes
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Scope = "type", Target = "Microsoft.OfficeCommunicationsServer.Applications.Common.UnitTests.LoggerTest")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", MessageId = "System.String.Compare(System.String,System.String,System.Boolean)", Scope = "member", Target = "CDFMonitor.CDFMonitor.#ProcessConfig()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.Convert.ToBoolean(System.String)", Scope = "member", Target = "CDFMonitor.CDFMonitor.#ProcessConfig()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.Convert.ToInt32(System.String)", Scope = "member", Target = "CDFMonitor.CDFMonitor.#ProcessConfig()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.Int32.ToString", Scope = "member", Target = "CDFMonitor.CDFMonitor.#ProcessConfig()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.String.Format(System.String,System.Object)", Scope = "member", Target = "CDFMonitor.CDFMonitor.#UploadPackage(System.Int32)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.String.Format(System.String,System.Object,System.Object)", Scope = "member", Target = "CDFMonitor.CDFMonitor.#UploadPackage(System.Int32)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1307:SpecifyStringComparison", MessageId = "System.String.StartsWith(System.String)", Scope = "member", Target = "CDFMonitor.CDFMonitor.#WriteConsoleQ(System.String)")]