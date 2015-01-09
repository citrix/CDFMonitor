// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="FTP.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Network
{
    using CDFM.Engine;
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;

    /// <summary>
    /// Class AsynchronousFtpUpLoader
    /// </summary>
    public class AsynchronousFtpUpLoader
    {
        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AsynchronousFtpUpLoader" /> class.
        /// </summary>
        /// <param name="uritarget">The uritarget.</param>
        /// <param name="ftpuser">The ftpuser.</param>
        /// <param name="ftppassword">The ftppassword.</param>
        /// <param name="ftpfilename">The ftpfilename.</param>
        public AsynchronousFtpUpLoader(string uritarget, string ftpuser, string ftppassword, string ftpfilename)
        {
            var state = new FtpState();
            try
            {
                Status = "OK";

                // Create a Uri instance with the specified URI string. If the URI is not correctly
                // formed, the Uri constructor will throw an exception.
                ManualResetEvent waitObject;

                var target = new Uri(uritarget);
                string fileName = ftpfilename;

                var request = (FtpWebRequest)WebRequest.Create(target);

                request.Credentials = new NetworkCredential(ftpuser, ftppassword);

                // If fileName empty , just try to getresponsestream to see if uri and creds are working
                if (string.IsNullOrEmpty(ftpfilename))
                {
                    try
                    {
                        request.Method = WebRequestMethods.Ftp.ListDirectory;
                        request.GetResponse();
                        CDFMonitor.LogOutputHandler(Status = string.Format("AsyncFTP:Success checking access to FTP"));
                        return;
                    }
                    catch (Exception e)
                    {
                        CDFMonitor.LogOutputHandler(Status = string.Format("AsyncFTP:Failure checking access to FTP - {0}", e));
                        return;
                    }
                }

                request.Method = WebRequestMethods.Ftp.UploadFile;

                // Store the request in the object that we pass into the asynchronous operations.
                state.Request = request;
                state.FileName = fileName;

                // Get the event to wait on.
                waitObject = state.OperationComplete;

                // Asynchronously get the stream for the file contents.
                request.BeginGetRequestStream(
                    EndGetStreamCallback,
                    state
                    );

                // Block the current thread until all operations are complete.
                CDFMonitor.LogOutputHandler("AsyncFTP:waiting for upload completion");
                waitObject.WaitOne();
                CDFMonitor.LogOutputHandler("AsyncFTP:upload returned.");

                // The operations either completed or threw an exception.
                if (state.OperationException != null)
                {
                    CDFMonitor.LogOutputHandler("AsyncFTP:operation exception.");
                    throw state.OperationException;
                }
                else
                {
                    //Console.WriteLine("The operation completed - {0}", state.StatusDescription);
                    Status = string.Format("AsyncFTP:The operation completed - {0}", state.StatusDescription);
                    CDFMonitor.LogOutputHandler(Status);
                }
            }
            catch (Exception e)
            {
                Status = string.Format("AsyncFTP:Failure sending file to FTP - {0}", e);
                CDFMonitor.LogOutputHandler(Status);
            }
            finally
            {
                state.Dispose();
            }
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>The status.</value>
        public string Status
        {
            get;
            set;
        }

        #endregion Public Properties

        #region Private Methods

        // The EndGetResponseCallback method
        // completes a call to BeginGetResponse.
        /// <summary>
        /// Ends the get response callback.
        /// </summary>
        /// <param name="ar">The ar.</param>
        private void EndGetResponseCallback(IAsyncResult ar)
        {
            Status = "AsyncFTP:getting response.";
            CDFMonitor.LogOutputHandler(Status);
            var state = (FtpState)ar.AsyncState;
            FtpWebResponse response = null;
            try
            {
                response = (FtpWebResponse)state.Request.EndGetResponse(ar);
                response.Close();
                state.StatusDescription = response.StatusDescription;

                // Signal the main application thread that the operation is complete.
                state.OperationComplete.Set();
            }

            // Return exceptions to the main application thread.
            catch (Exception e)
            {
                //Console.WriteLine("Error getting response.");
                Status = "AsyncFTP:Error getting response.";
                CDFMonitor.LogOutputHandler(Status);
                state.OperationException = e;
                state.OperationComplete.Set();
            }
        }

        /// <summary>
        /// Ends the get stream callback.
        /// </summary>
        /// <param name="ar">The ar.</param>
        private void EndGetStreamCallback(IAsyncResult ar)
        {
            var state = (FtpState)ar.AsyncState;

            Stream requestStream = null;

            // End the asynchronous call to get the request stream.
            try
            {
                requestStream = state.Request.EndGetRequestStream(ar);

                // Copy the file contents to the request stream.
                const int bufferLength = 2048;
                var buffer = new byte[bufferLength];
                int count = 0;
                int readBytes = 0;
                FileStream stream = File.OpenRead(state.FileName);
                do
                {
                    readBytes = stream.Read(buffer, 0, bufferLength);
                    requestStream.Write(buffer, 0, readBytes);
                    count += readBytes;
                } while (readBytes != 0);

                Status = string.Format("AsyncFTP:Writing {0} bytes to the stream.", count);
                CDFMonitor.LogOutputHandler(Status);

                // IMPORTANT: Close the request stream before sending the request.
                requestStream.Close();

                // Asynchronously get the response to the upload request.
                state.Request.BeginGetResponse(
                    EndGetResponseCallback,
                    state
                    );
            }

            // Return exceptions to the main application thread.
            catch (Exception e)
            {
                //Console.WriteLine("Could not get the request stream.");
                Status = "AsyncFTP:Could not get the request stream.";
                CDFMonitor.LogOutputHandler(Status);
                state.OperationException = e;
                state.OperationComplete.Set();
                return;
            }
        }

        #endregion Private Methods
    }

    /// <summary>
    /// Class FtpState
    /// </summary>
    public class FtpState : IDisposable
    {
        #region Private Fields

        /// <summary>
        /// The wait
        /// </summary>
        private readonly ManualResetEvent wait;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpState" /> class.
        /// </summary>
        public FtpState()
        {
            wait = new ManualResetEvent(false);
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// Gets or sets the name of the file.
        /// </summary>
        /// <value>The name of the file.</value>
        public string FileName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the operation complete.
        /// </summary>
        /// <value>The operation complete.</value>
        public ManualResetEvent OperationComplete
        {
            get { return wait; }
        }

        /// <summary>
        /// Gets or sets the operation exception.
        /// </summary>
        /// <value>The operation exception.</value>
        public Exception OperationException
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the request.
        /// </summary>
        /// <value>The request.</value>
        public FtpWebRequest Request
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the status description.
        /// </summary>
        /// <value>The status description.</value>
        public string StatusDescription
        {
            get;
            set;
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting
        /// unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion Public Methods

        #region Protected Methods

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
        /// <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                wait.Close();
            }
        }

        #endregion Protected Methods
    }
}