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

using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Crypto_LP_Compounder.DTO.Farm
{
    public class YEL_Reward : MasterChef
    {
        [FunctionOutput]
        public class PoolInfoOutputDTO : IFunctionOutputDTO
        {
            [Parameter("address", "stakingToken", 1)]
            public string StakingToken { get; set; }

            [Parameter("uint256", "stakingTokenTotalAmount", 2)]
            public BigInteger StakingTokenTotalAmount { get; set; }

            [Parameter("uint256", "accYelPerShare", 3)]
            public BigInteger AccYelPerShare { get; set; }

            [Parameter("uint32", "lastRewardTime", 4)]
            public BigInteger LastRewardTime { get; set; }

            [Parameter("uint16", "allocPoint", 5)]
            public BigInteger AllocPoint { get; set; }
        }

        [Function("pendingYel", "uint256")]
        public class PendingRewardFunction : FunctionMessage
        {
            [Parameter("uint256", "_pid", 1)]
            public BigInteger PoolID { get; set; }

            [Parameter("address", "_user", 2)]
            public string User { get; set; }
        }

        [Function("poolInfo", typeof(PoolInfoOutputDTO))]
        public class PoolInfo : FunctionMessage
        {
            [Parameter("uint256", "")]
            public BigInteger PoolID { get; set; }
        }

        [Function("yelPerSecond", "uint256")]
        public class RewardPerSecondFunction : FunctionMessage
        {
        }
    }
}
