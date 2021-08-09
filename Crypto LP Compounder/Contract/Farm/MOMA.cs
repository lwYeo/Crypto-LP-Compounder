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

using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Numerics;
using System.Threading.Tasks;

namespace Crypto_LP_Compounder.Contract.Farm
{
    internal class MOMA : MasterChef
    {
        public MOMA(Log log, Settings.CompounderSettings settings, Web3 web3, UniswapV2.Router router, ERC20 rewardToken) :
            base(log, settings, web3, router, rewardToken)
        {
        }

        public override async Task<BigInteger> GetRewardPerSecondTask()
        {
            BigInteger BSC_BlockTimeSec = 3;
            HexBigInteger currentBlockNo = await _Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            DTO.Farm.MOMA_Reward.GetMultiplierFunction multiplierFunction = new()
            {
                FromBlock = currentBlockNo.Value - 1,
                ToBlock = currentBlockNo.Value,
                FromAddress = _Settings.Wallet.Address
            };
            BigInteger multiplier =
                await _ContractHandler.QueryAsync<DTO.Farm.MOMA_Reward.GetMultiplierFunction, BigInteger>(multiplierFunction);

            DTO.Farm.MOMA_Reward.RewardPerBlockFunction rewardPerSecondFunction = new()
            {
                FromAddress = _Settings.Wallet.Address
            };
            BigInteger rewardPerBlock =
                await _ContractHandler.QueryAsync<DTO.Farm.MOMA_Reward.RewardPerBlockFunction, BigInteger>(rewardPerSecondFunction);

            return rewardPerBlock / BSC_BlockTimeSec * multiplier / BigInteger.Pow(10, 12);
        }

        public override async Task<DTO.Farm.MasterChef.UserInfoFunctionOutputDTO> GetUserInfoTask()
        {
            DTO.Farm.MOMA_Reward.UserInfoFunction userInfoFunction = new()
            {
                User = _Settings.Wallet.Address,
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryDeserializingToObjectAsync<DTO.Farm.MOMA_Reward.UserInfoFunction, DTO.Farm.MasterChef.UserInfoFunctionOutputDTO>(userInfoFunction);
        }

        public override Task<BigInteger> GetAllocPointTask()
        {
            return Task.Run(() => BigInteger.One);
        }

        public override Task<BigInteger> GetTotalAllocPointTask()
        {
            return Task.Run(() => BigInteger.One);
        }

        public override async Task<BigInteger> GetPendingRewardTask()
        {
            DTO.Farm.MOMA_Reward.PendingRewardFunction pendingShareFunction = new()
            {
                User = _Settings.Wallet.Address,
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.Farm.MOMA_Reward.PendingRewardFunction, BigInteger>(pendingShareFunction);
        }

        public override async Task<TransactionReceipt> DepositTask(BigInteger inAmount, BigInteger gasPrice)
        {
            DTO.Farm.MOMA_Reward.DepositFunction depositFunction = new()
            {
                Amount = inAmount,
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _ContractHandler.
                SendRequestAndWaitForReceiptAsync(depositFunction, _Settings.GetCancelTokenByTimeout());
        }

        public override async Task<TransactionReceipt> HarvestTask(BigInteger gasPrice)
        {
            DTO.Farm.MOMA_Reward.WithdrawFunction withdrawFunction = new()
            {
                Amount = BigInteger.Zero,
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _ContractHandler.
                SendRequestAndWaitForReceiptAsync(withdrawFunction, _Settings.GetCancelTokenByTimeout());
        }
    }
}
