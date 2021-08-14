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

using System.Text.Json.Serialization;

namespace DTO
{
    public interface ICompounder
    {
        [JsonPropertyOrder(0)]
        string InstanceName { get; }

        [JsonPropertyOrder(1)]
        DateTimeOffset LastUpdate { get; }

        [JsonPropertyOrder(2)]
        ValueSymbol CurrentAPR { get; }

        [JsonPropertyOrder(3)]
        ValueSymbol OptimalAPY { get; }

        [JsonPropertyOrder(4)]
        int OptimalCompoundsPerYear { get; }

        [JsonPropertyOrder(5)]
        DateTimeOffset LastCompoundDateTime { get; }

        [JsonPropertyOrder(6)]
        TimeSpan LastCompoundProcessDuration { get; }

        [JsonPropertyOrder(7)]
        DateTimeOffset NextEstimateCompoundDateTime { get; }

        [JsonPropertyOrder(8)]
        ValueSymbol EstimateGasPerTxn { get; }

        [JsonPropertyOrder(9)]
        TokenValue CurrentDeposit { get; }

        [JsonPropertyOrder(10)]
        TokenValue UnderlyingTokenA_Deposit { get; }

        [JsonPropertyOrder(11)]
        TokenValue UnderlyingTokenB_Deposit { get; }

        [JsonPropertyOrder(12)]
        TokenValue CurrentPendingReward { get; }

        [JsonPropertyOrder(13)]
        SingleTokenValue TokenA { get; }

        [JsonPropertyOrder(14)]
        SingleTokenValue TokenB { get; }

        [JsonPropertyOrder(15)]
        SingleTokenValue Reward { get; }

        string[] GetRecentAllLogs();

        string[] GetRecentProcessLogs();
    }
}
