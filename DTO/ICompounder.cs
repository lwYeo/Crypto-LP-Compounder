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
using System.Text.Json.Serialization;

namespace DTO
{
    public interface ICompounder
    {
        [JsonPropertyOrder(0)]
        string Name { get; }

        [JsonPropertyOrder(1)]
        DateTimeOffset LastUpdate { get; }

        [JsonPropertyOrder(2)]
        string[] Summary { get; }

        [JsonPropertyOrder(3)]
        ValueSymbol CurrentAPR { get; }

        [JsonPropertyOrder(4)]
        ValueSymbol OptimalAPY { get; }

        [JsonPropertyOrder(5)]
        int OptimalCompoundsPerYear { get; }

        [JsonPropertyOrder(6)]
        DateTimeOffset LastCompoundDateTime { get; }

        [JsonPropertyOrder(7)]
        TimeSpan LastCompoundProcessDuration { get; }

        [JsonPropertyOrder(8)]
        DateTimeOffset NextEstimateCompoundDateTime { get; }

        [JsonPropertyOrder(9)]
        ValueSymbol EstimateGasPerTxn { get; }

        [JsonPropertyOrder(10)]
        TokenValue CurrentDeposit { get; }

        [JsonPropertyOrder(11)]
        TokenValue UnderlyingTokenA_Deposit { get; }

        [JsonPropertyOrder(12)]
        TokenValue UnderlyingTokenB_Deposit { get; }

        [JsonPropertyOrder(13)]
        TokenValue CurrentPendingReward { get; }

        [JsonPropertyOrder(14)]
        TokenValue TokenA { get; }

        [JsonPropertyOrder(15)]
        TokenValue TokenB { get; }

        [JsonPropertyOrder(16)]
        TokenValue Reward { get; }
    }
}
