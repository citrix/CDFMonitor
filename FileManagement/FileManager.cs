// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="FileManager.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************
namespace CDFM.FileManagement
{
    using CDFM.Config;
    using CDFM.Engine;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Class FileManager
    /// </summary>
    public class FileManager
    {
        #region Public Fields

        public const int LOG_FILE_MAX_COUNT = 9999;

        #endregion Public Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FileManager" /> class.
        /// </summary>
        public FileManager()
        {
        }

        #endregion Public Constructors

        #region enums

        #region Public Enums

        public enum Results
        {
            Unknown,
            Fail,
            Success,
            SuccessWithErrors
        }

        #endregion Public Enums

        #region Public Methods

        /// <summary>
        /// Checks the path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="create">if set to <c>true</c> [create].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool CheckPath(string path, bool create = false)
        {
            CDFMonitor.LogOutputHandler(string.Format("DEBUG:CheckPath: enter:{0}:{1}", path, create));

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    CDFMonitor.LogOutputHandler("CheckPath: empty path. returning false.");
                    return false;
                }

                path = FileManager.GetFullPath(path);

                ResourceManagement rm = new ResourceManagement();
                ResourceManagement.ResourceType rt = rm.GetPathType(path);
                if (rm.CheckResourceCredentials(path) != ResourceManagement.CommandResults.Successful)
                {
                    // 131022 if multiple dirs in path do not exist above will fail.
                    if (!create | rm.DeterminePathObj(path) != ResourceManagement.DeterminePathObjType.Directory)
                    {
                        CDFMonitor.LogOutputHandler("CheckPath: checkresourcecredentials failed. returning false.");
                        return false;
                    }
                }

                if (rt == ResourceManagement.ResourceType.Unc
                    && rm.CheckUncPath(path, create))
                {
                    CDFMonitor.LogOutputHandler("CheckPath: able to access unc. returning true.");
                    return true;
                }

                if (rt == ResourceManagement.ResourceType.Url)
                {
                    return true;
                }

                ResourceManagement.DeterminePathObjType dt = rm.DeterminePathObj(path);

                if (dt == ResourceManagement.DeterminePathObjType.Directory)
                {
                    if (Directory.Exists(path))
                    {
                        return true;
                    }

                    if (create)
                    {
                        Directory.CreateDirectory(path);
                        // reset creds in case they failed on non-existent dir above
                        if (rm.CheckResourceCredentials(path, true) != ResourceManagement.CommandResults.Successful)
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }

                    CDFMonitor.LogOutputHandler("DEBUG:CheckPath: directory doesnt exist and not configured to create. returning false:" + path);
                    return false;
                }

                if (dt == ResourceManagement.DeterminePathObjType.File)
                {
                    if (File.Exists(path) | Directory.Exists(Path.GetDirectoryName(path)))
                    {
                        return true;
                    }

                    if (create)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        return true;
                    }

                    CDFMonitor.LogOutputHandler("CheckPath: file doesnt exist and directory cannot be created. returning false.");
                    return false;
                }

                CDFMonitor.LogOutputHandler(string.Format("CheckPath: unknown object:{0}", dt));
                return false;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("CheckPath: exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Copies the file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="destinationPath">The destination path.</param>
        /// <param name="createDestination">if set to <c>true</c> [create destination].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool CopyFile(string file, string destinationPath, bool createDestination = false)
        {
            return CopyFiles(new string[] { file }, destinationPath, createDestination);
        }

        /// <summary>
        /// Copies the files.
        /// </summary>
        /// <param name="files">The files.</param>
        /// <param name="destinationPath">The destination path.</param>
        /// <param name="createDestination">if set to <c>true</c> [create destination].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool CopyFiles(string[] files, string destinationPath, bool createDestination = false)
        {
            string sourcePathRoot = string.Empty;
            string destPathRoot = destinationPath.TrimEnd('\\');
            string destFile = string.Empty;
            bool retval = true;

            try
            {
                CDFMonitor.LogOutputHandler(string.Format("DEBUG:CopyFiles:enter"));

                ResourceManagement rm = new ResourceManagement();

                if (rm.CheckResourceCredentials(destinationPath, false) != ResourceManagement.CommandResults.Successful)
                {
                    CDFMonitor.LogOutputHandler(string.Format("Error:CopyFiles:invalid credential return:{0}:{1}", rm, destinationPath));
                    return false;
                }
                else if (!Directory.Exists(destinationPath))
                {
                    if (!createDestination)
                    {
                        CDFMonitor.LogOutputHandler("CopyFiles:destination does not exist and create = false:" + destinationPath);
                        return false;
                    }

                    CDFMonitor.LogOutputHandler(string.Format("CopyFiles:creating directory:{0}", destinationPath));
                    Directory.CreateDirectory(destinationPath);
                }

                foreach (string file in files)
                {
                    string sourceFile = GetFullPath(file);

                    // populate sourcePathRoot if empty to avoid errors below
                    if (string.IsNullOrEmpty(sourcePathRoot))
                    {
                        sourcePathRoot = Path.GetDirectoryName(sourceFile);
                    }

                    CDFMonitor.LogOutputHandler(string.Format("CopyFiles:copying file:{0} : {1}", sourceFile, destinationPath));
                    if (rm.GetPathType(sourceFile) == ResourceManagement.ResourceType.Unc
                        && rm.CheckResourceCredentials(sourceFile) == ResourceManagement.CommandResults.Successful)
                    {
                        if (!rm.ConnectUncPath(sourceFile))
                        {
                            retval = false;
                            continue;
                        }
                    }

                    // setup dest path and directory
                    if (String.Compare(sourcePathRoot, Path.GetDirectoryName(sourceFile)) != 0)
                    {
                        string newSourcePath = Path.GetDirectoryName(sourceFile);
                        if (newSourcePath.Contains(sourcePathRoot))
                        {
                            // then its a subdirectory. add dir to dest
                            destFile = sourceFile.Replace(sourcePathRoot, destPathRoot);
                            string newdestPath = Path.GetDirectoryName(destFile);

                            if (!Directory.Exists(newdestPath))
                            {
                                Directory.CreateDirectory(newdestPath);
                            }
                        }
                        else
                        {
                            // new root
                            sourcePathRoot = newSourcePath;
                            destFile = sourceFile.Replace(sourcePathRoot, destPathRoot);
                        }
                    }
                    else
                    {
                        // no change in path
                        destFile = sourceFile.Replace(sourcePathRoot, destPathRoot);
                    }

                    if (!File.Exists(sourceFile))
                    {
                        CDFMonitor.LogOutputHandler(string.Format("Warning:CopyFiles:source file does not exist:{0}", sourceFile));
                        retval = false;
                        continue;
                    }

                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                    }

                    CDFMonitor.LogOutputHandler(string.Format("CopyFiles:copying file source:{0} dest:{1}", sourceFile, destFile));
                    File.Copy(sourceFile, destFile, true);

                    if (rm.GetPathType(sourceFile) == ResourceManagement.ResourceType.Unc)
                    {
                        rm.DisconnectUncPath(destinationPath);
                    }
                }

                if (rm.GetPathType(destinationPath) == ResourceManagement.ResourceType.Unc)
                {
                    rm.DisconnectUncPath(destinationPath);
                }

                return retval;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("CopyFiles:exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Creates the folder.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool CreateFolder(string path)
        {
            return CheckPath(path, true);
        }

        /// <summary>
        /// Deletes the file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="removeAfterDelete">if set to <c>true</c> [remove after delete].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static Results DeleteFile(string file, bool removeParentAfterDelete = true)
        {
            return DeleteFiles(new string[] { file }, removeParentAfterDelete);
        }

        /// <summary>
        /// Deletes the file.
        /// </summary>
        /// <param name="destinationFiles">The destination files.</param>
        /// <param name="removeParentAfterDelete">if set to <c>true</c> [remove after delete].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static Results DeleteFiles(string[] destinationFiles, bool removeParentAfterDelete = true)
        {
            ResourceManagement rm = new ResourceManagement();
            Results results = Results.Success;

            try
            {
                foreach (string destinationFile in destinationFiles.Distinct())
                {
                    string path = Path.GetDirectoryName(destinationFile);

                    if (rm.GetPathType(path) == ResourceManagement.ResourceType.Unc
                        && rm.CheckUncPath(path, false))
                    {
                        if (!rm.ConnectUncPath(path))
                        {
                            return Results.Fail;
                        }
                    }

                    if (!File.Exists(destinationFile))
                    {
                        CDFMonitor.LogOutputHandler("DeleteFile:destination does not exist:" + destinationFile);
                        results = Results.SuccessWithErrors;
                        continue;
                    }

                    if (!FileInUse(destinationFile))
                    {
                        CDFMonitor.LogOutputHandler("DeleteFile: deleting file:" + destinationFile);
                        File.Delete(destinationFile);
                    }
                    else
                    {
                        CDFMonitor.LogOutputHandler("DeleteFile: file in use:" + destinationFile);
                        results = Results.SuccessWithErrors;
                        continue;
                    }

                    // try to remove parent but is just best effort
                    try
                    {
                        if (removeParentAfterDelete
                            && Directory.GetFiles(path).Count() == 0
                            && Directory.GetDirectories(path).Count() == 0)
                        {
                            CDFMonitor.LogOutputHandler("DeleteFile: removing parent:" + path);
                            Directory.Delete(path);
                        }
                    }
                    catch { }

                    if (rm.GetPathType(destinationFile) == ResourceManagement.ResourceType.Unc)
                    {
                        rm.DisconnectUncPath(destinationFile);
                    }
                }

                return results;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DeleteFile:exception:" + e.ToString());
                return Results.Fail;
            }
        }

        /// <summary>
        /// Deletes the folder.
        /// </summary>
        /// <param name="destinationPath">The destination path.</param>
        /// <param name="deleteOnlyIfEmpty">if set to <c>true</c> [delete only if empty].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool DeleteFolder(string destinationPath, bool deleteOnlyIfEmpty = false)
        {
            ResourceManagement rm = new ResourceManagement();

            try
            {
                if (rm.GetPathType(destinationPath) == ResourceManagement.ResourceType.Unc
                   && rm.CheckUncPath(destinationPath, false))
                {
                    if (!rm.ConnectUncPath(Directory.GetParent(destinationPath).FullName))
                    {
                        return false;
                    }
                }
                if (!Directory.Exists(destinationPath))
                {
                    CDFMonitor.LogOutputHandler("DeleteFolder:destination does not exist:" + destinationPath);
                    return false;
                }

                if (deleteOnlyIfEmpty
                    && (Directory.GetFiles(destinationPath).Length != 0
                    || Directory.GetDirectories(destinationPath).Length != 0))
                {
                    CDFMonitor.LogOutputHandler("DeleteFolder:destination is not empty:" + destinationPath);
                    return false;
                }

                CDFMonitor.LogOutputHandler("DeleteFolder: deleting folder:" + destinationPath);
                Directory.Delete(destinationPath, true);
                return true;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DeleteFolder:exception:" + e.ToString());
                return false;
            }
            finally
            {
                if (rm.GetPathType(destinationPath) == ResourceManagement.ResourceType.Unc)
                {
                    rm.DisconnectUncPath(Directory.GetParent(destinationPath).FullName);
                }
            }
        }

        /// <summary>
        /// Files the exists.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool FileExists(string file)
        {
            try
            {
                string tempFilePath = GetFullPath(file);
                if (!string.IsNullOrEmpty(tempFilePath))
                {
                    if (CheckPath(tempFilePath, false))
                    {
                        if (File.Exists(file))
                        {
                            CDFMonitor.LogOutputHandler("DEBUG:FileExists:true:" + file);
                            return true;
                        }
                    }
                }

                CDFMonitor.LogOutputHandler("DEBUG:FileExists:false:" + file);
                return false;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:FileExists:exception:" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// FileInUse checks to see whether file is currently open. Returns true if file is in use
        /// or exception occurs. Returns false if not in use or if file does not exist.
        /// </summary>
        /// <param name="file">string file name and path</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool FileInUse(string file)
        {
            // todo: add creds
            try
            {
                if (FileExists(file))
                {
                    FileStream fs = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    fs.Close();
                    CDFMonitor.LogOutputHandler("DEBUG:File NOT in use: " + file);
                    return false;
                }
                else
                {
                    CDFMonitor.LogOutputHandler("DEBUG:File does not exist: " + file);
                    return false;
                }
            }
            catch //(Exception e)
            {
                CDFMonitor.LogOutputHandler("DEBUG:File in use: " + file);
                return true;
            }
        }

        /// <summary>
        /// Gets the files.
        /// </summary>
        /// <param name="cleanPath">The path. Path can contain wildcard</param>
        /// <param name="wildcard">The wildcard. * default</param>
        /// <param name="subDir">The sub dir.</param>
        /// <returns>System.String[][].</returns>
        public static string[] GetFiles(string path, string wildcard = "*.*", SearchOption subDir = SearchOption.TopDirectoryOnly)
        {
            string cleanPath = path.Trim('"');
            List<string> retList = new List<string>();
            ResourceManagement rm = new ResourceManagement();
            ResourceManagement.DeterminePathObjType dt = new ResourceManagement.DeterminePathObjType();

            // sometimes wildcards in 'path' so override 'wildcard' if so
            if (Regex.IsMatch(cleanPath, @"[^\\]*$"))
            {
                // get path type before modifying dt = rm.DeterminePathObj(GetFullPath(path));

                string tempString = Regex.Match(cleanPath, @"[^\\]*$").Groups[0].Value;
                if (tempString.Contains("*") | tempString.Contains("?"))
                {
                    wildcard = tempString;
                    cleanPath = cleanPath.Replace(string.Format(@"\{0}", tempString), "");
                }
            }

            dt = rm.DeterminePathObj(GetFullPath(cleanPath));

            if (string.IsNullOrEmpty(cleanPath) || string.IsNullOrEmpty(Path.GetDirectoryName(cleanPath)))
            {
                cleanPath = string.Format("{0}\\{1}", Environment.CurrentDirectory, cleanPath);
            }

            if (rm.GetPathType(cleanPath) == ResourceManagement.ResourceType.Unc
               && rm.CheckUncPath(cleanPath, false))
            {
                if (!rm.ConnectUncPath(cleanPath))
                {
                    return new string[0];
                }
            }

            try
            {
                if (dt == ResourceManagement.DeterminePathObjType.Directory
                    && Directory.Exists(cleanPath))
                {
                    try
                    {
                        retList.AddRange(Directory.GetFiles(cleanPath, wildcard, subDir));
                    }
                    catch (Exception e)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("GetFiles:directory exception:{0}", e.ToString()));
                    }
                }
                else if (dt == ResourceManagement.DeterminePathObjType.File
                    && File.Exists(cleanPath))
                {
                    retList.Add(cleanPath);
                }
                else if (dt == ResourceManagement.DeterminePathObjType.WildCard)
                {
                    try
                    {
                        retList.AddRange(Directory.GetFiles(Path.GetDirectoryName(cleanPath), Path.GetFileName(cleanPath), subDir));
                    }
                    catch (Exception e)
                    {
                        CDFMonitor.LogOutputHandler(string.Format("GetFiles:wildcard exception:{0}", e.ToString()));
                    }
                }
                else
                {
                    CDFMonitor.LogOutputHandler("GetFiles:Error:no files. returning.");
                    return new string[0];
                }

                CDFMonitor.LogOutputHandler(string.Format("GetFiles: returning {0} files.", retList.Count));
                return retList.ToArray();
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler(string.Format("GetFiles:main exception:{0}", e.ToString()));
                return new string[0];
            }
            finally
            {
                if (rm.GetPathType(cleanPath) == ResourceManagement.ResourceType.Unc)
                {
                    rm.DisconnectUncPath(cleanPath);
                }
            }
        }

        /// <summary>
        /// Gets the full path.
        /// </summary>
        /// <param name="path">The path. assumes this is a path to a file or folder.</param>
        /// <returns>System.String.</returns>
        public static string GetFullPath(string path)
        {
            string tempPath = path.Trim('"');

            if (string.IsNullOrEmpty(tempPath))
            {
                return tempPath;
            }

            try
            {
                tempPath = Path.GetFullPath(tempPath);
            }
            catch { }

            return string.IsNullOrEmpty(tempPath) ? path : tempPath;
        }

        /// <summary>
        /// Renames the file.
        /// </summary>
        /// <param name="oldFileName">Old name of the file.</param>
        /// <param name="newFileName">New name of the file.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool RenameFile(string oldFileName, string newFileName)
        {
            ResourceManagement rm = new ResourceManagement();
            if (rm.GetPathType(oldFileName) == ResourceManagement.ResourceType.Unc
                && rm.CheckUncPath(oldFileName, false))
            {
                if (!rm.ConnectUncPath(oldFileName))
                {
                    return false;
                }
            }
            try
            {
                CDFMonitor.LogOutputHandler(string.Format("RenameFile:file:{0}:{1}", oldFileName, newFileName));
                if (File.Exists(newFileName))
                {
                    File.Delete(newFileName);
                }

                File.Move(oldFileName, newFileName);
                return true;
            }
            catch
            {
                CDFMonitor.LogOutputHandler(string.Format("RenameFile:exception renaming file:{0}", oldFileName));
                return false;
            }
            finally
            {
                if (rm.GetPathType(oldFileName) == ResourceManagement.ResourceType.Unc)
                {
                    rm.DisconnectUncPath(oldFileName);
                }
            }
        }

        #endregion Public Methods

        #endregion
    }
}