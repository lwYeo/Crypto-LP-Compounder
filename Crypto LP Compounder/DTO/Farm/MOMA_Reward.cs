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
    public class MOMA_Reward : MasterChef
    {
        [Function("getMultiplier", "uint256")]
        public class GetMultiplierFunction : FunctionMessage
        {
            [Parameter("uint256", "_fromBlock", 1)]
            public BigInteger FromBlock { get; set; }

            [Parameter("uint256", "_toBlock", 2)]
            public BigInteger ToBlock { get; set; }
        }

        [Function("deposit")]
        new public class DepositFunction : FunctionMessage
        {
            [Parameter("uint256", "_amount")]
            public BigInteger Amount { get; set; }
        }

        [Function("rewardPerBlock", "uint256")]
        public class RewardPerBlockFunction : FunctionMessage
        {
        }

        [Function("pendingReward", "uint256")]
        public class PendingRewardFunction : FunctionMessage
        {
            [Parameter("address", "_user")]
            public virtual string User { get; set; }
        }

        [Function("userInfo", typeof(UserInfoFunctionOutputDTO))]
        new public class UserInfoFunction : FunctionMessage
        {
            [Parameter("address", "")]
            public string User { get; set; }
        }

        [Function("withdraw")]
        new public class WithdrawFunction : FunctionMessage
        {
            [Parameter("uint256", "_amount")]
            public BigInteger Amount { get; set; }
        }
    }
}
