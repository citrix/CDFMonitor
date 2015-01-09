// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="UDP.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Network
{
    using CDFM.Engine;
    using CDFM.Gui;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text;
    using System.Threading;
    using System.Timers;
    using System.Windows.Threading;

    /// <summary>
    /// Class Udp
    /// </summary>
    public class Udp
    {
        #region Public Properties

        /// <summary>
        /// Gets or sets the _udp counter.
        /// </summary>
        /// <value>The _udp counter.</value>
        public Int64 UdpCounter
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether [writer enabled].
        /// </summary>
        /// <value><c>true</c> if [writer enabled]; otherwise, /c>.</value>
        public bool WriterEnabled
        {
            get;
            private set;
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Disables the writer.
        /// </summary>
        public void DisableWriter()
        {
            CDFMonitor.LogOutputHandler("DEBUG:UDP.DisableWriter:enter");
            WriterEnabled = false;

            if (_pingTimer != null)
            {
                PingEnabled = false;
                _pingTimer.Enabled = false;
                _pingTimer.Dispose();
            }

            UnBindClientSockets();
        }

        /// <summary>
        /// Enables the writer.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="port">The port.</param>
        //public void EnableWriter(string server, int port, bool pingEnabled)
        public void EnableWriter()
        {
            CDFMonitor.LogOutputHandler("DEBUG:UDP.EnableWriter:enter");
            WriterEnabled = true;

            if (_settings.UdpClientEnabled)
            {
                _udpClient = BindClientSocket(_settings.UdpServer, _settings.UdpServerPort);
            }

            if (_settings.UdpPingEnabled)
            {
                PingEnabled = true;
                _udpPingClient = BindClientSocket(string.IsNullOrEmpty(
                    _settings.UdpPingServer) ? _settings.UdpServer : _settings.UdpPingServer, _settings.UdpServerPort);
                _pingTimer = new System.Timers.Timer(_settings.UdpPingTimer * 1000);
                _pingTimer.Elapsed += new ElapsedEventHandler(SendPing);
                _pingTimer.Start();
                SendPing(null, null);
            }
        }

        /// <summary>
        /// Receives the callback.
        /// </summary>
        /// <param name="ar">The ar.</param>
        public void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                UdpClient udpClient = (UdpClient)((UdpState)(ar.AsyncState)).listener;
                IPEndPoint ipEndPoint = (IPEndPoint)((UdpState)(ar.AsyncState)).groupEP;
                UdpPingPacket packet;

                // using index 0 to determine if packet bytes are just a string (0) or UdpPingPacket
                // (1)
                // todo: keeping this way may be more efficient network packet size (smaller) but
                //       need to verify

                List<byte> receiveBytes = new List<byte>(udpClient.EndReceive(ar, ref ipEndPoint));
                UdpTraceType packetType = (UdpTraceType)receiveBytes[0];
                receiveBytes.RemoveAt(0);

                _listenerCallback.Set();

                if (packetType == UdpTraceType.TraceString)
                {
                    // trace string and not UdpPingPacket so create one
                    packet = new UdpPingPacket()
                    {
                        UdpTraceType = UdpTraceType.TraceString,
                        FormattedTraceString = Encoding.ASCII.GetString(receiveBytes.ToArray())
                    };
                }
                else
                {
                    // not a tracestring so UdpPingPacket of some type
                    IFormatter formatter = new BinaryFormatter();
                    Stream stream = new MemoryStream(receiveBytes.ToArray());
                    packet = (UdpPingPacket)formatter.Deserialize(stream);
                    stream.Close();
                }

                // determine udp trace type
                switch (packet.UdpTraceType)
                {
                    case UdpTraceType.PingReply:

                    // to do: send response?

                    case UdpTraceType.Ping:

                        // update list in gui

                        Config.Configuration.UdpClientsListViewItem item = new Config.Configuration.UdpClientsListViewItem(packet);

                        CDFMonitorGui.Instance.Dispatcher.Invoke(DispatcherPriority.Background, new Action<Config.Configuration.UdpClientsListViewItem>((listItem) =>
                        {
                            try
                            {
                                if (_cdfm.Config.UdpClientsListViewCollection.Any(i => i.ClientName == listItem.ClientName))
                                {
                                    int index = -1;
                                    Config.Configuration.UdpClientsListViewItem lvi = _cdfm.Config.UdpClientsListViewCollection.First(i => i.ClientName == listItem.ClientName);

                                    index = _cdfm.Config.UdpClientsListViewCollection.IndexOf(lvi);
                                    _cdfm.Config.UdpClientsListViewCollection.RemoveAt(index);
                                    _cdfm.Config.UdpClientsListViewCollection.Insert(index, listItem);
                                    _cdfm.Config.UdpClientsListViewCollection.Remove(lvi);
                                }
                                else
                                {
                                    // new entry
                                    _cdfm.Config.UdpClientsListViewCollection.Add(listItem);
                                }
                            }
                            catch (Exception ex)
                            {
                                CDFMonitor.LogOutputHandler("Udp.ReceiveCallback:exception:" + ex.ToString());
                            }
                        }), item);

                        packet.FormattedTraceString = string.Format("{0},{1},{2},ping: Activity:{3} TPS:{4} Matched Events:{5} Missed Events:{6} Max Queue Missed Events:{7} Parser Queue:{8} Processed Events:{9} Avg Cpu:{10} Current Cpu:{11} Total Cpu:{12} Duration:{13}",
                               packet.ClientName,
                               packet.UdpCounter,
                               packet.ClientPingTime,
                               packet.UdpTraceType,
                               packet.TracesPerSecond,
                               packet.MatchedEvents,
                               packet.MissedMatchedEvents,
                               packet.MaxQueueMissedEvents,
                               packet.ParserQueue,
                               packet.ProcessedEvents,
                               packet.AvgProcessCpu,
                               packet.CurrentMachineCpu,
                               packet.CurrentProcessCpu,
                               packet.Duration
                               );

                        _cdfm.ServerRegexParserThread.Queue(packet.FormattedTraceString);
                        break;

                    case UdpTraceType.TraceEtl:
                    case UdpTraceType.TraceString:
                        _cdfm.ServerRegexParserThread.Queue(packet.FormattedTraceString);
                        break;

                    case UdpTraceType.Unknown:
                    default:
                        break;
                }
            }
            catch (ObjectDisposedException)
            {
                // usually gets exception when stopping server
                return;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler(string.Format("DEBUG:ReceiveCallback:exception:{0}", e.ToString()));
            }
        }

        /// <summary>
        /// Sends the ping.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ElapsedEventArgs" /> instance containing the event
        /// data.</param>
        public void SendPing(object sender, ElapsedEventArgs e)
        {
            TimeSpan currentProcessTimeSpan = Process.GetCurrentProcess().TotalProcessorTime - _lastProcessorTime;
            TimeSpan currentDuration = DateTime.Now - _lastGetStatTime;
            TimeSpan processProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;

            if (_machineCpuCounter == null)
            {
                _machineCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }

            TimeSpan processDuration = DateTime.Now.Subtract(_cdfm.StartTime);

            UdpPingPacket packet = new UdpPingPacket()
            {
                UdpTraceType = Udp.UdpTraceType.Ping,
                ClientName = Environment.MachineName,
                UdpCounter = UdpCounter++,
                ClientPingTime = DateTime.Now.ToString("o"),
                ClientActivity = _cdfm.Config.Activity,
                TracesPerSecond = (_cdfm.ProcessedEvents / processDuration.TotalSeconds).ToString("F"),
                MatchedEvents = _cdfm.MatchedEvents,
                MissedMatchedEvents = _cdfm.MissedMatchedEvents,
                MaxQueueMissedEvents = CDFMonitor.Instance.Config.MissedControllerEvents,
                ParserQueue = _cdfm.RegexParserThread.QueueLength(),
                ProcessedEvents = _cdfm.ProcessedEvents,
                AvgProcessCpu = (((processProcessorTime.TotalMilliseconds / Environment.ProcessorCount) / processDuration.TotalMilliseconds) * 100).ToString("F"),
                CurrentProcessCpu = (((currentProcessTimeSpan.TotalMilliseconds / Environment.ProcessorCount) / currentDuration.TotalMilliseconds) * 100).ToString("F"),
                CurrentMachineCpu = _machineCpuCounter.NextValue(),

                // AvgMachineCpu = _machineCpuCounter.NextValue(),
                Duration = processDuration,
                FormattedTraceString = string.Empty
            };

            UDPPingWriter(packet);

            _lastProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;
            _lastGetStatTime = DateTime.Now;
        }

        /// <summary>
        /// Starts the listener.
        /// </summary>
        public void StartListener()
        {
            CDFMonitor.LogOutputHandler("DEBUG:UDP.StartListener:enter");
            if (!ListenerEnabled)
            {
                CDFMonitor.LogOutputHandler("DEBUG:UDP.StartListener:starting");
                ListenerEnabled = true;
                UdpReader();
            }

            CDFMonitor.LogOutputHandler("DEBUG:UDP.StartListener:exit");
        }

        /// <summary>
        /// Stops the listener.
        /// </summary>
        public void StopListener()
        {
            CDFMonitor.LogOutputHandler("DEBUG:UDP.StopListener:enter");
            if (ListenerEnabled)
            {
                CDFMonitor.LogOutputHandler("DEBUG:UDP.StopListener:stopping");
                ListenerEnabled = false;
            }

            CDFMonitor.LogOutputHandler("DEBUG:UDP.StopListener:exit");
        }

        /// <summary>
        /// UDPs the writer.
        /// </summary>
        /// <param name="packet">The args.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool UDPPingWriter(UdpPingPacket packet)
        {
            // CDFMonitor.LogOutputHandler("DEBUG:UDP.UDPPingWriter:packet:enter");
            Debug.Print("UDP.UDPPingWriter:packet:enter");

            try
            {
                if (!WriterEnabled)
                {
                    return false;
                }

                IFormatter formatter = new BinaryFormatter();
                MemoryStream stream = new MemoryStream();
                formatter.Serialize(stream, packet);

                byte[] sendbuf = new byte[stream.Length + 1];

                // set first byte to '1' to denote its an udp object
                sendbuf[0] = (byte)UdpTraceType.Unknown;

                stream.ToArray().CopyTo(sendbuf, 1);
                stream.Close();

                _udpPingClient.Send(sendbuf, sendbuf.Length);
                return true;
            }
            catch (Exception e)
            {
                Debug.Print("UDP.UDPWriter:packet: Exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// UDPs the writer.
        /// </summary>
        /// <param name="packet">The args.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool UDPWriter(string packet)
        {
            Debug.Print("DEBUG:UDP.UDPWriter:packet:enter");

            try
            {
                if (!WriterEnabled)
                {
                    return false;
                }

                byte[] sendbuf = new byte[packet.Length + 1];
                sendbuf[0] = (byte)UdpTraceType.TraceString;
                Encoding.ASCII.GetBytes(packet).CopyTo(sendbuf, 1);

                _udpClient.Send(sendbuf, sendbuf.Length);
                return true;
            }
            catch (Exception e)
            {
                Debug.Print("UDP.UDPWriter:packet: Exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// UDPs the writer trace.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool UDPWriterTrace(string data)
        {
            Debug.Print("Udp.UDPWriterTrace:" + data);
            return UDPWriter(string.Format("{0},{1},{2}", Environment.MachineName, UdpCounter++, data));
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Binds the client socket.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="serverPort">The server port.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private UdpClient BindClientSocket(string serverName, int serverPort)
        {
            UdpClient udpClient = new UdpClient();
            IPAddress serverIpAddress;

            try
            {
                if (WriterEnabled)
                {
                    IPAddress address;
                    if (IPAddress.TryParse(serverName, out address))
                    {
                        serverIpAddress = address;
                    }
                    else
                    {
                        serverIpAddress = Dns.GetHostAddresses(serverName)[0];
                    }

                    CDFMonitor.LogOutputHandler(string.Format("BindClientSocket:binding to:{0}:{1}", serverIpAddress, serverPort));
                    udpClient.Connect(new IPEndPoint(serverIpAddress, serverPort));
                }

                return udpClient;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("BindClientSocket:exception:" + e.ToString());
                return null;
            }
        }

        /// <summary>
        /// UDPs the reader.
        /// </summary>
        private void UdpReader()
        {
            UdpClient udpServer = new UdpClient();

            try
            {
                IPAddress ip;
                IPEndPoint endPoint;

                // check if local ip and if so assign listener get local IP addresses
                IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

                // test if any host IP is a loopback IP or is equal to any local IP
                IPAddress.TryParse(_settings.UdpServer, out ip);
                if (ip == null || (!IPAddress.IsLoopback(ip) && !localIPs.Contains(ip)))
                {
                    endPoint = new IPEndPoint(IPAddress.Any, _settings.UdpServerPort);
                    CDFMonitor.LogOutputHandler("Listening for UDP broadcast with any ip.");
                }
                else
                {
                    endPoint = new IPEndPoint(ip, _settings.UdpServerPort);
                    CDFMonitor.LogOutputHandler("Listening for UDP broadcast with specified ip:" + ip.ToString());
                }

                udpServer = new UdpClient(endPoint);

                while (ListenerEnabled)
                {
                    UdpState udpState = new UdpState();
                    udpState.groupEP = endPoint;
                    udpState.listener = udpServer;

                    udpServer.BeginReceive(new AsyncCallback(ReceiveCallback), udpState);

                    while (!_listenerCallback.WaitOne(100))
                    {
                        if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(0) | !ListenerEnabled)
                        {
                            return;
                        }
                    }
                }

                return;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler(string.Format("UdpListener:exception:{0}", e.ToString()));
            }
            finally
            {
                udpServer.Close();
            }
        }

        /// <summary>
        /// Uns the bind client socket.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool UnBindClientSockets()
        {
            try
            {
                if (_settings.UdpClientEnabled)
                {
                    _udpClient.Close();
                }

                if (_settings.UdpPingEnabled)
                {
                    _udpPingClient.Close();
                }
                return true;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("UnBindClientSocket:exception:" + e.ToString());
                return false;
            }
        }

        #endregion Private Methods

        #region Public Classes

        /// <summary>
        /// Class UdpState
        /// </summary>
        public class UdpState
        {
            #region Public Fields

            public IPEndPoint groupEP;
            public UdpClient listener;

            #endregion Public Fields
        }

        #endregion Public Classes

        #region Private Fields

        private CDFMonitor _cdfm;

        private DateTime _lastGetStatTime;

        private TimeSpan _lastProcessorTime;

        private volatile AutoResetEvent _listenerCallback = new AutoResetEvent(false);

        private PerformanceCounter _machineCpuCounter;

        private System.Timers.Timer _pingTimer;

        private Config.Configuration.ConfigurationProperties _settings;

        private UdpClient _udpClient;

        // = new UdpClient();
        private UdpClient _udpPingClient;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Udp" /> class.
        /// </summary>
        public Udp(CDFMonitor cdfmInstance)
        {
            _cdfm = cdfmInstance;
            _settings = _cdfm.Config.AppSettings;
        }

        #endregion Public Constructors

        #region Public Enums

        // = new UdpClient();
        /// <summary>
        /// Enum UdpTraceType
        /// </summary>
        public enum UdpTraceType
        {
            Unknown,
            Ping,
            PingReply,
            TraceEtl,
            TraceString
        }

        #endregion Public Enums

        /// <summary>
        /// Gets or sets a value indicating whether [listener enabled].
        /// </summary>
        /// <value><c>true</c> if [listener enabled]; otherwise, /c>.</value>
        public bool ListenerEnabled
        {
            get;
            private set;
        }

        public bool PingEnabled { get; set; }
    }

    [Serializable]
    public class UdpPingPacket
    {
        #region Public Properties

        public string AvgProcessCpu { get; set; }

        public Config.Configuration.ActivityType ClientActivity { get; set; }

        public string ClientName { get; set; }

        public string ClientPingTime { get; set; }

        public float CurrentMachineCpu { get; set; }

        public string CurrentProcessCpu { get; set; }

        public TimeSpan Duration { get; set; }

        public string FormattedTraceString { get; set; }

        public Int64 MatchedEvents { get; set; }

        public Int64 MaxQueueMissedEvents { get; set; }

        public Int64 MissedMatchedEvents { get; set; }

        public Int64 ParserQueue { get; set; }

        public Int64 ProcessedEvents { get; set; }

        public string TracesPerSecond { get; set; }

        public Int64 UdpCounter { get; set; }

        public Udp.UdpTraceType UdpTraceType { get; set; }

        #endregion Public Properties
    }
}