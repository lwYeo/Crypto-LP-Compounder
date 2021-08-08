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

namespace DTO
{
    public interface ICompounder
    {
        string Name { get; }

        DateTimeOffset LastUpdate { get; }

        string[] Summary { get; }

        ValueSymbol CurrentAPR { get; }

        ValueSymbol OptimalAPY { get; }

        int OptimalCompoundsPerYear { get; }

        ValueSymbol EstimateGasPerTxn { get; }

        TokenValue CurrentDeposit { get; }

        TokenValue UnderlyingTokenA_Deposit { get; }

        TokenValue UnderlyingTokenB_Deposit { get; }

        TokenValue CurrentPendingReward { get; }

        TokenValue TokenA { get; }

        TokenValue TokenB { get; }

        TokenValue Reward { get; }
    }
}
