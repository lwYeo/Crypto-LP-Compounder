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

namespace Crypto_LP_Compounder
{
    internal class Log
    {
        private readonly bool _IsLogAll;
        private readonly int _DeleteLogAfterDays;
        private readonly string _InstanceName;

        private string _LogFileName;
        private string _LogProcessFileName;

        private StreamWriter _LogStream;
        private StreamWriter _LogProcessStream;

        private DateTime _RecentDate;
        private readonly Queue<string> _LogQueue;

        public bool IsCompoundProcess { get; set; }

        public Log(Settings.CompounderSettings settings)
        {
            _IsLogAll = settings.IsLogAll;
            _DeleteLogAfterDays = (int)settings.DeleteLogsAfterDays;
            _InstanceName = settings.Name;
            _RecentDate = DateTime.Today.AddDays(-1);
            _LogQueue = new();
        }

        public string[] ReadRecentLogFile() =>
            ReadRecentLogFile((DateTime dateTime) => GetLogFileName(dateTime));

        public string[] ReadRecentProcessLogFile() =>
            ReadRecentLogFile((DateTime dateTime) => GetProcessLogFileName(dateTime));

        public async Task FlushLogsAsync()
        {
            while (_LogQueue.TryPeek(out _)) await Task.Delay(100);
        }

        public void Write(string log)
        {
            Program.LogConsole($"[{_InstanceName}] {log}");

            try
            {
                if (_RecentDate != DateTime.Today)
                {
                    _LogProcessStream?.Dispose();
                    _LogStream?.Dispose();

                    DeleteOldLogs();

                    _LogFileName = GetLogFileName(DateTime.Today);
                    _LogProcessFileName = GetProcessLogFileName(DateTime.Today);

                    _LogProcessStream =
                        new StreamWriter(
                            new FileStream(
                                _LogProcessFileName,
                                File.Exists(_LogProcessFileName) ? FileMode.Append : FileMode.Create,
                                FileAccess.Write,
                                FileShare.ReadWrite,
                                1024,
                                FileOptions.WriteThrough));

                    if (_IsLogAll)
                    {
                        _LogStream =
                            new StreamWriter(
                                new FileStream(
                                    _LogFileName,
                                    File.Exists(_LogProcessFileName) ? FileMode.Append : FileMode.Create,
                                    FileAccess.Write,
                                    FileShare.ReadWrite,
                                    1024,
                                    FileOptions.WriteThrough));
                    }

                    _RecentDate = DateTime.Today;
                }

                if (IsCompoundProcess)
                {
                    _LogProcessStream?.Write(log);
                    _LogProcessStream?.Flush();
                }

                _LogStream?.Write(log);
                _LogStream?.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void WriteLine()
        {
            Write(Environment.NewLine);
        }

        public void WriteLine(string input)
        {
            Write(input + Environment.NewLine);
        }

        public void WriteLineBreak()
        {
            WriteLine(Program.GetLineBreak());
        }

        private string GetLogFileName(DateTime datetime) =>
            Path.Combine(AppContext.BaseDirectory, $"{_InstanceName}_{datetime:yyyy-MM-dd}.log");

        private string GetProcessLogFileName(DateTime datetime) =>
            Path.Combine(AppContext.BaseDirectory, $"{_InstanceName}_{datetime:yyyy-MM-dd}_Process.log");

        private string[] ReadRecentLogFile(Func<DateTime, string> getFilePath)
        {
            string[] logs = null;
            DateTime lastDate = DateTime.Today.AddDays(1);

            while (!(logs?.Any() ?? false))
            {
                lastDate = lastDate.AddDays(-1);
                string filePath = getFilePath.Invoke(lastDate);

                if (!File.Exists(filePath)) break;

                logs = ReadLines(filePath).ToArray();
            }

            return logs ?? Array.Empty<string>();
        }

        private IEnumerable<string> ReadLines(string filePath)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(stream);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }

        private void DeleteOldLogs()
        {
            if (_DeleteLogAfterDays < 1) return;

            string[] foundLogs = Directory.GetFiles(AppContext.BaseDirectory, $"{_InstanceName}_*.log");

            string[] logsToKeep =
                Enumerable.Range(0, _DeleteLogAfterDays)
                .Select(i => GetLogFileName(DateTime.Today.AddDays(i)))
                .Concat(
                    Enumerable.Range(0, _DeleteLogAfterDays)
                    .Select(i => GetProcessLogFileName(DateTime.Today.AddDays(i))))
                .ToArray();

            string[] logsToDelete = foundLogs.Except(logsToKeep).ToArray();

            foreach (string log in logsToDelete)
            {
                try
                {
                    Program.LogLineConsole($"Deleting log file: {log}");
                    File.Delete(log);
                }
                catch (Exception ex)
                {
                    Program.LogLineConsole(ex.ToString());
                }
            }
        }
    }
}
