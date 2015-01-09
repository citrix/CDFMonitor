// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="Console.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc. </copyright>
// <summary></summary>
// ***********************************************************************

namespace CDFM.Engine
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows.Media;

    /// <summary>
    /// Class ConsoleManager
    /// </summary>
    public static class ConsoleManager
    {
        #region Private Fields

        private const string Kernel32_DllName = "kernel32.dll";

        private static TextWriter _consoleErr;

        private static TextWriter _consoleOut;

        #endregion Private Fields

        #region Private Delegates

        private delegate int ReadLineDelegate();

        #endregion Private Delegates

        #region Public Properties

        /// <summary>
        /// Gets a value indicating whether this instance has console.
        /// </summary>
        /// <value><c>true</c> if this instance has console; otherwise, /c>.</value>
        public static bool HasConsole
        {
            get { return GetConsoleWindow() != IntPtr.Zero; }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Consoles the read input.
        /// </summary>
        public static void ConsoleReadInput()
        {
            // listen for 'Q' to quit application. had to do this to make it work through psexec i
            // could not get ctrl-c to pass while in psexec only Console.readline works in psexec

            Thread.CurrentThread.Name = "_consoleReader";
            string resultstr = string.Empty;
            ReadLineDelegate d = Console.Read;
            IAsyncResult result = d.BeginInvoke(null, null);

            while (true)
            {
                try
                {
                    if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(100))
                    {
                        Debug.Print("exiting console reader");
                        return;
                    }

                    result.AsyncWaitHandle.WaitOne(1000);
                    if (result.IsCompleted)
                    {
                        resultstr = Convert.ToString(Convert.ToChar(d.EndInvoke(result)));

                        switch (resultstr.ToUpper())
                        {
                            case "Q":
                                CDFMonitor.CloseCurrentSessionEvent.Set();
                                return;

                            case "C":

                                // clear console output
                                Console.Clear();
                                break;

                            case "M":

                                CDFMonitor.LogOutputHandler(string.Format("CDFMarker:{0}:{1}", CDFMonitor.Instance.MarkerEvents, CDFMonitor.Instance.MarkerEvents++), JobOutputType.Etw);
                                break;

                            case "S":

                                // Show stats
                                Console.Clear();
                                while (true)
                                {
                                    if (CDFMonitor.CloseCurrentSessionEvent.WaitOne(200))
                                    {
                                        return;
                                    }

                                    // Console.Clear();
                                    Console.SetCursorPosition(0, 0);
                                    int count = 0;
                                    foreach (
                                        string s in
                                            CDFMonitor.Instance.GetStats().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        string newString = s;
                                        while (Console.BufferWidth - 1 > newString.Length)
                                        {
                                            newString += " ";
                                        }

                                        if (++count == Console.WindowHeight - 2)
                                        {
                                            break;
                                        }

                                        Console.WriteLine(newString);
                                    }
                                }

                            default:
                                break;
                        }

                        // restart
                        result = d.BeginInvoke(null, null);
                    }
                    else
                    {
                        continue;
                    }
                }
                catch (Exception e)
                {
                    // dont writeoutput as ctrl-c will throw exception
                    Debug.Print("Fail:ConsoleReadInput exception: " + e.ToString());
                    return;
                }
            }
        }

        /// <summary>
        /// If the process has a console attached to it, it will be detached and no longer visible.
        /// Writing to the System.Console is still possible, but no output will be shown.
        /// </summary>
        public static void Dispose()
        {
            //#if DEBUG
            if (HasConsole)
            {
                // ShowWindow(GetConsoleWindow(), 0);
                SetOutAndErrorNull();
                FreeConsole();
            }

            //#endif
        }

        /// <summary>
        /// Gets the color of the console brush.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <returns>ConsoleColor.</returns>
        public static ConsoleColor GetConsoleColor(Brush color)
        {
            // return (ConsoleColor)Enum.Parse(typeof(ConsoleColor), color.ToString(),true);
            if (color == Brushes.White)
            {
                return ConsoleColor.White;
            }
            if (color == Brushes.Red)
            {
                return ConsoleColor.Red;
            }
            if (color == Brushes.Green)
            {
                return ConsoleColor.Green;
            }
            if (color == Brushes.Gray)
            {
                return ConsoleColor.Gray;
            }
            if (color == Brushes.Yellow)
            {
                return ConsoleColor.Yellow;
            }
            if (color == Brushes.Cyan)
            {
                return ConsoleColor.Cyan;
            }
            if (color == Brushes.Magenta)
            {
                return ConsoleColor.Magenta;
            }

            return ConsoleColor.White;
        }

        /// <summary>
        /// If the process has a console attached to it, it will be detached and no longer visible.
        /// Writing to the System.Console is still possible, but no output will be shown.
        /// </summary>
        public static void Hide()
        {
            //#if DEBUG
            if (HasConsole)
            {
                Debug.Print("Console.Hide:hiding console window.");
                ShowWindow(GetConsoleWindow(), 0);

                SetOutAndErrorNull();
                FreeConsole();
            }
            else
            {
                Debug.Print("Console.Hide:warning:no console window to hide.");
            }

            //#endif
        }

        /// <summary>
        /// Redirects the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool Redirect(TextWriter stream = null)
        {
            //#if DEBUG

            if (HasConsole)
            {
                try
                {
                    if (stream == null
                        && _consoleOut != null
                        && _consoleErr != null)
                    {
                        Console.SetError(_consoleErr);
                        Console.SetOut(_consoleOut);
                    }
                    else
                    {
                        _consoleErr = Console.Error;
                        _consoleOut = Console.Out;
                        Console.SetOut(stream);
                        Console.SetError(stream);
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a new console instance if the process is not attached to a console already.
        /// </summary>
        public static void Show()
        {
            if (!HasConsole)
            {
                ShowWindow(GetConsoleWindow(), 0);
            }
        }

        /// <summary>
        /// Toggles this instance.
        /// </summary>
        public static void Toggle()
        {
            if (HasConsole)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Allocs the console.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        [DllImport(Kernel32_DllName)]
        private static extern bool AllocConsole();

        /// <summary>
        /// Frees the console.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        [DllImport(Kernel32_DllName)]
        private static extern bool FreeConsole();

        /// <summary>
        /// Gets the console output CP.
        /// </summary>
        /// <returns>System.Int32.</returns>
        [DllImport(Kernel32_DllName)]
        private static extern int GetConsoleOutputCP();

        /// <summary>
        /// Gets the console window.
        /// </summary>
        /// <returns>IntPtr.</returns>
        [DllImport(Kernel32_DllName)]
        private static extern IntPtr GetConsoleWindow();

        /// <summary>
        /// Invalidates the out and error.
        /// </summary>
        private static void InvalidateOutAndError()
        {
            Type type = typeof(Console);

            FieldInfo _out = type.GetField("_out",
                                           BindingFlags.Static | BindingFlags.NonPublic);

            FieldInfo _error = type.GetField("_error",
                                             BindingFlags.Static | BindingFlags.NonPublic);

            MethodInfo _InitializeStdOutError = type.GetMethod("InitializeStdOutError",
                                                               BindingFlags.Static | BindingFlags.NonPublic);

            Debug.Assert(_out != null);
            Debug.Assert(_error != null);

            Debug.Assert(_InitializeStdOutError != null);

            _out.SetValue(null, null);
            _error.SetValue(null, null);

            _InitializeStdOutError.Invoke(null, new object[] { true });
        }

        /// <summary>
        /// Sets the out and error null.
        /// </summary>
        private static void SetOutAndErrorNull()
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
        }

        /// <summary>
        /// Shows the window.
        /// </summary>
        /// <param name="hWnd">The h WND.</param>
        /// <param name="nCmdShow">The n CMD show.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        #endregion Private Methods
    }
}