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

using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Numerics;
using System.Threading.Tasks;

namespace Crypto_LP_Compounder.Contract.Farm
{
    internal class YEL : MasterChef
    {
        public YEL(Log log, Settings.CompounderSettings settings, Web3 web3, UniswapV2.Router router, ERC20 rewardToken) :
            base(log, settings, web3, router, rewardToken)
        {
        }

        public override async Task<BigInteger> GetRewardPerSecondTask()
        {
            DTO.Farm.YEL_Reward.RewardPerSecondFunction rewardPerSecondFunction = new()
            {
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.Farm.YEL_Reward.RewardPerSecondFunction, BigInteger>(rewardPerSecondFunction);
        }

        public async Task<DTO.Farm.YEL_Reward.PoolInfoOutputDTO> GetPoolInfoTask()
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

        public override async Task<BigInteger> GetAllocPointTask()
        {
            return await GetPoolInfoTask().ContinueWith(t => t.Result.AllocPoint);
        }

        public override async Task<BigInteger> GetTotalAllocPointTask()
        {
            DTO.Farm.MasterChef.TotalAllocPoint totalAllocPointFunction = new()
            {
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.Farm.MasterChef.TotalAllocPoint, BigInteger>(totalAllocPointFunction);
        }

        public override async Task<TransactionReceipt> DepositTask(BigInteger inAmount, BigInteger gasPrice)
        {
            DTO.Farm.MasterChef.DepositFunction depositFunction = new()
            {
                PoolID = _Settings.Farm.FarmPoolID,
                Amount = inAmount,
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _ContractHandler.
                SendRequestAndWaitForReceiptAsync(depositFunction, _Settings.GetCancelTokenByTimeout());
        }

        public override async Task<TransactionReceipt> HarvestTask(BigInteger gasPrice)
        {
            DTO.Farm.MasterChef.WithdrawFunction withdrawFunction = new()
            {
                PoolID = _Settings.Farm.FarmPoolID,
                Amount = BigInteger.Zero,
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _ContractHandler.
                SendRequestAndWaitForReceiptAsync(withdrawFunction, _Settings.GetCancelTokenByTimeout());
        }
    }
}
