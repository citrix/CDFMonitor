// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="TMF.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Trace
{
    using CDFM.Config;
    using CDFM.Engine;
    using CDFM.FileManagement;
    using CDFM.Network;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// TMF variable argument types
    /// </summary>
    public enum ArgDetailType
    {
        Unknown,
        String,
        WideString,
        Pointer,
        Long,
        LongLong,
        Double,
        Short
    }

    /// <summary>
    /// Class used in internal list to hold individual TMF Guid information in memory so TMF cahce
    /// doesnt have to be hit.
    /// </summary>
    public class TMFTrace
    {
        #region Public Fields

        public Dictionary<int, Args> ArgsList = new Dictionary<int, Args>();
        public string BaseString = string.Empty;
        public string Class;
        public string EventStringBigEndian;
        public byte[] EventStringBytes;
        public string Function;
        public Guid Guid;
        public bool IsPopulated;
        public string Module;
        public string Source;
        public bool Status = true;
        public string TMFParsedString = "CDFMONITOR:ERR UNKNOWN TMF: ";
        public string TMFString;
        public string TMFVariables;
        public int TraceFunc;
        public int Version;

        #endregion Public Fields

        #region Public Classes

        /// <summary>
        /// Class Args
        /// </summary>
        public class Args
        {
            #region Public Fields

            public byte[] ArgBigEndianBytes;
            public string ArgDetailFormat; // = string.Empty;
            public string ArgDetailType; // = string.Empty;
            public string ArgReplacementString; // = string.Empty;
            public string ArgString;

            #endregion Public Fields

            // = string.Empty;
        }

        #endregion Public Classes
    }

    /// <summary>
    /// Class used to parse and format ETW trace events against the associated TMF files
    /// </summary>
    internal class TMF
    {
        #region Public Fields

        public static readonly Guid EventTraceHeaderTMF = new Guid(Properties.Resources.EventTraceHeaderTMFGuid);
        public static readonly Guid MagicTMF = new Guid(Properties.Resources.MagicTMFGuid);

        #endregion Public Fields

        #region Private Fields

        private const string TMF_ID_SEARCH_PATTERN = @"#typev.*? {0} ";
        private readonly string _cacheDir = string.Empty;
        private readonly HttpGet _httpGet;
        private readonly Dictionary<Guid, TmfTraces> _tmfsList;
        private readonly bool _writeToCache;
        private int _nextIndex;
        private int _pointerSize = 0;
        private int _startIndex;

        #endregion Private Fields

        #region Public Constructors

        //string cacheDir, string tmfServer)
        /// <summary>
        /// Initializes a new instance of the <see cref="TMF" /> class.
        /// </summary>
        public TMF()
        {
            TMFServerMissedCount = 0;
            TMFServerAlternateHitCount = 0;
            TMFServerHitCount = 0;
            TMFCacheMissedCount = 0;
            TMFCacheHitCount = 0;

            try
            {
                // Determine cachedir capability
                if (FileManager.CheckPath(CDFMonitor.Instance.Config.AppSettings.TmfCacheDir, true))
                {
                    _writeToCache = true;
                }

                _cacheDir = CDFMonitor.Instance.Config.AppSettings.TmfCacheDir;

                _httpGet = new HttpGet();
                _tmfsList = new Dictionary<Guid, TmfTraces>();

                AddTMFToList(new Guid(Properties.Resources.MagicTMFGuid), Properties.Resources.MagicTMF);
                AddTMFToList(EtwTraceWriter.EtwTraceGuid, Properties.Resources.CDFMonitorTMF);
                AddTMFToList(new Guid(Properties.Resources.EventTraceHeaderTMFGuid), Properties.Resources.EventTraceHeaderTMF);

                AddTMFToList(new Guid(Properties.Resources.ProcessTMFGuid), Properties.Resources.ProcessTMF);
                AddTMFToList(new Guid(Properties.Resources.ThreadTMFGuid), Properties.Resources.ThreadTMF);
                AddTMFToList(new Guid(Properties.Resources.UdpTMFGuid), Properties.Resources.UdpTMF);
                AddTMFToList(new Guid(Properties.Resources.TcpTMFGuid), Properties.Resources.TcpTMF);
            }
            catch (Exception e)
            {
                Status += e.ToString();
            }
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// Gets the status.
        /// </summary>
        /// <value>The status.</value>
        public string Status
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the TMF cache hit count.
        /// </summary>
        /// <value>The TMF cache hit count.</value>
        public Int64 TMFCacheHitCount
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the TMF cache missed count.
        /// </summary>
        /// <value>The TMF cache missed count.</value>
        public Int64 TMFCacheMissedCount
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the TMF server hit count.
        /// </summary>
        /// <value>The TMF server hit count.</value>
        public Int64 TMFServerAlternateHitCount
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the TMF server hit count.
        /// </summary>
        /// <value>The TMF server hit count.</value>
        public Int64 TMFServerHitCount
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the TMF server missed count.
        /// </summary>
        /// <value>The TMF server missed count.</value>
        public Int64 TMFServerMissedCount
        {
            get;
            set;
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Main method to process incoming ETW event. Searches for corresponding TMF and formats
        /// Trace Event.
        /// </summary>
        /// <param name="evt">ETW event</param>
        /// <returns>TMFTrace</returns>
        public TMFTrace ProcessTMFTrace(EventReadEventArgs evt)
        {
            try
            {
                Debug.Print("ProcessTMF:Enter:" + evt.EventGuid.ToString());
                _startIndex = 0;
                _nextIndex = 0;
                TMFTrace tmfTraceMsg = new TMFTrace();
                Status = string.Empty;

                // Add all other items to _tmf in case TMF file is not found
                tmfTraceMsg.Guid = evt.EventGuid;
                tmfTraceMsg.TraceFunc = evt.TraceFunc;
                tmfTraceMsg.EventStringBytes = evt.EventStringBytes();
                tmfTraceMsg.TMFParsedString += tmfTraceMsg.Guid;

                Debug.Print("ProcessTMF:tmf EventStringBytes:" + Encoding.ASCII.GetString(tmfTraceMsg.EventStringBytes));

                if (_tmfsList.ContainsKey(tmfTraceMsg.Guid))
                {
                    tmfTraceMsg = PopulateTraceMsg(evt, tmfTraceMsg);
                }
                else if (FileManager.FileExists(_cacheDir + "\\" + tmfTraceMsg.Guid + ".tmf"))
                {
                    // It is in cache
                    Debug.Print("ProcessTMF:cache hit");
                    string tmfContent = File.ReadAllText(_cacheDir + "\\" + tmfTraceMsg.Guid + ".tmf");

                    // make sure cache copy contains ID
                    if (!Regex.IsMatch(tmfContent, string.Format(TMF_ID_SEARCH_PATTERN, tmfTraceMsg.TraceFunc))
                        && DownloadTMF(tmfTraceMsg))
                    {
                        Debug.Print("ProcessTMF:DownloadTMF ALTERNATE successful");
                    }

                    // Add item 0 with complete TMF text regardless if id found
                    AddTMFToList(tmfTraceMsg.Guid,
                                 tmfTraceMsg.BaseString =
                                 tmfContent);
                    TMFCacheHitCount++;
                }
                else
                {
                    Debug.Print("ProcessTMF:cache miss");
                    TMFCacheMissedCount++;

                    // Download from server
                    if (DownloadTMF(tmfTraceMsg))
                    {
                        Debug.Print("ProcessTMF:DownloadTMF successful");
                        TMFServerHitCount++;
                    }
                    else
                    {
                        Debug.Print("ProcessTMF:DownloadTMF failed");
                        TMFServerMissedCount++;

                        // Add empty key to dictionary so we dont keep looking for unfound tmf
                        AddTMFToList(tmfTraceMsg.Guid, "");
                    }
                }

                if (tmfTraceMsg.BaseString.Length < 1)
                {
                    Debug.Print("ProcessTMF: Error: unable to find tmf:" + evt.EventGuid.ToString());
                    // display bytes as a comma separated string
                    tmfTraceMsg.TMFParsedString =
                        String.Format("CDFMONITOR:ERR Missing TMF. ID={0}: BYTE STRING={1}: MISSING TMF={2} ",
                                      tmfTraceMsg.TraceFunc,
                                      Encoding.ASCII.GetString(tmfTraceMsg.EventStringBytes).Replace("\0", ""),
                                      tmfTraceMsg.Guid.ToString());

                    tmfTraceMsg.Status = false;
                    return (tmfTraceMsg);
                }

                if (tmfTraceMsg.IsPopulated)
                {
                    Debug.Print("ProcessTMF:_tmfTraceMsg already populated");
                    if (tmfTraceMsg.ArgsList.Count > 0)
                    {
                        ReplaceTMFVars(tmfTraceMsg);
                    }
                }
                else
                {
                    // Send TMF string to parser and return
                    Debug.Print("ProcessTMF:_tmfTraceMsg not populated");
                    ParseTMF(ref tmfTraceMsg);
                    if (tmfTraceMsg.IsPopulated)
                    {
                        _tmfsList[tmfTraceMsg.Guid].Add(tmfTraceMsg);
                    }
                    else
                    {
                        // TMF exists but ID in TMF does not
                        tmfTraceMsg.TMFParsedString =
                            String.Format("CDFMONITOR:ERR Missing ID={0}: MODULE={1}: BYTE STRING={2}: in existing TMF={3}: Try deleting TMF cache.",
                                          tmfTraceMsg.TraceFunc,
                                          tmfTraceMsg.Module,
                                          Encoding.ASCII.GetString(tmfTraceMsg.EventStringBytes).Replace("\0", ""),
                                          tmfTraceMsg.Guid.ToString());
                        tmfTraceMsg.Status = false;
                    }
                }

                return (tmfTraceMsg);
            }
            catch (Exception e)
            {
                Status += e.ToString();
                return (new TMFTrace());
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Adds TMF guid and complete TMF contents to private _tmfList
        /// </summary>
        /// <param name="tmfGuid">Guid string</param>
        /// <param name="baseString">TMF file content string</param>
        private void AddTMFToList(Guid tmfGuid, string baseString)
        {
            Debug.Print("AddTMFToList:Enter:" + tmfGuid.ToString());
            if (!_tmfsList.ContainsKey(tmfGuid))
            {
                var list = new TmfTraces();
                list.BaseTmfString = baseString;
                if (!string.IsNullOrEmpty((baseString)))
                {
                    list.IsPopulated = true;
                    // Codemaid likes to mess this up cause of '//'
                    // list.Module = Regex.Match(baseString, @".* (?<pdbpath>.*?) // SRC=").Groups["pdbpath"].Value;
                    list.Module = Regex.Match(baseString, @".* (?<pdbpath>.*?) // SRC=").Groups["pdbpath"].Value;
                }

                _tmfsList.Add(tmfGuid, list);
                Debug.Print("AddTMFToList:Added");
            }
            else
            {
                Debug.Print("AddTMFToList:Error guid already exists in list:" + tmfGuid.ToString());
            }
        }

        /// <summary>
        /// FindAlternateTMF searches TMF 'archive' folder for given function ID
        /// </summary>
        /// <param name="tmfTraceMsg"></param>
        /// <param name="tmfServer"></param>
        /// <returns></returns>
        private string DownloadAlternateTMF(TMFTrace tmfTraceMsg, string tmfServer)
        {
            string result = string.Empty;
            // look in archive and get count
            CDFMonitor.LogOutputHandler("CDFMONITOR:WARNING:TMF ID NOT IN CURRENT TMF. SEARCHING ARCHIVE");
            if (GetTmfInfo(tmfTraceMsg.Guid, tmfServer, string.Format("{0}/archive/{1}/version.txt", tmfServer, tmfTraceMsg.Guid), ref result))
            {
                // loop through all files in archive for match
                int total = Convert.ToInt32(result);
                for (int i = total; i >= 1; i--)
                {
                    if (GetTmfInfo(tmfTraceMsg.Guid, tmfServer, string.Format("{0}/archive/{1}/{1}.{2}.tmf", tmfServer, tmfTraceMsg.Guid, i), ref result)
                        && Regex.IsMatch(result, string.Format(TMF_ID_SEARCH_PATTERN, tmfTraceMsg.TraceFunc)))
                    {
                        CDFMonitor.LogOutputHandler(string.Format("CDFMONITOR:WARNING:TMF ID NOT IN CURRENT TMF. FOUND ALTERNATE VER:{0}", i));
                        TMFServerAlternateHitCount++;
                        tmfTraceMsg.Version = i;
                        break;
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Finds the TMF.
        /// </summary>
        /// <param name="tmfTraceMsg">The TMF trace MSG.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool DownloadTMF(TMFTrace tmfTraceMsg)
        {
            string result = string.Empty;

            try
            {
                foreach (string tmfServer in CDFMonitor.Instance.Config.TMFServersList)
                {
                    string server = tmfServer.TrimEnd(new char[] { '/', '\\' });
                    if (string.IsNullOrEmpty(tmfServer))
                    {
                        return false;
                    }

                    if (GetTmfInfo(tmfTraceMsg.Guid, server, string.Format("{0}\\{1}.tmf", server, tmfTraceMsg.Guid), ref result))
                    {
                        // make sure id exists in tmf
                        if (!Regex.IsMatch(result, string.Format(TMF_ID_SEARCH_PATTERN, tmfTraceMsg.TraceFunc)))
                        {
                            result = DownloadAlternateTMF(tmfTraceMsg, tmfServer);
                        }

                        break;
                    }
                }

                if (string.IsNullOrEmpty(result))
                {
                    return false;
                }
                else
                {
                    tmfTraceMsg.BaseString = result;

                    // Write file to dictionary
                    RemoveTMFFromList(tmfTraceMsg.Guid);
                    AddTMFToList(tmfTraceMsg.Guid, result);
                    return true;
                }
            }
            catch (Exception e)
            {
                Status += e.ToString();
                return false;
            }
        }

        /// <summary>
        /// Enumerates TMF traces with given guid and complete TMF contents from private _tmfList
        /// </summary>
        /// <param name="tmfGuid">Guid string</param>
        private TmfTraces EnumTMFFromList(Guid tmfGuid)
        {
            TmfTraces trace = new TmfTraces();

            Debug.Print("EnumTMFFromList:Enter:" + tmfGuid.ToString());
            if (_tmfsList.ContainsKey(tmfGuid))
            {
                if (_tmfsList.TryGetValue(tmfGuid, out trace))
                {
                    Debug.Print("EnumTMFFromList:Enumerated");
                }
                else
                {
                    Debug.Print("ERROR:EnumTMFFromList:Enumeration failed.");
                }
            }
            else
            {
                Debug.Print("EnumTMFFromList:Error guid does not exist in list:" + tmfGuid.ToString());
            }

            return trace;
        }

        /// <summary>
        /// Gets length of string types variables and sets _startIndex and _nextIndex
        /// </summary>
        /// <param name="zeroByteCount">Length of terminator based on string type</param>
        /// <param name="tmfTraceMsg">The TMF trace MSG.</param>
        /// <returns>Returns true if successful</returns>
        private bool GetNextByteRange(int zeroByteCount, TMFTrace tmfTraceMsg)
        {
            Debug.Print(
                string.Format("GetNextByteRange before:_startIndex:{0} _nextIndex:{1} EventStringBytes.Length-1:{2}",
                              _startIndex, _nextIndex, tmfTraceMsg.EventStringBytes.Length - 1));

            int tmpIndex = _nextIndex - 1;

            while (tmpIndex <= tmfTraceMsg.EventStringBytes.Length - 1)
            {
                tmpIndex = Array.IndexOf<byte>(tmfTraceMsg.EventStringBytes, 00, ++tmpIndex);

                //zerobyte match
                if (tmpIndex >= 0
                    && ((zeroByteCount < 2)
                        || (tmpIndex < tmfTraceMsg.EventStringBytes.Length - 2
                            && tmfTraceMsg.EventStringBytes[tmpIndex + 1] == 00)))
                {
                    _startIndex = _nextIndex;
                    _nextIndex = tmpIndex;
                    Debug.Print(
                        string.Format(
                            "GetNextByteRange after:_startIndex:{0} _nextIndex:{1} EventStringBytes.Length-1:{2}",
                            _startIndex, _nextIndex, tmfTraceMsg.EventStringBytes.Length - 1));

                    if (_startIndex + 1 == _nextIndex)
                    {
                        return false;
                    }

                    return true;
                }
                else if (tmpIndex < 0)
                {
                    return false;
                }
            }
            Debug.Print("GetNextByteRange index fail" + _startIndex.ToString() + ":" + _nextIndex.ToString());
            return false;
        }

        /// <summary>
        /// Switch to determine variable type and size inside ETW trace message
        /// </summary>
        /// <param name="argDetailTypeType">Type of the arg detail type.</param>
        /// <param name="tmfTraceMsg">The TMF trace MSG.</param>
        /// <returns>Returns true if successful</returns>
        private bool GetNextByteRangeSwitch(ArgDetailType argDetailTypeType, TMFTrace tmfTraceMsg)
        {
            Debug.Print("argDetailTypeType:" + argDetailTypeType.ToString());
            switch (argDetailTypeType)
            {
                case ArgDetailType.Short:
                    if (_startIndex <= tmfTraceMsg.EventStringBytes.Length - 2)
                    {
                        _nextIndex = _nextIndex + 1;
                        return true;
                    }
                    else
                    {
                        Debug.Print(
                            string.Format(
                                "GetNextByteRange:Short:Nonstandard byte range:start:{0} next:{1} byte length: {2}",
                                _startIndex, _nextIndex,
                                tmfTraceMsg.EventStringBytes.Length));
                        return true;
                    }

                case ArgDetailType.Long:

                    if (_startIndex <= tmfTraceMsg.EventStringBytes.Length - 4)
                    {
                        _nextIndex = _nextIndex + 3;
                        return true;
                    }
                    else
                    {
                        Debug.Print(
                            string.Format(
                                "GetNextByteRange:Long:Nonstandard byte range:start:{0} next:{1} byte length: {2}",
                                _startIndex, _nextIndex,
                                tmfTraceMsg.EventStringBytes.Length));
                        return true;
                    }

                case ArgDetailType.Pointer:

                    _pointerSize = _pointerSize > 0
                        ? _pointerSize
                        : (int)CDFMonitor.Instance.Consumer.BufferCallback.LogfileHeader.BufferUnion.PointerSize > 0
                            ? (int)CDFMonitor.Instance.Consumer.BufferCallback.LogfileHeader.BufferUnion.PointerSize
                            : 4;

                    if (_startIndex <= tmfTraceMsg.EventStringBytes.Length - _pointerSize)
                    {
                        _nextIndex = _nextIndex + _pointerSize - 1;
                        return true;
                    }
                    else
                    {
                        Debug.Print(
                            string.Format(
                                "GetNextByteRange:Pointer:Nonstandard byte range:start:{0} next:{1} byte length: {2}",
                                _startIndex, _nextIndex,
                                tmfTraceMsg.EventStringBytes.Length));
                        return true;
                    }

                case ArgDetailType.Double:
                case ArgDetailType.LongLong:

                    // Hard coded at least 32bit to 8 bytes?
                    if (_startIndex <= tmfTraceMsg.EventStringBytes.Length - 8)
                    {
                        _nextIndex = _nextIndex + 7;
                        return true;
                    }
                    else
                    {
                        Debug.Print(
                            string.Format(
                                "GetNextByteRange:LongLong:Nonstandard byte range:start:{0} next:{1} byte length: {2}",
                                _startIndex, _nextIndex,
                                tmfTraceMsg.EventStringBytes.Length));
                        return true;
                    }

                case ArgDetailType.String:
                    return (GetNextByteRange(1, tmfTraceMsg));

                case ArgDetailType.WideString:
                    return (GetNextByteRange(3, tmfTraceMsg));

                case ArgDetailType.Unknown:

                default:
                    Debug.Print("Error:unknown argDetailTypeType:" + argDetailTypeType.ToString());
                    return false;
            }
        }

        /// <summary>
        /// Downloads the TMF and TMF version info
        /// </summary>
        /// <param name="tmfTraceGuid"></param>
        /// <returns></returns>
        //private bool DownloadTMF(TMFTrace tmfTraceMsg, string tmfServer, string tmfServerPath, ref string result)
        private bool GetTmfInfo(Guid tmfTraceGuid, string tmfServer, string tmfServerPath, ref string result)
        {
            CDFMonitor.LogOutputHandler(string.Format("DEBUG:GetTmfInfo:enter:{0}:{1}:{2}", tmfTraceGuid, tmfServer, tmfServerPath));
            ResourceManagement rm = new ResourceManagement();

            // Remove trailing slashes
            tmfServerPath = tmfServerPath.TrimEnd(new char[] { '/', '\\' });

            ResourceManagement.ResourceType rt = rm.GetPathType(tmfServer);
            if (rm.CheckResourceCredentials(tmfServer) != ResourceManagement.CommandResults.Successful)
            {
                CDFMonitor.LogOutputHandler("DEBUG:DownloadTMF: checkresourcecredentials failed. returning false.");
                return false;
            }

            if (rt == ResourceManagement.ResourceType.Unc
                | rt == ResourceManagement.ResourceType.Local)
            {
                // convert to backslash
                tmfServerPath = tmfServerPath.Replace('/', '\\');
                if (!FileManager.FileExists(tmfServerPath))
                {
                    return false;
                }

                if (_writeToCache && FileManager.CopyFile(tmfServerPath, _cacheDir))
                {
                    result = File.ReadAllText(string.Format("{0}\\{1}.tmf", _cacheDir, tmfTraceGuid));
                    return true;
                }
                else
                {
                    try
                    {
                        result = File.ReadAllText(tmfServerPath);
                        return true;
                    }
                    catch (Exception e)
                    {
                        CDFMonitor.LogOutputHandler("DownloadTMF: read from TMF server failed:" + e.ToString());
                        return false;
                    }
                }
            }
            else if (rt == ResourceManagement.ResourceType.Url)
            {
                // convert to forward slash
                tmfServerPath = tmfServerPath.Replace('\\', '/');

                string path = string.Format(tmfServerPath, tmfServer, tmfTraceGuid);

                result = _httpGet.GetRequest(path, rm.GetCredentials(tmfServer));
                Status += _httpGet.Status;

                if (_httpGet.Status.Contains("404") || _httpGet.Status.Contains("401"))
                {
                    result = string.Empty;
                    return false;
                }
                else
                {
                    if (_writeToCache)
                    {
                        string file = string.Format("{0}\\{1}.tmf", _cacheDir, tmfTraceGuid);
                        if (FileManager.FileExists(file))
                        {
                            FileManager.DeleteFile(file, false);
                        }

                        File.WriteAllText(file, result, Encoding.ASCII);
                    }
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Parses TMF file content to find matching TMF Function string and populates _tmfTraceMsg
        /// with TMF Function
        /// </summary>
        /// <param name="tmfTraceMsg">The TMF trace MSG.</param>
        /// <returns>returns true if successful</returns>
        private bool ParseTMF(ref TMFTrace tmfTraceMsg)
        {
            try
            {
                Debug.Print("ParseTMF:Enter");

                // Populate eventstring info into TMF object

                bool retval = false;

                var tmfRegex = new Regex("(#typev\\s*?(?<Source>\\w*?)\\s*?" + tmfTraceMsg.TraceFunc
                                         +
                                         "\\s*?(?<TMFString>\"%0.*?\"))\\s*?(\\r\\n|//.*?(CLASS|LEVEL)=(?<Class>\\w*).*?FUNC=(?<Func>.*?)\\s*)"
                                         + "(?<TMFVariables>{.*?})", RegexOptions.Singleline);

                MatchCollection mc = tmfRegex.Matches(tmfTraceMsg.BaseString);
                if (mc.Count < 1)
                {
                    Debug.Print(string.Format("ParseTMF: Error: Could not find TraceFunc:{0}", tmfTraceMsg.TraceFunc));
                    return false;
                }

                foreach (Match m in mc)
                {
                    tmfTraceMsg.BaseString = m.ToString();
                    tmfTraceMsg.TMFString = m.Groups["TMFString"].Value.Replace("%0", "");
                    Debug.Print("ParseTMF:TMFString" + tmfTraceMsg.TMFString);
                    tmfTraceMsg.TMFVariables = m.Groups["TMFVariables"].Value;
                    tmfTraceMsg.Source = m.Groups["Source"].Value;
                    tmfTraceMsg.Class = m.Groups["Class"].Value;
                    tmfTraceMsg.Function = m.Groups["Func"].Value;
                    tmfTraceMsg.Module = _tmfsList[tmfTraceMsg.Guid].Module;
                    retval = true;
                }

                // Add variables to 'TMFString'
                if (retval)
                {
                    return (ReplaceTMFVars(tmfTraceMsg));
                }

                return retval;
            }
            catch (Exception e)
            {
                Debug.Print("ParseTMF:Error Exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Populates the trace MSG.
        /// </summary>
        /// <param name="evt">The <see cref="EventReadEventArgs"/> instance containing the event
        /// data.</param>
        /// <param name="tmfTraceMsg">The TMF trace MSG.</param>
        /// <returns>TMFTrace.</returns>
        private TMFTrace PopulateTraceMsg(EventReadEventArgs evt, TMFTrace tmfTraceMsg)
        {
            int func = tmfTraceMsg.TraceFunc;

            Debug.Print("ProcessTMF:_tmfList contains key for tmf");

            tmfTraceMsg.Module = _tmfsList[tmfTraceMsg.Guid].Module;

            // Is tmfTrace list populated?
            if (_tmfsList[tmfTraceMsg.Guid].IsPopulated)
            {
                Debug.Print("ProcessTMF:_tmfList.ArgsList is populated");

                // Determine if traceFunc has been populated
                TMFTrace tmf = _tmfsList[tmfTraceMsg.Guid].Find(t => t.TraceFunc == func);

                if (tmf == null)
                {
                    Debug.Print("ProcessTMF:_tmfList.ArgsList does not contain TraceFunc. populating...");

                    // Get complete TMF from list item 0
                    tmfTraceMsg.BaseString = _tmfsList[tmfTraceMsg.Guid].BaseTmfString;
                }
                else
                {
                    Debug.Print("ProcessTMF:_tmfList.ArgsList contains TraceFunc");
                    tmfTraceMsg = tmf;
                    tmfTraceMsg.Guid = evt.EventGuid;
                    tmfTraceMsg.TraceFunc = evt.TraceFunc;
                    tmfTraceMsg.EventStringBytes = evt.EventStringBytes();
                    if (!tmfTraceMsg.IsPopulated)
                    {
                        Debug.Print(
                            "ProcessTMF:_tmfList.ArgsList contains TraceFunc but TraceFunc not found in TMF file. returning");
                        return (tmfTraceMsg);
                    }
                }
            }
            else
            {
                // Check to see if it is eventheader
                if (tmfTraceMsg.Guid == EventTraceHeaderTMF)
                {
                    tmfTraceMsg.TMFParsedString = "Start of Trace";
                    tmfTraceMsg.Status = true;
                }
                else
                {
                    // TMF could not be found in previous search so return
                    Debug.Print("ProcessTMF:tmf not found");
                    tmfTraceMsg.Status = false;
                }

                return (tmfTraceMsg);
            }

            return (tmfTraceMsg);
        }

        /// <summary>
        /// Removes TMF guid and complete TMF contents from private _tmfList
        /// </summary>
        /// <param name="tmfGuid">Guid string</param>
        private void RemoveTMFFromList(Guid tmfGuid)
        {
            Debug.Print("RemoveTMFFromList:Enter:" + tmfGuid.ToString());
            if (_tmfsList.ContainsKey(tmfGuid))
            {
                _tmfsList.Remove(tmfGuid);
                Debug.Print("RemoveTMFFromList:Removed");
            }
            else
            {
                Debug.Print("RemoveTMFFromList:Error guid does not exist in list:" + tmfGuid.ToString());
            }
        }

        /// <summary>
        /// Replaces variables in TMF message with variables provided by ETW trace event
        /// </summary>
        /// <param name="tmfTraceMsg">The TMF trace MSG.</param>
        /// <returns>Returns true if successful</returns>
        private bool ReplaceTMFVars(TMFTrace tmfTraceMsg)
        {
            Debug.Print("ReplaceTMFVars:Enter");
            tmfTraceMsg.TMFParsedString = tmfTraceMsg.TMFString;

            // Check to see if we already have arguments and types
            if (!tmfTraceMsg.IsPopulated)
            {
                tmfTraceMsg.IsPopulated = true;

                // Get arguments
                MatchCollection argCol = Regex.Matches(tmfTraceMsg.TMFParsedString,
                                                       "(%(?<argNumber>\\d*?)!(?<argType>.*?)!)");

                // If no arguments return TMF as no more processing needs to be done
                if (argCol.Count < 1)
                {
                    return true;
                }

                int argNumber = 0;
                int fillerNumber = argCol.Count + 10;

                // Loop through arguments
                foreach (Match arg in argCol)
                {
                    // Determine
                    var argItems = new TMFTrace.Args();
                    argItems.ArgString = arg.Value;
                    argItems.ArgDetailFormat = arg.Groups["argType"].Value;

                    // Not all arguments have a number %!FUNC!
                    if (arg.Groups["argNumber"].Value.Length < 2)
                    {
                        Debug.Print("ReplaceTMFVars:fillernumber:" + fillerNumber.ToString());
                        tmfTraceMsg.ArgsList.Add(fillerNumber++, argItems);
                        argNumber = fillerNumber;
                    }
                    else
                    {
                        argNumber = Convert.ToInt32(arg.Groups["argNumber"].Value);
                        tmfTraceMsg.ArgsList.Add(argNumber, argItems);
                    }
                }

                // Get argDetailType
                MatchCollection argDetailCol = Regex.Matches(tmfTraceMsg.TMFVariables,
                                                             "((?<argDetailTypeVariableName>\\w*).*?,.*?(?<argDetailType>\\w*?)(\\(.*\\))?\\s*--\\s*(?<argNumber>\\d*))");

                if (argDetailCol.Count > 0)
                {
                    // Loop through detail arguments
                    foreach (Match arg in argDetailCol)
                    {
                        int dargNumber = Convert.ToInt32(arg.Groups["argNumber"].Value);
                        if (tmfTraceMsg.ArgsList.ContainsKey(dargNumber))
                        {
                            tmfTraceMsg.ArgsList[dargNumber].ArgDetailType
                                = arg.Groups["argDetailType"].Value;
                        }
                    }
                }
            } //end if !IsPopulated

            foreach (KeyValuePair<int, TMFTrace.Args> argItem in tmfTraceMsg.ArgsList.OrderBy(i => i.Key))
            {
                Debug.Print("ArgItem.Key:" + argItem.Key.ToString());
                argItem.Value.ArgReplacementString = string.Empty;
                bool unsigned = false;

                switch (argItem.Value.ArgDetailType)
                {
                    case "ItemUnknown":
                    case "ItemTimeStamp":
                    case "ItemMerror":
                    case "ItemNETEVENT":
                    case "ItemWINERROR":
                    case "ItemNTSTATUS":
                    case "ItemNTerror":
                    case "ItemGuid":
                    case "ItemListLong":
                    case "ItemListShort":
                    case "ItemListByte":
                    case "ItemPort":
                    case "ItemCLSID":
                    //case "ItemIPAddr":
                    case "ItemKSid":
                    case "ItemMLString":
                    case "ItemRString":
                    case "ItemString":
                        if (GetNextByteRangeSwitch(ArgDetailType.String, tmfTraceMsg))
                        {
                            argItem.Value.ArgReplacementString = Encoding.ASCII.GetString(tmfTraceMsg.EventStringBytes)
                                .Substring(_startIndex, _nextIndex - _startIndex);
                            Debug.Print("ReplaceTMFVars:ItemString:" + argItem.Value.ArgReplacementString);
                        }
                        else // Print error
                        {
                            tmfTraceMsg.Status = false;
                            Debug.Print("DEBUG:ReplaceTMFVars:ItemString:Error:" + argItem.Value.ArgReplacementString);
                        }
                        _nextIndex++;
                        break;

                    case "ItemIPAddr":

                        // kernel network tracing uses this for ipv4 assume 4 bytes
                        // todo: ipv6?
                        argItem.Value.ArgReplacementString = string.Format("{0}.{1}.{2}.{3}",
                            tmfTraceMsg.EventStringBytes[_startIndex++],
                            tmfTraceMsg.EventStringBytes[_startIndex++],
                            tmfTraceMsg.EventStringBytes[_startIndex++],
                            tmfTraceMsg.EventStringBytes[_startIndex++]);

                        Debug.Print("ReplaceTMFVars:ItemIPAddr:" + argItem.Value.ArgReplacementString);
                        _nextIndex = _startIndex;
                        break;

                    case "ItemPString":

                        // first byte contains string length
                        _nextIndex = tmfTraceMsg.EventStringBytes[_startIndex];
                        _startIndex += 2;
                        argItem.Value.ArgReplacementString = Encoding.ASCII.GetString(tmfTraceMsg.EventStringBytes)
                            .Substring(_startIndex, _nextIndex);

                        _nextIndex += 2;
                        Debug.Print("ReplaceTMFVars:ItemPString:" + argItem.Value.ArgReplacementString);
                        break;

                    case "ItemPWString":

                        // first byte contains string length
                        _nextIndex = tmfTraceMsg.EventStringBytes[_startIndex];
                        _startIndex += 2;
                        argItem.Value.ArgReplacementString = Encoding.ASCII.GetString(tmfTraceMsg.EventStringBytes)
                            .Substring(_startIndex, _nextIndex)
                            .Replace("\0", "");
                        Debug.Print("ReplaceTMFVars:ItemPWString:" + argItem.Value.ArgReplacementString);
                        _nextIndex += 2;
                        break;

                    case "ItemNWString":
                    case "ItemWString":
                        if (GetNextByteRangeSwitch(ArgDetailType.WideString, tmfTraceMsg))
                        {
                            argItem.Value.ArgReplacementString = Encoding.ASCII.GetString(tmfTraceMsg.EventStringBytes)
                                .Substring(_startIndex, _nextIndex - _startIndex)
                                .Replace("\0", "");
                            Debug.Print("ReplaceTMFVars:ItemWString:" + argItem.Value.ArgReplacementString);
                        }
                        else // Print error
                        {
                            tmfTraceMsg.Status = false;
                            Debug.Print("DEBUG:ReplaceTMFVars:ItemWString:Error:" + argItem.Value.ArgReplacementString);
                        }

                        // Increment past null char 0
                        _nextIndex += 3;
                        break;

                    case "ItemUChar":
                    case "ItemChar": // 1 byte
                        _nextIndex++;

                        argItem.Value.ArgReplacementString = Encoding.ASCII.GetString(tmfTraceMsg.EventStringBytes)
                            .Substring(_startIndex, _nextIndex - _startIndex);
                        Debug.Print("ReplaceTMFVars:ItemChar:" + argItem.Value.ArgReplacementString);
                        break;

                    case "ItemUShort":
                    case "ItemUshort":
                        unsigned = true;
                        goto case "ItemShort";
                    case "ItemCharShort":
                    case "ItemCharSign":
                    case "ItemShort":
                        if (GetNextByteRangeSwitch(ArgDetailType.Short, tmfTraceMsg))
                        {
                            if (unsigned)
                            {
                                argItem.Value.ArgReplacementString =
                                    BitConverter.ToUInt16(tmfTraceMsg.EventStringBytes, _startIndex).ToString();
                            }
                            else
                            {
                                argItem.Value.ArgReplacementString =
                                    BitConverter.ToInt16(tmfTraceMsg.EventStringBytes, _startIndex).ToString();
                            }
                        }
                        else // Print error
                        {
                            CDFMonitor.LogOutputHandler("DEBUG:ItemShort:" + argItem.Value.ArgReplacementString);
                            tmfTraceMsg.Status = false;
                        }

                        _nextIndex++;
                        break;

                    case "ItemPtr": // 4 or 8 bytes

                        if (GetNextByteRangeSwitch(ArgDetailType.Pointer, tmfTraceMsg))
                        {
                            for (int iByte = _nextIndex; iByte >= _startIndex; iByte--)
                            {
                                string s = tmfTraceMsg.EventStringBytes[iByte].ToString("X");
                                argItem.Value.ArgReplacementString += s.Length == 1 ? "0" + s : s;
                            }

                            Debug.Print("ReplaceTMFVars:ItemPtr:" + argItem.Value.ArgReplacementString);
                        }
                        else // Print error
                        {
                            for (int i = tmfTraceMsg.EventStringBytes.Length - 1; i >= 0; i--)
                            {
                                string s = tmfTraceMsg.EventStringBytes[i].ToString("X");
                                tmfTraceMsg.EventStringBigEndian += s;
                            }

                            CDFMonitor.LogOutputHandler("DEBUG:ReplaceTMFVars:ItemPtr:Error:" +
                                                          argItem.Value.ArgReplacementString);
                            tmfTraceMsg.Status = false;
                        }
                        _nextIndex++;
                        break;

                    case "ItemULong":
                    case "ItemUlong":
                    case "ItemULongX":
                        unsigned = true;
                        goto case "ItemLong";
                    case "ItemChar4":
                    case "ItemHRESULT":
                    case "ItemLong": // 4 bytes
                        if (GetNextByteRangeSwitch(ArgDetailType.Long, tmfTraceMsg))
                        {
                            // Reverse to BigEndian and truncate array to size of argument
                            argItem.Value.ArgBigEndianBytes = new byte[_nextIndex - _startIndex + 1];

                            Array.Copy(tmfTraceMsg.EventStringBytes.Reverse().ToArray(),
                                       Math.Max(0, tmfTraceMsg.EventStringBytes.Length - _nextIndex - 1),
                                       argItem.Value.ArgBigEndianBytes, 0, _nextIndex - _startIndex + 1);
                            Debug.Print("ItemLong:BigEndian:" + BitConverter.ToString(argItem.Value.ArgBigEndianBytes));

                            // Convert to hex from decimal base 16 and remove dashes
                            argItem.Value.ArgReplacementString =
                                BitConverter.ToString(argItem.Value.ArgBigEndianBytes).Replace("-", "");

                            Debug.Print("ItemLong:hex:" + argItem.Value.ArgReplacementString);

                            // Trim leading zero's if more than one digit
                            if (argItem.Value.ArgReplacementString.TrimStart('0').Length > 0)
                            {
                                argItem.Value.ArgReplacementString = argItem.Value.ArgReplacementString.TrimStart('0');
                                Debug.Print("ItemLong:trim:" + argItem.Value.ArgReplacementString);

                                if (argItem.Value.ArgDetailFormat.ToLower().Contains("d")
                                    || argItem.Value.ArgDetailFormat.ToLower().Contains("u")
                                    || argItem.Value.ArgDetailFormat.ToLower().Contains("s"))
                                {
                                    // Convert hex string to decimal string
                                    if (unsigned)
                                    {
                                        argItem.Value.ArgReplacementString =
                                        Convert.ToUInt32(argItem.Value.ArgReplacementString, 16).ToString();
                                        Debug.Print("ItemULong:decimal:" + argItem.Value.ArgReplacementString);
                                    }
                                    else
                                    {
                                        argItem.Value.ArgReplacementString =
                                            Convert.ToInt32(argItem.Value.ArgReplacementString, 16).ToString();
                                        Debug.Print("ItemLong:decimal:" + argItem.Value.ArgReplacementString);
                                    }
                                }
                            }
                            else
                            {
                                Debug.Print("ItemLong: empty array:" + argItem.Value.ArgReplacementString);
                                argItem.Value.ArgReplacementString = "0";
                            }
                        }
                        else // Print error
                        {
                            CDFMonitor.LogOutputHandler("DEBUG:ItemLong:" + argItem.Value.ArgReplacementString);
                            tmfTraceMsg.Status = false;
                        }

                        _nextIndex++;
                        break;

                    case "ItemDouble": // 8 bytes
                        if (GetNextByteRangeSwitch(ArgDetailType.Double, tmfTraceMsg))
                        {
                            argItem.Value.ArgReplacementString =
                                BitConverter.ToDouble(tmfTraceMsg.EventStringBytes, _startIndex).ToString();
                        }
                        _nextIndex++;
                        break;

                    case "ItemULongLong":
                        unsigned = true;
                        goto case "ItemLongLong";
                    case "ItemLongLong": // 8 bytes

                        // ItemLongLong is 8 bytes for 32 bit, 16 bytes for 64
                        if (GetNextByteRangeSwitch(ArgDetailType.LongLong, tmfTraceMsg))
                        {
                            // Reverse to BigEndian and truncate array to size of argument
                            argItem.Value.ArgBigEndianBytes = new byte[_nextIndex - _startIndex + 1];

                            Array.Copy(tmfTraceMsg.EventStringBytes.Reverse().ToArray(),
                                       Math.Max(0, tmfTraceMsg.EventStringBytes.Length - _nextIndex - 1),
                                       argItem.Value.ArgBigEndianBytes, 0, _nextIndex - _startIndex + 1);
                            Debug.Print("ItemLongLong:BigEndian:" +
                                        BitConverter.ToString(argItem.Value.ArgBigEndianBytes));

                            // Convert to hex from decimal base 16 and remove dashes
                            argItem.Value.ArgReplacementString =
                                BitConverter.ToString(argItem.Value.ArgBigEndianBytes).Replace("-", "");

                            Debug.Print("ItemLongLong:hex:" + argItem.Value.ArgReplacementString);

                            // Trim leading zero's if more than one digit
                            if (argItem.Value.ArgReplacementString.TrimStart('0').Length > 0)
                            {
                                argItem.Value.ArgReplacementString = argItem.Value.ArgReplacementString.TrimStart('0');
                                Debug.Print("ItemLongLong:trim:" + argItem.Value.ArgReplacementString);

                                // Check variable sub type
                                if (argItem.Value.ArgDetailFormat.ToLower().Contains("i64d")
                                    || argItem.Value.ArgDetailFormat.ToLower().Contains("i64u")
                                    || argItem.Value.ArgDetailFormat.ToLower().Contains("s"))
                                {
                                    // Convert hex string to decimal string
                                    if (unsigned)
                                    {
                                        argItem.Value.ArgReplacementString =
                                        Convert.ToUInt64(argItem.Value.ArgReplacementString, 16).ToString();
                                        Debug.Print("ItemULongLong:decimal:" + argItem.Value.ArgReplacementString);
                                    }
                                    else
                                    {
                                        argItem.Value.ArgReplacementString =
                                            Convert.ToInt64(argItem.Value.ArgReplacementString, 16).ToString();
                                        Debug.Print("ItemLongLong:decimal:" + argItem.Value.ArgReplacementString);
                                    }
                                }
                            }
                            else
                            {
                                Debug.Print("ItemLongLong: empty array:" + argItem.Value.ArgReplacementString);
                                argItem.Value.ArgReplacementString = "0";
                            }
                        }
                        else // Print error
                        {
                            CDFMonitor.LogOutputHandler("DEBUG:ItemLongLong:" + argItem.Value.ArgReplacementString);
                            tmfTraceMsg.Status = false;
                        }

                        _nextIndex++;
                        break;

                    default:
                        if (argItem.Value.ArgDetailFormat == "FUNC")
                        {
                            argItem.Value.ArgReplacementString = tmfTraceMsg.Function;
                        }
                        else
                        {
                            CDFMonitor.LogOutputHandler(string.Format("CDFMONITOR:ERR:Unknown ArgDetailType:{0}:{1}",
                                                          argItem.Value.ArgDetailType, argItem.Value.ArgDetailFormat));
                        }
                        break;
                }

                tmfTraceMsg.ArgsList[argItem.Key] = argItem.Value;
                tmfTraceMsg.TMFParsedString = tmfTraceMsg.TMFParsedString.Replace(argItem.Value.ArgString,
                                                                                    argItem.Value.ArgReplacementString);
                _startIndex = _nextIndex;
            } //end foreach

            return true;
        }

        #endregion Private Methods

        #region Private Classes

        /// <summary>
        /// Class TmfTraces
        /// </summary>
        private class TmfTraces : List<TMFTrace>
        {
            #region Public Fields

            public string BaseTmfString;
            public bool IsPopulated;
            public string Module;

            #endregion Public Fields
        }

        #endregion Private Classes
    }
}