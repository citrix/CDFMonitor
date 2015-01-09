// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="PackageWrite.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************

//-----------------------------------------------------------------------------
// <copyright file="PackageWrite.cs" company="Microsoft">
//   Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright>
//
// Description:
//   PackageWrite shows how to write a Package zip file
//   containing content, resource, and relationship parts.
//-----------------------------------------------------------------------------

namespace CDFM.Engine
{
    using CDFM.FileManagement;
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.IO.Packaging;
    using System.Linq;

    /// <summary>
    /// Class PackageManager
    /// </summary>
    internal class PackageManager
    {
        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageManager" /> class.
        /// </summary>
        /// <param name="files">The files.</param>
        /// <param name="path">The path.</param>
        public PackageManager()
        {
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

        /// <summary>
        /// Gets or sets the zip file.
        /// </summary>
        /// <value>The zip file.</value>
        public string ZipFile
        {
            get;
            set;
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Creates a package zip file containing specified content and resource files.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool CreatePackage(string[] files, string path = null)
        {
            // todo: redo to match DecompressStream or wait for .net45 min and use new ZipFile Class
            try
            {
                DateTime dt = DateTime.Now;
                string[] resourcePaths = files.Distinct().ToArray();

                string packagePath = string.Empty;

                if (path != null)
                {
                    packagePath = path + "\\";
                }

                packagePath += string.Format("CDFMonitor-{0}.zip", dt.ToString("yyyy-MM-dd--HH-mm-ss"));
                bool filesExist = false;

                // Create the Package
                using (Package package = Package.Open(packagePath, FileMode.Create))
                {
                    string[] tempArray = new string[resourcePaths.Length];
                    Array.Copy(resourcePaths, tempArray, resourcePaths.Length);
                    foreach (string resourcePath in tempArray)
                    {
                        CDFMonitor.LogOutputHandler("CreatePackage:processing resource:" + resourcePath);

                        Uri relativePath = new Uri("/" + Path.GetFileName(resourcePath), UriKind.Relative);
                        if (!File.Exists(resourcePath))
                        {
                            continue;
                        }

                        filesExist = true;

                        PackagePart packagePartResource =
                            package.CreatePart(relativePath,
                                               "text/plain", CompressionOption.Maximum);

                        // Copy the data to the Resource Part
                        using (FileStream fileStream = new FileStream(
                            resourcePath, FileMode.Open, FileAccess.Read))
                        {
                            CopyStream(fileStream, packagePartResource.GetStream());
                        }
                    }
                }

                if (!filesExist)
                {
                    File.Delete(packagePath);
                    CDFMonitor.LogOutputHandler("CreatePackage:0 files to compress");
                    Status = "0 files to compress";
                    return false;
                }

                if (File.Exists(packagePath))
                {
                    ZipFile = packagePath;
                    CDFMonitor.LogOutputHandler("CreatePackage:package created:" + ZipFile);
                    Status = ZipFile;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Status = e.ToString();
                CDFMonitor.LogOutputHandler("CreatePackage:exception:" + Status);
                return false;
            }
        }

        /// <summary>
        /// Reads a package zip file containing specified content and resource files.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool ExtractPackage(string packageName, string outputFolder, bool force = false)
        {
            CDFMonitor.LogOutputHandler("ReadPackage:processing package:" + packageName);

            try
            {
                if (!FileManager.FileExists(packageName))
                {
                    return false;
                }

                // create subfolder for package files based on package name
                outputFolder = string.Format("{0}\\{1}", outputFolder, Path.GetFileNameWithoutExtension(packageName));

                if (FileManager.CheckPath(outputFolder))
                {
                    if (force)
                    {
                        FileManager.DeleteFolder(outputFolder);
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("ReadPackage:output folder exists and no force. returning:" + packageName);
                        return false;
                    }
                }

                if (!FileManager.CheckPath(outputFolder, true))
                {
                    return false;
                }

                // Read the Package
                using (ZipPackage package = (ZipPackage)ZipPackage.Open(packageName, FileMode.Open))
                {
                    CDFMonitor.LogOutputHandler("ReadPackage:package open. retrieving parts");
                    foreach (PackagePart pP in package.GetParts())
                    {
                        DecompressStream(pP.GetStream(FileMode.Open, FileAccess.Read),
                             string.Format("{0}\\{1}", outputFolder, pP.Uri.ToString().TrimStart('/')));
                    }
                }

                if (Directory.GetFiles(outputFolder).Length > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Status = e.ToString();
                CDFMonitor.LogOutputHandler("ReadPackage:exception:" + Status);
                return false;
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Copies data from a path stream to a target stream.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The source stream to copyQ to.</param>
        private void CopyStream(Stream source, Stream target)
        {
            CDFMonitor.LogOutputHandler("Debug:CopyStream:enter");
            byte[] buf = new byte[0x1000];
            int bytesRead = 0;

            while ((bytesRead = source.Read(buf, 0, buf.Length)) > 0)
            {
                target.Write(buf, 0, bytesRead);
            }
            CDFMonitor.LogOutputHandler("Debug:CopyStream:exit");
        }

        private long DecompressStream(Stream source, string fileTarget)
        {
            Stream target = null;
            byte[] buf = new byte[0x1000];
            long nBytes = 0;

            CDFMonitor.LogOutputHandler("Debug:DecompressStream:enter");

            if (FileManager.FileExists(fileTarget))
            {
                FileManager.DeleteFile(fileTarget);
            }

            try
            {
                target = File.OpenWrite(fileTarget);

                using (DeflateStream dSource = new DeflateStream(source, CompressionMode.Decompress))
                {
                    int len;
                    while ((len = dSource.BaseStream.Read(buf, 0, buf.Length)) > 0)
                    {
                        target.Write(buf, 0, len);
                        nBytes += len;
                    }
                }

                return nBytes;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DecompressStream:exception:" + e.ToString());
                return 0;
            }
            finally
            {
                target.Close();
                CDFMonitor.LogOutputHandler("Debug:DecompressStream:exit:" + nBytes.ToString());
            }
        }

        #endregion Private Methods
    }
}