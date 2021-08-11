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
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Crypto_LP_Compounder
{
    internal class Program
    {
        private static string GetApplicationName() => typeof(Program).Assembly.GetName().Name;

        private static string GetCompanyName() => typeof(Program).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company;

        private static string GetApplicationVersion() => typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        private static string GetApplicationYear() => "2021";

        private static readonly Queue<string> _ConsoleLogQueue = new();

        private static readonly ManualResetEvent _FlushCompleteEvent = new(false);

        private static Compounder[] _Compounders;

        private static readonly Func<ICompounder[]> _GetAllCompounders = () => _Compounders?.OfType<ICompounder>().ToArray();

        private static volatile bool _IsPause;

        private static volatile bool _IsTerminate;

        private static Settings.MainSettings _Settings;

        internal static bool IsTerminate => _IsTerminate;

        internal static void LogConsole(string log) => _ConsoleLogQueue.Enqueue(log);

        internal static void LogLineConsole() => LogConsole(Environment.NewLine);

        internal static void LogLineConsole(string log) => LogConsole(log + Environment.NewLine);

        internal static string GetLineBreak() => string.Concat(Enumerable.Repeat("=", 100));

        internal static void LogLineBreakConsole() => LogLineConsole(GetLineBreak());

        internal static async Task FlushConsoleLogsAsync()
        {
            while (_ConsoleLogQueue.TryPeek(out _)) await Task.Delay(100);
        }

        internal static async Task StartConsoleLog()
        {
            await Task.Factory.StartNew(() =>
            {
                while (!_FlushCompleteEvent.WaitOne(10))
                    if (_ConsoleLogQueue.TryDequeue(out string log))
                        Console.Write(log);
            }
            , TaskCreationOptions.LongRunning);
        }

        internal static void ExitWithErrorCode(int errorCode)
        {
            ExitWithErrorCodeAsync(errorCode).Wait();
        }

        private static async Task ExitWithErrorCodeAsync(int errorCode)
        {
            _IsTerminate = true;
            _IsPause = true;

            await FlushConsoleLogsAsync();

            LogLineConsole();
            LogConsole("Press any key to continue...");

            await FlushConsoleLogsAsync();

            Console.ReadKey();

            Environment.Exit(errorCode);

            _IsPause = false;
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

            _ = StartConsoleLog();

            SetConsoleCtrlHandler();

            _Settings = Settings.MainSettings.LoadSettings();

            if (_Settings.Compounders.Select(c => c.Name).Distinct().Count() < _Settings.Compounders.Count)
            {
                LogLineConsole();
                LogLineConsole("Instance name(s) in all settings file must be unique!");
                await ExitWithErrorCodeAsync(13);
            }

            LogLineConsole();
            LogLineConsole("Starting Web API...");
            _ = WebApi.Program
                .Start(_Settings.WebApiURL, _GetAllCompounders, args)
                .ContinueWith(async t =>
                {
                    if (t.IsFaulted)
                    {
                        _IsTerminate = true;
                        _IsPause = true;

                        await FlushConsoleLogsAsync();

                        LogLineConsole();
                        LogLineConsole(t.Exception.Message);

                        await ExitWithErrorCodeAsync(20);
                    }
                });

            await Task.Delay(1000); // Wait for web API to initialize

            if (!_IsTerminate)
            {
                _Compounders = _Settings.Compounders.Select(settings => new Compounder(settings)).ToArray();

                LogLineConsole();
                LogLineConsole("Starting autocompounders...");

                await Task.WhenAll(_Compounders.Select(c => c.Start()));

                await Task.WhenAll(_Compounders.Select(c => c.Log.FlushLogsAsync()));

                LogLineConsole();
                LogLineBreakConsole();
                LogLineConsole("Autocompounder stopped");
                LogLineBreakConsole();
            }

            await FlushConsoleLogsAsync();

            while (_IsPause) await Task.Delay(100);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SetConsoleMode(GetStdHandle(STD_INPUT_HANDLE), originalConsoleMode);

            _FlushCompleteEvent.Set();
        }

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

                LogLineConsole();
                LogLineBreakConsole();
                LogLineConsole("Stopping autocompounder...");
                LogLineBreakConsole();

                _IsTerminate = true;

                _FlushCompleteEvent.WaitOne();
            }
        }

        #endregion Closing handler
    }
}
