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
using System.Diagnostics;
using System.Numerics;

namespace Crypto_LP_Compounder
{
    internal class Compounder : ICompounder, ISummary
    {
        private const float DevFee = 1.0f;
        private const string DevAddress = "0x9172ff7884CEFED19327aDaCe9C470eF1796105c";
        private const string BurnAddress = "0x0000000000000000000000000000000000000000";
        private const uint MaxRetries = 20;

        private readonly Settings.CompounderSettings _Settings;
        private readonly Web3 _Web3;

        private readonly Contract.UniswapV2.Factory _Factory;
        private readonly Contract.UniswapV2.MlnlRouter _Router;
        private readonly Contract.Farm.MasterChef _Farm;
        private readonly Contract.ERC20 _RewardToken;

        private readonly uint DefaultTxnCountPerProcess;

        private BigInteger _LastEstimateGasCostPerTxn;
        private uint _LastProcessTxnCount;

        private DateTimeOffset _LastCompoundDateTime, _LastUpdate, _NextLoopDateTime;
        private TimeSpan _LastProcessTimeTaken;

        private readonly ValueSymbol _EstimateGasPerTxn;

        #region ICompounder

        string ICompounder.InstanceName => _Settings.Name;

        DateTimeOffset ICompounder.LastUpdate =>
            new(_LastUpdate.Year,
                _LastUpdate.Month,
                _LastUpdate.Day,
                _LastUpdate.Hour,
                _LastUpdate.Minute,
                _LastUpdate.Second,
                _LastUpdate.Offset);

        ValueSymbol ICompounder.CurrentAPR => _Farm.CurrentAPR;

        ValueSymbol ICompounder.OptimalAPY => _Farm.OptimalAPY;

        int ICompounder.OptimalCompoundsPerYear => _Farm.OptimalCompoundsPerYear;

        DateTimeOffset ICompounder.LastCompoundDateTime =>
            new(_LastCompoundDateTime.Year,
                _LastCompoundDateTime.Month,
                _LastCompoundDateTime.Day,
                _LastCompoundDateTime.Hour,
                _LastCompoundDateTime.Minute,
                _LastCompoundDateTime.Second,
                _LastCompoundDateTime.Offset);

        TimeSpan ICompounder.LastCompoundProcessDuration => TimeSpan.FromSeconds(Math.Round(_LastProcessTimeTaken.TotalSeconds));

        DateTimeOffset ICompounder.NextEstimateCompoundDateTime =>
            new(_NextLoopDateTime.Year,
                _NextLoopDateTime.Month,
                _NextLoopDateTime.Day,
                _NextLoopDateTime.Hour,
                _NextLoopDateTime.Minute,
                _NextLoopDateTime.Second,
                _NextLoopDateTime.Offset);

        ValueSymbol ICompounder.EstimateGasPerTxn => _EstimateGasPerTxn;

        TokenValue ICompounder.CurrentDeposit => _Farm.CurrentDeposit;

        TokenValue ICompounder.UnderlyingTokenA_Deposit => _Farm.UnderlyingTokenA_Deposit;

        TokenValue ICompounder.UnderlyingTokenB_Deposit => _Farm.UnderlyingTokenB_Deposit;

        TokenValue ICompounder.CurrentPendingReward => _Farm.CurrentPendingReward;

        SingleTokenValue ICompounder.TokenA => _Farm.TokenA;

        SingleTokenValue ICompounder.TokenB => _Farm.TokenB;

        SingleTokenValue ICompounder.Reward => _Farm.Reward;

        string[] ICompounder.GetRecentAllLogs() => Log?.ReadRecentLogFile();

        string[] ICompounder.GetRecentProcessLogs() => Log?.ReadRecentProcessLogFile();

        #endregion

        #region ISummary

        string ISummary.InstanceName => _Settings.Name;

        DateTimeOffset ISummary.LastUpdate =>
            new(_LastUpdate.Year,
                _LastUpdate.Month,
                _LastUpdate.Day,
                _LastUpdate.Hour,
                _LastUpdate.Minute,
                _LastUpdate.Second,
                _LastUpdate.Offset);

        string ISummary.CurrentAPR =>
            $"{_Farm.CurrentAPR.Value.ToString(_Farm.CurrentAPR.Value < 1000 ? "n3" : "n0")} {_Farm.CurrentAPR.Symbol}" +
            $" ({_Farm.CurrentDeposit.FiatValue.Value * (_Farm.CurrentAPR.Value / 100):n2} {_Farm.CurrentDeposit.FiatValue.Symbol} /" +
            $" {_Farm.CurrentDeposit.ChainValue.Value * (_Farm.CurrentAPR.Value / 100):n9} {_Farm.CurrentDeposit.ChainValue.Symbol})";

        string ISummary.OptimalAPY =>
            $"{_Farm.OptimalAPY.Value.ToString(_Farm.OptimalAPY.Value < 1000 ? "n3" : "n0")} {_Farm.OptimalAPY.Symbol}" +
            $" ({_Farm.CurrentDeposit.FiatValue.Value * (_Farm.OptimalAPY.Value / 100):n2} {_Farm.CurrentDeposit.FiatValue.Symbol} /" +
            $" {_Farm.CurrentDeposit.ChainValue.Value * (_Farm.OptimalAPY.Value / 100):n9} {_Farm.CurrentDeposit.ChainValue.Symbol})";

        string ISummary.OptimalCompoundsPerYear => _Farm.OptimalCompoundsPerYear.ToString("n0");

        DateTimeOffset ISummary.LastCompoundDateTime =>
            new(_LastCompoundDateTime.Year,
                _LastCompoundDateTime.Month,
                _LastCompoundDateTime.Day,
                _LastCompoundDateTime.Hour,
                _LastCompoundDateTime.Minute,
                _LastCompoundDateTime.Second,
                _LastCompoundDateTime.Offset);

        string ISummary.NextOptimalCompoundIn =>
            $"{(_NextLoopDateTime - DateTimeOffset.Now).TotalDays:n0} d" +
            $" {_NextLoopDateTime - DateTimeOffset.Now:hh' hr 'mm' min 'ss' sec'}";

        DateTimeOffset ISummary.NextOptimalCompoundDateTime =>
            new(_NextLoopDateTime.Year,
                _NextLoopDateTime.Month,
                _NextLoopDateTime.Day,
                _NextLoopDateTime.Hour,
                _NextLoopDateTime.Minute,
                _NextLoopDateTime.Second,
                _NextLoopDateTime.Offset);

        string ISummary.TotalLiquidity =>
            $"{_Farm.CurrentDeposit.FiatValue.Value + _Farm.CurrentPendingReward.FiatValue.Value:n2} {_Farm.CurrentDeposit.FiatValue.Symbol} /" +
            $" {_Farm.CurrentDeposit.ChainValue.Value + _Farm.CurrentPendingReward.ChainValue.Value:n9} {_Farm.CurrentDeposit.ChainValue.Symbol}";

        string ISummary.PendingReward =>
            $"{_Farm.CurrentPendingReward.Value.Value:n9} {_Farm.CurrentPendingReward.Value.Symbol}" +
            $" ({_Farm.CurrentPendingReward.FiatValue.Value:n2} {_Farm.CurrentPendingReward.FiatValue.Symbol} /" +
            $" {_Farm.CurrentPendingReward.ChainValue.Value:n9} {_Farm.CurrentPendingReward.ChainValue.Symbol})";

        string ISummary.CurrentDeposit =>
            $"{_Farm.CurrentDeposit.Value.Value:n9} {_Farm.CurrentDeposit.Value.Symbol}" +
            $" ({_Farm.CurrentDeposit.FiatValue.Value:n2} {_Farm.CurrentDeposit.FiatValue.Symbol} /" +
            $" {_Farm.CurrentDeposit.ChainValue.Value:n9} {_Farm.CurrentDeposit.ChainValue.Symbol})";

        string ISummary.UnderlyingTokenA_Deposit =>
            $"{_Farm.UnderlyingTokenA_Deposit.Value.Value:n9} {_Farm.UnderlyingTokenA_Deposit.Value.Symbol}" +
            $" ({_Farm.UnderlyingTokenA_Deposit.FiatValue.Value:n2} {_Farm.UnderlyingTokenA_Deposit.FiatValue.Symbol} /" +
            $" {_Farm.UnderlyingTokenA_Deposit.ChainValue.Value:n9} {_Farm.UnderlyingTokenA_Deposit.ChainValue.Symbol})";

        string ISummary.UnderlyingTokenB_Deposit =>
            $"{_Farm.UnderlyingTokenB_Deposit.Value.Value:n9} {_Farm.UnderlyingTokenB_Deposit.Value.Symbol}" +
            $" ({_Farm.UnderlyingTokenB_Deposit.FiatValue.Value:n2} {_Farm.UnderlyingTokenB_Deposit.FiatValue.Symbol} /" +
            $" {_Farm.UnderlyingTokenB_Deposit.ChainValue.Value:n9} {_Farm.UnderlyingTokenB_Deposit.ChainValue.Symbol})";

        string ISummary.RewardValue =>
            $"{_Farm.Reward.Symbol}:" +
            $" {_Farm.Reward.FiatValue.Value:n2} {_Farm.Reward.FiatValue.Symbol} /" +
            $" {_Farm.Reward.ChainValue.Value:n9} {_Farm.Reward.ChainValue.Symbol}";

        string ISummary.TokenA_Value =>
            $"{_Farm.TokenA.Symbol}:" +
            $" {_Farm.TokenA.FiatValue.Value:n2} {_Farm.TokenA.FiatValue.Symbol} /" +
            $" {_Farm.TokenA.ChainValue.Value:n9} {_Farm.TokenA.ChainValue.Symbol}";

        string ISummary.TokenB_Value =>
            $"{_Farm.TokenB.Symbol}:" +
            $" {_Farm.TokenB.FiatValue.Value:n2} {_Farm.TokenB.FiatValue.Symbol} /" +
            $" {_Farm.TokenB.ChainValue.Value:n9} {_Farm.TokenB.ChainValue.Symbol}";

        #endregion

        public Log Log { get; }

        public Compounder(Settings.CompounderSettings settings)
        {
            _LastProcessTimeTaken = TimeSpan.MinValue;
            _LastCompoundDateTime = DateTimeOffset.UnixEpoch;
            _NextLoopDateTime = DateTimeOffset.UnixEpoch;
            _Settings = settings;

            Log = new(settings);

            if (!string.IsNullOrWhiteSpace(_Settings.LiquidityPool.ZapContract))
                DefaultTxnCountPerProcess = 12;
            else if (!string.IsNullOrWhiteSpace(_Settings.LiquidityPool.TaxFreeContract))
                DefaultTxnCountPerProcess = 9;
            else
                DefaultTxnCountPerProcess = 8;

            _LastProcessTxnCount = DefaultTxnCountPerProcess;
            _LastEstimateGasCostPerTxn = BigInteger.Zero;

            Log.WriteLine();
            Log.WriteLineBreak();
            Log.WriteLine("Autocompounder settings");
            Log.WriteLineBreak();
            Log.WriteLine();

            float? fixedGasGwei = _Settings.FixedGasPriceGwei <= 0.0f ? null : _Settings.FixedGasPriceGwei;
            string sFixedGasGwei = fixedGasGwei?.ToString("#.# Gwei") ?? "auto";
            string zapContract =
                string.IsNullOrWhiteSpace(_Settings.LiquidityPool.ZapContract) ? "disabled" : _Settings.LiquidityPool.ZapContract;
            string taxFreeContract =
                string.IsNullOrWhiteSpace(_Settings.LiquidityPool.TaxFreeContract) ? "disabled" : _Settings.LiquidityPool.TaxFreeContract;

            Log.WriteLine("Name:               " + _Settings.Name);
            Log.WriteLine("IsLogAll:           " + _Settings.IsLogAll.ToString());
            Log.WriteLine("DeleteLogsAfterDays:" + _Settings.DeleteLogsAfterDays.ToString());
            Log.WriteLine("RPC URL:            " + _Settings.RPC_URL);
            Log.WriteLine("RPC_Timeout:        " + _Settings.RPC_Timeout.ToString() + " s");
            Log.WriteLine("RPC_ChainID:        " + _Settings.RPC_ChainID?.ToString() ?? "null");
            Log.WriteLine("GasPriceOffsetGwei: " + _Settings.GasPriceOffsetGwei.ToString() + " Gwei");
            Log.WriteLine("FixedGasPriceGwei:  " + sFixedGasGwei);
            Log.WriteLine("MinGasPriceGwei:    " + _Settings.GetUserMinGasPrice().ToString() + " Gwei");
            Log.WriteLine("GasSymbol:          " + _Settings.GasSymbol);
            Log.WriteLine("WETH_Contract:      " + _Settings.WETH_Contract);
            Log.WriteLine("USD_Contract:       " + _Settings.USD_Contract);
            Log.WriteLine("USD_Decimals:       " + _Settings.USD_Decimals);
            Log.WriteLine("WalletAddress       " + _Settings.Wallet.Address);
            Log.WriteLine("TokenA_Contract:    " + _Settings.LiquidityPool.TokenA_Contract);
            Log.WriteLine("TokenA_Decimals:    " + _Settings.LiquidityPool.TokenA_Decimals);
            Log.WriteLine("TokenB_Contract:    " + _Settings.LiquidityPool.TokenB_Contract);
            Log.WriteLine("TokenB_Decimals:    " + _Settings.LiquidityPool.TokenB_Decimals);
            Log.WriteLine("LP_Contract:        " + _Settings.LiquidityPool.LP_Contract);
            Log.WriteLine("LP_Decimals:        " + _Settings.LiquidityPool.LP_Decimals);
            Log.WriteLine("FactoryContract:    " + _Settings.LiquidityPool.FactoryContract);
            Log.WriteLine("RouterContract:     " + _Settings.LiquidityPool.RouterContract);
            Log.WriteLine("Slippage:           " + _Settings.LiquidityPool.Slippage.ToString() + " %");
            Log.WriteLine("TokenA_Offset:      " + _Settings.LiquidityPool.TokenA_Offset.ToString() + " %");
            Log.WriteLine("TokenB_Offset:      " + _Settings.LiquidityPool.TokenB_Offset.ToString() + " %");
            Log.WriteLine("ZapContract:        " + zapContract);
            Log.WriteLine("TaxFreeContract:    " + taxFreeContract);
            Log.WriteLine("FarmType:           " + _Settings.Farm.FarmType);
            Log.WriteLine("FarmContract:       " + _Settings.Farm.FarmContract);
            Log.WriteLine("RewardContract:     " + _Settings.Farm.RewardContract);
            Log.WriteLine("PoolID:             " + _Settings.Farm.FarmPoolID.ToString());
            Log.WriteLine("ProcessAllRewards:  " + _Settings.Farm.ProcessAllRewards.ToString());

            Account account = new(Crypto_Crypt.Factory.Instance.Decrypt(_Settings.Wallet.PrivateKeyCrypt), _Settings.RPC_ChainID);

            if (!account.Address.Equals(_Settings.Wallet.Address, StringComparison.OrdinalIgnoreCase))
            {
                Log.WriteLine("Invalid wallet private key");
                Program.ExitWithErrorCode(3);
            }

            _Web3 = new(account, url: _Settings.RPC_URL);

            Nethereum.JsonRpc.Client.ClientBase.ConnectionTimeout = TimeSpan.FromSeconds(_Settings.RPC_Timeout);

            _EstimateGasPerTxn = new() { Symbol = _Settings.GasSymbol };

            _RewardToken = new(Log, _Settings, _Web3, _Settings.Farm.RewardContract);

            _Factory = new(Log, _Settings, _Web3);
            _Router = new(Log, _Settings, _Web3);

            _Farm = _Settings.Farm.FarmType switch
            {
                "WFTM-TOMB_TSHARE" => new Contract.Farm.TombFinance(Log, _Settings, _Web3, _Router, _RewardToken),
                "WFTM-YEL_YEL" => new Contract.Farm.YEL(Log, _Settings, _Web3, _Router, _RewardToken),
                "WBNB-MOMA_MOMA" => new Contract.Farm.MOMA(Log, _Settings, _Web3, _Router, _RewardToken),
                _ => null
            };

            if (_Farm == null)
            {
                Log.WriteLine("Invalid farm type!");
                Program.ExitWithErrorCode(4);
            }

            _Factory.HandleCheckLiquidityPool(_Settings.LiquidityPool.LP_Contract).Wait();
        }

        public async Task Start()
        {
            int intervalSecond;
            DateTimeOffset lastUpdate = DateTimeOffset.Now;
            Stopwatch stopwatch = new();

            DateTimeOffset stateDateTime = DateTimeOffset.UnixEpoch;

            string statePath = Path.Combine(AppContext.BaseDirectory, "state_" + _Settings.Name);
            byte[] stateBytes = File.Exists(statePath) ? File.ReadAllBytes(statePath) : null;

            if (stateBytes?.Length.Equals(sizeof(long) * 2) ?? false)
            {
                long state = BitConverter.ToInt64(stateBytes.Take(sizeof(long)).ToArray());
                stateDateTime = DateTimeOffset.FromUnixTimeSeconds(state).ToOffset(DateTimeOffset.Now.Offset);

                state = BitConverter.ToInt64(stateBytes.Skip(sizeof(long)).ToArray());
                _LastProcessTimeTaken = TimeSpan.FromTicks(state);
            }

            while (!Program.IsTerminate)
            {
                if (stateDateTime > DateTimeOffset.UnixEpoch)
                {
                    _LastCompoundDateTime = stateDateTime;
                    stateDateTime = DateTimeOffset.UnixEpoch;
                }
                else
                {
                    Log.IsCompoundProcess = true;

                    stopwatch.Restart();
                    _LastCompoundDateTime = DateTimeOffset.Now;

                    await ProcessCompound();

                    _LastProcessTimeTaken = stopwatch.Elapsed;

                    Log.WriteLine($"Time taken: {_LastProcessTimeTaken.TotalHours:n0} hr {_LastProcessTimeTaken:mm' min 'ss' sec'}");
                    Log.WriteLineBreak();

                    stateBytes =
                        BitConverter.GetBytes(_LastCompoundDateTime.ToUnixTimeSeconds()).
                        Concat(BitConverter.GetBytes(_LastProcessTimeTaken.Ticks)).
                        ToArray();

                    _ = File.WriteAllBytesAsync(statePath, stateBytes);

                    Log.IsCompoundProcess = false;
                }

                _NextLoopDateTime = _LastCompoundDateTime;
                intervalSecond = 0;

                stopwatch.Restart();

                while ((_NextLoopDateTime == _LastCompoundDateTime || DateTimeOffset.Now < _NextLoopDateTime) && !Program.IsTerminate)
                {
                    // Update interval every 5 minutes as gas fee fluctuates
                    if (stopwatch.Elapsed.Seconds < 1 && stopwatch.Elapsed.Minutes % 5 == 0)
                    {
                        if (_NextLoopDateTime != _LastCompoundDateTime)
                        {
                            lastUpdate = DateTimeOffset.Now;

                            Log.WriteLine();
                            Log.WriteLineBreak();
                            Log.WriteLine(lastUpdate.ToString("yyyy-MM-dd T HH:mm:ss K"));
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

                        intervalSecond -= (int)Math.Floor(_LastProcessTimeTaken.TotalSeconds);

                        _NextLoopDateTime = _LastCompoundDateTime.AddSeconds(intervalSecond);

                        Log.WriteLine();
                        Log.WriteLine(
                            $"Next compound in {(_NextLoopDateTime - DateTimeOffset.Now).TotalDays:n0} d" +
                            $" {_NextLoopDateTime - DateTimeOffset.Now:hh' hr 'mm' min 'ss' sec'}" +
                            $" ({_NextLoopDateTime:yyyy-MM-dd T HH:mm:ss K})");
                    }

                    await Task.Delay(1000);
                }
            }
        }

        private async Task ProcessCompound()
        {
            Log.WriteLine();
            Log.WriteLineBreak();
            Log.WriteLine(DateTimeOffset.Now.ToString("yyyy-MM-dd T HH:mm:ss K"));
            Log.WriteLine("Compound process starting...");
            Log.WriteLineBreak();

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

                    Log.WriteLine($"Retrying... ({retryAttempt}/{MaxRetries})");
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

                    Log.WriteLine($"Retrying... ({retryAttempt}/{MaxRetries})");
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

                    Log.WriteLine($"Retrying... ({retryAttempt}/{MaxRetries})");
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

                        Log.WriteLine($"Retrying... ({retryAttempt}/{MaxRetries})");
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

                    Log.WriteLine($"Retrying... ({retryAttempt}/{MaxRetries})");
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

                    Log.WriteLine($"Retrying... ({retryAttempt}/{MaxRetries})");
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

                    Log.WriteLine($"Retrying... ({retryAttempt}/{MaxRetries})");
                    await Task.Delay(5000);

                    if (Program.IsTerminate) return;
                }
                currentProcessTxnCount++;
                isCompounded = true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(ex.ToString());
            }
            finally
            {
                Log.WriteLine();
                Log.WriteLineBreak();

                if (isCompounded)
                    Log.WriteLine("Compound process completed");
                else
                    Log.WriteLine("Compound process incomplete due to termination");

                _LastEstimateGasCostPerTxn = estimateGasCostPerTxn;
                _LastProcessTxnCount = currentProcessTxnCount;
            }
        }

        private bool EstimateGasCost(ref BigInteger gasCostPerTxn)
        {
            Log.WriteLine();
            Log.Write("Estimating gas cost per txn... ");
            try
            {
                BigInteger gasPrice = _Settings.GetGasPrice(_Web3);

                BigInteger estimateGasPerTxn = _Router.SwapExactETHForRewardFunctionEstimateGasTask(gasPrice).Result;

                gasCostPerTxn = gasPrice * estimateGasPerTxn;

                if (gasCostPerTxn.IsZero)
                {
                    gasCostPerTxn = BigInteger.One;

                    Log.WriteLine();
                    Log.WriteLine("Failed: Estimate gas cost per txn");

                    return false;
                }
                else
                {
                    Log.WriteLine(
                        $"{(decimal)UnitConversion.Convert.FromWeiToBigDecimal(gasCostPerTxn, UnitConversion.EthUnit.Ether):n10}" +
                        $" {_Settings.GasSymbol}");

                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine();
                Log.WriteLine("Failed: Estimate gas cost per txn");
                Log.WriteLine(ex.ToString());

                return false;
            }
        }

        private bool CheckAndTopUpGas(BigInteger gasAmount, ref BigInteger rewardHarvestAmt)
        {
            Log.WriteLine();
            Log.Write("Checking for remaining gas... ");
            try
            {
                Task<BigInteger> gasBalanceTask =
                    _Web3.Eth.GetBalance.SendRequestAsync(_Settings.Wallet.Address).ContinueWith(t => t.Result.Value);

                decimal balance = UnitConversion.Convert.FromWei(gasBalanceTask.Result, UnitConversion.EthUnit.Ether);

                Log.WriteLine($"{balance:n10} {_Settings.GasSymbol}");
            }
            catch (Exception ex)
            {
                Log.WriteLine();
                Log.WriteLine("Failed: Check for remaining gas");
                Log.WriteLine(ex.ToString());

                return false;
            }

            Log.WriteLine("Topping up gas...");
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
                    Log.WriteLine(
                        $"Failed: Top up gas" +
                        $" (gas: {UnitConversion.Convert.FromWei(topUpGasReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether):n10}" +
                        $" {_Settings.GasSymbol}, txn ID: {topUpGasReceipt.TransactionHash})");

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

                    Log.WriteLine(
                        $"Success: (gas: {UnitConversion.Convert.FromWei(topUpGasReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether):n10}" +
                        $" {_Settings.GasSymbol}, txn ID: {topUpGasReceipt.TransactionHash})");

                    Log.WriteLine(
                        $"Topped up gas of {ethAmount:n10} {_Settings.GasSymbol} with {rewardSwapAmt:n10} reward token");

                    return true;
                }
                else
                {
                    Log.WriteLine("Failed: Top up gas, Transfer event not found");

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine("Failed: Top up gas");
                Log.WriteLine(ex.ToString());

                return false;
            }
        }

        private bool SendDevFee(ref BigInteger rewardHarvestAmt)
        {
            Log.WriteLine();
            Log.WriteLine($"Sending {DevFee:n1} % dev fee...");
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
                    Log.WriteLine(
                        $"Failed: Send dev fee" +
                        $" (gas: {UnitConversion.Convert.FromWei(transferReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether):n10}" +
                        $" {_Settings.GasSymbol}, txn ID: {transferReceipt.TransactionHash})");

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

                    BigDecimal transferOutRewards =
                        UnitConversion.Convert.FromWeiToBigDecimal(transferOutAmt, UnitConversion.EthUnit.Wei) / BigDecimal.Pow(10, _Settings.Farm.RewardDecimals);

                    Log.WriteLine(
                        $"Success: Sent {(decimal)transferOutRewards:n10}" +
                        $" reward as {DevFee:n1} % dev fee");

                    Log.WriteLine(
                        $"(gas: {UnitConversion.Convert.FromWei(transferReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether):n10}" +
                        $" {_Settings.GasSymbol}, txn ID: {transferReceipt.TransactionHash})");

                    return true;
                }
                else
                {
                    Log.WriteLine("Failed: Send dev fee, Transfer event not found");

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine("Failed: Send dev fee");
                Log.WriteLine(ex.ToString());

                return false;
            }
        }
    }
}
