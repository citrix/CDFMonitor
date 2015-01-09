// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="ConfigurationOperators.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************
namespace CDFM.Config
{
    /// <summary>
    /// Class Configuration
    /// </summary>
    partial class Configuration
    {
        #region Public Classes

        /// <summary>
        /// Class ConfigurationOperators
        /// </summary>
        public class ConfigurationOperators
        {
            #region Public Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="ConfigurationOperators" /> class.
            /// </summary>
            public ConfigurationOperators()
            {
                InitializeConfig();
            }

            #endregion Public Constructors

            #region Public Enums

            /// <summary>
            /// Enum AppOperators
            /// </summary>
            public enum AppOperators
            {
                Check,
                CheckService,
                Clean,
                Deploy,
                DisplayHelp,
                DownloadConfigs,
                DownloadTMFs,
                EnumModules,
                Fta,
                Gather,
                InstallService,
                Kill,
                Modify,
                Path,
                RegisterFta,
                ResetConfig,
                RunningAsService,
                SeServiceLogonRight,
                Start,
                StartRemote,
                StartService,
                Stop,
                StopRemote,
                StopService,
                Undeploy,
                UninstallService,
                UnregisterFta,
                Update,
                Upload,
                Zip
            }

            #endregion Public Enums

            #region Public Properties

            // public bool Check { get; set; }
            /// <summary>
            /// Gets or sets the check.
            /// </summary>
            /// <value>The check.</value>
            public bool Check
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [check service].
            /// </summary>
            /// <value><c>true</c> if [check service]; otherwise, /c>.</value>
            public bool CheckService
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="ConfigurationOperators" />
            /// is clean.
            /// </summary>
            /// <value><c>true</c> if clean; otherwise, /c>.</value>
            public bool Clean
            {
                get;
                set;
            }

            //  public bool Deploy { get; set; }
            /// <summary>
            /// Gets or sets the deploy.
            /// </summary>
            /// <value>The deploy.</value>
            public bool Deploy
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [display help].
            /// </summary>
            /// <value><c>true</c> if [display help]; otherwise, /c>.</value>
            public bool DisplayHelp
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [download configs].
            /// </summary>
            /// <value><c>true</c> if [download configs]; otherwise, /c>.</value>
            public bool DownloadConfigs
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [download TM fs].
            /// </summary>
            /// <value><c>true</c> if [download TM fs]; otherwise, /c>.</value>
            public bool DownloadTMFs
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [enum modules].
            /// </summary>
            /// <value><c>true</c> if [enum modules]; otherwise, /c>.</value>
            public bool EnumModules
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [Fta].
            /// </summary>
            /// <value><c>true</c> if [Fta]; otherwise, /c>.</value>
            public bool Fta
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the gather.
            /// </summary>
            /// <value>The gather.</value>
            public bool Gather
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [install service].
            /// </summary>
            /// <value><c>true</c> if [install service]; otherwise, /c>.</value>
            public bool InstallService
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether to Kill all instances of CDFMonitor.
            /// </summary>
            /// <value><c>true</c> if [kill]; otherwise, /c>.</value>
            public bool Kill
            {
                get;
                set;
            }

            //  public bool Path { get; set; }
            /// <summary>
            /// Gets or sets the path.
            /// </summary>
            /// <value>The path.</value>
            public bool Modify
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the path.
            /// </summary>
            /// <value>The path.</value>
            public string Path
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [register fta].
            /// </summary>
            /// <value><c>true</c> if [register fta]; otherwise, /c>.</value>
            public bool RegisterFta
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [reset config].
            /// </summary>
            /// <value><c>true</c> if [reset config]; otherwise, /c>.</value>
            public bool ResetConfig
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [process is running as a service from
            /// command line].
            /// </summary>
            /// <value><c>true</c> if [running as a service]; otherwise, /c>.</value>
            public bool RunningAsService
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the se service logon right.
            /// </summary>
            /// <value>The se service logon right.</value>
            public string SeServiceLogonRight
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="ConfigurationOperators" />
            /// is start.
            /// </summary>
            /// <value><c>true</c> if start; otherwise, /c>.</value>
            public bool Start
            {
                get;
                set;
            }

            //  public bool StartRemote { get; set; }
            /// <summary>
            /// Gets or sets the start remote.
            /// </summary>
            /// <value>The start remote.</value>
            public bool StartRemote
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [start service].
            /// </summary>
            /// <value><c>true</c> if [start service]; otherwise, /c>.</value>
            public bool StartService
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="ConfigurationOperators" />
            /// is stop.
            /// </summary>
            /// <value><c>true</c> if stop; otherwise, /c>.</value>
            public bool Stop
            {
                get;
                set;
            }

            //  public bool StopRemote { get; set; }
            /// <summary>
            /// Gets or sets the stop remote.
            /// </summary>
            /// <value>The stop remote.</value>
            public bool StopRemote
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [stop service].
            /// </summary>
            /// <value><c>true</c> if [stop service]; otherwise, /c>.</value>
            public bool StopService
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="ConfigurationOperators" />
            /// is test.
            /// </summary>
            /// <value><c>true</c> if test; otherwise, /c>.</value>
            public bool Test
            {
                get;
                set;
            }

            //  public bool UnDeploy { get; set; }
            /// <summary>
            /// Gets or sets the undeploy.
            /// </summary>
            /// <value>The undeploy.</value>
            public bool Undeploy
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [uninstall service].
            /// </summary>
            /// <value><c>true</c> if [uninstall service]; otherwise, /c>.</value>
            public bool UninstallService
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether [unregister fta].
            /// </summary>
            /// <value><c>true</c> if [unregister fta]; otherwise, /c>.</value>
            public bool UnregisterFta
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="ConfigurationOperators" />
            /// is update.
            /// </summary>
            /// <value><c>true</c> if update; otherwise, /c>.</value>
            public bool Update
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="ConfigurationOperators" />
            /// is upload.
            /// </summary>
            /// <value><c>true</c> if upload; otherwise, /c>.</value>
            public bool Upload
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="ConfigurationOperators" />
            /// is zip.
            /// </summary>
            /// <value><c>true</c> if zip; otherwise, /c>.</value>
            public bool Zip
            {
                get;
                set;
            }

            #endregion Public Properties

            #region Private Methods

            /// <summary>
            /// Initializes the config.
            /// </summary>
            private void InitializeConfig()
            {
            }

            #endregion Private Methods
        }

        #endregion Public Classes
    }
}