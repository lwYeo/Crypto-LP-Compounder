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

using Nethereum.Contracts;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Crypto_LP_Compounder.Contract.UniswapV2
{
    internal class MlnlRouter : Router
    {
        private readonly ContractHandler _MlnlContractHandler;

        public MlnlRouter(Log log, Settings.CompounderSettings settings, Web3 web3) : base(log, settings, web3)
        {
            if (!string.IsNullOrWhiteSpace(_LpSettings.ZapContract))
                _MlnlContractHandler = _Web3.Eth.GetContractHandler(_LpSettings.ZapContract);
        }

        public async Task<TransactionReceipt> ZapInTokensTask(
            string inTokenContract,
            BigInteger amount,
            string outLpContract,
            BigInteger gasPrice)
        {
            DTO.Uniswap.MlnlV2Router.ZapInTokenFunction zapInTokenFunction = new()
            {
                From = inTokenContract,
                Amount = amount,
                To = outLpContract,
                RouterAddr = _LpSettings.RouterContract,
                Recipient = _Settings.Wallet.Address,
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _MlnlContractHandler.
                SendRequestAndWaitForReceiptAsync(zapInTokenFunction, _Settings.GetCancelTokenByTimeout());
        }

        public override void HandleSwapRewardToLP(
            BigInteger rewardHarvestAmt,
            uint maxRetries,
            ref uint retryAttempt,
            ref uint currentProcessTxnCount,
            ref BigInteger lpAmount)
        {
            if (_MlnlContractHandler == null)
                base.HandleSwapRewardToLP(
                    rewardHarvestAmt,
                    maxRetries,
                    ref retryAttempt,
                    ref currentProcessTxnCount,
                    ref lpAmount);
            else
            {
                while (!HandleZapRewardToLP(rewardHarvestAmt, ref lpAmount))
                {
                    currentProcessTxnCount += 3;
                    retryAttempt++;

                    if (retryAttempt > maxRetries || Program.IsTerminate) return;

                    _Log.WriteLine($"Retrying... ({retryAttempt}/{maxRetries})");
                    Task.Delay(5000).Wait();

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount += 3;
                retryAttempt = 0;
            }
        }

        public bool HandleZapRewardToLP(BigInteger rewardAmount, ref BigInteger lpAmount)
        {
            lpAmount = BigInteger.Zero;

            BigDecimal rewardDecAmount =
                UnitConversion.Convert.FromWeiToBigDecimal(rewardAmount, UnitConversion.EthUnit.Wei) /
                    BigDecimal.Pow(10, _Settings.Farm.RewardDecimals);

            _Log.WriteLine();
            _Log.WriteLine($"Zapping {(decimal)rewardDecAmount:n10} reward to LP...");
            try
            {
                BigInteger gasPrice = _Settings.GetGasPrice(_Web3);

                TransactionReceipt zapTxnReceipt =
                    ZapInTokensTask(
                        _Settings.Farm.RewardContract,
                        rewardAmount,
                        _LpSettings.LP_Contract,
                        gasPrice).
                    Result;

                while (zapTxnReceipt.Status == null)
                {
                    zapTxnReceipt =
                        _Web3.Eth.Transactions.GetTransactionReceipt.
                        SendRequestAsync(zapTxnReceipt.TransactionHash).
                        Result;
                }

                if (zapTxnReceipt.Failed())
                {
                    _Log.WriteLine(
                        $"Failed: Zap reward to LP (gas: {UnitConversion.Convert.FromWei(zapTxnReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether):n10}" +
                        $" {_Settings.GasSymbol}, txn ID: {zapTxnReceipt.TransactionHash})");

                    return false;
                }

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferEvents = _Web3.Eth.
                    GetEvent<DTO.ERC20.TransferEventDTO>().
                    DecodeAllEventsForEvent(zapTxnReceipt.Logs);

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferOutEvents = transferEvents.
                    Where(e => e.Event.From.Equals(_Settings.Wallet.Address, StringComparison.OrdinalIgnoreCase)).
                    ToList();

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferInEvents = transferEvents.
                    Where(e => e.Event.To.Equals(_Settings.Wallet.Address, StringComparison.OrdinalIgnoreCase)).
                    ToList();

                rewardAmount = transferOutEvents.Select(l => l.Event.Value).Aggregate((currentSum, item) => currentSum + item);

                lpAmount = transferInEvents.Select(l => l.Event.Value).Aggregate((currentSum, item) => currentSum + item);

                BigDecimal lpAmountDec =
                    UnitConversion.Convert.FromWeiToBigDecimal(lpAmount, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _LpSettings.LP_Decimals);

                _Log.WriteLine(
                    $"Success: Zap {(decimal)rewardDecAmount:n10} reward to" +
                    $" {(decimal)lpAmountDec:n10} LP" +
                    $" (gas: {UnitConversion.Convert.FromWei(zapTxnReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether):n10}" +
                    $" {_Settings.GasSymbol}, txn ID: {zapTxnReceipt.TransactionHash})");

                return true;
            }
            catch (AggregateException ex)
            {
                _Log.WriteLine("Failed: Zap reward to LP");

                if (ex.InnerExceptions.Any(e => e is TaskCanceledException))
                    _Log.WriteLine($"Timeout: {_Settings.RPC_Timeout:n0} s");
                else
                    _Log.WriteLine(ex.ToString());

                return false;
            }
            catch (Exception ex)
            {
                _Log.WriteLine("Failed: Zap reward to LP");
                _Log.WriteLine(ex.ToString());

                return false;
            }
        }
    }
}
