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
    internal class Router
    {
        protected readonly Settings _Settings;
        protected readonly Settings.LiquidityPoolParams _LpSettings;

        protected readonly Web3 _Web3;
        protected readonly ContractHandler _ContractHandler;
        protected readonly ContractHandler _TaxFreeContractHandler;

        private readonly ERC20 _TokenA;
        private readonly ERC20 _TokenB;

        public Router(Settings settings, Web3 web3)
        {
            _Settings = settings;
            _LpSettings = settings.LiquidityPool;
            _Web3 = web3;
            _ContractHandler = _Web3.Eth.GetContractHandler(_LpSettings.RouterContract);
            _TokenA = new(_Settings, _Web3, _Settings.LiquidityPool.TokenA_Contract);
            _TokenB = new(_Settings, _Web3, _Settings.LiquidityPool.TokenB_Contract);

            if (!string.IsNullOrWhiteSpace(_LpSettings.TaxFreeContract))
                _TaxFreeContractHandler = _Web3.Eth.GetContractHandler(_LpSettings.TaxFreeContract);
        }

        public async Task<BigInteger> GetAmountsOutTask(BigInteger inAmount, string inToken, string outToken)
        {
            string[] paths = new[] { inToken, outToken };

            if (!paths.Any(p => p.Equals(_Settings.WETH_Contract, StringComparison.OrdinalIgnoreCase)))
                paths = new[] { inToken, _Settings.WETH_Contract, outToken };

            DTO.Uniswap.V2Router.GetAmountsOutFunction getAmountsOutFunction = new()
            {
                AmountIn = inAmount,
                Path = paths.ToList(),
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.Uniswap.V2Router.GetAmountsOutFunction, List<BigInteger>>(getAmountsOutFunction).
                ContinueWith(t => t.Result.Last());
        }

        public async Task<TransactionReceipt> AddLiquidityTask(
            string tokenA,
            string tokenB,
            BigInteger tokenAAmount,
            BigInteger tokenBAmount,
            BigInteger tokenAMinAmount,
            BigInteger tokenBMinAmount,
            BigInteger gasPrice)
        {
            DTO.Uniswap.V2Router.AddLiquidityFunction addLiquidityFunction = new()
            {
                TokenA = tokenA,
                TokenB = tokenB,
                AmountADesired = tokenAAmount,
                AmountBDesired = tokenBAmount,
                AmountAMin = tokenAMinAmount,
                AmountBMin = tokenBMinAmount,
                To = _Settings.Wallet.Address,
                Deadline = _Settings.GetTimeoutEpoch(),
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _ContractHandler.
                SendRequestAndWaitForReceiptAsync(addLiquidityFunction, _Settings.GetCancelTokenByTimeout());
        }

        public async Task<TransactionReceipt> AddTaxFreeLiquidityTask(
            string tokenA,
            string tokenB,
            BigInteger tokenAAmount,
            BigInteger tokenBAmount,
            BigInteger tokenAMinAmount,
            BigInteger tokenBMinAmount,
            BigInteger gasPrice)
        {
            if (!tokenB.Equals("0x6c021ae822bea943b2e66552bde1d2696a53fbb7", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Tax free LP is only valid for TOMB Finance!");

            DTO.TombTaxOfficeV2.AddLiquidityTaxFreeFunction addLiquidityTaxFreeFunction = new()
            {
                Token = tokenA,
                AmtTomb = tokenAAmount,
                AmtToken = tokenBAmount,
                AmtTombMin = tokenAMinAmount,
                AmtTokenMin = tokenBMinAmount,
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _TaxFreeContractHandler.
                SendRequestAndWaitForReceiptAsync(addLiquidityTaxFreeFunction, _Settings.GetCancelTokenByTimeout());
        }

        public async Task<BigInteger> SwapExactETHForRewardFunctionEstimateGasTask(BigInteger gasPrice)
        {
            BigInteger ethBalance = _Web3.Eth.GetBalance.SendRequestAsync(_Settings.Wallet.Address).Result;

            DTO.Uniswap.V2Router.SwapExactETHForTokensFunction swapExactETHForTokensFunction = new()
            {
                AmountOutMin = BigInteger.Zero,
                Path = new[] { _Settings.WETH_Contract, _Settings.Farm.RewardContract }.ToList(),
                To = _Settings.Wallet.Address,
                Deadline = _Settings.GetTimeoutEpoch(),
                FromAddress = _Settings.Wallet.Address,
                AmountToSend = ethBalance / 2,
                GasPrice = gasPrice
            };
            return await _ContractHandler.
                EstimateGasAsync(swapExactETHForTokensFunction).
                ContinueWith(t => t.Result.Value);
        }

        public async Task<TransactionReceipt> SwapExactTokensForEthTask(
            BigInteger inAmount,
            BigInteger outMinAmount,
            List<string> paths,
            BigInteger gasPrice)
        {
            if (!paths.Last().Equals(_Settings.WETH_Contract))
                paths.Add(_Settings.WETH_Contract); // Ensure last token is WETH before swapping to ETH

            DTO.Uniswap.V2Router.SwapExactTokensForETHFunction swapExactTokensForETHFunction = new()
            {
                AmountIn = inAmount,
                AmountOutMin = outMinAmount,
                Path = paths,
                To = _Settings.Wallet.Address,
                Deadline = _Settings.GetTimeoutEpoch(),
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _ContractHandler.
                SendRequestAndWaitForReceiptAsync(swapExactTokensForETHFunction, _Settings.GetCancelTokenByTimeout());
        }

        public async Task<TransactionReceipt> SwapExactTokensForTokensTask(
            BigInteger inAmount,
            BigInteger outMinAmount,
            List<string> paths,
            BigInteger gasPrice)
        {
            DTO.Uniswap.V2Router.SwapExactTokensForTokensFunction swapExactTokensForTokensFunction = new()
            {
                AmountIn = inAmount,
                AmountOutMin = outMinAmount,
                Path = paths,
                To = _Settings.Wallet.Address,
                Deadline = _Settings.GetTimeoutEpoch(),
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _ContractHandler.
                SendRequestAndWaitForReceiptAsync(swapExactTokensForTokensFunction, _Settings.GetCancelTokenByTimeout());
        }

        public bool HandleSwapRewardToToken(BigInteger rewardAmount, string tokenContract, ref BigInteger tokenAmount)
        {
            tokenAmount = BigInteger.Zero;

            string contractName = tokenContract.Equals(_LpSettings.TokenA_Contract) ? "A" : "B";

            BigDecimal contractDecimals =
                BigDecimal.Pow(
                    10,
                    tokenContract.Equals(_LpSettings.TokenA_Contract) ?
                    _LpSettings.TokenA_Decimals :
                    _LpSettings.TokenB_Decimals);

            Program.WriteLineLog();
            Program.WriteLineLog("Swapping {0:n10} reward to token {1}...",
                (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(rewardAmount, UnitConversion.EthUnit.Wei) /
                    BigDecimal.Pow(10, _Settings.Farm.RewardDecimals)),
                contractName);
            try
            {
                List<string> paths = new() { _Settings.Farm.RewardContract };

                if (!tokenContract.Equals(_Settings.WETH_Contract, StringComparison.OrdinalIgnoreCase))
                    paths.Add(_Settings.WETH_Contract);

                paths.Add(tokenContract);

                BigInteger amountsOut = GetAmountsOutTask(rewardAmount, _Settings.Farm.RewardContract, tokenContract).Result;

                amountsOut = (amountsOut * (int)((100 - _LpSettings.Slippage) * 10)) / 1000;

                BigInteger gasPrice = _Settings.GetGasPrice(_Web3);

                TransactionReceipt swapTxnReceipt =
                    SwapExactTokensForTokensTask(
                        rewardAmount,
                        amountsOut,
                        paths,
                        gasPrice).
                    Result;

                while (swapTxnReceipt.Status == null)
                {
                    swapTxnReceipt =
                        _Web3.Eth.Transactions.GetTransactionReceipt.
                        SendRequestAsync(swapTxnReceipt.TransactionHash).
                        Result;
                }

                if (swapTxnReceipt.Failed())
                {
                    Program.WriteLineLog("Failed: Swap reward to token (gas: {0:n10} ETH, txn ID: {1})",
                        UnitConversion.Convert.FromWei(swapTxnReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                        swapTxnReceipt.TransactionHash);

                    return false;
                }

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferEvents = _Web3.Eth.
                    GetEvent<DTO.ERC20.TransferEventDTO>().
                    DecodeAllEventsForEvent(swapTxnReceipt.Logs);

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferInEvents = transferEvents.
                    Where(e => e.Event.To.Equals(_Settings.Wallet.Address, StringComparison.OrdinalIgnoreCase)).
                    ToList();

                tokenAmount = transferInEvents.Select(l => l.Event.Value).Aggregate((currentSum, item) => currentSum + item);

                Program.WriteLineLog("Success: Swap reward to {0:n10} token {1}",
                    (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(tokenAmount, UnitConversion.EthUnit.Wei) / contractDecimals),
                    contractName);

                Program.WriteLineLog("(gas: {0:n10} ETH, txn ID: {1})",
                    UnitConversion.Convert.FromWei(swapTxnReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                    swapTxnReceipt.TransactionHash);

                return true;
            }
            catch (AggregateException ex)
            {
                Program.WriteLineLog("Failed: Swap reward to token");

                if (ex.InnerExceptions.Any(e => e is TaskCanceledException))
                    Program.WriteLineLog("Timeout: {0:n0} s", _Settings.RPC_Timeout);
                else
                    Program.WriteLineLog(ex.ToString());

                return false;
            }
            catch (Exception ex)
            {
                Program.WriteLineLog("Failed: Swap reward to token");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
        }

        public bool HandleAddTokensToLP(
            BigInteger tokenA_Amount,
            BigInteger tokenB_Amount,
            ref uint currentProcessTxnCount,
            ref BigInteger lpAmount)
        {
            lpAmount = BigInteger.Zero;

            Program.WriteLineLog();
            try
            {
                Program.WriteLineLog("Adding LP with {0:n10} token A and {1:n10} token B...",
                    (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(tokenA_Amount, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _LpSettings.TokenA_Decimals)),
                    (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(tokenB_Amount, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _LpSettings.TokenB_Decimals)));

                BigInteger gasPrice = _Settings.GetGasPrice(_Web3);

                TransactionReceipt addLiquidityReceipt;

                if (_TaxFreeContractHandler != null)
                {
                    currentProcessTxnCount += 2;

                    addLiquidityReceipt =
                        AddTaxFreeLiquidityTask(
                            _LpSettings.TokenA_Contract,
                            _LpSettings.TokenB_Contract,
                            tokenA_Amount * 95 / 100, // workaround for transfer amount exceeds balance
                            tokenB_Amount * 95 / 100, // workaround for transfer amount exceeds balance
                            tokenA_Amount / 2,
                            tokenB_Amount / 2,
                            gasPrice).
                        Result;
                }
                else
                {
                    currentProcessTxnCount++;

                    addLiquidityReceipt =
                        AddLiquidityTask(
                            _LpSettings.TokenA_Contract,
                            _LpSettings.TokenB_Contract,
                            tokenA_Amount,
                            tokenB_Amount,
                            tokenA_Amount / 2,
                            tokenB_Amount / 2,
                            gasPrice).
                        Result;
                }

                while (addLiquidityReceipt.Status == null)
                {
                    addLiquidityReceipt =
                        _Web3.Eth.Transactions.GetTransactionReceipt.
                        SendRequestAsync(addLiquidityReceipt.TransactionHash).
                        Result;
                }

                if (addLiquidityReceipt.Failed())
                {
                    Program.WriteLineLog("Failed: Add LP with tokens (gas: {0:n10} ETH, txn ID: {1})",
                        UnitConversion.Convert.FromWei(addLiquidityReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                        addLiquidityReceipt.TransactionHash);

                    return false;
                }

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferEvents = _Web3.Eth.
                    GetEvent<DTO.ERC20.TransferEventDTO>().
                    DecodeAllEventsForEvent(addLiquidityReceipt.Logs);

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferInEvents = transferEvents.
                    Where(e => e.Event.To.Equals(_Settings.Wallet.Address, StringComparison.OrdinalIgnoreCase)).
                    ToList();

                lpAmount = transferInEvents.Select(l => l.Event.Value).Aggregate((currentSum, item) => currentSum + item);

                Program.WriteLineLog("Success: Added {0:n10} LP tokens (gas: {1:n10} ETH, txn ID: {2})",
                    (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(lpAmount, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _LpSettings.LP_Decimals)),
                    UnitConversion.Convert.FromWei(addLiquidityReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                    addLiquidityReceipt.TransactionHash);

                return true;
            }
            catch (AggregateException ex)
            {
                Program.WriteLineLog("Failed: Add LP with tokens");

                if (ex.InnerExceptions.Any(e => e is TaskCanceledException))
                    Program.WriteLineLog("Timeout: {0:n0} s", _Settings.RPC_Timeout);
                else
                    Program.WriteLineLog(ex.ToString());

                return false;
            }
            catch (Exception ex)
            {
                Program.WriteLineLog("Failed: Add LP with tokens");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
        }

        public virtual void HandleSwapRewardToLP(
            BigInteger rewardHarvestAmt,
            uint maxRetries,
            ref uint retryAttempt,
            ref uint currentProcessTxnCount,
            ref BigInteger lpAmount)
        {
            BigInteger tokenA_Amount = BigInteger.Zero;
            BigInteger tokenB_Amount = BigInteger.Zero;

            if (_LpSettings.TokenA_Contract.Equals(_Settings.Farm.RewardContract, StringComparison.OrdinalIgnoreCase))
                tokenA_Amount = rewardHarvestAmt / 2;
            else
            {
                while (!HandleSwapRewardToToken(rewardHarvestAmt / 2, _LpSettings.TokenA_Contract, ref tokenA_Amount))
                {
                    currentProcessTxnCount++;
                    retryAttempt++;

                    if (retryAttempt > maxRetries || Program.IsTerminate) return;

                    Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, maxRetries);
                    Task.Delay(5000).Wait();

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount++;
                retryAttempt = 0;

                if (Program.IsTerminate) return;
            }

            if (_LpSettings.TokenB_Contract.Equals(_Settings.Farm.RewardContract, StringComparison.OrdinalIgnoreCase))
                tokenB_Amount = rewardHarvestAmt / 2;
            else
            {
                while (!HandleSwapRewardToToken(rewardHarvestAmt / 2, _LpSettings.TokenB_Contract, ref tokenB_Amount))
                {
                    currentProcessTxnCount++;
                    retryAttempt++;

                    if (retryAttempt > maxRetries || Program.IsTerminate) return;

                    Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, maxRetries);
                    Task.Delay(5000).Wait();

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount++;
                retryAttempt = 0;

                if (Program.IsTerminate) return;
            }

            if (_Settings.Farm.ProcessAllRewards)
            {
                BigInteger tokenA_Balance = _TokenA.GetBalanceTask(_Settings.Wallet.Address).Result;
                BigInteger tokenB_Balance = _TokenB.GetBalanceTask(_Settings.Wallet.Address).Result;

                BigInteger tokenB_Estimate =
                    GetAmountsOutTask(tokenA_Balance, _LpSettings.TokenA_Contract, _LpSettings.TokenB_Contract).Result;

                BigInteger tokenA_Estimate =
                    GetAmountsOutTask(tokenB_Balance, _LpSettings.TokenB_Contract, _LpSettings.TokenA_Contract).Result;

                if (tokenA_Estimate > tokenA_Balance)
                {
                    tokenA_Amount = tokenA_Balance;
                    tokenB_Amount = tokenB_Estimate;
                }
                else
                {
                    tokenA_Amount = tokenA_Estimate;
                    tokenB_Amount = tokenB_Balance;
                }
            }

            if (!string.IsNullOrWhiteSpace(_Settings.LiquidityPool.TaxFreeContract))
            {
                while (!_TokenA.HandleApproveSpend(
                    _Settings.LiquidityPool.TaxFreeContract,
                    _Settings.LiquidityPool.TokenA_Decimals,
                    tokenA_Amount))
                {
                    currentProcessTxnCount++;
                    retryAttempt++;

                    if (retryAttempt > maxRetries || Program.IsTerminate) return;

                    Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, maxRetries);
                    Task.Delay(5000).Wait();

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount++;
                retryAttempt = 0;

                if (Program.IsTerminate) return;

                while (!_TokenB.HandleApproveSpend(
                    _Settings.LiquidityPool.TaxFreeContract,
                    _Settings.LiquidityPool.TokenB_Decimals,
                    tokenB_Amount))
                {
                    currentProcessTxnCount++;
                    retryAttempt++;

                    if (retryAttempt > maxRetries || Program.IsTerminate) return;

                    Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, maxRetries);
                    Task.Delay(5000).Wait();

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount++;
                retryAttempt = 0;

                if (Program.IsTerminate) return;
            }

            while (!HandleAddTokensToLP(tokenA_Amount, tokenB_Amount, ref currentProcessTxnCount, ref lpAmount))
            {
                retryAttempt++;

                if (retryAttempt > maxRetries || Program.IsTerminate) return;

                Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, maxRetries);
                Task.Delay(5000).Wait();

                if (Program.IsTerminate) return;
            }
            retryAttempt = 0;
        }
    }
}
