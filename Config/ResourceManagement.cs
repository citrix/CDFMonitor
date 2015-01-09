// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="ResourceManagement.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************

//using CDFMonitor.FileManagement;

//using CDFMonitor.Engine.Properties;
namespace CDFM.Config
{
    using CDFM.Engine;
    using CDFM.FileManagement;
    using CDFM.Network;
    using CDFM.Properties;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text.RegularExpressions;

    /// <summary>
    /// embedded class in dictionary to hold credentials and current status
    /// </summary>
    public class ResourceCredential : NetworkCredential
    {
        #region Public Properties

        /// <summary>
        /// Gets or sets the host.
        /// </summary>
        /// <value>The host.</value>
        public string Host
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>The status.</value>
        public ResourceManagement.CommandResults Status
        {
            get;
            set;
        }

        //    public string UserAndDomain { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether [utility credential].
        /// </summary>
        /// <value><c>true</c> if [utility credential]; otherwise, <c>false</c>.</value>
        public bool UtilityCredential
        {
            get;
            set;
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns>ResourceCredential.</returns>
        public ResourceCredential Clone()
        {
            return (ResourceCredential)this.MemberwiseClone();
        }

        #endregion Public Methods

        // todo: remove networkcredential inheritance and just make an instance of it.
        // change everything to resourcecredential test with defaultnetworkcredential

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceCredential" /> class.
        /// </summary>
        public ResourceCredential()
            : this(new NetworkCredential())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceCredential" /> class.
        /// </summary>
        /// <param name="credential">The credential.</param>
        public ResourceCredential(NetworkCredential credential = null)
            : base(credential.UserName, credential.Password, credential.Domain)
        {
            // PopulateInfo();
        }

        #endregion Public Constructors

        public bool DefaultCredential { get; set; }
    }

    /// <summary>
    /// Class ResourceManagement
    /// </summary>
    public class ResourceManagement
    {
        #region Private Fields

        /// <summary>
        /// The _ credentials list
        /// </summary>
        private static volatile Dictionary<string, ResourceCredential> _CredentialsList = new Dictionary<string, ResourceCredential>();

        private string _path;

        #endregion Private Fields

        #region Public Enums

        /// <summary>
        /// results enum returned by checkcredential functions
        /// </summary>
        public enum CommandResults
        {
            Error,
            Successful,
            Fail,
            Skipped,
            InvalidPath
        }

        /// <summary>
        /// Enum DeterminePathObjType
        /// </summary>
        public enum DeterminePathObjType
        {
            Error,
            Unknown,
            UnsupportedPathType,
            File,
            Directory,
            WildCard
        }

        /// <summary>
        /// Resource type enumerator
        /// </summary>
        public enum ResourceType
        {
            Error,
            Local,
            Unc,
            Unknown,
            Url,
            Utility
        }

        #endregion Public Enums

        #region Private Properties

        /// <summary>
        /// returns dictionary of credentials
        /// </summary>
        /// <value>The credentials list.</value>
        private Dictionary<string, ResourceCredential> CredentialsList
        {
            get
            {
                return (_CredentialsList);
            }
            set
            {
                _CredentialsList = value;
            }
        }

        #endregion Private Properties

        #region Public Methods

        /// <summary>
        /// checks path for proper read access
        /// </summary>
        /// <param name="path">string path to be checked</param>
        /// <param name="force">if true forces path to be checked again. otherwise cached status
        /// will be returned</param>
        /// <param name="credentials">optional credentials</param>
        /// <returns>ComandResults status</returns>
        public CommandResults CheckResourceCredentials(string path, bool force = false, NetworkCredential credentials = null)
        {
            CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentials:Enter" + path);
            _path = path;

            // Uri uriHost;
            if (credentials == null)
            {
                credentials = new NetworkCredential();
            }

            try
            {
                // Test
                //GetPathType("test.txt");
                //GetPathType("c:\temp");
                //GetPathType("c:\temp\test.txt");
                //GetPathType("www.contoso.com");
                //GetPathType("http://ctxsym.citrix.com");

                switch (GetPathType(path))
                {
                    case ResourceType.Unknown:
                    case ResourceType.Local:
                        return CheckResourceCredentialsLocal(path, force, credentials);

                    case ResourceType.Unc:
                        return CheckResourceCredentialsUnc(path, force, credentials);

                    case ResourceType.Url:
                        return CheckResourceCredentialsUrl(path, force, credentials);

                    case ResourceType.Utility:
                        return CheckResourceCredentialsUtility(path, force, credentials);

                    default:
                        return CommandResults.InvalidPath;
                }
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentials:Exception:" + e.ToString());
                return (CommandResults.Error);
            }
        }

        /// <summary>
        /// Checks the unc path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="create">if set to <c>true</c> [create].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool CheckUncPath(string path, bool create = false)
        {
            CommandResults rc = CheckResourceCredentials(path, false);

            if (rc != ResourceManagement.CommandResults.Successful
                && rc != ResourceManagement.CommandResults.InvalidPath)
            {
                CDFMonitor.LogOutputHandler("CheckUncPath:Fail no access to share:" + path);
                return false;
            }
            return CheckUncPath(path, GetCredentials(path), create);
        }

        /// <summary>
        /// Connects the unc path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool ConnectUncPath(string path)
        {
            if (ConnectUncPath(path, GetCredentials(path)) == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determines the path obj.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>DeterminePathObjType.</returns>
        public DeterminePathObjType DeterminePathObj(string path)
        {
            CDFMonitor.LogOutputHandler(string.Format("DEBUG:DeterminePathObj: enter:{0}", path));

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    CDFMonitor.LogOutputHandler(string.Format("DEBUG:DeterminePathObj: error empty path:{0}", path));
                    return DeterminePathObjType.Error;
                }

                // Are there wildcards?
                if (Regex.IsMatch(path, @"\?|\*"))
                {
                    CDFMonitor.LogOutputHandler(string.Format("DEBUG:DeterminePathObj: returning wildcard:{0}", path));
                    return DeterminePathObjType.WildCard;
                }

                ResourceType rt = GetPathType(path);
                if (rt != ResourceType.Unc & rt != ResourceType.Local)
                {
                    CDFMonitor.LogOutputHandler(string.Format("ERROR:DeterminePathObj: unsupported type:{0}", rt));
                    return DeterminePathObjType.UnsupportedPathType;
                }

                path = FileManager.GetFullPath(path);

                try
                {
                    if (File.Exists(path))
                    {
                        CDFMonitor.LogOutputHandler(string.Format("DEBUG:DeterminePathObj: returning file:{0}", path));
                        return DeterminePathObjType.File;
                    }
                }
                catch { }

                try
                {
                    if (Directory.Exists(path))
                    {
                        CDFMonitor.LogOutputHandler(string.Format("DEBUG:DeterminePathObj: returning directory:{0}", path));
                        return DeterminePathObjType.Directory;
                    }
                }
                catch { }

                // check if file or directory and fully path it
                if (string.IsNullOrEmpty(Path.GetExtension(path)))
                {
                    // Assume its a directory
                    CDFMonitor.LogOutputHandler(string.Format("DEBUG:DeterminePathObj: assuming directory:{0}", path));
                    return DeterminePathObjType.Directory;
                }
                else
                {
                    // Assume its a file
                    CDFMonitor.LogOutputHandler(string.Format("DEBUG:DeterminePathObj: assuming file:{0}", path));
                    return DeterminePathObjType.File;
                }
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DeterminePathObj: exception:" + e.ToString());
                return DeterminePathObjType.Error;
            }
        }

        /// <summary>
        /// Disconnects the unc path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool DisconnectUncPath(string path)
        {
            try
            {
                return WindowsNetworking.DisconnectRemote(path) == 0;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:DisconnectUncPath:Exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Returns cached ResourceCredential or generates new empty one
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>ResourceCredential.</returns>
        public ResourceCredential GetCredentials(string path)
        {
            string pathKey = path;
            Uri uri;
            try
            {
                if (Uri.TryCreate(path, UriKind.Absolute, out uri))
                {
                    pathKey = uri.Host;
                }

                if (CredentialsList.ContainsKey(pathKey))
                {
                    return (CredentialsList[pathKey]);
                }
                else
                {
                    return (new ResourceCredential());
                }
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:GetCredentials:Exception:" + e.ToString());
                return (new ResourceCredential());
            }
        }

        /// <summary>
        /// determines by given path what resource type is being used if CDFMonitor is passed it
        /// will simply store credentials for utility
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>ResourceType</returns>
        public ResourceType GetPathType(string path)
        {
            try
            {
                if (path == Resources.SessionName)
                {
                    return ResourceType.Utility;
                }

                if (string.IsNullOrEmpty(path))
                {
                    return ResourceType.Unknown;
                }

                //path = FileManager.GetFullPath(path);

                Uri uri;
                if (Uri.TryCreate(path, UriKind.Absolute, out uri))
                {
                    // 130915
                    if (uri.IsUnc)
                    {
                        return ResourceType.Unc;
                    }
                    else if (uri.IsLoopback)
                    {
                        return ResourceType.Local;
                    }
                    else
                    {
                        return ResourceType.Url;
                    }
                }

                string fullpath = FileManager.GetFullPath(path);

                if (fullpath.StartsWith(@"\\\\"))
                {
                    return ResourceType.Unc;
                }

                if (File.Exists(fullpath)
                  || Directory.Exists(fullpath))
                {
                    return ResourceType.Local;
                }

                return ResourceType.Unknown;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:GetPathType:Exception" + e.ToString());
                return ResourceType.Error;
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Add credentials to class list. Returns CommandResults.Successful if success.
        /// </summary>
        /// <param name="credentials">The credentials.</param>
        /// <param name="uriHost">The URI host.</param>
        /// <param name="status">The status.</param>
        /// <returns>CommandResults.</returns>
        private CommandResults AddToCredentialList(NetworkCredential credentials, string uriHost, bool defaultCredential = false,
            CommandResults status = CommandResults.Successful)
        {
            var creds = new ResourceCredential(credentials);
            creds.Status = status;
            creds.Host = uriHost;
            creds.DefaultCredential = defaultCredential;

            if (!string.IsNullOrEmpty(creds.Domain) && credentials.Domain == Properties.Resources.SessionName)
            {
                if (uriHost != Properties.Resources.SessionName)
                {
                    creds.Domain = uriHost;
                }
                else
                {
                    creds.Domain = string.Empty;
                }
                creds.UtilityCredential = true;
            }

            // now clean up user
            if (Regex.IsMatch(creds.UserName, @"@"))
            {
                Match match = Regex.Match(creds.UserName, @"(?<user>.*)@(?<domain>.*)");
                creds.Domain = match.Groups["domain"].Value;
                creds.UserName = match.Groups["user"].Value;
            }
            else if (Regex.IsMatch(creds.UserName, @"\\"))
            {
                Match match = Regex.Match(creds.UserName, @"(?<domain>.*)\\(?<user>.*)");
                creds.Domain = match.Groups["domain"].Value;
                creds.UserName = match.Groups["user"].Value;
            }

            if (CredentialsList.ContainsKey(uriHost))
            {
                CDFMonitor.LogOutputHandler(string.Format("DEBUG:AddToCredentialList:removing:{0}:{1}:{2}", credentials.UserName, uriHost,
                                          status));
                CredentialsList.Remove(uriHost);
            }

            CDFMonitor.LogOutputHandler(string.Format("DEBUG:AddToCredentialList:adding:{0}:{1}:{2}:{3}", credentials.UserName, uriHost, status, CredentialsList.Count));
            CredentialsList.Add(uriHost, creds);

            return (creds.Status);
        }

        private bool CanPromptForCredentials()
        {
            return CDFMonitor.Instance.Config.AppSettings.UseCredentials & !CDFMonitor.Instance.Config.AppOperators.RunningAsService;
        }

        /// <summary>
        /// Checks local drive resource for access. Returns CommandResults.Successful on success.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="force">if set to <c>true</c> [force].</param>
        /// <param name="credentials">The credentials.</param>
        /// <returns>CommandResults.</returns>
        private CommandResults CheckResourceCredentialsLocal(string path, bool force,
            NetworkCredential credentials = null)
        {
            string pathKey = string.Empty;

            try
            {
                pathKey = FileManager.GetFullPath(Path.GetDirectoryName(path));

                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsLocal:treating as Local");

                if (CredentialsList.ContainsKey(pathKey) && !force)
                {
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsLocal:returning cached authentication status");

                    // Already tried authenticating previously so just return false
                    return (CredentialsList[pathKey].Status);
                }

                if (File.Exists(pathKey) || Directory.Exists(pathKey) || Directory.GetParent(pathKey).Exists)
                {
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsLocal:able to enumerate path");
                    return
                        (AddToCredentialList(CredentialCache.DefaultNetworkCredentials, pathKey, true,
                                             CommandResults.Successful));
                }

                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsLocal:failed to enumerate path");
                return (AddToCredentialList(CredentialCache.DefaultNetworkCredentials, pathKey, true, CommandResults.InvalidPath));
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsLocal:Exception:" + e.ToString());

                //return (CommandResults.Fail);
                return (AddToCredentialList(CredentialCache.DefaultNetworkCredentials, pathKey, true, CommandResults.Fail));
            }
        }

        /// <summary>
        /// Checks unc resource for access. Looks in cache, tries windows cache, and then prompts.
        /// Returns CommandResults.Successful on success.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="force">if set to <c>true</c> [force].</param>
        /// <param name="credentials">The credentials.</param>
        /// <returns>CommandResults.</returns>
        private CommandResults CheckResourceCredentialsUnc(string path, bool force, NetworkCredential credentials = null)
        {
            try
            {
                string uriHost = new Uri(path).Host;
                int ret = 0;

                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUnc:treating as UNC");

                if (CredentialsList.ContainsKey(uriHost) && !force)
                {
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUnc:returning cached authentication status");

                    // Already tried authenticating previously so just return false
                    return (CredentialsList[uriHost].Status);
                }
                else if (CredentialsList.ContainsKey(uriHost))
                {
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUnc:trying locally cached credentials");

                    // Authenticate with creds
                    if ((TestUncPath(path, CredentialsList[uriHost])))
                    {
                        return CommandResults.Successful;
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUnc:removing locally cached credentials. force = true");
                        CredentialsList.Remove(uriHost);
                    }
                }
                else
                {
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUnc:trying passed in credentials");
                    if (TestUncPath(path, credentials))
                    {
                        return AddToCredentialList(credentials, uriHost);
                    }

                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUnc:trying default credentials");

                    // no credentials so try as default
                    if (Directory.Exists(path))
                    {
                        return AddToCredentialList(CredentialCache.DefaultNetworkCredentials, uriHost, true,
                                                  CommandResults.Successful);
                    }

                    // try with utility credentials if available
                    if (CredentialsList.ContainsKey(Resources.SessionName))
                    {
                        CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUnc:trying utility credentials");
                        if (TestUncPath(path, CredentialsList[Resources.SessionName]))
                        {
                            return AddToCredentialList(CredentialsList[Resources.SessionName], uriHost);
                        }
                    }
                }

                if (CanPromptForCredentials())
                {
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUnc:trying with prompted credentials");

                    // this queries windows credential manager for cached credential
                    credentials = Credentials.PromptForCredentials(credentials.UserName, credentials.Password, uriHost);

                    if (TestUncPath(path, credentials))
                    {
                        return AddToCredentialList(credentials, uriHost);
                    }
                    else
                    {
                        return CommandResults.Fail;
                    }
                }

                // bad set as bad for future check
                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUnc:CheckResourceCredentials return:" +
                            WindowsNetworking.GetErrorForNumber(ret));
                if (ret != 53) // path not found
                {
                    AddToCredentialList(credentials, uriHost, false, CommandResults.Fail);
                    return CommandResults.Error;
                }
                else
                {
                    return CommandResults.InvalidPath;
                }
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUnc:Exception:" + e.ToString());

                return (AddToCredentialList(credentials, path, false, CommandResults.Fail));
            }
        }

        /// <summary>
        /// Checks Url resource for access. Looks in cache, tries windows cache, and then prompts.
        /// Returns CommandResults.Successful on success.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="force">if set to <c>true</c> [force].</param>
        /// <param name="credentials">The credentials.</param>
        /// <returns>CommandResults.</returns>
        private CommandResults CheckResourceCredentialsUrl(string path, bool force, NetworkCredential credentials = null)
        {
            HttpGet httpGet = new HttpGet();
            string result = string.Empty;

            try
            {
                string uriHost = new Uri(path).Host;
                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUri:treating as HTTP");

                if (CredentialsList.ContainsKey(uriHost) && !force)
                {
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUri:http skipping credentials");
                    return (CredentialsList[uriHost].Status);
                }

                if (CredentialsList.ContainsKey(Resources.SessionName))
                {
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUrl:trying utility credentials");
                    result = httpGet.GetRequest(path, CredentialsList[Resources.SessionName], true);

                    // try with utility credentials if available
                    if (CheckUrlReturnCode(httpGet.Status))
                    {
                        return AddToCredentialList(CredentialsList[Resources.SessionName], uriHost);
                    }
                }

                if (CredentialsList.ContainsKey(uriHost) && CanPromptForCredentials())
                {
                    // Try with cached creds
                    httpGet = new HttpGet();

                    result = httpGet.GetRequest(path, CredentialsList[uriHost], true);

                    if (!CheckUrlReturnCode(httpGet.Status))
                    {
                        // this queries windows credential manager for cached credential
                        credentials = Credentials.PromptForCredentials(credentials.UserName, credentials.Password,
                                                                       uriHost);
                        CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUri:Error:" + httpGet.Status);

                        result = httpGet.GetRequest(path, credentials, true);
                        if (!CheckUrlReturnCode(httpGet.Status))
                        {
                            CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUri2:Error:" + httpGet.Status);
                            return AddToCredentialList(credentials, uriHost, false, CommandResults.Fail);
                        }
                        else
                        {
                            return AddToCredentialList(credentials, uriHost);
                        }
                    }
                    else
                    {
                        return AddToCredentialList(credentials, uriHost);
                    }
                }

                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUri:http using default credentials");
                httpGet = new HttpGet();
                result = httpGet.GetRequest(path, CredentialCache.DefaultNetworkCredentials, true);

                if (!CheckUrlReturnCode(httpGet.Status) && CanPromptForCredentials())
                {
                    // this queries windows credential manager for cached credential
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUri:Error:" + httpGet.Status);
                    credentials = Credentials.PromptForCredentials(credentials.UserName, credentials.Password,
                                                                    uriHost);
                    result = httpGet.GetRequest(path, credentials, true);

                    if (!CheckUrlReturnCode(httpGet.Status))
                    {
                        CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUri:Error2:" + httpGet.Status);
                        return AddToCredentialList(credentials, uriHost, false, CommandResults.Fail);
                    }
                    else
                    {
                        return AddToCredentialList(credentials, uriHost);
                    }
                }
                else
                {
                    return AddToCredentialList(CredentialCache.DefaultNetworkCredentials, uriHost, true,
                                                CommandResults.Successful);
                }
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUrl:Exception:" + e.ToString());
                return (AddToCredentialList(credentials, path, false, CommandResults.Fail));
            }
        }

        /// <summary>
        /// Checks utility credentials Looks in cache, tries windows cache, and then prompts.
        /// Returns CommandResults.Successful always unless exception.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="force">if set to <c>true</c> [force].</param>
        /// <param name="credentials">The credentials.</param>
        /// <returns>CommandResults.</returns>
        private CommandResults CheckResourceCredentialsUtility(string path, bool force,
            NetworkCredential credentials = null)
        {
            try
            {
                if (CredentialsList.ContainsKey(path) && !force)
                {
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUtility:returning cached authentication status");
                    return (CommandResults.Successful);
                }
                else if (CredentialsList.ContainsKey(path))
                {
                    CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUtility:removing cached authentication");

                    CredentialsList.Remove(path);
                }

                if (string.IsNullOrEmpty(credentials.UserName) && string.IsNullOrEmpty(credentials.Password) && CanPromptForCredentials())
                {
                    credentials = Credentials.PromptForCredentials(credentials.UserName, credentials.Password, path);
                }

                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUtility:adding cached authentication:" + path);

                return (AddToCredentialList(credentials, path));
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:CheckResourceCredentialsUtiltiy:Exception:" + e.ToString());
                return (AddToCredentialList(credentials, path, false, CommandResults.Fail));
            }
        }

        /// <summary>
        /// Checks Unc Path existence
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="nc">The nc.</param>
        /// <param name="create">if set to <c>true</c> [create].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool CheckUncPath(string path, NetworkCredential nc, bool create = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            ResourceManagement.ResourceType rt = GetPathType(path);
            string originalPath = path.ToString();

            if (rt == ResourceManagement.ResourceType.Unc)
            {
                // strip path to \\server\share
                string pattern = @"^\\\\[^\\]+?\\[^\\]+";
                if (Regex.IsMatch(path, pattern))
                {
                    path = Regex.Matches(path, pattern)[0].Groups[0].Value;
                }
            }
            else
            {
                CDFMonitor.LogOutputHandler(string.Format("Error:CheckUncPath:invalid argument:{0}", path));
                return false;
            }

            try
            {
                int ret = 0;

                if ((ret = ConnectUncPath(originalPath, nc)) == 0)
                {
                    return true;
                }
                else
                {
                    CDFMonitor.LogOutputHandler(string.Format("CheckUncPath:1:Warning no access to directory:{0}:{1}", originalPath, ret));

                    if (string.Compare(path, originalPath) == 0)
                    {
                        return false;
                    }
                }

                if ((ret = ConnectUncPath(path, nc)) != 0)
                {
                    CDFMonitor.LogOutputHandler(string.Format("CheckUncPath:2:Warning no access to directory:{0}:{1}", path, ret));
                    return false;
                }

                DeterminePathObjType objectType = DeterminePathObj(originalPath);

                switch (objectType)
                {
                    case DeterminePathObjType.File:
                        if (File.Exists(originalPath) | Directory.Exists(Path.GetDirectoryName(originalPath)))
                        {
                            CDFMonitor.LogOutputHandler("CheckUncPath:file exists.");
                            return true;
                        }
                        if (create)
                        {
                            // only create dir
                            Directory.CreateDirectory(Path.GetDirectoryName(originalPath));
                            return true;
                        }

                        break;

                    case DeterminePathObjType.Directory:
                        if (Directory.Exists(originalPath))
                        {
                            CDFMonitor.LogOutputHandler("CheckUncPath:Directory exists.");
                            return true;
                        }
                        if (create)
                        {
                            // only create dir
                            Directory.CreateDirectory(originalPath);
                            return true;
                        }

                        break;

                    case DeterminePathObjType.WildCard:
                        if (Directory.Exists(Path.GetDirectoryName(originalPath)))
                        {
                            CDFMonitor.LogOutputHandler("CheckUncPath:Wildcard Directory exists.");
                            return true;
                        }

                        break;

                    default:
                        CDFMonitor.LogOutputHandler(string.Format("CheckUncPath:error unknown object:{0}", objectType));
                        return false;
                }

                return true;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:CheckUncPathShare:Exception:" + e.ToString());
                return false;
            }
            finally
            {
                DisconnectUncPath(path);
                DisconnectUncPath(originalPath);
            }
        }

        /// <summary>
        /// Checks Url return codes. Returns true if empty return, 200, 404, or OK
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool CheckUrlReturnCode(string status)
        {
            return (string.IsNullOrEmpty(status)
                    || status.Contains("200")
                    || status.Contains("404")
                    || status.Equals("OK")
                    && !status.Contains("401")
                    && !status.Contains("Exception"));
        }

        /// <summary>
        /// Connects the unc path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="credentials">The credentials.</param>
        /// <returns>System.Int32.</returns>
        private int ConnectUncPath(string path, NetworkCredential credentials)
        {
            CDFMonitor.LogOutputHandler("DEBUG:ConnectUncPath:enter:" + path);
            string userName = credentials.UserName;

            if (!string.IsNullOrEmpty(credentials.Domain))
            {
                userName = string.Format("{0}\\{1}", credentials.Domain, credentials.UserName);
            }

            if (GetPathType(path) != ResourceType.Unc)
            {
                return -1;
            }

            DeterminePathObjType objectType = DeterminePathObj(path);
            if (objectType == DeterminePathObjType.File)
            {
                path = Path.GetDirectoryName(FileManager.GetFullPath(path));
                CDFMonitor.LogOutputHandler("DEBUG:ConnectUncPath:new path:" + path);
            }

            int ret = WindowsNetworking.ConnectToRemote(path,
                                                        userName,
                                                        credentials.Password, false);

            CDFMonitor.LogOutputHandler(string.Format("DEBUG:ConnectToRemote:return:{0}:{1}", path, ret == 0 ? true : false));
            return ret;
        }

        /// <summary>
        /// Tries to connect to remote device specified in path using specified credentials. Returns
        /// 0 if success.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="credentials">The credentials.</param>
        /// <returns>0 if success</returns>
        private bool TestUncPath(string path, NetworkCredential credentials)
        {
            return CheckUncPath(path, credentials, true);
        }

        #endregion Private Methods
    }
}