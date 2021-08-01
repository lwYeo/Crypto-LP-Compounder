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
    internal class TombFinance : MasterChef
    {
        public TombFinance(Settings settings, Web3 web3, UniswapV2.Router router, ERC20 rewardToken) :
            base(settings, web3, router, rewardToken)
        {
        }

        public override async Task<BigInteger> GetTotalRewardsTask()
        {
            DTO.Farm.TShareReward.TotalRewardsFunction totalRewardsFunction = new()
            {
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.Farm.TShareReward.TotalRewardsFunction, BigInteger>(totalRewardsFunction);
        }

        public override async Task<BigInteger> GetRewardPerSecondTask()
        {
            DTO.Farm.TShareReward.RewardPerSecondFunction tSharePerSecondFunction = new()
            {
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.Farm.TShareReward.RewardPerSecondFunction, BigInteger>(tSharePerSecondFunction);
        }

        public async Task<DTO.Farm.TShareReward.PoolInfoOutputDTO> GetPoolInfo()
        {
            DTO.Farm.TShareReward.PoolInfo poolInfoFunction = new()
            {
                PoolID = _Settings.Farm.FarmPoolID,
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryDeserializingToObjectAsync<DTO.Farm.TShareReward.PoolInfo, DTO.Farm.TShareReward.PoolInfoOutputDTO>(poolInfoFunction);
        }

        public override async Task<BigInteger> GetPendingRewardTask()
        {
            DTO.Farm.TShareReward.PendingRewardFunction pendingShareFunction = new()
            {
                PoolID = _Settings.Farm.FarmPoolID,
                User = _Settings.Wallet.Address,
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.Farm.TShareReward.PendingRewardFunction, BigInteger>(pendingShareFunction);
        }

        public override async Task<BigInteger> GetAllocPoint()
        {
            return await GetPoolInfo().ContinueWith(t => t.Result.AllocPoint);
        }
    }
}
