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

using Nethereum.Web3;
using System.Numerics;
using System.Threading.Tasks;

namespace Crypto_LP_Compounder.Contract.Farm
{
    internal class YEL : MasterChef
    {
        public YEL(Settings settings, Web3 web3, UniswapV2.Router router, ERC20 rewardToken) :
            base(settings, web3, router, rewardToken)
        {
        }

        public override async Task<BigInteger> GetTotalRewardsTask()
        {
            Task<BigInteger> getRewardPerSecondTask = GetRewardPerSecondTask();

            DTO.Farm.YEL_Reward.StartTimeFunction startTimeFunction = new()
            {
                FromAddress = _Settings.Wallet.Address
            };
            Task<BigInteger> startTimeTask = _ContractHandler.
                QueryAsync<DTO.Farm.YEL_Reward.StartTimeFunction, BigInteger>(startTimeFunction);

            DTO.Farm.YEL_Reward.EndTimeFunction endTimeFunction = new()
            {
                FromAddress = _Settings.Wallet.Address
            };
            Task<BigInteger> endTimeTask = _ContractHandler.
                QueryAsync<DTO.Farm.YEL_Reward.EndTimeFunction, BigInteger>(endTimeFunction);

            await Task.WhenAll(getRewardPerSecondTask, startTimeTask, endTimeTask);

            return getRewardPerSecondTask.Result * (endTimeTask.Result - startTimeTask.Result);
        }

        public override async Task<BigInteger> GetRewardPerSecondTask()
        {
            DTO.Farm.YEL_Reward.RewardPerSecondFunction tSharePerSecondFunction = new()
            {
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.Farm.YEL_Reward.RewardPerSecondFunction, BigInteger>(tSharePerSecondFunction);
        }

        public async Task<DTO.Farm.YEL_Reward.PoolInfoOutputDTO> GetPoolInfo()
        {
            DTO.Farm.YEL_Reward.PoolInfo poolInfoFunction = new()
            {
                PoolID = _Settings.Farm.FarmPoolID,
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryDeserializingToObjectAsync<DTO.Farm.YEL_Reward.PoolInfo, DTO.Farm.YEL_Reward.PoolInfoOutputDTO>(poolInfoFunction);
        }

        public override async Task<BigInteger> GetPendingRewardTask()
        {
            DTO.Farm.YEL_Reward.PendingRewardFunction pendingShareFunction = new()
            {
                PoolID = _Settings.Farm.FarmPoolID,
                User = _Settings.Wallet.Address,
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.Farm.YEL_Reward.PendingRewardFunction, BigInteger>(pendingShareFunction);
        }

        public override async Task<BigInteger> GetAllocPoint()
        {
            return await GetPoolInfo().ContinueWith(t => t.Result.AllocPoint);
        }
    }
}
