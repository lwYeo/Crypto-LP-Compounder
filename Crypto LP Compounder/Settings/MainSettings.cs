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
using System.Linq;
using System.Text.Json.Serialization;

namespace Crypto_LP_Compounder.Settings
{
    internal class MainSettings
    {
        private const string FileName = "settings.json";

        public static MainSettings LoadSettings()
        {
            Program.LogLineConsole();
            Program.LogConsole("Loading main settings... ");

            MainSettings settings = null;
            string filePath = System.IO.Path.Combine(AppContext.BaseDirectory, FileName);

            if (System.IO.File.Exists(filePath))
            {
                settings = Json.DeserializeFromFile<MainSettings>(filePath);
                Program.LogLineConsole("Done!");
            }
            else
            {
                Program.LogLineConsole("File not found!");
                Program.ExitWithErrorCode(10);
            }

            settings.Compounders.AddRange(
                settings.SettingsFileNames.Select(
                    fileName => CompounderSettings.LoadSettings(
                        System.IO.Path.Combine(AppContext.BaseDirectory, fileName))));

            if (!settings.Compounders.Any())
            {
                Program.LogLineConsole();
                Program.LogLineConsole("Compounder setting(s) NOT loaded!");
                Program.ExitWithErrorCode(12);
            }

            return settings;
        }

        public string WebApiURL { get; set; } = "http://127.0.0.1:5050";

        public List<string> SettingsFileNames { get; set; } = new();

        [JsonIgnore]
        public List<CompounderSettings> Compounders { get; private set; } = new();
    }
}
