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

namespace WebApi
{
    public class Program
    {
        internal static Func<ICompounder[]> GetInstances { get; private set; }

        internal static Func<ISummary[]> GetSummaries { get; private set; }

        public static async Task Start(
            string webApiUrl,
            Func<ICompounder[]> getInstances,
            Func<ISummary[]> getSummaries,
            string[] args)
        {
            GetInstances = getInstances;
            GetSummaries = getSummaries;

            IHost host = Host
                .CreateDefaultBuilder(args)
                .ConfigureLogging(log => log.ClearProviders())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseStartup<Startup>()
                        .UseUrls(webApiUrl);
                })
                .Build();

            await Task.Factory.StartNew(() => host.Run(), TaskCreationOptions.LongRunning);
        }
    }
}
