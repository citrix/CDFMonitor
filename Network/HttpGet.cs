// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="HttpGet.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.Network
{
    using CDFM.Config;
    using CDFM.Engine;
    using System;
    using System.IO;
    using System.Net;
    using System.Text;

    /// <summary>
    /// Class HttpGet
    /// </summary>
    internal class HttpGet
    {
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
        /// Gets the request.
        /// </summary>
        /// <param name="fileRequest">The file request.</param>
        /// <param name="userCredentials">The user credentials.</param>
        /// <param name="onlyCheckExistence">if set to <c>true</c> [only check existence].</param>
        /// <returns>System.String.</returns>
        public string GetRequest(string fileRequest, NetworkCredential userCredentials = null, bool onlyCheckExistence = false)
        {
            try
            {
                Status = "OK";

                // used to build entire input
                var sb = new StringBuilder();

                // used on each read operation
                var buf = new byte[8192];

                // prepare the web page we will be asking for
                var request = (HttpWebRequest)WebRequest.Create(fileRequest);
                request.Method = "GET";
                request.KeepAlive = true;
                request.Accept = "*/*";

                if (userCredentials != null)
                {
                    if ((userCredentials is ResourceCredential))
                    {
                        // problem passing DefaultNetworkCredentials as ResourceCredential so dont
                        // use from cache and use new one.
                        userCredentials = (userCredentials as ResourceCredential).DefaultCredential ? CredentialCache.DefaultNetworkCredentials : userCredentials;
                    }

                    var cc = new CredentialCache();
                    cc.Add(new Uri(fileRequest),
                           "NTLM",

                           userCredentials);
                    request.Credentials = cc;
                }

                // execute the request
                var response = (HttpWebResponse)
                               request.GetResponse();

                if (!onlyCheckExistence)
                {
                    // we will read data via the response stream
                    Stream resStream = response.GetResponseStream();

                    string tempString = null;
                    int count = 0;

                    do
                    {
                        // fill the buffer with data
                        count = resStream.Read(buf, 0, buf.Length);

                        // make sure we read some data
                        if (count != 0)
                        {
                            // translate from bytes to ASCII text
                            tempString = Encoding.ASCII.GetString(buf, 0, count);

                            // continue building the string
                            sb.Append(tempString);
                        }
                    } while (count > 0); // Any more data to read?
                }

                // print out page path
                return (sb.ToString());
            }
            catch (Exception e)
            {
                Status += e.ToString();
                CDFMonitor.LogOutputHandler("DEBUG:GetRequest:exception:" + e.ToString());
                return string.Empty;
            }
        }

        #endregion Public Methods
    }
}