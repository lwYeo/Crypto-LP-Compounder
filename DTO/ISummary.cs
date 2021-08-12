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
    public interface ISummary
    {
        [JsonPropertyOrder(0)]
        string InstanceName { get; }

        [JsonPropertyOrder(1)]
        DateTimeOffset LastUpdate { get; }

        [JsonPropertyOrder(2)]
        string CurrentAPR { get; }

        [JsonPropertyOrder(3)]
        string OptimalAPY { get; }

        [JsonPropertyOrder(4)]
        string OptimalCompoundsPerYear { get; }

        [JsonPropertyOrder(5)]
        DateTimeOffset LastCompoundDateTime { get; }

        [JsonPropertyOrder(6)]
        string NextOptimalCompoundIn { get; }

        [JsonPropertyOrder(7)]
        DateTimeOffset NextOptimalCompoundDateTime { get; }

        [JsonPropertyOrder(8)]
        string TotalLiquidity { get; }

        [JsonPropertyOrder(9)]
        string PendingReward { get; }

        [JsonPropertyOrder(10)]
        string CurrentDeposit { get; }

        [JsonPropertyOrder(11)]
        string UnderlyingTokenA_Deposit { get; }

        [JsonPropertyOrder(12)]
        string UnderlyingTokenB_Deposit { get; }

        [JsonPropertyOrder(13)]
        string RewardValue { get; }

        [JsonPropertyOrder(14)]
        string TokenA_Value { get; }

        [JsonPropertyOrder(15)]
        string TokenB_Value { get; }
    }
}
