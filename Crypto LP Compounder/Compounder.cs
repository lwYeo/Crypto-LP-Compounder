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

using DTO;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Crypto_LP_Compounder
{
    internal class Compounder : ICompounder
    {
        private const float DevFee = 1.0f;
        private const string DevAddress = "0x9172ff7884CEFED19327aDaCe9C470eF1796105c";
        private const string BurnAddress = "0x0000000000000000000000000000000000000000";
        private const uint MaxRetries = 20;

        private readonly Settings _Settings;
        private readonly Web3 _Web3;

        private readonly Contract.UniswapV2.Factory _Factory;
        private readonly Contract.UniswapV2.MlnlRouter _Router;
        private readonly Contract.Farm.MasterChef _Farm;
        private readonly Contract.ERC20 _RewardToken;

        private readonly uint DefaultTxnCountPerProcess;

        private BigInteger _LastEstimateGasCostPerTxn;
        private uint _LastProcessTxnCount;

        private DateTimeOffset _LastUpdate;
        private readonly ValueSymbol _EstimateGasPerTxn;

        string ICompounder.Name => _Settings.Name;

        DateTimeOffset ICompounder.LastUpdate =>
            new(_LastUpdate.Year,
                _LastUpdate.Month,
                _LastUpdate.Day,
                _LastUpdate.Hour,
                _LastUpdate.Minute,
                _LastUpdate.Second,
                _LastUpdate.Offset);

        string[] ICompounder.Summary
        {
            get
            {
                return new[] {
                    $"Current APR: {_Farm.CurrentAPR.Value.ToString(_Farm.CurrentAPR.Value < 1000 ? "n3" : "n0")} {_Farm.CurrentAPR.Symbol}" +
                    $" ({_Farm.CurrentDeposit.FiatValue.Value * (_Farm.CurrentAPR.Value / 100):n2} {_Farm.CurrentDeposit.FiatValue.Symbol} /" +
                    $" ({_Farm.CurrentDeposit.ChainValue.Value * (_Farm.CurrentAPR.Value / 100):n2} {_Farm.CurrentDeposit.ChainValue.Symbol})"
                    ,
                    $"Optimal APY: {_Farm.OptimalAPY.Value.ToString(_Farm.OptimalAPY.Value < 1000 ? "n3" : "n0")} {_Farm.OptimalAPY.Symbol}" +
                    $" ({_Farm.CurrentDeposit.FiatValue.Value * (_Farm.OptimalAPY.Value / 100):n2} {_Farm.CurrentDeposit.FiatValue.Symbol} /" +
                    $" ({_Farm.CurrentDeposit.ChainValue.Value * (_Farm.OptimalAPY.Value / 100):n2} {_Farm.CurrentDeposit.ChainValue.Symbol})" +
                    $" ({_Farm.OptimalCompoundsPerYear} compounds per year)"
                    ,
                    $"Pending reward value: {_Farm.CurrentPendingReward.Value.Value:n9} {_Farm.CurrentPendingReward.Value.Symbol}" +
                    $" ({_Farm.CurrentPendingReward.FiatValue.Value:n2} {_Farm.CurrentPendingReward.FiatValue.Symbol} /" +
                    $" {_Farm.CurrentPendingReward.ChainValue.Value:n9} {_Farm.CurrentPendingReward.ChainValue.Symbol})"
                    ,
                    $"Deposit value: {_Farm.CurrentDeposit.Value.Value:n9} {_Farm.CurrentDeposit.Value.Symbol}" +
                    $" ({_Farm.CurrentDeposit.FiatValue.Value:n2} {_Farm.CurrentDeposit.FiatValue.Symbol} /" +
                    $" {_Farm.CurrentDeposit.ChainValue.Value:n9} {_Farm.CurrentDeposit.ChainValue.Symbol})"
                    ,
                    $"Underlying Token A deposit value: {_Farm.UnderlyingTokenA_Deposit.Value.Value:n9} {_Farm.UnderlyingTokenA_Deposit.Value.Symbol}" +
                    $" ({_Farm.UnderlyingTokenA_Deposit.FiatValue.Value:n2} {_Farm.UnderlyingTokenA_Deposit.FiatValue.Symbol} /" +
                    $" {_Farm.UnderlyingTokenA_Deposit.ChainValue.Value:n9} {_Farm.UnderlyingTokenA_Deposit.ChainValue.Symbol})"
                    ,
                    $"Underlying Token B deposit value: {_Farm.UnderlyingTokenB_Deposit.Value.Value:n9} {_Farm.UnderlyingTokenB_Deposit.Value.Symbol}" +
                    $" ({_Farm.UnderlyingTokenB_Deposit.FiatValue.Value:n2} {_Farm.UnderlyingTokenB_Deposit.FiatValue.Symbol} /" +
                    $" {_Farm.UnderlyingTokenB_Deposit.ChainValue.Value:n9} {_Farm.UnderlyingTokenB_Deposit.ChainValue.Symbol})"
                    ,
                    $"Token A value: {_Farm.TokenA.FiatValue.Value:n2} {_Farm.TokenA.FiatValue.Symbol} /" +
                    $" {_Farm.TokenA.ChainValue.Value:n9} {_Farm.TokenA.ChainValue.Symbol}"
                    ,
                    $"Token B value: {_Farm.TokenB.FiatValue.Value:n2} {_Farm.TokenB.FiatValue.Symbol} /" +
                    $" {_Farm.TokenB.ChainValue.Value:n9} {_Farm.TokenB.ChainValue.Symbol}"
                };
            }
        }

        ValueSymbol ICompounder.CurrentAPR => _Farm.CurrentAPR;

        ValueSymbol ICompounder.OptimalAPY => _Farm.OptimalAPY;

        int ICompounder.OptimalCompoundsPerYear => _Farm.OptimalCompoundsPerYear;

        ValueSymbol ICompounder.EstimateGasPerTxn => _EstimateGasPerTxn;

        TokenValue ICompounder.CurrentDeposit => _Farm.CurrentDeposit;

        TokenValue ICompounder.UnderlyingTokenA_Deposit => _Farm.UnderlyingTokenA_Deposit;

        TokenValue ICompounder.UnderlyingTokenB_Deposit => _Farm.UnderlyingTokenB_Deposit;

        TokenValue ICompounder.CurrentPendingReward => _Farm.CurrentPendingReward;

        TokenValue ICompounder.TokenA => _Farm.TokenA;

        TokenValue ICompounder.TokenB => _Farm.TokenB;

        TokenValue ICompounder.Reward => _Farm.Reward;

        public Compounder(Settings settings)
        {
            _Settings = settings;

            if (!string.IsNullOrWhiteSpace(_Settings.LiquidityPool.ZapContract))
                DefaultTxnCountPerProcess = 12;
            else if (!string.IsNullOrWhiteSpace(_Settings.LiquidityPool.TaxFreeContract))
                DefaultTxnCountPerProcess = 9;
            else
                DefaultTxnCountPerProcess = 8;

            _LastProcessTxnCount = DefaultTxnCountPerProcess;
            _LastEstimateGasCostPerTxn = BigInteger.Zero;

            Program.CreateLineBreak();
            Program.WriteLineLog("Autocompounder settings");
            Program.CreateLineBreak();
            Program.WriteLineLog();

            float? fixedGasGwei = _Settings.FixedGasPriceGwei <= 0.0f ? null : _Settings.FixedGasPriceGwei;
            string sFixedGasGwei = fixedGasGwei?.ToString("#.# Gwei") ?? "auto";
            string zapContract =
                string.IsNullOrWhiteSpace(_Settings.LiquidityPool.ZapContract) ? "disabled" : _Settings.LiquidityPool.ZapContract;
            string taxFreeContract =
                string.IsNullOrWhiteSpace(_Settings.LiquidityPool.TaxFreeContract) ? "disabled" : _Settings.LiquidityPool.TaxFreeContract;

            Program.WriteLineLog("Name:              " + _Settings.Name);
            Program.WriteLineLog("WebApiURL:         " + _Settings.WebApiURL);
            Program.WriteLineLog("RPC URL:           " + _Settings.RPC_URL);
            Program.WriteLineLog("RPC_Timeout:       " + _Settings.RPC_Timeout.ToString() + " s");
            Program.WriteLineLog("GasPriceOffsetGwei:" + _Settings.GasPriceOffsetGwei.ToString() + " Gwei");
            Program.WriteLineLog("FixedGasPriceGwei: " + sFixedGasGwei);
            Program.WriteLineLog("MinGasPriceGwei:   " + _Settings.GetUserMinGasPrice().ToString() + " Gwei");
            Program.WriteLineLog("GasSymbol:         " + _Settings.GasSymbol);
            Program.WriteLineLog("WETH_Contract:     " + _Settings.WETH_Contract);
            Program.WriteLineLog("USD_Contract:      " + _Settings.USD_Contract);
            Program.WriteLineLog("USD_Decimals:      " + _Settings.USD_Decimals);
            Program.WriteLineLog("WalletAddress      " + _Settings.Wallet.Address);
            Program.WriteLineLog("TokenA_Contract:   " + _Settings.LiquidityPool.TokenA_Contract);
            Program.WriteLineLog("TokenA_Decimals:   " + _Settings.LiquidityPool.TokenA_Decimals);
            Program.WriteLineLog("TokenB_Contract:   " + _Settings.LiquidityPool.TokenB_Contract);
            Program.WriteLineLog("TokenB_Decimals:   " + _Settings.LiquidityPool.TokenB_Decimals);
            Program.WriteLineLog("LP_Contract:       " + _Settings.LiquidityPool.LP_Contract);
            Program.WriteLineLog("LP_Decimals:       " + _Settings.LiquidityPool.LP_Decimals);
            Program.WriteLineLog("FactoryContract:   " + _Settings.LiquidityPool.FactoryContract);
            Program.WriteLineLog("RouterContract:    " + _Settings.LiquidityPool.RouterContract);
            Program.WriteLineLog("Slippage:          " + _Settings.LiquidityPool.Slippage.ToString() + " %");
            Program.WriteLineLog("TokenA_Offset:     " + _Settings.LiquidityPool.TokenA_Offset.ToString() + " %");
            Program.WriteLineLog("TokenB_Offset:     " + _Settings.LiquidityPool.TokenB_Offset.ToString() + " %");
            Program.WriteLineLog("ZapContract:       " + zapContract);
            Program.WriteLineLog("TaxFreeContract:   " + taxFreeContract);
            Program.WriteLineLog("FarmType:          " + _Settings.Farm.FarmType);
            Program.WriteLineLog("FarmContract:      " + _Settings.Farm.FarmContract);
            Program.WriteLineLog("RewardContract:    " + _Settings.Farm.RewardContract);
            Program.WriteLineLog("PoolID:            " + _Settings.Farm.FarmPoolID.ToString());
            Program.WriteLineLog("ProcessAllRewards: " + _Settings.Farm.ProcessAllRewards.ToString());

            Account account = new(Crypto_Crypt.Factory.Instance.Decrypt(_Settings.Wallet.PrivateKeyCrypt));

            if (!account.Address.Equals(_Settings.Wallet.Address, StringComparison.OrdinalIgnoreCase))
                Program.ExitWithErrorMessage(3, "Invalid wallet private key");

            _Web3 = new(account, url: _Settings.RPC_URL);

            Nethereum.JsonRpc.Client.ClientBase.ConnectionTimeout = TimeSpan.FromSeconds(_Settings.RPC_Timeout);

            _EstimateGasPerTxn = new() { Symbol = _Settings.GasSymbol };

            _RewardToken = new(_Settings, _Web3, _Settings.Farm.RewardContract);

            _Factory = new(_Settings, _Web3);
            _Router = new(_Settings, _Web3);

            _Farm = _Settings.Farm.FarmType switch
            {
                "WFTM-TOMB:TSHARE" => new Contract.Farm.TombFinance(_Settings, _Web3, _Router, _RewardToken),
                "WFTM-YEL:YEL" => new Contract.Farm.YEL(_Settings, _Web3, _Router, _RewardToken),
                "WBNB-MOMA:MOMA" => new Contract.Farm.MOMA(_Settings, _Web3, _Router, _RewardToken),
                _ => null
            };

            if (_Farm == null)
                Program.ExitWithErrorMessage(4, "Invalid farm type!");

            _Factory.HandleCheckLiquidityPool(_Settings.LiquidityPool.LP_Contract).Wait();
        }

        public async Task Start()
        {
            int intervalSecond;
            TimeSpan intervalRemaining;
            DateTimeOffset beginLoopTime, nextLoopTime;
            DateTimeOffset lastUpdate = DateTimeOffset.Now;
            Stopwatch stopwatch = new();

            DateTimeOffset stateDateTime = DateTimeOffset.UnixEpoch;
            TimeSpan processTimeTaken = TimeSpan.MinValue;

            string statePath = System.IO.Path.Combine(AppContext.BaseDirectory, "state_" + _Settings.Name);
            byte[] stateBytes = System.IO.File.Exists(statePath) ? System.IO.File.ReadAllBytes(statePath) : null;

            if (stateBytes?.Length.Equals(sizeof(long) * 2) ?? false)
            {
                long state = BitConverter.ToInt64(stateBytes.Take(sizeof(long)).ToArray());
                stateDateTime = DateTimeOffset.FromUnixTimeSeconds(state);

                state = BitConverter.ToInt64(stateBytes.Skip(sizeof(long)).ToArray());
                processTimeTaken = TimeSpan.FromTicks(state);
            }

            while (!Program.IsTerminate)
            {
                if (stateDateTime > DateTimeOffset.UnixEpoch)
                {
                    beginLoopTime = stateDateTime;
                    stateDateTime = DateTimeOffset.UnixEpoch;
                }
                else
                {
                    Program.SetIsProcessingLog(true);

                    stopwatch.Restart();

                    await ProcessCompound();

                    processTimeTaken = stopwatch.Elapsed;

                    Program.WriteLineLog("Time taken: {0:n0} hr {1:mm' min 'ss' sec'}", processTimeTaken.TotalHours, processTimeTaken);
                    Program.CreateLineBreak();

                    beginLoopTime = DateTimeOffset.Now;

                    stateBytes =
                        BitConverter.GetBytes(beginLoopTime.ToUnixTimeSeconds()).
                        Concat(BitConverter.GetBytes(processTimeTaken.Ticks)).
                        ToArray();

                    _ = System.IO.File.WriteAllBytesAsync(statePath, stateBytes);

                    Program.SetIsProcessingLog(false);
                }

                nextLoopTime = beginLoopTime;
                intervalSecond = 0;

                stopwatch.Restart();

                while ((nextLoopTime == beginLoopTime || DateTimeOffset.Now < nextLoopTime) && !Program.IsTerminate)
                {
                    // Update interval every 5 minutes as gas fee fluctuates
                    if (stopwatch.Elapsed.Seconds < 1 && stopwatch.Elapsed.Minutes % 5 == 0)
                    {
                        if (nextLoopTime != beginLoopTime)
                        {
                            lastUpdate = DateTimeOffset.Now;

                            Program.WriteLineLog();
                            Program.CreateLineBreak();
                            Program.WriteLineLog(lastUpdate.ToString("yyyy-MM-dd T HH:mm:ss K"));
                        }

                        while (!EstimateGasCost(ref _LastEstimateGasCostPerTxn))
                        {
                            await Task.Delay(5000);

                            if (Program.IsTerminate) return;
                        }

                        while (!_Farm.CalculateOptimalApy(_LastEstimateGasCostPerTxn * _LastProcessTxnCount, ref intervalSecond))
                        {
                            await Task.Delay(5000);

                            if (Program.IsTerminate) return;
                        }

                        _LastUpdate = lastUpdate;

                        _EstimateGasPerTxn.Value =
                            (decimal)UnitConversion.Convert.FromWeiToBigDecimal(_LastEstimateGasCostPerTxn, UnitConversion.EthUnit.Ether);

                        intervalSecond -= (int)Math.Floor(processTimeTaken.TotalSeconds);

                        nextLoopTime = beginLoopTime.AddSeconds(intervalSecond);

                        intervalRemaining = nextLoopTime - DateTimeOffset.Now;

                        Program.WriteLineLog();
                        Program.WriteLineLog("Next compound in {0:n0} d {1:hh' hr 'mm' min 'ss' sec'} ({2:yyyy-MM-dd T HH:mm:ss K})",
                            intervalRemaining.TotalDays, intervalRemaining, DateTimeOffset.Now.Add(intervalRemaining));
                    }

                    await Task.Delay(1000);
                }
            }
        }

        private async Task ProcessCompound()
        {
            Program.WriteLineLog();
            Program.CreateLineBreak();
            Program.WriteLineLog(DateTimeOffset.Now.ToString("yyyy-MM-dd T HH:mm:ss K"));
            Program.WriteLineLog("Compound process starting...");
            Program.CreateLineBreak();

            BigInteger estimateGasCostPerTxn = BigInteger.Zero;
            BigInteger rewardHarvestAmt = BigInteger.Zero;
            BigInteger lpAmount = BigInteger.Zero;

            uint currentProcessTxnCount = 0;
            uint retryAttempt = 0;

            bool isToPostpone = false;
            bool isCompounded = false;
            try
            {
                while (!EstimateGasCost(ref estimateGasCostPerTxn))
                {
                    await Task.Delay(5000);

                    if (Program.IsTerminate) return;
                }

                if (_LastEstimateGasCostPerTxn.IsZero)
                    _LastEstimateGasCostPerTxn = estimateGasCostPerTxn;

                if (Program.IsTerminate) return;

                while (!_Farm.HandleCheckReward(_LastEstimateGasCostPerTxn * _LastProcessTxnCount, ref rewardHarvestAmt, ref isToPostpone))
                {
                    if (isToPostpone) currentProcessTxnCount = DefaultTxnCountPerProcess;

                    retryAttempt++;

                    if (retryAttempt > MaxRetries || Program.IsTerminate || isToPostpone) return;

                    Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, MaxRetries);
                    await Task.Delay(5000);

                    if (Program.IsTerminate) return;
                }
                retryAttempt = 0;

                if (Program.IsTerminate) return;

                while (!_Farm.HandleHarvest(ref rewardHarvestAmt))
                {
                    currentProcessTxnCount++;
                    retryAttempt++;

                    if (retryAttempt > MaxRetries || Program.IsTerminate || isToPostpone) return;

                    Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, MaxRetries);
                    await Task.Delay(5000);

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount++;
                retryAttempt = 0;

                if (Program.IsTerminate) return;

                while (!_RewardToken.HandleApproveSpend(
                    _Settings.LiquidityPool.RouterContract,
                    _Settings.Farm.RewardDecimals,
                    rewardHarvestAmt))
                {
                    currentProcessTxnCount++;
                    retryAttempt++;

                    if (retryAttempt > MaxRetries || Program.IsTerminate) return;

                    Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, MaxRetries);
                    await Task.Delay(5000);

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount++;
                retryAttempt = 0;

                if (Program.IsTerminate) return;

                if (!string.IsNullOrWhiteSpace(_Settings.LiquidityPool.ZapContract))
                {
                    while (!_RewardToken.HandleApproveSpend(
                        _Settings.LiquidityPool.ZapContract,
                        _Settings.Farm.RewardDecimals,
                        rewardHarvestAmt))
                    {
                        currentProcessTxnCount++;
                        retryAttempt++;

                        if (retryAttempt > MaxRetries || Program.IsTerminate) return;

                        Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, MaxRetries);
                        await Task.Delay(5000);

                        if (Program.IsTerminate) return;
                    }
                    currentProcessTxnCount++;
                    retryAttempt = 0;

                    if (Program.IsTerminate) return;
                }

                while (!CheckAndTopUpGas(_LastEstimateGasCostPerTxn * _LastProcessTxnCount, ref rewardHarvestAmt))
                {
                    currentProcessTxnCount++;
                    retryAttempt++;

                    if (retryAttempt > MaxRetries || Program.IsTerminate) return;

                    Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, MaxRetries);
                    await Task.Delay(5000);

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount++;
                retryAttempt = 0;

                if (Program.IsTerminate || rewardHarvestAmt <= BigInteger.One) return;

                while (!SendDevFee(ref rewardHarvestAmt))
                {
                    currentProcessTxnCount++;
                    retryAttempt++;

                    if (retryAttempt > MaxRetries || Program.IsTerminate) return;

                    Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, MaxRetries);
                    await Task.Delay(5000);

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount++;
                retryAttempt = 0;

                _Router.HandleSwapRewardToLP(
                    rewardHarvestAmt,
                    MaxRetries,
                    ref retryAttempt,
                    ref currentProcessTxnCount,
                    ref lpAmount);

                if (Program.IsTerminate) return;

                while (!_Farm.HandleDepositLpToFarm(lpAmount, ref isToPostpone))
                {
                    currentProcessTxnCount++;
                    retryAttempt++;

                    if (retryAttempt > MaxRetries || Program.IsTerminate || isToPostpone) return;

                    Program.WriteLineLog("Retrying... ({0}/{1})", retryAttempt, MaxRetries);
                    await Task.Delay(5000);

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount++;
                isCompounded = true;
            }
            catch (Exception ex)
            {
                Program.WriteLineLog(ex.ToString());
            }
            finally
            {
                Program.WriteLineLog();
                Program.CreateLineBreak();

                if (isCompounded)
                    Program.WriteLineLog("Compound process completed");
                else
                    Program.WriteLineLog("Compound process incomplete due to termination");

                _LastEstimateGasCostPerTxn = estimateGasCostPerTxn;
                _LastProcessTxnCount = currentProcessTxnCount;
            }
        }

        private bool EstimateGasCost(ref BigInteger gasCostPerTxn)
        {
            Program.WriteLineLog();
            Program.WriteLog("Estimating gas cost per txn... ");
            try
            {
                BigInteger gasPrice = _Settings.GetGasPrice(_Web3);

                BigInteger estimateGasPerTxn = _Router.SwapExactETHForRewardFunctionEstimateGasTask(gasPrice).Result;

                gasCostPerTxn = gasPrice * estimateGasPerTxn;

                if (gasCostPerTxn.IsZero)
                {
                    gasCostPerTxn = BigInteger.One;

                    Program.WriteLineLog();
                    Program.WriteLineLog("Failed: Estimate gas cost per txn");

                    return false;
                }
                else
                {
                    Program.WriteLineLog("{0:n10} {1}",
                        (decimal)UnitConversion.Convert.FromWeiToBigDecimal(gasCostPerTxn, UnitConversion.EthUnit.Ether),
                        _Settings.GasSymbol);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Program.WriteLineLog();
                Program.WriteLineLog("Failed: Estimate gas cost per txn");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
        }

        private bool CheckAndTopUpGas(BigInteger gasAmount, ref BigInteger rewardHarvestAmt)
        {
            Program.WriteLineLog();
            Program.WriteLog("Checking for remaining gas... ");
            try
            {
                Task<BigInteger> gasBalanceTask =
                    _Web3.Eth.GetBalance.SendRequestAsync(_Settings.Wallet.Address).ContinueWith(t => t.Result.Value);

                decimal balance = UnitConversion.Convert.FromWei(gasBalanceTask.Result, UnitConversion.EthUnit.Ether);

                Program.WriteLineLog("{0:n10} {1}", balance, _Settings.GasSymbol);
            }
            catch (Exception ex)
            {
                Program.WriteLineLog();
                Program.WriteLineLog("Failed: Check for remaining gas");
                Program.WriteLineLog(ex.ToString());

                return false;
            }

            Program.WriteLineLog("Topping up gas...");
            try
            {
                Task<BigInteger> rewardToSwap =
                    _Router.GetAmountsOutTask(gasAmount, _Settings.WETH_Contract, _Settings.Farm.RewardContract);

                BigInteger topUpGasAmtAftSlippageEth = gasAmount * (int)((100 - _Settings.LiquidityPool.Slippage) * 10) / 1000;

                BigInteger gasPrice = _Settings.GetGasPrice(_Web3);

                TransactionReceipt topUpGasReceipt =
                    _Router.SwapExactTokensForEthTask(
                        rewardToSwap.Result,
                        topUpGasAmtAftSlippageEth,
                        new[] { _Settings.Farm.RewardContract, _Settings.WETH_Contract }.ToList(),
                        gasPrice).
                    Result;

                while (topUpGasReceipt.Status == null)
                {
                    topUpGasReceipt =
                        _Web3.Eth.Transactions.GetTransactionReceipt.
                        SendRequestAsync(topUpGasReceipt.TransactionHash).
                        Result;
                }

                if (topUpGasReceipt.Failed())
                {
                    Program.WriteLineLog("Failed: Top up gas (gas: {0:n10} {1}, txn ID: {2})",
                        UnitConversion.Convert.FromWei(topUpGasReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                        _Settings.GasSymbol,
                        topUpGasReceipt.TransactionHash);

                    return false;
                }

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferEvents = _Web3.Eth.
                    GetEvent<DTO.ERC20.TransferEventDTO>().
                    DecodeAllEventsForEvent(topUpGasReceipt.Logs);

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferOutEvents = transferEvents.
                    Where(e => e.Event.From.Equals(_Settings.Wallet.Address, StringComparison.OrdinalIgnoreCase)).
                    ToList();

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferInEvents = transferEvents.
                    Where(e => e.Event.To.Equals(BurnAddress, StringComparison.OrdinalIgnoreCase)).
                    ToList();

                if (transferOutEvents.Any())
                    rewardHarvestAmt -= transferOutEvents.Select(l => l.Event.Value).Aggregate((currentSum, item) => currentSum + item);

                if (transferInEvents.Any())
                {
                    decimal ethAmount =
                        UnitConversion.Convert.FromWei(
                            transferInEvents.Select(l => l.Event.Value).Aggregate((currentSum, item) => currentSum + item),
                            UnitConversion.EthUnit.Ether);

                    decimal rewardSwapAmt =
                        (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(rewardToSwap.Result, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _Settings.Farm.RewardDecimals));

                    Program.WriteLineLog("Success: (gas: {0:n10} {1}, txn ID: {2})",
                        UnitConversion.Convert.FromWei(topUpGasReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                        _Settings.GasSymbol,
                        topUpGasReceipt.TransactionHash);

                    Program.WriteLineLog("Topped up gas of {0:n10} {1} with {2:n10} reward token",
                        ethAmount, _Settings.GasSymbol, rewardSwapAmt);

                    return true;
                }
                else
                {
                    Program.WriteLineLog("Failed: Top up gas, Transfer event not found");

                    return false;
                }
            }
            catch (Exception ex)
            {
                Program.WriteLineLog("Failed: Top up gas");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
        }

        private bool SendDevFee(ref BigInteger rewardHarvestAmt)
        {
            Program.WriteLineLog();
            Program.WriteLineLog("Sending {0:n1} % dev fee...", DevFee);
            try
            {
                BigInteger rewardToSend = rewardHarvestAmt * (int)(DevFee * 10) / 1000;

                BigInteger gasPrice = _Settings.GetGasPrice(_Web3);

                TransactionReceipt transferReceipt =
                    _RewardToken.Transfer(DevAddress, rewardToSend, gasPrice).Result;

                while (transferReceipt.Status == null)
                {
                    transferReceipt =
                        _Web3.Eth.Transactions.GetTransactionReceipt.
                        SendRequestAsync(transferReceipt.TransactionHash).
                        Result;
                }

                if (transferReceipt.Failed())
                {
                    Program.WriteLineLog("Failed: Send dev fee (gas: {0:n10} {1}, txn ID: {2})",
                        UnitConversion.Convert.FromWei(transferReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                        _Settings.GasSymbol,
                        transferReceipt.TransactionHash);

                    return false;
                }

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferEvents = _Web3.Eth.
                    GetEvent<DTO.ERC20.TransferEventDTO>().
                    DecodeAllEventsForEvent(transferReceipt.Logs);

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferOutEvents = transferEvents.
                    Where(e => e.Event.From.Equals(_Settings.Wallet.Address, StringComparison.OrdinalIgnoreCase)).
                    ToList();

                if (transferOutEvents.Any())
                {
                    BigInteger transferOutAmt = transferOutEvents.Select(l => l.Event.Value).Aggregate((currentSum, item) => currentSum + item);

                    rewardHarvestAmt -= transferOutAmt;

                    Program.WriteLineLog("Success: Sent {0:n10} reward as {1:n1} % dev fee",
                        (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(transferOutAmt, UnitConversion.EthUnit.Wei) /
                            BigDecimal.Pow(10, _Settings.Farm.RewardDecimals)),
                        DevFee);

                    Program.WriteLineLog("(gas: {0:n10} {1}, txn ID: {2})",
                        UnitConversion.Convert.FromWei(transferReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                        _Settings.GasSymbol,
                        transferReceipt.TransactionHash);

                    return true;
                }
                else
                {
                    Program.WriteLineLog("Failed: Send dev fee, Transfer event not found");

                    return false;
                }
            }
            catch (Exception ex)
            {
                Program.WriteLineLog("Failed: Send dev fee");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
        }
    }
}
