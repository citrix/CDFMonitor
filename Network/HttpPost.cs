// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="HttpPost.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Network
{
    using CDFM.Engine;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;

    /// <summary>
    /// Class AsynchronousHttpUpLoader
    /// </summary>
    public class AsynchronousHttpUpLoader
    {
        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AsynchronousHttpUpLoader" /> class.
        /// </summary>
        /// <param name="uritarget">The uritarget.</param>
        /// <param name="Httpuser">The httpuser.</param>
        /// <param name="Httppassword">The httppassword.</param>
        /// <param name="Httpfilename">The httpfilename.</param>
        public AsynchronousHttpUpLoader(string uritarget, string Httpuser, string Httppassword, string Httpfilename)
        {
            try
            {
                Status = "OK";

                // Create a Uri instance with the specified URI string. If the URI is not correctly
                // formed, the Uri constructor will throw an exception.
                ManualResetEvent waitObject;

                var target = new Uri(uritarget);

                // SSL?
                ServicePointManager.ServerCertificateValidationCallback = AcceptAllCertifications;
                string fileName = Httpfilename;
                var state = new HttpState();
                var request = (HttpWebRequest)WebRequest.Create(target);
                request.Method = WebRequestMethods.Http.Put;
                request.Credentials = new NetworkCredential(Httpuser, Httppassword);

                if (string.IsNullOrEmpty(Httpfilename))
                {
                    try
                    {
                        request.Method = WebRequestMethods.Http.Post;
                        request.ContentLength = 1;
                        request.Timeout = 1000;
                        request.GetRequestStream();
                        CDFMonitor.LogOutputHandler(Status = string.Format("AsyncHTTP:Success checking access to HTTP"));
                        return;
                    }
                    catch (Exception e)
                    {
                        CDFMonitor.LogOutputHandler(Status = string.Format("AsyncHTTP:Failure checking access to HTTP - {0}", e));
                        return;
                    }
                }

                request.SendChunked = true;

                //request.Headers.Add("Translate: f");
                request.AllowWriteStreamBuffering = true;
                request.ContentLength = (new FileInfo(Httpfilename).Length);

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
                waitObject.WaitOne();

                // The operations either completed or threw an exception.
                if (state.OperationException != null)
                {
                    throw state.OperationException;
                }
                else
                {
                    Status = string.Format("The operation completed - {0}", state.StatusDescription);
                }
            }
            catch (Exception e)
            {
                Status = string.Format("Failure sending file to Http - {0}", e);
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

        #region Public Methods

        /// <summary>
        /// Accepts all certifications.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="certification">The certification.</param>
        /// <param name="chain">The chain.</param>
        /// <param name="sslPolicyErrors">The SSL policy errors.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool AcceptAllCertifications(object sender, X509Certificate certification, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        #endregion Public Methods

        #region Private Methods

        // The EndGetResponseCallback method
        // completes a call to BeginGetResponse.
        /// <summary>
        /// Ends the get response callback.
        /// </summary>
        /// <param name="ar">The ar.</param>
        private void EndGetResponseCallback(IAsyncResult ar)
        {
            var state = (HttpState)ar.AsyncState;
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)state.Request.EndGetResponse(ar);
                response.Close();
                state.StatusDescription = response.StatusDescription;

                // Signal the main application thread that the operation is complete.
                state.OperationComplete.Set();
            }

            // Return exceptions to the main application thread.
            catch (Exception e)
            {
                Status = "Error getting response.";
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
            var state = (HttpState)ar.AsyncState;

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

                Status = string.Format("Writing {0} bytes to the stream.", count);

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
                Status = "Could not get the request stream.";
                state.OperationException = e;
                state.OperationComplete.Set();
                return;
            }
        }

        #endregion Private Methods
    }

    /// <summary>
    /// Class HttpState
    /// </summary>
    public class HttpState : IDisposable
    {
        #region Private Fields

        /// <summary>
        /// The wait
        /// </summary>
        private readonly ManualResetEvent wait;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpState" /> class.
        /// </summary>
        public HttpState()
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
        public HttpWebRequest Request
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