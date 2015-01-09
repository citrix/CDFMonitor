// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="FileAssociation.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************
namespace CDFM.FileManagement
{
    using CDFM.Engine;
    using Microsoft.Win32;
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Class FileAssociation
    /// </summary>
    internal class FileAssociation
    {
        #region Private Fields

        private static readonly string _extensionBackup = _extension + "_back";
        private static readonly string _keyName = Process.GetCurrentProcess().ProcessName;
        private static string _extension = ".etl";
        private static string _fileDescription = "Citrix CDFMonitor";
        private static string _hkcuKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\";
        private static string _hkcuKeyExt = _hkcuKey + _extension;
        private static string _openWith = Process.GetCurrentProcess().MainModule.FileName;
        private static string user = Environment.UserDomainName + "\\" + Environment.UserName;

        #endregion Private Fields

        #region Public Methods

        /// <summary>
        /// Copies the key.
        /// </summary>
        /// <param name="parentKey">The parent key.</param>
        /// <param name="keyNameToCopy">The key name to copy.</param>
        /// <param name="newKeyName">New name of the key.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool CopyKey(RegistryKey parentKey,
            string keyNameToCopy, string newKeyName)
        {
            // Create new key
            RegistryKey destinationKey = parentKey.CreateSubKey(newKeyName);

            // Open the sourceKey we are copying from
            RegistryKey sourceKey = parentKey.OpenSubKey(keyNameToCopy);

            RecurseCopyKey(sourceKey, destinationKey);

            return true;
        }

        /// <summary>
        /// Renames the sub key.
        /// </summary>
        /// <param name="parentKey">The parent key.</param>
        /// <param name="subKeyName">Name of the sub key.</param>
        /// <param name="newSubKeyName">New name of the sub key.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool RenameSubKey(RegistryKey parentKey, string subKeyName, string newSubKeyName)
        {
            CDFMonitor.LogOutputHandler(string.Format("RenameSubKey: enter:{0} to {1}", subKeyName, newSubKeyName));
            DeleteKey(parentKey, subKeyName);
            return true;
        }

        /// <summary>
        /// Sets the association.
        /// </summary>
        /// <param name="file">if specified, path and name of executable to use.</param>
        public static void SetAssociation(string file = null)
        {
            RegistryKey BaseKey;
            RegistryKey OpenMethod;
            RegistryKey Shell;
            RenameSubKey(Registry.ClassesRoot, _extension, _extensionBackup);
            CDFMonitor.LogOutputHandler("SetAssociation:enter");
            BaseKey = Registry.ClassesRoot.CreateSubKey(_extension);
            BaseKey.SetValue("", _keyName);

            OpenMethod = Registry.ClassesRoot.CreateSubKey(_keyName);
            OpenMethod.SetValue("", _fileDescription);

            if (!string.IsNullOrEmpty(file))
            {
                _openWith = file;
            }

            OpenMethod.CreateSubKey("DefaultIcon").SetValue("", "\"" + _openWith + "\",0");
            Shell = OpenMethod.CreateSubKey("Shell");
            Shell.CreateSubKey("edit").CreateSubKey("command").SetValue("", "\"" + _openWith + "\"" + " \"%1\"");
            Shell.CreateSubKey("open").CreateSubKey("command").SetValue("", "\"" + _openWith + "\"" + " \"%1\"");
            BaseKey.Close();
            OpenMethod.Close();
            Shell.Close();

            // Tell explorer the file association has been changed
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// SHs the change notify.
        /// </summary>
        /// <param name="wEventId">The w event id.</param>
        /// <param name="uFlags">The u flags.</param>
        /// <param name="dwItem1">The dw item1.</param>
        /// <param name="dwItem2">The dw item2.</param>
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        /// <summary>
        /// Uns the set association.
        /// </summary>
        public static void UnSetAssociation()
        {
            DeleteKey(Registry.ClassesRoot, _keyName);
            DeleteKey(Registry.ClassesRoot, _extension);
            RenameSubKey(Registry.ClassesRoot, _extensionBackup, _extension);

            // Tell explorer the file association has been changed
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Deletes the key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="keyName">Name of the key.</param>
        private static void DeleteKey(RegistryKey key, string keyName)
        {
            if (key.OpenSubKey(keyName) != null)
            {
                key.Close();
                key.DeleteSubKeyTree(keyName);
            }
        }

        /// <summary>
        /// Recurses the copy key.
        /// </summary>
        /// <param name="sourceKey">The source key.</param>
        /// <param name="destinationKey">The destination key.</param>
        private static void RecurseCopyKey(RegistryKey sourceKey, RegistryKey destinationKey)
        {
            // CopyQ all the values
            foreach (string valueName in sourceKey.GetValueNames())
            {
                object objValue = sourceKey.GetValue(valueName);
                RegistryValueKind valKind = sourceKey.GetValueKind(valueName);
                destinationKey.SetValue(valueName, objValue, valKind);
            }

            // For each subKey
            // Create a new subKey in destinationKey
            foreach (string sourceSubKeyName in sourceKey.GetSubKeyNames())
            {
                RegistryKey sourceSubKey = sourceKey.OpenSubKey(sourceSubKeyName);
                RegistryKey destSubKey = destinationKey.CreateSubKey(sourceSubKeyName);
                RecurseCopyKey(sourceSubKey, destSubKey);
            }
        }

        #endregion Private Methods
    }
}