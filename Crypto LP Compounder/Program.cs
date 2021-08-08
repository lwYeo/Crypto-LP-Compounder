/*
   Copyright 2021 Lip Wee Yeo Amano

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using DTO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Crypto_LP_Compounder
{
    internal class Program
    {
        private static string GetApplicationName() => typeof(Program).Assembly.GetName().Name;

        private static string GetCompanyName() => typeof(Program).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company;

        private static string GetApplicationVersion() => typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        private static string GetApplicationYear() => "2021";

        private static readonly Queue<object> _LogQueue = new();

        private static readonly Queue<string> _ConsoleLogQueue = new();

        private static readonly ManualResetEvent _FlushCompleteEvent = new(false);

        private static Compounder _AutoCompounder;

        private static readonly Func<ICompounder[]> _GetAllCompounders = () =>
        {
            return new ICompounder[] { _AutoCompounder };
        };

        private static volatile bool _IsTerminate;

        private static Settings _Settings;

        internal static bool IsTerminate => _IsTerminate;

        internal static void CreateLineBreak() => WriteLineLog(string.Concat(Enumerable.Repeat("=", 100)));

        internal static void ExitWithErrorMessage(int exitCode, string errorMessage, params object[] messageParams)
        {
            WriteLineLog();

            if (messageParams?.Any() ?? false)
                WriteLineLog(string.Format(errorMessage, messageParams));
            else
                WriteLineLog(errorMessage);

            WriteLineLog();
            WriteLog("Press any key to continue...");
            Console.ReadKey();
            WriteLineLog();

            Environment.Exit(exitCode);
        }

        private static async Task Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            Console.Title = $"{GetApplicationName()} {GetApplicationVersion()} by {GetCompanyName()} ({GetApplicationYear()})";

            int originalConsoleMode = 0;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && GetConsoleMode(GetStdHandle(STD_INPUT_HANDLE), out int mode))
            {
                originalConsoleMode = mode;
                DisableQuickEdit();
            }

            StartProcessLog();

            SetConsoleCtrlHandler();

            _Settings = Settings.LoadSettings();

            WebApi.Program.Start(_Settings.WebApiURL, _GetAllCompounders, args);

            _AutoCompounder = new(_Settings);

            await _AutoCompounder.Start();

            WriteLineLog();
            CreateLineBreak();
            WriteLineLog("Autocompounder stopped");
            CreateLineBreak();

            await WaitForLogsToFlush();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SetConsoleMode(GetStdHandle(STD_INPUT_HANDLE), originalConsoleMode);

            _FlushCompleteEvent.Set();
        }

        #region Logging

        public static void SetIsProcessingLog(bool isProcessing) => _LogQueue.Enqueue(isProcessing);

        public static void WriteLog(string log) => _LogQueue.Enqueue(log);

        public static void WriteLineLog() => _LogQueue.Enqueue(Environment.NewLine);

        public static void WriteLineLog(string log) => _LogQueue.Enqueue(log + Environment.NewLine);

        public static void WriteLog(string log, params object[] args) => _LogQueue.Enqueue(string.Format(log, args));

        public static void WriteLineLog(string log, params object[] args) => _LogQueue.Enqueue(string.Format(log, args) + Environment.NewLine);

        private static void StartProcessLog()
        {
            Task.Factory.StartNew(() =>
            {
                bool isLogCompoundProcess = false;
                string logFileName = string.Empty;
                string logProcessFileName = string.Empty;
                StreamWriter logStream = null;
                StreamWriter logProcessStream = null;

                DateTime recentDate = DateTime.Today.AddDays(-1);

                while (!_FlushCompleteEvent.WaitOne(10))
                {
                    try
                    {
                        if (logStream == null || recentDate != DateTime.Today)
                        {
                            logProcessStream?.Dispose();
                            logStream?.Dispose();

                            logFileName =
                                Path.Combine(
                                    AppContext.BaseDirectory,
                                    string.Format("{0}.log", DateTime.Today.ToString("yyyy-MM-dd")));

                            logProcessFileName =
                                Path.Combine(
                                    AppContext.BaseDirectory,
                                    string.Format("{0}_Process.log", DateTime.Today.ToString("yyyy-MM-dd")));

                            logStream = new StreamWriter(new FileStream(logFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                            logProcessStream = new StreamWriter(new FileStream(logProcessFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));

                            recentDate = DateTime.Today;
                        }
                        else if (_LogQueue.TryPeek(out object preview))
                        {
                            if (preview is bool)
                                isLogCompoundProcess = (bool)_LogQueue.Dequeue();
                            else
                            {
                                string log = (string)_LogQueue.Dequeue();

                                _ConsoleLogQueue.Enqueue(log);

                                logStream.Write(log);
                                logStream.Flush();

                                if (isLogCompoundProcess)
                                {
                                    logProcessStream.Write(log);
                                    logProcessStream.Flush();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
                logProcessStream?.Dispose();
                logStream?.Dispose();
            },
            TaskCreationOptions.LongRunning);

            StartConsoleLog();
        }

        private static void StartConsoleLog()
        {
            Task.Factory.StartNew(() =>
            {
                while (!_FlushCompleteEvent.WaitOne(10))
                    if (_ConsoleLogQueue.TryDequeue(out string log))
                        Console.Write(log);
            },
            TaskCreationOptions.LongRunning);
        }

        private static async Task WaitForLogsToFlush()
        {
            await Task.Delay(100);
            while (_LogQueue.Count > 0) await Task.Delay(100);
            await Task.Delay(100);
        }

        #endregion Logging

        #region Console Quick Edit

        public const int STD_INPUT_HANDLE = -10;

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, int ioMode);

        /// <summary>
        /// This flag enables the user to use the mouse to select and edit text. To enable
        /// this option, you must also set the ExtendedFlags flag.
        /// </summary>
        private const int QuickEditMode = 64;

        // ExtendedFlags must be combined with
        // InsertMode and QuickEditMode when setting
        /// <summary>
        /// ExtendedFlags must be enabled in order to enable InsertMode or QuickEditMode.
        /// </summary>
        private const int ExtendedFlags = 128;

        internal static void EnableQuickEdit()
        {
            IntPtr conHandle = GetStdHandle(STD_INPUT_HANDLE);

            if (!GetConsoleMode(conHandle, out int mode))
            {
                // error getting the console mode. Exit.
                return;
            }

            mode |= (QuickEditMode | ExtendedFlags);

            if (!SetConsoleMode(conHandle, mode))
            {
                // error setting console mode.
            }
        }

        internal static void DisableQuickEdit()
        {
            IntPtr conHandle = GetStdHandle(STD_INPUT_HANDLE);

            if (!GetConsoleMode(conHandle, out int mode))
            {
                // error getting the console mode. Exit.
                return;
            }

            mode &= ~(QuickEditMode | ExtendedFlags);

            if (!SetConsoleMode(conHandle, mode))
            {
                // error setting console mode.
            }
        }

        #endregion Console Quick Edit

        #region Closing handler

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private delegate void EventHandler(CtrlType sig);

        private static EventHandler _ConsoleCtrlHandler;

        private static void SetConsoleCtrlHandler()
        {
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                ConsoleCtrlHandler(CtrlType.CTRL_CLOSE_EVENT);
            };
            Console.CancelKeyPress += (s, ev) =>
            {
                ConsoleCtrlHandler(CtrlType.CTRL_C_EVENT);
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _ConsoleCtrlHandler += new EventHandler(ConsoleCtrlHandler);
                SetConsoleCtrlHandler(_ConsoleCtrlHandler, true);
            }
        }

        private static void ConsoleCtrlHandler(CtrlType sig)
        {
            if (_ConsoleCtrlHandler == null)
                _ConsoleCtrlHandler += new EventHandler(ConsoleCtrlHandler);

            lock (_ConsoleCtrlHandler)
            {
                if (_IsTerminate) return;

                WriteLineLog();
                CreateLineBreak();
                WriteLineLog("Stopping autocompounder...");
                CreateLineBreak();

                _IsTerminate = true;

                _FlushCompleteEvent.WaitOne();
            }
        }

        #endregion Closing handler
    }
}
