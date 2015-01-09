// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="ServiceCDFMonitor.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************
namespace CDFM.Service
{
    using System.Diagnostics;
    using System.ServiceProcess;
    using System.Threading;

    using CDFM.Engine;

    /// <summary>
    /// Class ServiceCDFMonitor
    /// </summary>
    partial class ServiceCDFMonitor : ServiceBase
    {

        #region Private Fields

        /// <summary>
        /// The _CDFT
        /// </summary>
        private CDFMonitor _cdft;

        /// <summary>
        /// The _thread
        /// </summary>
        private Thread _thread;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceCDFMonitor"/> class.
        /// </summary>
        /// <param name="cdft">The CDFT.</param>
        public ServiceCDFMonitor(CDFMonitor cdft)
        {
            _cdft = cdft;
            InitializeComponent();
        }

        #endregion Public Constructors

        #region Protected Methods

        /// <summary>
        /// When implemented in a derived class, executes when a Start command is sent to the
        /// service by the Service Control Manager (SCM) or when the operating system starts (for a
        /// service that starts automatically). Specifies actions to take when the service starts.
        /// </summary>
        /// <param name="args">Data passed by the start command.</param>
        protected override void OnStart(string[] args)
        {
            ThreadStart ts = new ThreadStart(_cdft.ServiceStart);

            // create the worker thread
            _thread = new Thread(ts);

            // go ahead and start the worker thread
            _thread.Start();

            // call the base class so it has a chance to perform any work it needs to
            base.OnStart(args);
        }

        /// <summary>
        /// When implemented in a derived class, executes when a Stop command is sent to the service
        /// by the Service Control Manager (SCM). Specifies actions to take when a service stops
        /// running.
        /// </summary>
        protected override void OnStop()
        {
            // give shutdown process time to run if running
            for (int i = 0; i <= 120; i++)
            {
                if (_cdft.ProcessRunning == 0 | CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000))
                {
                    //if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(1000))
                    //{
                    break;

                    //}
                }
                else
                {
                    RequestAdditionalTime(1000);
                }
            }

            Debug.Print("OnStop:requesting 2 minutes shutdown from service.");
            RequestAdditionalTime(120000);
            Debug.Print("OnStop:setting sesion close event.");
            _cdft.WndMsgHandler(CDFMonitor.CtrlType.CTRL_CLOSE_EVENT);
            Debug.Print("OnStop:joining start thread.");
            _thread.Join(120000);
            Debug.Print("OnStop:calling base OnStop().");
            base.OnStop();
        }

        #endregion Protected Methods

    }
}