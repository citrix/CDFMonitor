// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="CDFMonitorGui.xaml.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright> <summary></summary>
// ***********************************************************************
namespace CDFM.Gui
{
    using CDFM.Config;
    using CDFM.Engine;
    using CDFM.FileManagement;
    using CDFM.Network;
    using CDFM.Service;
    using Microsoft.Win32;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Threading;
    using Operators = CDFM.Config.Configuration.ConfigurationOperators.AppOperators;

    /// <summary>
    /// Interaction logic for CDFMonitorGui.xaml
    /// </summary>
    public partial class CDFMonitorGui : Window//, INotifyPropertyChanged
    {
        #region Public Fields

        public static WriteStatusDelegate WriteStatusHandler;
        public CDFMonitor _cdfMonitor;
        public Queue<WriteListObj> WriteList = new Queue<WriteListObj>(5100);

        #endregion Public Fields

        #region Private Fields

        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        private GridViewColumnHeader _lastHeaderClicked = null;
        private object _listBoxLock = new object();
        private WriterJob _loggerGui = new WriterJob();
        private bool _moduleFilterNew = false;
        private Dictionary<string, string> _moduleListRemoteCache = new Dictionary<string, string>();
        private string _moduleListRemoteCacheSource = string.Empty;
        private Queue<TextBlock> _outputConsoleItems = new Queue<TextBlock>();
        private Queue<TextBlock> _outputConsoleItemsAll;
        private bool _outputFilterNew = false;
        private string _outputFilterPattern = string.Empty;
        private bool _outputFilterPatternInverted;
        private Configuration.ConfigurationProperties _parentConfigAppSettings;
        private Paragraph _statusBoxParagraph = new Paragraph();
        private bool _tabInitialized;
        private TabItem _tabItem = new TabItem();
        private BackgroundWorker _worker;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes delegate new instance of the <see cref="CDFMonitorGui" /> class.
        /// </summary>
        /// <exception cref="System.Exception">CDFMonitorGui:exception:</exception>
        public CDFMonitorGui()
        {
            Thread guiThread = new Thread(new ThreadStart(OutputConsoleUpdateThreadProc));
            try
            {
                Instance = this;
                _cdfMonitor = CDFMonitor.Instance;
                _cdfMonitor.WriteGuiStatus = true;

                InitializeComponent();
                SetActivity();

                // Setup write callback
                WriteStatusHandler = WriteStatus;

                // Set the binding source here.
                this.DataContext = _cdfMonitor.Config;

                // Redirect console output
                ConfigureGuiWriter();

                _worker = new BackgroundWorker
                {
                    WorkerSupportsCancellation = true,
                    WorkerReportsProgress = true
                };

                radioButtonModulesConfigFile.IsChecked = true;
                listBoxConsoleOutput.ItemsSource = _outputConsoleItems;
                guiThread.Start();

                // Set up save buttons to auto enable on property change
                SetPropertyNotifications(true);
                EnableSaveButtons(false);

                // Send an event in case filter is populated
                textBoxOutputFilter_TextChanged(null, null);
                this.Background = Brushes.Gray;
                Closing += CDFMonitorGui_Closing;
                Activity.Focus();
                ShowDialog();
            }
            catch (Exception e)
            {
                Debug.Print("CDFMonitorGui:exception:" + e.ToString());
                _cdfMonitor.LogOutput("CDFMonitorGui:exception:" + e.ToString());
                MessageBox.Show(e.ToString(), "CDFMonitorGui:Main Exception. Copy and email to supporttools@citrix.com for assistance.");
            }
            finally
            {
                guiThread.Abort();
            }
        }

        #endregion Public Constructors

        #region Public Delegates

        /// <summary>
        /// Delegate WriteStatusDelegate
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="reset">if set to <c>true</c> [reset].</param>
        public delegate void WriteStatusDelegate(string input, bool reset, string hyperlink);

        #endregion Public Delegates

        #region Public Events

        // Need for inotifypropertychanged implementation
        /// <summary>
        /// Occurs when [property changed].
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Gets or sets the instance.
        /// </summary>
        /// <value>The instance.</value>
        public static CDFMonitorGui Instance
        {
            get;
            set;
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Returns True if 'Stop' button has been selected.
        /// </summary>
        /// <returns></returns>
        public bool CancellationPending()
        {
            return _worker.CancellationPending;
        }

        /// <summary>
        /// Report progress on multi iteration jobs from remote and from CDFMonitor
        /// </summary>
        /// <param name="count"></param>
        /// <param name="info"></param>
        public void ReportProgress(int count, string info)
        {
            if (_worker.IsBusy)
            {
                _worker.ReportProgress(count, info ?? string.Empty);
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Handles the GotFocus event of the Action control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void Action_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Action_GotFocus:enter");
                WriteStatus(Properties.Resources.ActionGotFocus, true);
                _tabInitialized = true;
            }
        }

        /// <summary>
        /// Handles the GotFocus event of the Activity control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void Activity_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Activity_GotFocus:enter");
                WriteStatus(Properties.Resources.ActivityGotFocus, true);

                // Populate trace file box
                if (_cdfMonitor.Config.Activity == Configuration.ActivityType.Unknown)
                {
                    Modules.IsEnabled = false;
                    Logging.IsEnabled = false;
                    Match.IsEnabled = false;
                    Action.IsEnabled = false;
                    Notify.IsEnabled = false;
                    Network.IsEnabled = false;
                    Upload.IsEnabled = false;
                    Remote.IsEnabled = false;

                    // Options.IsEnabled = false;
                    Output.IsEnabled = false;
                    textBoxTraceFileInput.IsEnabled = false;
                    buttonSelectTraceFile.IsEnabled = false;
                }
                _tabInitialized = true;
            }
        }

        /// <summary>
        /// Used to apply 'display' filter on 'Output' tab
        /// </summary>
        /// <param name="wlist">Queue of WriteListObj</param>
        /// <returns>Filtered Queue of WriteListObj</returns>
        private Queue<WriteListObj> ApplyOutputConsoleFilter(Queue<WriteListObj> wlist)
        {
            Queue<TextBlock> tempList;

            while (_outputFilterNew)
            {
                _outputFilterNew = false;
                tempList = new Queue<TextBlock>();

                // Update console with filtered results
                if (!string.IsNullOrEmpty(_outputFilterPattern) && _outputConsoleItemsAll != null)
                {
                    // Filtered modified. query full cache
                    Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                    {
                        tempList = GenerateConsoleFilteredList(_outputConsoleItemsAll);
                        if (_outputFilterNew)
                        {
                            return;
                        }
                        listBoxConsoleOutput.ItemsSource = _outputConsoleItems = tempList;
                    }));
                }
                else if (!string.IsNullOrEmpty(_outputFilterPattern) && _outputConsoleItemsAll == null)
                {
                    // New filter session. copy current output to full cache
                    Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                    {
                        tempList = GenerateConsoleFilteredList(_outputConsoleItems);

                        if (_outputFilterNew)
                        {
                            return;
                        }

                        _outputConsoleItemsAll = _outputConsoleItems; // New
                        listBoxConsoleOutput.ItemsSource = _outputConsoleItems = tempList;
                    }));
                }
                else if (string.IsNullOrEmpty(_outputFilterPattern) && _outputConsoleItemsAll != null)
                {
                    // End filter session. Set output back to original list from full cache
                    Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                    {
                        listBoxConsoleOutput.ItemsSource = _outputConsoleItems = _outputConsoleItemsAll;
                    }));

                    _outputConsoleItemsAll = null;
                    return wlist;
                }
            }

            return wlist;
        }

        /// <summary>
        /// Callback to enable Save buttons in gui if PropertyNotifications are enabled
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AppSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_cdfMonitor.Config.AppSettings.GetPropertyNotifications())
            {
                EnableSaveButtons(true);
            }
        }

        /// <summary>
        /// Cancels Background Worker
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void BackgroundCancel(object sender, RoutedEventArgs e)
        {
            _worker.CancelAsync();
        }

        /// <summary>
        /// Starts Background Worker
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        /// <param name="delegate">The delegate.</param>
        private void BackgroundStart(object sender, RoutedEventArgs e, Func<bool> @delegate)
        {
            while (_worker.IsBusy)
            {
                CDFMonitor.CloseCurrentSessionEvent.WaitOne(100);
            }

            _worker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            if (sender is Button) ToggleStartStop(false);
            ClearStatus();

            _outputConsoleItems.Clear();
            _cdfMonitor.WriteGuiStatus = false;
            _cdfMonitor.WriteGuiStatus = true;

            _worker.DoWork += (s, args) =>
            {
                BackgroundWorker worker = s as BackgroundWorker;
                if (worker.CancellationPending)
                {
                    args.Cancel = true;
                    return;
                }

                @delegate();
            };

            _worker.ProgressChanged += (s, args) =>
                                           {
                                               WriteStatus(args.UserState.ToString());
                                           };

            _worker.RunWorkerCompleted += (s, args) =>
            {
                ToggleStartStop(true);
                WriteStatus(string.Format("Activity stopped.{0}",
                    Output.IsSelected
                    ? string.Empty
                    : " For additional detail, view Output tab."),
                    false);
            };

            _worker.RunWorkerAsync();

            ToggleStartStop(false);
            WriteStatus(string.Format("Activity started.{0}",
                Output.IsSelected
                ? string.Empty
                : " For additional detail, view Output tab."),
                false);
        }

        /// <summary>
        /// Handles the Click event of the buttonActionSelectShutdownCommand control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonActionSelectShutdownCommand_Click(object sender, RoutedEventArgs e)
        {
            textBoxActionShutdownCommand.Text =
                _cdfMonitor.Config.AppSettings.ShutdownCommand = SelectFile("All Files (*.*)|*.*");
        }

        /// <summary>
        /// Handles the Click event of the buttonActionSelectStartupCommand control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonActionSelectStartupCommand_Click(object sender, RoutedEventArgs e)
        {
            textBoxActionStartupCommand.Text =
                _cdfMonitor.Config.AppSettings.StartupCommand = SelectFile("All Files (*.*)|*.*");
        }

        /// <summary>
        /// Handles the Click event of the buttonActionVerify control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonActionVerify_Click(object sender, RoutedEventArgs e)
        {
            ClearStatus();

            // Process regex to potentially get any regex named groups
            _cdfMonitor.Config.ProcessRegexPattern(textBoxRegexPattern.Text);

            _cdfMonitor.Config.EventCommands = Configuration.ProcessCommand(textBoxActionEventCommand.Text);
            _cdfMonitor.Config.ShutdownCommands = Configuration.ProcessCommand(textBoxActionShutdownCommand.Text);
            _cdfMonitor.Config.StartupCommands = Configuration.ProcessCommand(textBoxActionStartupCommand.Text);

            WriteStatus("Verifying EventCommands:");
            WriteStatus(Configuration.SerializeCommands(_cdfMonitor.Config.EventCommands));
            WriteStatus("Verifying StartupCommands:");
            WriteStatus(Configuration.SerializeCommands(_cdfMonitor.Config.StartupCommands));
            WriteStatus("Verifying ShutdownCommands:");
            WriteStatus(Configuration.SerializeCommands(_cdfMonitor.Config.ShutdownCommands));

            if (String.Compare(textBoxRegexTestText.Text, Properties.Resources.MatchRegexTestText) == 0
                || string.IsNullOrEmpty(textBoxRegexTestText.Text))
            {
                WriteStatus("If using named groups to pass variables from trace statement to a command, populate regexpattern and sample trace on matches tab and run again.");
            }
            else
            {
                // Run fake trace using sample text.
                _cdfMonitor.RegexTracePatternObj = new Regex(_cdfMonitor.Config.AppSettings.RegexPattern, RegexOptions.IgnoreCase);

                _cdfMonitor.ProcessEventThreadProc(new TraceEventThreadInfo()
                {
                    FormattedEventString = textBoxRegexTestText.Text.Length > 0
                        ? textBoxRegexTestText.Text
                        : _cdfMonitor.Config.AppSettings.RegexPattern,
                    TestMode = true
                });
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonCheckForUpdate control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonCheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.CheckForNewVersion();
        }

        /// <summary>
        /// Handles the Click event of the buttonCloseRemoteConfig control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonCloseRemoteConfig_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.LogOutput("DEBUG: buttonCloseRemoteConfig_Click:enter");

            CDFMonitorGui_Closing(null, null);

            buttonCloseRemoteConfig.Visibility = System.Windows.Visibility.Hidden;
            buttonCloseRemoteConfig.IsEnabled = false;
            this.Background = Brushes.Gray;

            _cdfMonitor.Config.ReadConfigFile(_parentConfigAppSettings.ConfigFile);
            _parentConfigAppSettings = null;
            _cdfMonitor.Config.ProcessConfiguration();

            // Not sure why this dropdown has to be refreshed manually
            comboBoxOptionsRunAs.GetBindingExpression(ComboBox.SelectedItemProperty).UpdateTarget();
            comboBoxRemoteServiceStartMode.GetBindingExpression(ComboBox.SelectedItemProperty).UpdateTarget();

            radioButtonActivityParseTraceMessage.IsEnabled = true;
            radioButtonActivityCaptureNetworkTraceMessage.IsEnabled = true;

            radioButtonActivityRemoteActivities.IsEnabled = true;
            radioButtonActivityRemoteActivities.IsChecked = true;
            Remote.IsEnabled = true;

            buttonEditRemoteConfig.Visibility = System.Windows.Visibility.Visible;
            buttonEditRemoteConfig.IsEnabled = true;
            buttonStart.Visibility = System.Windows.Visibility.Visible;
            buttonStop.Visibility = System.Windows.Visibility.Visible;

            SetActivity();

            EnableSaveButtons(false);
            Remote.Focus();
            _cdfMonitor.LogOutput("DEBUG: buttonCloseRemoteConfig_Click:enter");
        }

        /// <summary>
        /// Handles the Click event of the buttonDisplayCacheState control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonDisplayCacheState_Click(object sender, RoutedEventArgs e)
        {
            ClearStatus();

            foreach (KeyValuePair<string, RemoteOperations.RemoteStatus> kvp in _cdfMonitor.Config.RemoteMachineList)
            {
                WriteStatus(string.Format("{0}:{1}", kvp.Key, kvp.Value));
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonEditRemoteConfig control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonEditRemoteConfig_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.LogOutput("DEBUG: buttonEditRemoteConfig_Click:enter");
            ClearStatus();
            var checkedButton = gridRemoteActions.Children.OfType<RadioButton>()
              .FirstOrDefault(r => r.IsChecked == true);
            var radioButton = (RemoteOperations.RemoteOperationMethods)Enum.Parse(
                typeof(RemoteOperations.RemoteOperationMethods),
                checkedButton != null ? checkedButton.Content.ToString()
                : RemoteOperations.RemoteOperationMethods.Deploy.ToString(), true);

            if (string.IsNullOrEmpty(_cdfMonitor.Config.AppSettings.DeployPath)
                | !VerifySetting(Configuration.ConfigurationProperties.EnumProperties.DeployPath, false))
            {
                radioButtonRemoteDeploy.IsChecked = true;
                if (!VerifyRemoteActivityConfig())
                {
                    WriteStatus("Remote config not available. Select 'Deploy' to edit path and Setup/Verify to setup or repair.");
                    buttonRemoteSetupRemote.Focus();
                    return;
                }
            }

            _cdfMonitor.Config.WriteConfigFile(_cdfMonitor.Config.AppSettings.ConfigFile);

            // Get first remote machine listed
            string remoteMachine = _cdfMonitor.Config.ProcessRemoteMachines().FirstOrDefault().Key;
            _parentConfigAppSettings = _cdfMonitor.Config.AppSettings.ShallowCopy();
            string configFile = string.Format("{0}\\{1}.exe.config", textBoxRemoteDeployPath.Text, Process.GetCurrentProcess().ProcessName);

            _cdfMonitor.Config.ReadConfigFile(configFile);

            this.Background = Brushes.LightBlue;

            SetRemoteMandatorySettings();

            // Temporarily set use credentials if parent is using them. on save it will be disabled
            _cdfMonitor.Config.AppSettings.UseCredentials = _parentConfigAppSettings.UseCredentials;

            // Put first remote machine into module path if empty
            if (string.IsNullOrEmpty(_cdfMonitor.Config.AppSettings.ModulePath)
                && !string.IsNullOrEmpty(remoteMachine)
                && string.IsNullOrEmpty(_cdfMonitor.Config.AppSettings.ModuleListViewItems))
            {
                _cdfMonitor.Config.ModuleSource = Configuration.ModuleSourceType.RemoteMachine;
                _cdfMonitor.Config.AppSettings.ModulePath = remoteMachine;
                textBoxModulePath.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }

            comboBoxOptionsRunAs.GetBindingExpression(ComboBox.SelectedItemProperty).UpdateTarget();

            radioButtonActivityRemoteActivities.IsEnabled = false;
            radioButtonActivityRemoteActivities.IsChecked = false;

            radioButtonActivityParseTraceMessage.IsEnabled = false;
            radioButtonActivityParseTraceMessage.IsChecked = false;
            radioButtonActivityCaptureNetworkTraceMessage.IsEnabled = false;
            radioButtonActivityCaptureNetworkTraceMessage.IsChecked = false;

            buttonEditRemoteConfig.IsEnabled = false;
            buttonEditRemoteConfig.Visibility = System.Windows.Visibility.Hidden;
            buttonCloseRemoteConfig.IsEnabled = true;
            buttonCloseRemoteConfig.Visibility = System.Windows.Visibility.Visible;
            buttonStart.Visibility = System.Windows.Visibility.Hidden;
            buttonStop.Visibility = System.Windows.Visibility.Hidden;

            SetActivity("(REMOTE CONFIG)");

            if (_cdfMonitor.Config.ModuleListCurrentConfigCache.Count < 1)
            {
                radioButtonModulesRemoteMachine.IsChecked = true;
            }

            Activity.Focus();
            EnableSaveButtons(false);

            _cdfMonitor.LogOutput("DEBUG: buttonEditRemoteConfig_Click:exit");
        }

        /// <summary>
        /// Handles the Click event of the buttonSelectLogFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonLoggingSelectLogFile_Click(object sender, RoutedEventArgs e)
        {
            textBoxLoggingLogFileName.Text = _cdfMonitor.Config.AppSettings.LogFileName = SelectFile("All Files (*.*)|*.*", textBoxLoggingLogFileName.Text);
        }

        /// <summary>
        /// Handles the Click event of the buttonSelectTraceFile2 control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonLoggingSelectTraceFile_Click(object sender, RoutedEventArgs e)
        {
            textBoxLoggingTraceFileName.Text = _cdfMonitor.Config.AppSettings.TraceFileOutput = SelectFile("All Files (*.*)|*.*", textBoxLoggingTraceFileName.Text);
        }

        /// <summary>
        /// Handles the Click event of the buttonLoggingVerify control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonLoggingVerify_Click(object sender, RoutedEventArgs e)
        {
            // Check log file path and etl path can be written to
            // TODO: need to move this to configuration and call it VerifyLoggingOptions once status text is built out
            bool status = true;
            ClearStatus();

            try
            {
                if (!string.IsNullOrEmpty(_cdfMonitor.Config.AppSettings.LogFileName))
                {
                    WriteStatus(string.Format("LogFileName info OK: {0}:{1}",
                        FileManager.GetFullPath(_cdfMonitor.Config.AppSettings.LogFileName),
                        (status &= FileManager.CheckPath(_cdfMonitor.Config.AppSettings.LogFileName, true))));
                    WriteStatus(string.Format("LogFileName drive space OK: {0}",
                        (status &= _cdfMonitor.Config.VerifyPathSpace(
                        FileManager.GetFullPath(_cdfMonitor.Config.AppSettings.LogFileName), (_cdfMonitor.Config.AppSettings.LogFileMaxSize
                                * Math.Max(1, _cdfMonitor.Config.AppSettings.LogFileMaxCount))))));
                }
                else
                {
                    WriteStatus("LogFileName empty...");
                }

                if (!string.IsNullOrEmpty(_cdfMonitor.Config.AppSettings.TraceFileOutput))
                {
                    WriteStatus(string.Format("TraceFile info OK: {0}:{1}",
                        FileManager.GetFullPath(_cdfMonitor.Config.AppSettings.TraceFileOutput),
                        (status &= FileManager.CheckPath(Path.GetDirectoryName(FileManager.GetFullPath(_cdfMonitor.Config.AppSettings.TraceFileOutput)), true))));

                    WriteStatus(string.Format("TraceFile unique: {0}:{1}",
                       _cdfMonitor.Config.AppSettings.TraceFileOutput,
                       (status &= FileManager.GetFullPath(_cdfMonitor.Config.AppSettings.LogFileName) != FileManager.GetFullPath(_cdfMonitor.Config.AppSettings.TraceFileOutput))));
                    WriteStatus(string.Format("TraceFile drive space OK: {0}",
                        (status &= _cdfMonitor.Config.VerifyPathSpace(_cdfMonitor.Config.AppSettings.TraceFileOutput, (_cdfMonitor.Config.AppSettings.LogFileMaxSize
                                * Math.Max(1, _cdfMonitor.Config.AppSettings.LogFileMaxCount))))));
                }
                else
                {
                    if (_cdfMonitor.Config.Activity == Configuration.ActivityType.TraceToEtl)
                    {
                        WriteStatus("FAIL: TraceFile empty. Need .etl file specified in trace file if activity 'capture trace'");
                        status &= false;
                    }
                    else
                    {
                        WriteStatus("TraceFile empty...");
                    }
                }

                // Determine if config could fill up drive space
                if (_cdfMonitor.Config.AppSettings.LogFileMaxSize < 2
                    && _cdfMonitor.Config.AppSettings.LogFileMaxCount < 1
                    && !_cdfMonitor.Config.AppSettings.LogFileOverWrite)
                {
                    WriteStatus("WARNING: Logging Configuration could fill up drive space...");
                }

                // Check ranges
                if (_cdfMonitor.Config.AppSettings.LogFileMaxCount > FileManager.LOG_FILE_MAX_COUNT)
                {
                    WriteStatus(string.Format("FAIL: Too many log files specified. Current: {0} ; Max: {1}",
                        _cdfMonitor.Config.AppSettings.LogFileMaxCount,
                        FileManager.LOG_FILE_MAX_COUNT));
                    status = false;
                }

                if (_cdfMonitor.Config.AppSettings.LogFileMaxCount > 1
                    && _cdfMonitor.Config.AppSettings.LogFileMaxSize == 0)
                {
                    WriteStatus(string.Format("FAIL: Invalid configuration. LogFileMaxCount: {0} ; LogFileMaxSize: {1}",
                        _cdfMonitor.Config.AppSettings.LogFileMaxCount,
                        _cdfMonitor.Config.AppSettings.LogFileMaxSize));
                    status = false;
                }

                if (!string.IsNullOrEmpty(_cdfMonitor.Config.AppSettings.LogFileServer))
                {
                    WriteStatus(string.Format("LogFileServer info OK: {0}:{1}",
                       _cdfMonitor.Config.AppSettings.LogFileServer,
                       (status &= FileManager.CheckPath(_cdfMonitor.Config.AppSettings.LogFileServer, true))));
                }
                VerifySetting(Configuration.ConfigurationProperties.EnumProperties.LogFileName, false);
                VerifySetting(Configuration.ConfigurationProperties.EnumProperties.TraceFileInput, false);
                VerifySetting(Configuration.ConfigurationProperties.EnumProperties.TraceFileOutput, false);
                WriteStatus(string.Format("Verification Result: {0}. See output for more detail.", status ? "Success" : "Fail"));
            }
            catch (Exception ex)
            {
                WriteStatus(ex.ToString());
                WriteStatus(string.Format("Verification Result: Fail. See output for more detail."));
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonMatchTest control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonMatchTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (textBoxRegexTestText.Text.Length > 0)
                {
                    Regex regex = new Regex(textBoxRegexPattern.Text, RegexOptions.IgnoreCase);
                    MatchCollection mc = regex.Matches(textBoxRegexTestText.Text);

                    WriteStatus("Match results: ");

                    if (mc.Count < 1)
                    {
                        WriteStatus(Properties.Resources.MatchRegexTestNoMatch);
                        return;
                    }
                    foreach (Match m in mc)
                    {
                        WriteStatus(string.Format("Match:{0}", m.Value));
                        if (_cdfMonitor.Config.AppSettings.WriteEvent)
                        {
                            WriteStatus("Writing test event to event log");
                            EventLog.WriteEntry(Process.GetCurrentProcess().MainModule.ModuleName, m.Value,
                                                EventLogEntryType.Information, 100);
                        }
                        foreach (string groupName in regex.GetGroupNames())
                        {
                            WriteStatus(string.Format(
                               "Group: <{0}>: {1}",
                               groupName,
                               m.Groups[groupName].Value));
                        }
                    }
                }
                else
                {
                    WriteStatus(Properties.Resources.MatchRegexTest);
                }
            }
            catch
            {
                WriteStatus(Properties.Resources.MatchRegexTestInvalid);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonMatchTmfCacheDirBrowse control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonMatchTmfCacheDirBrowse_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.AppSettings.TmfCacheDir = SelectFolder(textBoxMatchTmfCacheDir.Text);
        }

        /// <summary>
        /// Handles the Click event of the buttonMatchTmfServerBrowse control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonMatchTmfServerBrowse_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.AppSettings.TmfServers = SelectFolder(
                !string.IsNullOrEmpty(textBoxMatchTmfServer.Text)
                ? textBoxMatchTmfServer.Text
                : AppDomain.CurrentDomain.BaseDirectory);
        }

        /// <summary>
        /// Handles the Click event of the buttonMatchValidate control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonMatchValidate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Regex regex = new Regex(textBoxRegexPattern.Text, RegexOptions.IgnoreCase);
                WriteStatus(Properties.Resources.MatchRegexPatternValid);
            }
            catch
            {
                WriteStatus(Properties.Resources.MatchRegexPatternInvalid);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonMatchVerify control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonMatchVerify_Click(object sender, RoutedEventArgs e)
        {
            // Check log file path and etl path can be written to
            // TODO: need to move this to configuration and call it VerifyLoggingOptions once status text is built out

            bool status = true;
            ClearStatus();

            try
            {
                foreach (string server in _cdfMonitor.Config.TMFServersList)
                {
                    WriteStatus(string.Format("TMF Server info ok: {0}:{1}",
                        server,
                        (status &= _cdfMonitor.Config.VerifyTMFServer(server))));

                    if (!status)
                    {
                        WriteStatus("WARNING: Unable to find " + Properties.Resources.TMFServerGuidList + " in TMFServer root path.");
                    }
                }

                if (!string.IsNullOrEmpty(_cdfMonitor.Config.AppSettings.TmfCacheDir))
                {
                    WriteStatus(string.Format("TMF Cache Directory info ok: {0}:{1}",
                        _cdfMonitor.Config.AppSettings.TmfCacheDir,
                        (status &= FileManager.CheckPath(_cdfMonitor.Config.AppSettings.TmfCacheDir, true))));
                }
                else
                {
                    WriteStatus("TMF Cache Dir empty...");
                }

                if (_cdfMonitor.Config.AppSettings.WriteEvent)
                {
                    WriteStatus("Writing test event to event log");
                    EventLog.WriteEntry(Process.GetCurrentProcess().MainModule.ModuleName, "CDFMonitor Match Validate test event",
                                        EventLogEntryType.Information, 100);
                }

                WriteStatus(string.Format("Verification Result: {0}. See output for more detail.", status ? "Success" : "Fail"));
            }
            catch (Exception ex)
            {
                WriteStatus(ex.ToString());
                WriteStatus(string.Format("Verification Result: Fail. See output for more detail."));
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonModulesLoad control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonModuleLoad_Click(object sender, RoutedEventArgs e)
        {
            // If button then always pop dialog if radio button then always reload if else (function) dont pop dialog
            bool showDialog = false;

            // Uncheck since list being rebuilt
            ModulesCheckAll.IsChecked = false;

            if (sender is Button)
            {
                showDialog = true;
            }

            Cursor = Cursors.Wait;
            if (radioButtonModulesConfigFile.IsChecked == true)
            {
                radioButtonModulesConfigFile_Checked(sender, e);
            }
            else if (radioButtonModulesFile.IsChecked == true)
            {
                string newPath = string.Empty;

                if (showDialog)
                {
                    newPath = SelectFile("Trace Control Files (.ctl)|*.ctl|All Files (*.*)|*.*", textBoxModulePath.Text);
                }

                if (!string.IsNullOrEmpty(newPath))
                {
                    _cdfMonitor.Config.AppSettings.ModulePath = newPath;
                }

                radioButtonModulesFile_Checked(sender, e);
            }
            else if (radioButtonModulesLocalMachine.IsChecked == true)
            {
                radioButtonModulesLocalMachine_Checked(sender, e);
            }
            else if (radioButtonModulesRemoteMachine.IsChecked == true)
            {
                if (!string.IsNullOrEmpty(textBoxModulePath.Text)) //| showDialog)//| reload)
                {
                    radioButtonModulesRemoteMachine_Checked(sender, e);
                }
                else
                {
                    WriteStatus("Enter Remote Machine for 'Module Path'.", true);
                }
            }
            Cursor = Cursors.Arrow;
        }

        /// <summary>
        /// Clears UdpClient listview.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonNetworkClearList_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.UdpClientsListViewCollection.Clear();
        }

        /// <summary>
        /// Handles the Click event of the buttonNetworkVerify control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonNetworkVerify_Click(object sender, RoutedEventArgs e)
        {
            VerifySetting(Configuration.ConfigurationProperties.EnumProperties.UdpClientEnabled);
            VerifySetting(Configuration.ConfigurationProperties.EnumProperties.UdpPingEnabled);
            VerifySetting(Configuration.ConfigurationProperties.EnumProperties.UdpClientPort);
            VerifySetting(Configuration.ConfigurationProperties.EnumProperties.UdpPingTimer);
            VerifySetting(Configuration.ConfigurationProperties.EnumProperties.UdpServer);
            VerifySetting(Configuration.ConfigurationProperties.EnumProperties.UdpServerPort);

            if (_cdfMonitor.Config.AppSettings.UdpPingEnabled)
            {
                CDFM.Network.Udp udp = new Network.Udp(_cdfMonitor);
                udp.EnableWriter();
                Thread.Sleep(100);
                udp.DisableWriter();
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonNotifyVerify control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonNotifyVerify_Click(object sender, RoutedEventArgs e)
        {
            // TODO: move to configurationverify
            ClearStatus();

            WriteStatus(string.Format("SMTP server:{0}",
                (!string.IsNullOrEmpty(textBoxSmtpServer.Text)
                    && RemoteOperations.Ping(textBoxSmtpServer.Text))));

            Int32 test = 0;
            WriteStatus(string.Format("SMTP port:{0}",
                (!string.IsNullOrEmpty(textBoxSmtpPort.Text)
                    && Int32.TryParse(textBoxSmtpPort.Text, out test))));

            WriteStatus(string.Format("SMTP send to address:{0}",
              _cdfMonitor.Config.IsValidMailAddress(textBoxSmtpSendTo.Text)));

            WriteStatus(string.Format("SMTP send from address:{0}",
              _cdfMonitor.Config.IsValidMailAddress(textBoxSmtpSendFrom.Text)));

            // Overiding property function and setting back
            bool sendSmtp = _cdfMonitor.Config.SendSmtp;
            _cdfMonitor.Config.SendSmtp = true;

            // So i can set prop back
            _cdfMonitor.SendSMTP("CDFMONITOR_TEST_SMTP");
            _cdfMonitor.Config.SendSmtp = sendSmtp;

            WriteStatus("Check output tab for additional information");
        }

        /// <summary>
        /// Handles the Click event of the buttonLocalClean control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonOptionClean_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.ProcessOperator(Operators.Clean, true);
        }

        /// <summary>
        /// Handles the Click event of the buttonLocalDownloadTmfs control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonOptionDownloadTmfs_Click(object sender, RoutedEventArgs e)
        {
            BackgroundStart(sender, e, delegate()
            {
                return _cdfMonitor.Config.ProcessOperator(Operators.DownloadTMFs, true);
            });
        }

        /// <summary>
        /// Setup configuration for FTA
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOptionFTAConfig_Click(object sender, RoutedEventArgs e)
        {
            // Common settings for parsing
            _cdfMonitor.Config.AppSettings.RunAs = CDFM.Config.Configuration.ExecutionOptions.Console.ToString();
            _cdfMonitor.Config.AppSettings.UseTraceSourceForDestination = true;
            _cdfMonitor.Config.AppSettings.LogToConsole = false;
            _cdfMonitor.Config.AppSettings.LogFileAutoFlush = false;
            //_cdfMonitor.Config.AppSettings.LogMatchDetail = true;
            _cdfMonitor.Config.AppSettings.AdvancedOptions = true;
            _cdfMonitor.Config.AppSettings.Activity = CDFM.Config.Configuration.ActivityType.RegexParseToCsv.ToString();
            _cdfMonitor.Config.AppSettings.LogFileMaxCount = 0;
            _cdfMonitor.Config.AppSettings.LogFileMaxSize = 0;
            _cdfMonitor.Config.AppSettings.LogFileOverWrite = true;

            SetActivity();
            WriteStatus("FTA Config: Modified current config with common file type association settings. Modify any other configurations and use 'Register FTA' to associate .etl file to CDFMonitor.", true);
        }

        /// <summary>
        /// Handles the Click event of the buttonLocalInstallService control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonOptionInstallService_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.ProcessOperator(Operators.InstallService, true);
            SetOptionsLocalServiceButtons();
        }

        /// <summary>
        /// Registers File Type Association using default settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOptionRegisterFTA_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.FTAFolder = string.Format("{0}\\registerFTA", _cdfMonitor.Config.CDFMonitorProgramFiles);
            _cdfMonitor.Config.ProcessOperator(Operators.RegisterFta, true);
            _cdfMonitor.Config.AppSettings.AdvancedOptions = true;
        }

        /// <summary>
        /// Reset config file to defaults
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOptionResetConfig_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.ProcessOperator(Operators.ResetConfig, true);
            _cdfMonitor.Config.AppSettings.AdvancedOptions = true;
            SetActivity();
        }

        /// <summary>
        /// Handles the Click event of the buttonOptionsInsertMarker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonOptionsInsertMarker_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.MarkerEvents++;
            _cdfMonitor.LogOutput(string.Format("CDFMarker:{0}:{1}", _cdfMonitor.MarkerEvents, textBoxOptionsInsertMarkerText.Text), JobOutputType.Etw);
        }

        /// <summary>
        /// Handles the Click event of the buttonLocalStartService control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonOptionStartService_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.ProcessOperator(Operators.StartService, true);
            SetOptionsLocalServiceButtons();
        }

        /// <summary>
        /// Handles the Click event of the buttonLocalStopService control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonOptionStopService_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.ProcessOperator(Operators.StopService, true);
            SetOptionsLocalServiceButtons();
        }

        /// <summary>
        /// Handles the Click event of the buttonLocalUnInstallService control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonOptionUnInstallService_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.ProcessOperator(Operators.UninstallService, true);
            SetOptionsLocalServiceButtons();
        }

        /// <summary>
        /// Unregisters File Type Association using default settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOptionUnregisterFTA_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.FTAFolder = string.Format("{0}\\registerFTA", _cdfMonitor.Config.CDFMonitorProgramFiles);
            _cdfMonitor.Config.ProcessOperator(Operators.UnregisterFta, true);

            // Do more cleanup on empty folders that could have been created
            // CDFMonitor
            FileManager.DeleteFolder(_cdfMonitor.Config.CDFMonitorProgramFiles, true);
            // Citrix
            FileManager.DeleteFolder(Path.GetDirectoryName(_cdfMonitor.Config.CDFMonitorProgramFiles), true);
        }

        /// <summary>
        /// Handles the Click event of the buttonOutputClear control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonOutputClear_Click(object sender, RoutedEventArgs e)
        {
            listBoxConsoleOutput.BeginInit();
            if (_outputConsoleItems != null)
            {
                _outputConsoleItems.Clear();
            }
            if (_outputConsoleItemsAll != null)
            {
                _outputConsoleItemsAll.Clear();
            }

            listBoxConsoleOutput.EndInit();
            ClearStatus();
            GC.Collect();
        }

        /// <summary>
        /// Handles the Click event of the buttonOutputShowStats control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonOutputShowStats_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();

            foreach (string str in _cdfMonitor.GetStats().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                sb.AppendLine(str);
            }

            WriteStatus(sb.ToString(), true);
        }

        /// <summary>
        /// Handles the Click event of the buttonRemoteDeployPathBrowse control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonRemoteDeployPathBrowse_Click(object sender, RoutedEventArgs e)
        {
            textBoxRemoteDeployPath.Text = _cdfMonitor.Config.AppSettings.DeployPath = SelectFolder(
                !string.IsNullOrEmpty(textBoxRemoteDeployPath.Text)
                ? textBoxRemoteDeployPath.Text
                : Directory.GetCurrentDirectory());
        }

        /// <summary>
        /// Handles the Click event of the buttonRemoteGatherPathBrowse control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonRemoteGatherPathBrowse_Click(object sender, RoutedEventArgs e)
        {
            textBoxRemoteGatherPath.Text = _cdfMonitor.Config.AppSettings.GatherPath = SelectFolder(
                !string.IsNullOrEmpty(textBoxRemoteGatherPath.Text)
                ? textBoxRemoteGatherPath.Text
                : Directory.GetCurrentDirectory());
        }

        /// <summary>
        /// Handles the Click event of the buttonRemoteLoad control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonRemoteLoad_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(textBoxRemoteMachinesPath.Text))
            {
                textBoxRemoteMachinesPath.Text = SelectFile("Text Files (.txt)|*.csv|All Files (*.*)|*.*");
            }

            if (File.Exists(textBoxRemoteMachinesPath.Text))
            {
                WriteStatus("RemoteMachineFile: Loading file");
                textBoxRemoteInput.Text = File.ReadAllText(textBoxRemoteMachinesPath.Text);
            }
            else
            {
                WriteStatus("RemoteMachineFile: File does not exist.");
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonRemoteResetCache control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonRemoteResetCache_Click(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.RemoteMachineList.Clear();
        }

        /// <summary>
        /// Handles the Click event of the buttonRemoteVerify control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonRemoteVerify_Click(object sender, RoutedEventArgs e)
        {
            VerifyRemoteActivityConfig();
        }

        /// <summary>
        /// Handles the Click event of the buttonSaveToCtl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void buttonSaveToCtl_Click(object sender, RoutedEventArgs e)
        {
            string ret = SaveFile("Config Files (.ctl)|*.ctl|All Files (*.*)|*.*");
            if (string.IsNullOrEmpty(ret))
            {
                return;
            }

            StringBuilder sb = new StringBuilder();

            // Process input if the user clicked OK.
            foreach (Configuration.ModuleListViewItem lvi in _cdfMonitor.Config.ModuleListViewItems)
            {
                if (!(bool)lvi.Checked)
                {
                    continue;
                }

                sb.AppendLine(string.Format("{0}\t{1}", lvi.ModuleGuid.Trim(new char[2] { '{', '}' }), lvi.ModuleName));
            }

            File.WriteAllText(ret, sb.ToString());
        }

        /// <summary>
        /// Handles the Click event of the buttonSelectEventCommand control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonSelectEventCommand_Click(object sender, RoutedEventArgs e)
        {
            textBoxActionEventCommand.Text =
                _cdfMonitor.Config.AppSettings.EventCommand = SelectFile("All Files (*.*)|*.*");
        }

        /// <summary>
        /// Handles the Click event of the buttonSelectTraceFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonSelectTraceFile_Click(object sender, RoutedEventArgs e)
        {
            string ret = SelectFile("Trace Files (.etl)|*.etl|CDFM Zip Files (.zip)|*.zip| All Files (*.*)|*.*", textBoxTraceFileInput.Text);
            if (!String.IsNullOrEmpty(ret))
            {
                textBoxTraceFileInput.Text = ret;
                textBoxTraceFileInput_LostFocus(null, null);
            }
        }

        /// <summary>
        /// Handles the Click event of the buttonUploadUpload control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonUploadUpload_Click(object sender, RoutedEventArgs e)
        {
            BackgroundStart(sender, e, delegate()
            {
                _cdfMonitor.UploadPackage(string.Empty);
                return true;
            });
        }

        /// <summary>
        /// Handles the Click event of the buttonUploadVerify control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void buttonUploadVerify_Click(object sender, RoutedEventArgs e)
        {
            ClearStatus();
            if (!string.IsNullOrEmpty(textBoxUrlSite.Text))
            {
                _cdfMonitor.UrlUploadThread.Queue("verify");
            }

            WriteStatus("Check output tab for additional information");
        }

        /// <summary>
        /// OnClosing verifies whether configuration should be saved if modified.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CDFMonitorGui_Closing(object sender, CancelEventArgs e)
        {
            if (buttonSave.IsEnabled && _cdfMonitor.Config.AppSettings.Annoyance)
            {
                TimedSaveDialog dialog = new TimedSaveDialog();
                dialog.Enable();

                switch (dialog.WaitForResult())
                {
                    case TimedSaveDialog.Results.Disable:
                        _cdfMonitor.Config.AppSettings.Annoyance = false;
                        Save_Click(null, null);
                        break;

                    case TimedSaveDialog.Results.DontSave:
                        break;

                    case TimedSaveDialog.Results.Save:
                        Save_Click(null, null);
                        break;

                    case TimedSaveDialog.Results.SaveAs:
                        SaveAs_Click(null, null);
                        break;

                    case TimedSaveDialog.Results.Unknown:
                        // Don't worry about errors since we are closing
                        break;
                }
            }
        }

        /// <summary>
        /// Enables Advanced Options (Tabs) if checked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxActivityAdvancedOptions_Checked(object sender, RoutedEventArgs e)
        {
            SetActivity();
        }

        /// <summary>
        /// Handles the Checked event of the checkBoxLogMatchOnly control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void checkBoxLogMatchOnly_Checked(object sender, RoutedEventArgs e)
        {
            if ((bool)checkBoxLogMatchOnly.IsChecked)
            {
                checkBoxLogBufferOnMatch.IsEnabled = true;
            }
            else
            {
                checkBoxLogBufferOnMatch.IsEnabled = false;
            }
        }

        /// <summary>
        /// Enables filtering of 'local machine' registry modules by value in Module Filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxModuleEnableByFilter_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)(sender as CheckBox).IsChecked)
            {
                listViewModules.IsEnabled = false;
            }
            else
            {
                listViewModules.IsEnabled = true;
            }

            SetModuleSource();
        }

        /// <summary>
        /// Handles the Checked event of the checkBoxOptionsStartTraceOnEvent control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void checkBoxOptionsStartTraceOnEvent_Checked(object sender, RoutedEventArgs e)
        {
            if ((bool)checkBoxOptionsStartTraceOnEvent.IsChecked)
            {
                checkBoxOptionsStartTraceImmediately.IsEnabled = true;
                comboBoxOptionsStartTraceEventSource.IsEnabled = true;
                textBoxOptionsStartEventID.IsEnabled = true;
            }
            else
            {
                checkBoxOptionsStartTraceImmediately.IsEnabled = false;
                comboBoxOptionsStartTraceEventSource.IsEnabled = false;
                textBoxOptionsStartEventID.IsEnabled = false;
            }

            // Call stop trace on event to verify checkboxes
            checkBoxOptionsStopTraceOnEvent_Checked(null, null);
        }

        /// <summary>
        /// Handles the Checked event of the checkBoxOptionsStopTraceOnEvent control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void checkBoxOptionsStopTraceOnEvent_Checked(object sender, RoutedEventArgs e)
        {
            if ((bool)checkBoxOptionsStopTraceOnEvent.IsChecked)
            {
                // Force stop of trace if using events and start event is not enabled
                if ((bool)checkBoxOptionsStartTraceOnEvent.IsChecked)
                {
                    checkBoxOptionsStopTracePermanently.IsEnabled = true;
                }
                else
                {
                    checkBoxOptionsStopTracePermanently.IsChecked = true;
                }

                comboBoxOptionsStopTraceEventSource.IsEnabled = true;
                textBoxOptionsStopEventID.IsEnabled = true;
            }
            else
            {
                checkBoxOptionsStopTracePermanently.IsEnabled = false;
                comboBoxOptionsStopTraceEventSource.IsEnabled = false;
                textBoxOptionsStopEventID.IsEnabled = false;
            }
        }

        /// <summary>
        /// Handles the Click event of the checkBoxOptionsUseCredentials control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void checkBoxOptionsUseCredentials_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)(sender as CheckBox).IsChecked)
            {
                checkBoxOptionsUseServiceCredentials.IsEnabled = true;
                _cdfMonitor.Config.ResourceCredentials.CheckResourceCredentials(CDFM.Properties.Resources.SessionName, true);
            }
            else
            {
                checkBoxOptionsUseServiceCredentials.IsEnabled = false;
                Credentials.DeleteCredentials(Properties.Resources.SessionName);
            }
        }

        /// <summary>
        /// Handles the Checked event of the checkBoxOutputInvertFilter control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void checkBoxOutputInvertFilter_Checked(object sender, RoutedEventArgs e)
        {
            if ((sender is CheckBox)
                && (bool)(sender as CheckBox).IsChecked)
            {
                _outputFilterPatternInverted = true;
            }
            else
            {
                _outputFilterPatternInverted = false;
            }

            textBoxOutputFilter_TextChanged(sender, null);
        }

        /// <summary>
        /// Handles the Checked event of the checkBoxOverwriteLogFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void checkBoxOverwriteLogFile_Checked(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.AppSettings.LogFileOverWrite = true;
        }

        /// <summary>
        /// Clears the status.
        /// </summary>
        private void ClearStatus()
        {
            WriteStatus(string.Empty, true);
        }

        /// <summary>
        /// Configures the GUI writer.
        /// </summary>
        private void ConfigureGuiWriter()
        {
            if (_cdfMonitor.Config.AppSettings.LogToConsole && !_loggerGui.Enabled)
            {
                _loggerGui = _cdfMonitor.Config.LoggerQueue.AddJob(JobType.Gui, "gui", this);
            }
            else if (!_cdfMonitor.Config.AppSettings.LogToConsole && _loggerGui.Enabled)
            {
                _cdfMonitor.Config.LoggerQueue.RemoveJob(_loggerGui);
            }
        }

        /// <summary>
        /// Configures the remote activity.
        /// </summary>
        /// <param name="set">if set to <c>true</c> [set].</param>
        private void ConfigureRemoteActivity(bool set)
        {
            if (set)
            {
                var checkedButton = gridRemoteActions.Children.OfType<RadioButton>()
                  .FirstOrDefault(r => r.IsChecked == true);
                if (checkedButton != null)
                {
                    _cdfMonitor.Config.RemoteActivity = (RemoteOperations.RemoteOperationMethods)Enum.Parse(
                        typeof(RemoteOperations.RemoteOperationMethods), checkedButton.Content.ToString(), true);
                }
                else
                {
                    _cdfMonitor.Config.RemoteActivity = RemoteOperations.RemoteOperationMethods.Unknown;
                }
            }
            else
            {
                try
                {
                    gridRemoteActions.Children.OfType<RadioButton>()
                        .FirstOrDefault(r => r.Content.ToString() == _cdfMonitor.Config.RemoteActivity.ToString())
                        .IsChecked = true;
                }
                catch { }
            }
        }

        /// <summary>
        /// Copies the CMD can execute.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="CanExecuteRoutedEventArgs" /> instance containing the event data.</param>
        private void CopyCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            try
            {
                if ((sender as ListBox).SelectedItems.Count > 0)
                {
                    e.CanExecute = true;
                }
                else
                {
                    e.CanExecute = false;
                }
            }
            catch (Exception ex)
            {
                WriteStatus("Exception:CopyCmd:" + ex.ToString());
            }
        }

        /// <summary>
        /// Copies the CMD can execute.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="CanExecuteRoutedEventArgs" /> instance containing the event data.</param>
        private void CopyCmdCanExecuteListView(object sender, CanExecuteRoutedEventArgs e)
        {
            try
            {
                if ((sender as ListView).SelectedItems.Count > 0)
                {
                    e.CanExecute = true;
                }
                else
                {
                    e.CanExecute = false;
                }
            }
            catch (Exception ex)
            {
                WriteStatus("Exception:CopyCmdListView:" + ex.ToString());
            }
        }

        /// <summary>
        /// Copies the CMD executed.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="e">The <see cref="ExecutedRoutedEventArgs" /> instance containing the event data.</param>
        private void CopyCmdExecuted(object target, ExecutedRoutedEventArgs e)
        {
            try
            {
                Clipboard.Clear();
                StringBuilder copyContent = new StringBuilder();

                foreach (TextBlock lbi in (target as ListBox).SelectedItems)
                {
                    if (lbi != null
                        && copyContent.Length < (copyContent.MaxCapacity - (lbi as TextBlock).Text.Length))
                    {
                        copyContent.AppendLine((lbi as TextBlock).Text);
                    }
                }

                Clipboard.SetText(copyContent.ToString());
            }
            catch (Exception ex)
            {
                WriteStatus("Exception:CopyCmdExecute:" + ex.ToString());
            }
        }

        /// <summary>
        /// Copies the CMD executed.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="e">The <see cref="ExecutedRoutedEventArgs" /> instance containing the event data.</param>
        private void CopyCmdExecutedListView(object target, ExecutedRoutedEventArgs e)
        {
            try
            {
                Clipboard.Clear();
                StringBuilder copyContent = new StringBuilder();

                foreach (Config.Configuration.UdpClientsListViewItem lvi in (target as ListView).SelectedItems)
                {
                    if (lvi != null)
                    {
                        copyContent.Append(lvi.ClientName);
                        copyContent.Append("," + lvi.ClientActivity);
                        copyContent.Append("," + lvi.ClientPingTime);
                        copyContent.Append("," + lvi.UdpCounter);
                        copyContent.Append("," + lvi.TracesPerSecond);
                        copyContent.Append("," + lvi.MatchedEvents);
                        copyContent.Append("," + lvi.MissedMatchedEvents);
                        copyContent.Append("," + lvi.ProcessedEvents);
                        copyContent.Append("," + lvi.AvgProcessCpu);
                        copyContent.Append("," + lvi.CurrentProcessCpu);
                        copyContent.Append("," + lvi.CurrentMachineCpu);
                        copyContent.Append("," + lvi.Duration + Environment.NewLine);
                    }
                }

                Clipboard.SetText(copyContent.ToString());
            }
            catch (Exception ex)
            {
                WriteStatus("Exception:CopyCmdExecuteListView:" + ex.ToString());
            }
        }

        private void DisplayLogPaths()
        {
            if (!string.IsNullOrEmpty(_cdfMonitor.Config.LoggerJobUtilityPath()))
            {
                WriteStatus("Current Utility Log: " + _cdfMonitor.Config.LoggerJobUtilityPath());
            }

            if (!string.IsNullOrEmpty(_cdfMonitor.Config.LoggerJobTracePath()))
            {
                WriteStatus("Current Trace Log: " + _cdfMonitor.Config.LoggerJobTracePath());
            }
        }

        /// <summary>
        /// Enables the remote.
        /// </summary>
        /// <param name="enable">if set to <c>true</c> [enable].</param>
        private void EnableRemoteConfig(bool enable)
        {
            if (enable)
            {
                buttonEditRemoteConfig.IsEnabled = true;
                buttonEditRemoteConfig.Visibility = System.Windows.Visibility.Visible;
                return;
            }
            else
            {
                buttonEditRemoteConfig.IsEnabled = false;
                buttonEditRemoteConfig.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        /// <summary>
        /// Enables Save Buttons in gui
        /// </summary>
        /// <param name="enable"></param>
        private void EnableSaveButtons(bool enable)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                {
                    EnableSaveButtons(enable);
                }));
            }
            else
            {
                buttonSave.IsEnabled = enable;
                buttonSaveAs.IsEnabled = enable;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="fullList"></param>
        /// <returns></returns>
        private Queue<TextBlock> GenerateConsoleFilteredList(Queue<TextBlock> fullList)
        {
            Queue<TextBlock> tempList = new Queue<TextBlock>();

            if (string.IsNullOrEmpty(_outputFilterPattern))
            {
                return fullList;
            }

            foreach (TextBlock lbi in fullList)
            {
                if (_outputFilterNew)
                {
                    // Return empty as no need to process further.
                    return new Queue<TextBlock>();
                }

                if (Regex.IsMatch(lbi.Text, _outputFilterPattern, RegexOptions.IgnoreCase))
                {
                    tempList.Enqueue(lbi);
                }
            }

            return tempList;
        }

        /// <summary>
        /// Lists the box scroll into view.
        /// </summary>
        /// <param name="reset">if set to <c>true</c> [reset].</param>
        private void ListBoxScrollIntoView(bool reset)
        {
            if (!_cdfMonitor.Config.AppSettings.AutoScroll)
            {
                return;
            }

            Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    if (listBoxConsoleOutput.HasItems)
                    {
                        if (reset)
                        {
                            listBoxConsoleOutput.Items.MoveCurrentToPosition(0);
                        }
                        else
                        {
                            listBoxConsoleOutput.Items.MoveCurrentToLast();
                        }

                        listBoxConsoleOutput.ScrollIntoView((TextBlock)listBoxConsoleOutput.Items.CurrentItem);
                    }
                }
                catch (Exception e)
                {
                    Debug.Print("Exception:" + e.ToString());
                }
            }));
        }

        /// <summary>
        /// Callback to handle Sorting of UdpClient listview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewNetworkUdpClientsColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader headerClicked =
              e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    string header = headerClicked.Column.Header as string;
                    listViewNetworkUdpClientsSort(header, direction);
                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }

        /// <summary>
        /// Handles sorting of UdpClient listview.
        /// </summary>
        /// <param name="sortBy"></param>
        /// <param name="direction"></param>
        private void listViewNetworkUdpClientsSort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView =
            CollectionViewSource.GetDefaultView(_cdfMonitor.Config.UdpClientsListViewCollection);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        /// <summary>
        /// Handles the Click event of the Load control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void Load_Click(object sender, RoutedEventArgs e)
        {
            string ret = SelectFile("Config Files (.config)|*.config|All Files (*.*)|*.*");

            // Process input if the user clicked OK.
            if (!String.IsNullOrEmpty(ret))
            {
                _cdfMonitor.Config.AppSettings.ConfigFile = ret; // openFileDialog1.FileName;
                Title = _cdfMonitor.Config.SessionName + " " + _cdfMonitor.Config.AppSettings.ConfigFile;
                _cdfMonitor.Config.ReadConfigFile(ret);

                SetActivity();
            }
        }

        /// <summary>
        /// Handles the GotFocus event of the Logging control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void Logging_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Logging_GotFocus:enter");
                WriteStatus(Properties.Resources.LoggingGotFocus, true);

                //WriteStatus("-----------------------");
                DisplayLogPaths();

                checkBoxLogMatchOnly_Checked(null, null);
                _tabInitialized = true;
            }
        }

        /// <summary>
        /// Handles the GotFocus event of the Match control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void Match_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Match_GotFocus:enter");
                WriteStatus(Properties.Resources.MatchGotFocus, true);
                if (string.IsNullOrEmpty(textBoxRegexTestText.Text))
                {
                    textBoxRegexTestText.Text = Properties.Resources.MatchRegexTestText;
                }

                _tabInitialized = true;
            }
        }

        /// <summary>
        /// Handles the GotFocus event of the Modules control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void Modules_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Modules_GotFocus:enter");
                WriteStatus(Properties.Resources.ModulesGotFocus, true);
                _tabInitialized = true;
            }
        }

        /// <summary>
        /// Handles the Click event of the ModulesCheckAll control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void ModulesCheckAll_Click(object sender, RoutedEventArgs e)
        {
            var cb = (CheckBox)sender;
            foreach (Config.Configuration.ModuleListViewItem lvi in _cdfMonitor.Config.ModuleListViewCollection)
            {
                lvi.Checked = cb.IsChecked;
            }
        }

        /// <summary>
        /// Moduleses the load list.
        /// </summary>
        /// <param name="modules">The modules.</param>
        /// <param name="isChecked">The is checked.</param>
        private void ModulesLoadList(Dictionary<string, string> modules)
        {
            List<Configuration.ModuleListViewItem> collection = new List<Configuration.ModuleListViewItem>();
            _moduleFilterNew = false;
            foreach (var kvp in modules)
            {
                if (_moduleFilterNew)
                {
                    _cdfMonitor.LogOutput("DEBUG: ModulesLoadList<List>:returning early.");
                    _moduleFilterNew = false;
                    return;
                }

                bool? ischecked = false;
                if (_cdfMonitor.Config.ModuleListViewItems.Any(v => v.ModuleGuid == kvp.Key))
                {
                    ischecked = _cdfMonitor.Config.ModuleListViewItems.Find(v => v.ModuleGuid == kvp.Key).Checked;
                    ischecked = ischecked == null ? false : ischecked;
                }

                collection.Add(new Configuration.ModuleListViewItem()
                {
                    Checked = ischecked,
                    ModuleGuid = kvp.Key,
                    ModuleName = kvp.Value
                });
            }

            ModulesLoadList(collection);
        }

        /// <summary>
        /// Moduleses the load list.
        /// </summary>
        /// <param name="modules">The modules.</param>
        private void ModulesLoadList(List<Configuration.ModuleListViewItem> modules)
        {
            SetPropertyNotifications(false);
            _moduleFilterNew = false;
            _cdfMonitor.LogOutput("DEBUG: ModulesLoadList:enter");

            // Load modules list
            ClearStatus();

            if (_cdfMonitor.Config.ModuleListViewCollection != null && _cdfMonitor.Config.ModuleListViewCollection.Count > 0)
            {
                _cdfMonitor.Config.ModuleListViewCollection.Clear();
            }

            foreach (Configuration.ModuleListViewItem lvi in modules)
            {
                if (_moduleFilterNew)
                {
                    _moduleFilterNew = false;
                    return;
                }

                if (!_cdfMonitor.Config.IsValidRegexPattern(textBoxModulesFilter.Text)
                    || !Regex.IsMatch(lvi.ModuleGuid, textBoxModulesFilter.Text, RegexOptions.IgnoreCase)
                    & !Regex.IsMatch(lvi.ModuleName, textBoxModulesFilter.Text, RegexOptions.IgnoreCase))
                {
                    continue;
                }

                bool? check;
                if (_cdfMonitor.Config.AppSettings.ModuleEnableByFilter)
                {
                    check = Regex.IsMatch(string.Format("{0} {1}", lvi.ModuleName, lvi.ModuleGuid), _cdfMonitor.Config.AppSettings.ModuleFilter, RegexOptions.IgnoreCase);
                }
                else
                {
                    check = lvi.Checked;
                }

                _cdfMonitor.Config.ModuleListViewCollection.Add(new Config.Configuration.ModuleListViewItem
                {
                    Checked = check,
                    //Checked = lvi.Checked,
                    ModuleName = lvi.ModuleName,
                    ModuleGuid = lvi.ModuleGuid
                });

                // }
            }

            listViewModules.GetBindingExpression(ListView.ItemsSourceProperty).UpdateTarget();
            if (Modules.IsFocused)
            {
                WriteStatus("To apply module list in current view to 'Current (Config)' select 'Save' button then switch to 'Current (Config)'", true);
            }

            SetPropertyNotifications(true);
        }

        /// <summary>
        /// Handles the GotFocus event of the Match control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void Network_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Network_GotFocus:enter");
                WriteStatus(Properties.Resources.NetworkGotFocus, true);

                if (_cdfMonitor.Config.Activity == Configuration.ActivityType.Server)
                {
                    listViewNetworkUdpClients.IsEnabled = true;
                }
                else
                {
                    listViewNetworkUdpClients.IsEnabled = false;
                }

                _tabInitialized = true;
            }
        }

        /// <summary>
        /// Handles the GotFocus event of the Notify control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void Notify_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Notify_GotFocus:enter");
                WriteStatus(Properties.Resources.NotifyGotFocus, true);
                _tabInitialized = true;
            }
        }

        /// <summary>
        /// Called when [property changed].
        /// </summary>
        /// <param name="propName">Name of the prop.</param>
        private void OnPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(
                    this, new PropertyChangedEventArgs(propName));
            }
        }

        /// <summary>
        /// Handles the GotFocus event of the Options control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void Options_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Options_GotFocus:enter");
                WriteStatus(Properties.Resources.OptionsGotFocus, true);
                _tabInitialized = true;

                // Start with all disabled
                gridOptionsOperators.IsEnabled = false;

                foreach (UIElement child in gridOptionsAdvanced.Children)
                {
                    child.IsEnabled = false;
                }
                foreach (UIElement child in gridOptionsChild.Children)
                {
                    child.IsEnabled = false;
                }

                // Should always be enabled
                checkBoxOptionsAllowSingleInstance.IsEnabled = true;

                // Selectively enable
                if (_cdfMonitor.Config.Activity == Configuration.ActivityType.Remote)
                {
                    gridOptionsOperators.IsEnabled = false;

                    textBoxOptionsBufferLines.IsEnabled = true;
                    textBoxOptionsOperationRetries.IsEnabled = true;
                    checkBoxOptionsUseCredentials.IsEnabled = true;
                    checkBoxOptionsAutoScroll.IsEnabled = true;
                    checkBoxOptionsUseServiceCredentials.IsEnabled = true;
                }
                else if (buttonCloseRemoteConfig.IsEnabled == true)
                {
                    // Then editing remote config
                    textBoxOptionsBufferMax.IsEnabled = true;
                    labelOptionBufferMax.IsEnabled = true;
                    textBoxOptionsBufferMin.IsEnabled = true;
                    labelOptionBufferMin.IsEnabled = true;
                    textBoxOptionsBufferSize.IsEnabled = true;
                    labelOptionBufferSize.IsEnabled = true;
                    textBoxOptionsLogLevel.IsEnabled = true;
                    labelOptionsLogLevel.IsEnabled = true;
                    textBoxOptionsConfigFile.IsEnabled = true;
                    labelOptionConfigFile.IsEnabled = true;

                    checkBoxOptionsStartTraceOnEvent.IsEnabled = true;
                    labelOptionsStartEventID.IsEnabled = true;
                    checkBoxOptionsStopTraceOnEvent.IsEnabled = true;
                    labelOptionsStopEventID.IsEnabled = true;
                }
                else
                {
                    foreach (UIElement child in gridOptionsAdvanced.Children)
                    {
                        child.IsEnabled = true;
                    }
                    foreach (UIElement child in gridOptionsChild.Children)
                    {
                        child.IsEnabled = true;
                    }

                    SetOptionsLocalServiceButtons();
                }

                checkBoxOptionsStartTraceOnEvent_Checked(checkBoxOptionsStartTraceOnEvent, null);
                checkBoxOptionsStopTraceOnEvent_Checked(checkBoxOptionsStopTraceOnEvent, null);
            }
        }

        /// <summary>
        /// Handles the GotFocus event of the Output control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void Output_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Output_GotFocus:enter");
                WriteStatus(Properties.Resources.OutputGotFocus, true);
                _tabInitialized = true;
                ListBoxScrollIntoView(false);
            }
        }

        /// <summary>
        /// Lists the box console update action.
        /// </summary>
        private void OutputConsoleUpdateThreadProc()
        {
            bool needsRefresh = false;
            while (true)
            {
                try
                {
                    Thread.Sleep(100);
                    Queue<WriteListObj> wlist;

                    lock (WriteList)
                    {
                        if (WriteList.Count < 1 & !_outputFilterNew)
                        {
                            if (needsRefresh)
                            {
                                ListBoxScrollIntoView(false);
                                needsRefresh = false;
                            }

                            continue;
                        }
                        else if (WriteList.Count > 0)
                        {
                            needsRefresh = true;
                        }

                        wlist = new Queue<WriteListObj>(WriteList);
                        WriteList.Clear();
                    }

                    UpdateOutputConsole(ApplyOutputConsoleFilter(wlist));
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Debug.Print("ListBoxConsoleUpdateAction:exception:" + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the radioButtonActivityCaptureNetworkTraceMessage control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void radioButtonActivityCaptureNetworkTraceMessage_Click(object sender = null, RoutedEventArgs e = null)
        {
            ClearStatus();
            WriteStatus(Properties.Resources.NetworkGotFocus);
            EnableRemoteConfig(false);
            Network.IsEnabled = true;
            Action.IsEnabled = true;
            Modules.IsEnabled = false;
            Logging.IsEnabled = true;
            Match.IsEnabled = true;
            Notify.IsEnabled = true;
            Upload.IsEnabled = true;
            Remote.IsEnabled = false;

            // Options.IsEnabled = true;
            Output.IsEnabled = true;
            textBoxTraceFileInput.IsEnabled = false;
            buttonSelectTraceFile.IsEnabled = false;
            checkBoxActivityParseFileNames.IsEnabled = false;

            _cdfMonitor.Config.Activity = Config.Configuration.ActivityType.Server;
        }

        /// <summary>
        /// Handles the Click event of the RadioButtonCaptureTrace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void radioButtonActivityCaptureTrace_Click(object sender = null, RoutedEventArgs e = null)
        {
            ClearStatus();
            WriteStatus(Properties.Resources.CaptureTrace);
            EnableRemoteConfig(false);
            Network.IsEnabled = true;
            Modules.IsEnabled = true;
            Logging.IsEnabled = true;
            Match.IsEnabled = false;
            Action.IsEnabled = false;
            Notify.IsEnabled = false;
            Upload.IsEnabled = false;
            Remote.IsEnabled = false;
            Output.IsEnabled = true;
            textBoxTraceFileInput.IsEnabled = false;
            buttonSelectTraceFile.IsEnabled = false;
            checkBoxActivityParseFileNames.IsEnabled = false;

            _cdfMonitor.Config.Activity = Config.Configuration.ActivityType.TraceToEtl;
        }

        /// <summary>
        /// Handles the Click event of the RadioButtonCaptureTraceMessage control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void radioButtonActivityCaptureTraceMessage_Click(object sender = null, RoutedEventArgs e = null)
        {
            ClearStatus();
            WriteStatus(Properties.Resources.CaptureTraceMessage);
            EnableRemoteConfig(false);
            Network.IsEnabled = true;
            Modules.IsEnabled = true;
            Logging.IsEnabled = true;
            Match.IsEnabled = true;
            Action.IsEnabled = true;
            Notify.IsEnabled = true;
            Upload.IsEnabled = true;
            Remote.IsEnabled = false;
            Output.IsEnabled = true;
            textBoxTraceFileInput.IsEnabled = false;
            buttonSelectTraceFile.IsEnabled = false;
            checkBoxActivityParseFileNames.IsEnabled = false;

            _cdfMonitor.Config.Activity = Config.Configuration.ActivityType.TraceToCsv;
        }

        /// <summary>
        /// Handles the Click event of the RadioButtonParseTraceMessage control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void radioButtonActivityParseTraceMessage_Click(object sender = null, RoutedEventArgs e = null)
        {
            ClearStatus();
            WriteStatus(Properties.Resources.ParseTraceMessage);
            EnableRemoteConfig(false);
            Network.IsEnabled = false;
            Modules.IsEnabled = false;
            Logging.IsEnabled = true;
            Match.IsEnabled = true;
            Action.IsEnabled = true;
            Notify.IsEnabled = false;
            Upload.IsEnabled = false;
            Remote.IsEnabled = false;
            Output.IsEnabled = true;
            textBoxTraceFileInput.IsEnabled = true;
            buttonSelectTraceFile.IsEnabled = true;
            checkBoxActivityParseFileNames.IsEnabled = true;

            _cdfMonitor.Config.Activity = Config.Configuration.ActivityType.RegexParseToCsv;
        }

        /// <summary>
        /// Handles the Click event of the radioButtonActivityRemoteActivities control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void radioButtonActivityRemoteActivities_Click(object sender = null, RoutedEventArgs e = null)
        {
            ClearStatus();
            WriteStatus(Properties.Resources.RemoteActivities);
            EnableRemoteConfig(true);
            Network.IsEnabled = false;
            Modules.IsEnabled = false;
            Logging.IsEnabled = true;
            Match.IsEnabled = false;
            Action.IsEnabled = false;
            Notify.IsEnabled = false;
            Upload.IsEnabled = false;
            Remote.IsEnabled = true;
            Output.IsEnabled = true;
            textBoxTraceFileInput.IsEnabled = false;
            buttonSelectTraceFile.IsEnabled = false;
            checkBoxActivityParseFileNames.IsEnabled = false;

            _cdfMonitor.Config.Activity = Config.Configuration.ActivityType.Remote;
        }

        /// <summary>
        /// Handles the Click event of the RadioButtonParseTrace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void radioButtonModulesConfigFile_Checked(object sender, RoutedEventArgs e)
        {
            if (_cdfMonitor.Config.ModuleSource != Configuration.ModuleSourceType.Configuration)
            {
                _cdfMonitor.Config.ModuleSource = Configuration.ModuleSourceType.Configuration;
            }

            buttonModulesLoad.IsEnabled = false;
            textBoxModulePath.IsEnabled = false;
            _cdfMonitor.LogOutput("DEBUG: radioButtonModulesConfigFile_Checked:enter");
            ModulesLoadList(_cdfMonitor.Config.ModuleListCurrentConfigCache);
        }

        /// <summary>
        /// Handles the Checked event of the radioButtonModulesFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void radioButtonModulesFile_Checked(object sender, RoutedEventArgs e)
        {
            if (_cdfMonitor.Config.ModuleSource != Configuration.ModuleSourceType.File)
            {
                _cdfMonitor.Config.ModuleSource = Configuration.ModuleSourceType.File;
            }

            buttonModulesLoad.IsEnabled = true;
            textBoxModulePath.IsEnabled = true;
            WriteStatus("Loading modules from file");

            if (File.Exists(textBoxModulePath.Text))
            {
                ModulesLoadList(_cdfMonitor.Config.ReadControlFile(textBoxModulePath.Text));
            }
        }

        /// <summary>
        /// Handles the Checked event of the radioButtonModulesLocalMachine control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void radioButtonModulesLocalMachine_Checked(object sender, RoutedEventArgs e)
        {
            if (_cdfMonitor.Config.ModuleSource != Configuration.ModuleSourceType.LocalMachine)
            {
                _cdfMonitor.Config.ModuleSource = Configuration.ModuleSourceType.LocalMachine;
            }

            _cdfMonitor.LogOutput("DEBUG: radioButtonModulesLocalMachine_Checked:enter");
            buttonModulesLoad.IsEnabled = false;
            textBoxModulePath.IsEnabled = false;
            WriteStatus("Loading modules from local machine");
            ModulesLoadList(_cdfMonitor.Config.EnumModulesFromReg());
        }

        /// <summary>
        /// Handles the Checked event of the radioButtonModulesRemoteMachine control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void radioButtonModulesRemoteMachine_Checked(object sender, RoutedEventArgs e)
        {
            if (_cdfMonitor.Config.ModuleSource != Configuration.ModuleSourceType.RemoteMachine)
            {
                _cdfMonitor.Config.ModuleSource = Configuration.ModuleSourceType.RemoteMachine;
            }

            buttonModulesLoad.IsEnabled = true;
            textBoxModulePath.IsEnabled = true;

            Cursor = Cursors.Wait;

            if (_moduleListRemoteCache.Count > 0 && !string.IsNullOrEmpty(textBoxModulePath.Text) && _moduleListRemoteCacheSource == textBoxModulePath.Text)
            {
                ModulesLoadList(_moduleListRemoteCache);
            }
            else if (!string.IsNullOrEmpty(textBoxModulePath.Text) && RemoteOperations.Ping(textBoxModulePath.Text))
            {
                _moduleListRemoteCache = _cdfMonitor.Config.EnumRemoteModulesFromReg();
                ModulesLoadList(_moduleListRemoteCache);
                _moduleListRemoteCacheSource = textBoxModulePath.Text;
            }

            Cursor = Cursors.Arrow;
        }

        /// <summary>
        /// Handles the Checked event of the radioButtonRemoteActivity control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void radioButtonRemoteActivity_Checked(object sender, RoutedEventArgs e)
        {
            RemoteDisableControls();
            ConfigureRemoteActivity(true);
            RadioButtonRemoteActivityVerify();
        }

        /// <summary>
        /// Radioes the button remote activity verify.
        /// </summary>
        /// <param name="repair">if set to <c>true</c> [repair].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool RadioButtonRemoteActivityVerify(bool repair = false)
        {
            _cdfMonitor.LogOutput("DEBUG: RadioButtonRemoteActivityVerify:enter");
            _cdfMonitor.Config.ProcessRemoteMachines();

            ConfigureRemoteActivity(false);

            bool retval = VerifySetting(Configuration.ConfigurationProperties.EnumProperties.RemoteMachines)
                    && VerifySetting(Configuration.ConfigurationProperties.EnumProperties.RemoteActivity);

            switch (_cdfMonitor.Config.RemoteActivity)
            {
                case RemoteOperations.RemoteOperationMethods.Check:
                    break;

                case RemoteOperations.RemoteOperationMethods.Deploy:
                    textBoxRemoteDeployPath.IsEnabled = true;
                    buttonRemoteDeployPathBrowse.IsEnabled = true;
                    comboBoxRemoteServiceStartMode.IsEnabled = true;
                    retval &= VerifySetting(Configuration.ConfigurationProperties.EnumProperties.DeployPath, repair);
                    textBoxRemoteDeployPath.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                    break;

                case RemoteOperations.RemoteOperationMethods.Gather:
                    textBoxRemoteGatherPath.IsEnabled = true;
                    buttonRemoteGatherPathBrowse.IsEnabled = true;
                    retval &= VerifySetting(Configuration.ConfigurationProperties.EnumProperties.GatherPath, repair);
                    textBoxRemoteGatherPath.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                    break;

                case RemoteOperations.RemoteOperationMethods.Modify:
                    comboBoxRemoteServiceStartMode.IsEnabled = true;
                    break;

                case RemoteOperations.RemoteOperationMethods.Start:
                case RemoteOperations.RemoteOperationMethods.Stop:
                case RemoteOperations.RemoteOperationMethods.UnDeploy:
                    break;

                case RemoteOperations.RemoteOperationMethods.Unknown:
                default:
                    _cdfMonitor.LogOutput(string.Format("RadioButtonRemoteActivityVerify:unknown button:returning false:{0}", _cdfMonitor.Config.RemoteActivity));
                    retval = false;
                    break;
            }

            return retval;
        }

        /// <summary>
        /// Handles the GotFocus event of the Remote control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void Remote_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Remote_GotFocus:enter");
                RemoteDisableControls();
                RadioButtonRemoteActivityVerify();
                WriteStatus(Properties.Resources.RemoteGotFocus, true);

                // Populate trace file box
                _tabInitialized = true;
            }
        }

        /// <summary>
        /// Remotes the disable controls.
        /// </summary>
        private void RemoteDisableControls()
        {
            textBoxRemoteDeployPath.IsEnabled = false;
            buttonRemoteDeployPathBrowse.IsEnabled = false;
            textBoxRemoteGatherPath.IsEnabled = false;
            buttonRemoteGatherPathBrowse.IsEnabled = false;
            comboBoxRemoteServiceStartMode.IsEnabled = false;
        }

        /// <summary>
        /// Handles the Click event of the Save control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SetPropertyNotifications(false);

            // Allowing modulesource other than configuration causes empty current config next utility start hardcoding to configuration makes gui more intuitive on load
            if (!_cdfMonitor.Config.AppSettings.ModuleEnableByFilter)
            {
                _cdfMonitor.Config.ModuleSource = Configuration.ModuleSourceType.Configuration;
            }

            SaveConfigFile(_cdfMonitor.Config.CurrentConfigFile());

            SetActivity();
            SetPropertyNotifications(true);
        }

        /// <summary>
        /// Handles the Click event of the SaveAs control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SetPropertyNotifications(false);
            string ret = SaveFile("Config Files (.config)|*.config|All Files (*.*)|*.*");
            if (!_cdfMonitor.Config.AppSettings.ModuleEnableByFilter)
            {
                _cdfMonitor.Config.ModuleSource = Configuration.ModuleSourceType.Configuration;
            }

            // Process input if the user clicked OK.
            if (!String.IsNullOrEmpty(ret))
            {
                SaveConfigFile(ret);
            }

            SetActivity();
            SetPropertyNotifications(true);
        }

        /// <summary>
        /// Saves the config file.
        /// </summary>
        /// <param name="file">The file.</param>
        private void SaveConfigFile(string file)
        {
            if (buttonCloseRemoteConfig.IsEnabled)
            {
                // Verify mandatory settings
                SetRemoteMandatorySettings();
            }
            _cdfMonitor.Config.WriteConfigFile(file);

            EnableSaveButtons(false);

            // EnablePropertyNotifications(true,true);
        }

        /// <summary>
        /// Saves the file.
        /// </summary>
        /// <param name="pattern">The pattern.</param>
        /// <returns>System.String.</returns>
        private string SaveFile(string pattern)
        {
            try
            {
                SaveFileDialog fileDialog = new SaveFileDialog();

                // To allow selector to select files and folders
                fileDialog.ValidateNames = false;
                fileDialog.CheckFileExists = false;
                fileDialog.CheckPathExists = true;

                // Set filter options and filter index.
                fileDialog.Filter = pattern; // "Config Files (.config)|*.config|All Files

                // (*.*)|*.*";
                fileDialog.FilterIndex = 1;

                // Call the ShowDialog method to show the dialog box.
                if (fileDialog.ShowDialog() == true)
                {
                    return fileDialog.FileName;
                }

                return string.Empty;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("SaveFile:exception:" + e.ToString());
                return string.Empty;
            }
            finally
            {
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            }
        }

        /// <summary>
        /// Selects the file.
        /// </summary>
        /// <param name="pattern">The pattern.</param>
        /// <param name="filename">The filename.</param>
        /// <returns>System.String.</returns>
        private string SelectFile(string pattern, string filename = null)
        {
            try
            {
                // Create an instance of the open file dialog box.
                OpenFileDialog openFileDialog1;

                openFileDialog1 = new OpenFileDialog();

                // Set filter options and filter index.
                openFileDialog1.Filter = pattern;
                openFileDialog1.FilterIndex = 1;
                if (!string.IsNullOrEmpty(filename))
                {
                    openFileDialog1.InitialDirectory = FileManager.GetFullPath(filename);
                }

                // Call the ShowDialog method to show the dialog box.
                if (openFileDialog1.ShowDialog() == true)
                {
                    return openFileDialog1.FileName;
                }
                return string.Empty;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("SelectFile:exception:" + e.ToString());
                return string.Empty;
            }
            finally
            {
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            }
        }

        /// <summary>
        /// Selects the file.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <returns>System.String.</returns>
        private string SelectFolder(string folder)
        {
            try
            {
                // Create an instance of the open file dialog box.
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "Open folder";
                    dialog.ShowNewFolderButton = true;
                    dialog.RootFolder = Environment.SpecialFolder.MyComputer;
                    dialog.SelectedPath = folder;
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        return dialog.SelectedPath;
                    }
                }

                return string.Empty;
            }
            catch (Exception e)
            {
                _cdfMonitor.LogOutput("SelectFolder:exception:" + e.ToString());
                return string.Empty;
            }
            finally
            {
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            }
        }

        /// <summary>
        /// Sets the configuration.
        /// </summary>
        /// <param name="titlePrefix">The title prefix.</param>
        private void SetActivity(string titlePrefix = "")
        {
            ConfigureRemoteActivity(false);
            ClearStatus();
            _cdfMonitor.LogOutput("DEBUG: SetActivity:enter");

            switch (_cdfMonitor.Config.Activity)
            {
                case Config.Configuration.ActivityType.TraceToEtl:
                    if (!(bool)radioButtonActivityCaptureTrace.IsChecked)
                    {
                        radioButtonActivityCaptureTrace.IsChecked = true;
                        radioButtonActivityCaptureTrace_Click();
                    }

                    break;

                case Config.Configuration.ActivityType.TraceToCsv:
                    if (!(bool)radioButtonActivityCaptureTraceMessage.IsChecked)
                    {
                        radioButtonActivityCaptureTraceMessage.IsChecked = true;
                        radioButtonActivityCaptureTraceMessage_Click();
                    }

                    break;

                case Config.Configuration.ActivityType.RegexParseToCsv:
                    if (!(bool)radioButtonActivityParseTraceMessage.IsChecked)
                    {
                        radioButtonActivityParseTraceMessage.IsChecked = true;
                        radioButtonActivityParseTraceMessage_Click();
                    }

                    break;

                case Config.Configuration.ActivityType.Remote:
                    if (!(bool)radioButtonActivityRemoteActivities.IsChecked)
                    {
                        radioButtonActivityRemoteActivities.IsChecked = true;
                        radioButtonActivityRemoteActivities_Click();
                    }

                    break;

                case Configuration.ActivityType.Server:
                    if (!(bool)radioButtonActivityCaptureNetworkTraceMessage.IsChecked)
                    {
                        radioButtonActivityCaptureNetworkTraceMessage.IsChecked = true;
                        radioButtonActivityCaptureNetworkTraceMessage_Click();
                    }

                    break;

                case Configuration.ActivityType.Unknown:
                default:
                    break;
            }

            // Reset current modulelist cache
            ModulesLoadList(_cdfMonitor.Config.ModuleListCurrentConfigCache = _cdfMonitor.Config.ModuleListViewItems);
            SetModuleSource();

            Title = string.Format("{0} {1} {2} {3} {4}", _cdfMonitor.Config.SessionName, _cdfMonitor.Config.AppSettings.Version,
                                  titlePrefix, Configuration.IsAdministrator() ? "(Administrator)" : string.Empty, _cdfMonitor.Config.CurrentConfigFile());
            _cdfMonitor.LogOutput(string.Format("DEBUG: SetActivity:exit:{0}", _cdfMonitor.Config.Activity.ToString()));

            // For tab management for advanced options
            // TODO: make into function for checked event and call from here
            if (_cdfMonitor.Config.AppSettings.AdvancedOptions)
            {
                // Identify optional tabs and gray tabs and make invisible

                Options.Visibility = Visibility.Visible;
                Network.Visibility = Visibility.Visible;
                Notify.Visibility = Visibility.Visible;
                Upload.Visibility = Visibility.Visible;
                Remote.Visibility = Visibility.Visible;
                Match.Visibility = Visibility.Visible;
                Action.Visibility = Visibility.Visible;

                // Remove advanced (server) activities
                radioButtonActivityRemoteActivities.Visibility = Visibility.Visible;
                radioButtonActivityCaptureNetworkTraceMessage.Visibility = Visibility.Visible;
            }
            else
            {
                // Make all tabs visible
                Options.Visibility = Visibility.Hidden;
                Network.Visibility = Visibility.Hidden;
                Notify.Visibility = Visibility.Hidden;
                Upload.Visibility = Visibility.Hidden;
                Remote.Visibility = Visibility.Hidden;
                Match.Visibility = Visibility.Hidden;
                Action.Visibility = Visibility.Hidden;

                // Make all activities visible
                radioButtonActivityRemoteActivities.Visibility = Visibility.Hidden;
                radioButtonActivityCaptureNetworkTraceMessage.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// Sets the module source.
        /// </summary>
        private void SetModuleSource()
        {
            // SetPropertyNotifications(false);
            switch (_cdfMonitor.Config.ModuleSource)
            {
                case Configuration.ModuleSourceType.Configuration:
                    radioButtonModulesConfigFile.IsChecked = true;
                    radioButtonModulesConfigFile_Checked(null, null);
                    break;

                case Configuration.ModuleSourceType.File:
                    radioButtonModulesFile.IsChecked = true;
                    radioButtonModulesFile_Checked(null, null);
                    break;

                case Configuration.ModuleSourceType.LocalMachine:
                    radioButtonModulesLocalMachine.IsChecked = true;
                    radioButtonModulesLocalMachine_Checked(null, null);
                    break;

                case Configuration.ModuleSourceType.RemoteMachine:
                    radioButtonModulesRemoteMachine.IsChecked = true;
                    radioButtonModulesRemoteMachine_Checked(null, null);
                    break;

                case Configuration.ModuleSourceType.Unknown:
                default:
                    _cdfMonitor.Config.ModuleSource = Configuration.ModuleSourceType.Unknown;
                    break;
            }
        }

        /// <summary>
        /// Sets the options local service buttons.
        /// </summary>
        private void SetOptionsLocalServiceButtons()
        {
            gridOptionsOperators.IsEnabled = true;
            buttonOptionInstallService.IsEnabled = false;
            buttonOptionStartService.IsEnabled = false;
            buttonOptionStopService.IsEnabled = false;
            buttonOptionUnInstallService.IsEnabled = false;

            // Check and configure for local service
            bool retVal = false;
            if (retVal = CDFServiceInstaller.ServiceIsInstalled(Properties.Resources.ServiceName))
            {
                switch (CDFServiceInstaller.GetServiceStatus(Properties.Resources.ServiceName))
                {
                    case ServiceState.Starting:
                    case ServiceState.Run:
                        buttonOptionStopService.IsEnabled = true;
                        break;

                    case ServiceState.Stopping:
                    case ServiceState.Stop:
                        buttonOptionStartService.IsEnabled = true;
                        break;

                    case ServiceState.NotFound:
                    case ServiceState.Unknown:
                    default:
                        break;
                }

                buttonOptionUnInstallService.IsEnabled = true;
                _cdfMonitor.LogOutput(string.Format("DEBUG: Service state: {0}", CDFServiceInstaller.GetServiceStatus(Properties.Resources.ServiceName)));

                // See if viewing service config file
                QueryServiceConfig Config = CDFServiceInstaller.GetServiceConfig(Properties.Resources.ServiceName);
                if (Config.lpBinaryPathName == null)
                {
                    return;
                }
                if (String.Compare(Config.lpBinaryPathName.Split()[0] + ".config", FileManager.GetFullPath(_cdfMonitor.Config.CurrentConfigFile()), true) != 0)
                {
                    _cdfMonitor.LogOutput(string.Format("WARNING: Not currently viewing service config file. To view service config file, 'Load' config from:{0}", Config.lpBinaryPathName.Split()[0] + ".config"));
                    _cdfMonitor.LogOutput(string.Format("Viewing service config file: FALSE"));
                }
                else
                {
                    _cdfMonitor.LogOutput(string.Format("Currently viewing service config file: TRUE"));
                }
            }
            else
            {
                buttonOptionInstallService.IsEnabled = true;
                _cdfMonitor.LogOutput("DEBUG: Service state: not installed");
            }
        }

        /// <summary>
        /// Sets PropertyNotifications enablement
        /// </summary>
        /// <param name="enable"></param>
        private void SetPropertyNotifications(bool enable)
        {
            if (enable & !_cdfMonitor.Config.AppSettings.GetPropertyNotifications())
            {
                _cdfMonitor.Config.AppSettings.SetPropertyNotifications(enable);
                _cdfMonitor.Config.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
            }
            else if (!enable & _cdfMonitor.Config.AppSettings.GetPropertyNotifications())
            {
                _cdfMonitor.Config.AppSettings.SetPropertyNotifications(enable);
                _cdfMonitor.Config.AppSettings.PropertyChanged -= AppSettings_PropertyChanged;
            }
            else
            {
                _cdfMonitor.LogOutput("DEBUG: EnablePropertyNotifications:property already:" + enable.ToString());
            }
        }

        /// <summary>
        /// Sets the remote mandatory settings.
        /// </summary>
        private void SetRemoteMandatorySettings()
        {
            // TODO move to configuration verify

            if (_cdfMonitor.Config.AppSettings.RunAs != Configuration.ExecutionOptions.Service.ToString()
                | _cdfMonitor.Config.AppSettings.LogToConsole == true
                | _cdfMonitor.Config.Activity == Configuration.ActivityType.Remote
                | _cdfMonitor.Config.AppSettings.UseCredentials == true
                | _cdfMonitor.Config.AppSettings.UseServiceCredentials == true)
            {
                WriteStatus("WARNING: Updating remote config with mandatory options");
                _cdfMonitor.Config.AppSettings.RunAs = Configuration.ExecutionOptions.Service.ToString();
                _cdfMonitor.Config.AppSettings.LogToConsole = false;
                _cdfMonitor.Config.AppSettings.UseCredentials = false;
                _cdfMonitor.Config.AppSettings.UseServiceCredentials = false;

                _cdfMonitor.Config.WriteConfigFile(_cdfMonitor.Config.CurrentConfigFile());
                _cdfMonitor.Config.ReadConfigFile(_cdfMonitor.Config.CurrentConfigFile());

                if (_cdfMonitor.Config.Activity == Configuration.ActivityType.Remote)
                {
                    _cdfMonitor.Config.Activity = Configuration.ActivityType.Unknown;
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the Start control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            _outputConsoleItems.Clear();
            _cdfMonitor.Config.LoggerQueue.ClearBuffer();
            ConfigureGuiWriter();

            int count = 0;
            while ((_cdfMonitor.Consumer == null || _cdfMonitor.Consumer.Running)
                & _worker.IsBusy
                & count < 30)
            {
                if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000))
                {
                    return;
                }

                WriteStatus(string.Format("DEBUG: Start_Click: Request to enable Start button but a session or background worker is active. Not enabling Start. count:{0}", count), true);

                count++;
            }

            if (count == 30)
            {
                WriteStatus("ERROR: Start_Click: Request to enable Start button but a session or background worker is active. not enabling Start. Returning", true);
                return;
            }

            CDFMonitor.CloseCurrentSessionEvent.Reset();

            // Verify remote settings
            if (_cdfMonitor.Config.Activity == Configuration.ActivityType.Remote && !RadioButtonRemoteActivityVerify(false))
            {
                buttonRemoteVerify_Click(null, null);
                if (!RadioButtonRemoteActivityVerify(false))
                {
                    WriteStatus("Start:Error with remote config. verify remote config settings.", true);
                    return;
                }
            }

            BackgroundStart(sender, e, delegate()
            {
                _cdfMonitor.WriteGuiStatus = false;
                _cdfMonitor.Start();
                _cdfMonitor.WriteGuiStatus = true;
                return false;
            });
        }

        /// <summary>
        /// Handles the Click event of the Stop control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            DisplayLogPaths();
            _cdfMonitor.Stop();
            _worker.CancelAsync();
            CDFMonitor.CloseCurrentSessionEvent.Set();
            ToggleStartStop(true);
        }

        /// <summary>
        /// Handles the SelectionChanged event of the TabControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Controls.SelectionChangedEventArgs" /> instance containing the event data.</param>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as TabControl).SelectedItem == null)
            {
                _tabInitialized = false;
            }
            else if (_tabItem.Name != ((sender as TabControl).SelectedItem as TabItem).Name)
            {
                _tabItem = ((sender as TabControl).SelectedItem as TabItem);
                _tabInitialized = false;
            }
        }

        /// <summary>
        /// Handles the TextChanged event of the textBoxModulesFilter control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="TextChangedEventArgs" /> instance containing the event data.</param>
        private void textBoxModulesFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            _moduleFilterNew = true;
            buttonModuleLoad_Click(sender, e);
        }

        /// <summary>
        /// Handles the TextChanged event of the textBoxOutputFilter control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="KeyEventArgs" /> instance containing the event data.</param>
        private void textBoxOutputFilter_TextChanged(object sender, KeyEventArgs e)
        {
            if ((bool)(sender is TextBox))
            {
                _cdfMonitor.Config.AppSettings.DisplayFilter = (sender as TextBox).Text;
            }

            string newPattern = string.Empty;

            if (_cdfMonitor.Config.IsValidRegexPattern(_cdfMonitor.Config.AppSettings.DisplayFilter))
            {
                if (_outputFilterPatternInverted)
                {
                    newPattern = string.Format(@"^(?:(?!{0}).)*$", _cdfMonitor.Config.AppSettings.DisplayFilter);
                }
                else
                {
                    newPattern = _cdfMonitor.Config.AppSettings.DisplayFilter;
                }

                if (String.Compare(newPattern, _outputFilterPattern, true) != 0)
                {
                    _outputFilterPattern = newPattern;
                    _outputFilterNew = true;
                }
            }
        }

        /// <summary>
        /// Handles the GotFocus event of the textBoxRegexPattern control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void textBoxRegexPattern_GotFocus(object sender, RoutedEventArgs e)
        {
            WriteStatus(Properties.Resources.MatchRegexPattern, true);
        }

        /// <summary>
        /// Handles the GotFocus event of the textBoxRegexTestText control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs" /> instance containing the event data.</param>
        private void textBoxRegexTestText_GotFocus(object sender, RoutedEventArgs e)
        {
            WriteStatus(Properties.Resources.MatchRegexTest, true);

            // Blank out box if it only has same text as it did when initialized
            if (String.Compare(textBoxRegexTestText.Text, Properties.Resources.MatchRegexTestText) == 0)
            {
                textBoxRegexTestText.Text = string.Empty;
            }
        }

        /// <summary>
        /// Handles the LostFocus event of the listBoxRemoteInput control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void textBoxRemoteInput_LostFocus(object sender, RoutedEventArgs e)
        {
            _cdfMonitor.Config.RemoteMachineList = _cdfMonitor.Config.ProcessRemoteMachines((sender as TextBox).Text);
        }

        /// <summary>
        /// Handles the LostFocus event of the textBoxTraceFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void textBoxTraceFileInput_LostFocus(object sender, RoutedEventArgs e)
        {
            // Force update before verifying as right now its happening afterwords
            _cdfMonitor.Config.AppSettings.TraceFileInput = textBoxTraceFileInput.Text;

            VerifySetting(Configuration.ConfigurationProperties.EnumProperties.TraceFileInput);
        }

        /// <summary>
        /// Toggles the start stop.
        /// </summary>
        /// <param name="enableStart">if set to <c>true</c> [enable start].</param>
        private void ToggleStartStop(bool enableStart)
        {
            if (enableStart)
            {
                buttonStart.IsEnabled = true;
                buttonStop.IsEnabled = false;
            }
            else
            {
                buttonStart.IsEnabled = false;
                buttonStop.IsEnabled = true;
            }
        }

        /// <summary>
        /// Update Output Console
        /// </summary>
        /// <param name="wlist"></param>
        private void UpdateOutputConsole(Queue<WriteListObj> wlist)
        {
            // For output display filter
            if (_outputConsoleItemsAll != null)
            {
                while (_cdfMonitor.Config.AppSettings.BufferLines > 0
                    && _outputConsoleItemsAll.Count > (_cdfMonitor.Config.AppSettings.BufferLines + wlist.Count))
                {
                    _outputConsoleItemsAll.Dequeue();
                }
            }

            // Callback to gui to update
            Dispatcher.Invoke(DispatcherPriority.Background, new Action<Queue<WriteListObj>>((list) =>
            {
                bool scroll = list.Count >= 5000 ? true : false;

                try
                {
                    listBoxConsoleOutput.BeginInit();

                    // Remove fifo all over bufferlines
                    while (_cdfMonitor.Config.AppSettings.BufferLines > 0
                        && _outputConsoleItems.Count > (_cdfMonitor.Config.AppSettings.BufferLines + list.Count))
                    {
                        _outputConsoleItems.Dequeue();
                    }

                    while (list.Count > 0)
                    {
                        WriteListObj writeListObj = list.Dequeue();
                        TextBlock listboxItem = new TextBlock();
                        listboxItem.Background = System.Windows.Media.Brushes.Black;
                        listboxItem.FontSize = 14.0;
                        listboxItem.FontWeight = FontWeights.Light;
                        listboxItem.TextWrapping = TextWrapping.Wrap;
                        listboxItem.TextTrimming = TextTrimming.CharacterEllipsis;
                        listboxItem.Foreground = writeListObj.color;
                        listboxItem.Text = writeListObj.line;

                        // For output display filter
                        if (_outputConsoleItemsAll == null)
                        {
                            // Not using display filter so only add to output.
                            _outputConsoleItems.Enqueue(listboxItem);
                        }
                        else
                        {
                            // Using display filter so only add to output if match, but add to full cache regardless
                            if (Regex.IsMatch(listboxItem.Text, _outputFilterPattern, RegexOptions.IgnoreCase))
                            {
                                _outputConsoleItems.Enqueue(listboxItem);
                            }

                            _outputConsoleItemsAll.Enqueue(listboxItem);
                        }
                    }

                    listBoxConsoleOutput.EndInit();

                    // 15000 tps vs 11000 tps on small 10mb trace big.etl 8000 vs 7000 tps
                    if (scroll)
                    {
                        ListBoxScrollIntoView(false);
                    }
                }
                catch (Exception ex)
                {
                    // Debug.Print("ListboxConsoleUpdateAction:exception:" + ex.ToString());
                }
            }), wlist);
        }

        /// <summary>
        /// Handles the TextChanged event of the textBoxTraceFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Controls.TextChangedEventArgs" /> instance containing the event data.</param>
        private void Upload_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_tabInitialized)
            {
                _cdfMonitor.LogOutput("DEBUG: Upload_GotFocus:enter");
                WriteStatus(Properties.Resources.UploadGotFocus, true);
                _tabInitialized = true;
            }
        }

        /// <summary>
        /// Checks deploy path
        /// </summary>
        private bool VerifyRemoteActivityConfig()
        {
            if (!RadioButtonRemoteActivityVerify(false))
            {
                if (MessageBox.Show(string.Format("{0} does not appear to be configured or configured correctly. Select Yes to setup / repair.",
                    Enum.GetName(typeof(RemoteOperations.RemoteOperationMethods), _cdfMonitor.Config.RemoteActivity)), "Setup / Repair", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    if (RadioButtonRemoteActivityVerify(true))
                    {
                        return true;
                    }
                    else
                    {
                        buttonEditRemoteConfig.Focus();
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Verifies the setting.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="repair">if set to <c>true</c> [repair].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool VerifySetting(Configuration.ConfigurationProperties.EnumProperties property, bool repair = false)
        {
            return _cdfMonitor.Config.Verify.VerifySetting(property.ToString(), repair);
        }

        /// <summary>
        /// Handles the Closing event of the Window control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CancelEventArgs" /> instance containing the event data.</param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            ConsoleManager.Dispose();
        }

        /// <summary>
        /// Writes the status.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="reset">if set to <c>true</c> [reset].</param>
        private void WriteStatus(string data, bool reset = false, string hyperlink = null)
        {
            try
            {
                Status.Dispatcher.Invoke(new Action<bool>((isReset) =>
                {
                    try
                    {
                        // Check for file paths and hyperlink
                        Hyperlink hLink = new Hyperlink();
                        MatchCollection mc = Regex.Matches(data,
                                    @"(\\\\|\\\\\\\\|[c-zC-Z]|file|http|https|ftp)\:(/{1,5}|\\{1,2})[\\/:a-zA-Z0-9\-\.]+");

                        if (!string.IsNullOrEmpty(hyperlink))
                        {
                            hLink.Inlines.Add(hyperlink + "\n");
                            hLink.NavigateUri = new Uri(hyperlink);
                            hLink.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler(WriteStatusRequestNavigate);
                        }

                        // TODO: needs to be reworked to insert hyperlinks back in correct location regardless of location in string.
                        // Right now they get appended at end of block out of order.
                        // Currently works best when links are at end of data string.
                        // So for now limit to one match, else do not modify.

                        //else if (mc.Count > 0)
                        else if (mc.Count == 1)
                        {
                            foreach (Match m in mc)
                            {
                                hLink.Inlines.Add(m.Value);
                                hLink.NavigateUri = new Uri(m.Value);
                                hLink.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler(WriteStatusRequestNavigate);
                                data = data.Remove(data.IndexOf(m.Value), m.Value.Length);
                            }
                        }

                        FlowDocument mcFlowDoc = new FlowDocument();

                        if (isReset)
                        {
                            _statusBoxParagraph = new Paragraph();
                        }

                        if (!string.IsNullOrEmpty(data))
                        {
                            _statusBoxParagraph.Inlines.Add(new Run(data));
                        }

                        if (hLink.NavigateUri != null)
                        {
                            _statusBoxParagraph.Inlines.Add(hLink);
                        }

                        if (!string.IsNullOrEmpty(data))
                        {
                            _statusBoxParagraph.Inlines.Add(new Run("\n"));
                        }

                        mcFlowDoc.Blocks.Add(_statusBoxParagraph);
                        Status.Document = mcFlowDoc;
                        Status.ScrollToEnd();
                    }
                    catch (Exception e)
                    {
                        Debug.Print("DEBUG: WriteStatus:exception:" + e.ToString());
                    }
                }), reset);
            }
            catch
            {
                Debug.Print("WriteStatus:missed message:" + data);
            }
        }

        /// <summary>
        /// Launches hyperlink when selected from status box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WriteStatusRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                _cdfMonitor.LogOutput("DEBUG: WriteStatusRequestNavigate:Navigate");
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                e.Handled = true;
            }
            catch (Exception ex)
            {
                _cdfMonitor.LogOutput("WriteStatusRequestNavigate:exception:" + ex.ToString());
            }
        }

        #endregion Private Methods

        #region Public Structs

        /// <summary>
        /// Class WriteListObj
        /// </summary>
        public struct WriteListObj
        {
            #region Public Fields

            /// <summary>
            /// The color
            /// </summary>
            public Brush color; // = Brushes.White;

            /// <summary>
            /// The line
            /// </summary>
            public string line;

            #endregion Public Fields

            // = string.Empty;

            /// <summary>
            /// The filtered
            /// </summary>
            //  public bool filtered;
        }

        #endregion Public Structs
    }
}