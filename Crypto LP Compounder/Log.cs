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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Crypto_LP_Compounder
{
    internal class Log
    {
        private readonly bool _IsLogAll;
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
            _InstanceName = settings.Name;
            _RecentDate = DateTime.Today.AddDays(-1);
            _LogQueue = new();
        }

        public async Task FlushLogsAsync()
        {
            while (_LogQueue.TryPeek(out _)) await Task.Delay(100);
        }

        public void Write(string log)
        {
            try
            {
                if (_RecentDate != DateTime.Today)
                {
                    _LogProcessStream?.Dispose();
                    _LogStream?.Dispose();

                    _LogFileName =
                        Path.Combine(AppContext.BaseDirectory, $"{_InstanceName}_{DateTime.Today:yyyy-MM-dd}.log");

                    _LogProcessFileName =
                        Path.Combine(AppContext.BaseDirectory, $"{_InstanceName}_{DateTime.Today:yyyy-MM-dd}_Process.log");

                    _LogProcessStream =
                        new StreamWriter(new FileStream(_LogProcessFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));

                    if (_IsLogAll)
                        _LogStream =
                            new StreamWriter(new FileStream(_LogFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));

                    _RecentDate = DateTime.Today;
                }

                if (IsCompoundProcess)
                {
                    _LogProcessStream?.Write(log);
                    _LogProcessStream?.Flush();
                }

                _LogStream?.Write(log);
                _LogStream?.Write(log);

                Program.LogConsole($"[{_InstanceName}] {log}");
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
    }
}
