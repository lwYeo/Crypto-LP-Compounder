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
using Nethereum.Contracts.ContractHandlers;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Crypto_LP_Compounder.Contract.Farm
{
    internal abstract class MasterChef
    {
        protected readonly Settings _Settings;
        protected readonly Web3 _Web3;
        protected readonly ERC20 _RewardToken;
        protected readonly ERC20 _TokenA;
        protected readonly ERC20 _TokenB;
        protected readonly ERC20 _LiquidityToken;
        protected readonly ContractHandler _ContractHandler;
        protected readonly UniswapV2.Router _Router;

        public MasterChef(Settings settings, Web3 web3, UniswapV2.Router router, ERC20 rewardToken)
        {
            _Settings = settings;
            _Web3 = web3;
            _Router = router;
            _RewardToken = rewardToken;
            _TokenA = new(_Settings, _Web3, _Settings.LiquidityPool.TokenA_Contract);
            _TokenB = new(_Settings, _Web3, _Settings.LiquidityPool.TokenB_Contract);
            _LiquidityToken = new(_Settings, _Web3, _Settings.LiquidityPool.LP_Contract);
            _ContractHandler = _Web3.Eth.GetContractHandler(_Settings.Farm.FarmContract);

            CurrentAPR = new() { Symbol = "%" };
            OptimalAPY = new() { Symbol = "%" };

            CurrentDeposit = new();
            SetTokenSymbol(CurrentDeposit, "LP");

            UnderlyingTokenA_Deposit = new();
            SetTokenSymbol(UnderlyingTokenA_Deposit, _Settings.Farm.FarmType.Split(':')[0].Split('-')[0]);

            UnderlyingTokenB_Deposit = new();
            SetTokenSymbol(UnderlyingTokenB_Deposit, _Settings.Farm.FarmType.Split(':')[0].Split('-')[1]);

            CurrentPendingReward = new();
            SetTokenSymbol(CurrentPendingReward, _Settings.Farm.FarmType.Split(':')[1]);

            TokenA = new();
            TokenA.Value.Value = 1;
            SetTokenSymbol(TokenA, _Settings.Farm.FarmType.Split(':')[0].Split('-')[0]);

            TokenB = new();
            TokenB.Value.Value = 1;
            SetTokenSymbol(TokenB, _Settings.Farm.FarmType.Split(':')[0].Split('-')[1]);

            Reward = new();
            Reward.Value.Value = 1;
            SetTokenSymbol(Reward, _Settings.Farm.FarmType.Split(':')[1]);
        }

        public ValueSymbol CurrentAPR { get; private set; }

        public ValueSymbol OptimalAPY { get; private set; }

        public int OptimalCompoundsPerYear { get; private set; }

        public TokenValue CurrentDeposit { get; private set; }

        public TokenValue UnderlyingTokenA_Deposit { get; private set; }

        public TokenValue UnderlyingTokenB_Deposit { get; private set; }

        public TokenValue CurrentPendingReward { get; private set; }

        public TokenValue TokenA { get; private set; }

        public TokenValue TokenB { get; private set; }

        public TokenValue Reward { get; private set; }

        private void SetTokenSymbol(TokenValue token, string valueSymbol)
        {
            token.FiatValue.Symbol = "USD";
            token.ChainValue.Symbol = _Settings.GasSymbol;
            token.Value.Symbol = valueSymbol;
        }

        public abstract Task<BigInteger> GetRewardPerSecondTask();

        public abstract Task<BigInteger> GetPendingRewardTask();

        public abstract Task<BigInteger> GetAllocPointTask();

        public abstract Task<BigInteger> GetTotalAllocPointTask();

        public abstract Task<TransactionReceipt> DepositTask(BigInteger inAmount, BigInteger gasPrice);

        public abstract Task<TransactionReceipt> HarvestTask(BigInteger gasPrice);

        public virtual async Task<DTO.Farm.MasterChef.UserInfoFunctionOutputDTO> GetUserInfoTask()
        {
            DTO.Farm.MasterChef.UserInfoFunction userInfoFunction = new()
            {
                PoolID = _Settings.Farm.FarmPoolID,
                User = _Settings.Wallet.Address,
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryDeserializingToObjectAsync<DTO.Farm.MasterChef.UserInfoFunction, DTO.Farm.MasterChef.UserInfoFunctionOutputDTO>(userInfoFunction);
        }

        public bool HandleDepositLpToFarm(BigInteger lpAmount, ref bool isToPostpone)
        {
            Program.WriteLineLog();

            if (_Settings.Farm.ProcessAllRewards)
                lpAmount = _LiquidityToken.GetBalanceTask(_Settings.Wallet.Address).Result;

            if (lpAmount.IsZero)
            {
                Program.WriteLineLog("LP balance is zero, postpone deposit to farm");

                isToPostpone = true;

                return false;
            }

            Program.WriteLineLog("Depositing {0:n10} LP tokens to farm...",
                (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(lpAmount, UnitConversion.EthUnit.Wei) /
                    BigDecimal.Pow(10, _Settings.LiquidityPool.LP_Decimals)));
            try
            {
                BigInteger gasPrice = _Settings.GetGasPrice(_Web3);

                TransactionReceipt depositLpReceipt = DepositTask(lpAmount, gasPrice).Result;

                while (depositLpReceipt.Status == null)
                {
                    depositLpReceipt =
                        _Web3.Eth.Transactions.GetTransactionReceipt.
                        SendRequestAsync(depositLpReceipt.TransactionHash).
                        Result;
                }

                if (depositLpReceipt.Failed())
                {
                    Program.WriteLineLog("Failed: Deposit LP tokens (gas: {0:n10} {1}, txn ID: {2})",
                        UnitConversion.Convert.FromWei(depositLpReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                        _Settings.GasSymbol,
                        depositLpReceipt.TransactionHash);

                    return false;
                }

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferEvents = _Web3.Eth.
                    GetEvent<DTO.ERC20.TransferEventDTO>().
                    DecodeAllEventsForEvent(depositLpReceipt.Logs);

                List<EventLog<DTO.ERC20.TransferEventDTO>> transferOutEvents = transferEvents.
                    Where(e => e.Event.From.Equals(_Settings.Wallet.Address, StringComparison.OrdinalIgnoreCase)).
                    ToList();

                lpAmount = transferOutEvents.Select(l => l.Event.Value).Aggregate((currentSum, item) => currentSum + item);

                Program.WriteLineLog("Success: Deposit {0:n10} LP tokens (gas: {1:n10} {2}, txn ID: {3})",
                    (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(lpAmount, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _Settings.LiquidityPool.LP_Decimals)),
                    UnitConversion.Convert.FromWei(depositLpReceipt.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                    _Settings.GasSymbol,
                    depositLpReceipt.TransactionHash);

                return true;
            }
            catch (AggregateException ex)
            {
                Program.WriteLineLog("Failed: Deposit LP tokens to farm");

                if (ex.InnerExceptions.Any(e => e is TaskCanceledException))
                    Program.WriteLineLog("Timeout: {0:n0} s", _Settings.RPC_Timeout);
                else
                    Program.WriteLineLog(ex.ToString());

                return false;
            }
            catch (Exception ex)
            {
                Program.WriteLineLog("Failed: Deposit LP tokens to farm");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
        }

        public bool HandleHarvest(ref BigInteger rewardHarvestAmt)
        {
            Program.WriteLineLog();
            Program.WriteLineLog("Harvesting reward...");
            try
            {
                rewardHarvestAmt = BigInteger.Zero;

                List<EventLog<DTO.ERC20.TransferEventDTO>> harvestLogs = null;

                BigInteger gasPrice = _Settings.GetGasPrice(_Web3);

                TransactionReceipt harvestResult = HarvestTask(gasPrice).Result;

                while (harvestResult.Status == null)
                {
                    harvestResult =
                        _Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(harvestResult.TransactionHash).Result;
                }

                if (harvestResult.Succeeded())
                {
                    harvestLogs = _Web3.Eth.
                        GetEvent<DTO.ERC20.TransferEventDTO>().
                        DecodeAllEventsForEvent(harvestResult.Logs);

                    Program.WriteLineLog("Success: Harvest reward (gas: {0:n10} {1}, txn ID: {2})",
                        UnitConversion.Convert.FromWei(harvestResult.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                        _Settings.GasSymbol,
                        harvestResult.TransactionHash);
                }
                else
                {
                    Program.WriteLineLog("Failed: Harvest reward, gas: {0:n10} {1}, txn ID: {2})",
                        UnitConversion.Convert.FromWei(harvestResult.GasUsed * gasPrice, UnitConversion.EthUnit.Ether),
                        _Settings.GasSymbol,
                        harvestResult.TransactionHash);

                    return false;
                }

                Task<BigInteger> rewardBalanceTask = _RewardToken.GetBalanceTask(_Settings.Wallet.Address);

                rewardHarvestAmt = harvestLogs.Select(l => l.Event.Value).Aggregate((currentSum, item) => currentSum + item);

                Program.WriteLineLog("Reward harvested/balance: {0:n10} / {1:n10}",
                    (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(rewardHarvestAmt, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _Settings.Farm.RewardDecimals)),
                    (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(rewardBalanceTask.Result, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _Settings.Farm.RewardDecimals)));

                if (rewardHarvestAmt.IsZero || rewardHarvestAmt > rewardBalanceTask.Result) return false;

                if (_Settings.Farm.ProcessAllRewards) rewardHarvestAmt = rewardBalanceTask.Result;

                return true;
            }
            catch (AggregateException ex)
            {
                Program.WriteLineLog("Failed: Harvesting reward");

                if (ex.InnerExceptions.Any(e => e is TaskCanceledException))
                    Program.WriteLineLog("Timeout: {0:n0} s", _Settings.RPC_Timeout);
                else
                    Program.WriteLineLog(ex.ToString());

                return false;
            }
            catch (Exception ex)
            {
                Program.WriteLineLog("Failed: Harvesting reward");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
        }

        public bool HandleCheckReward(BigInteger estimateGasUse, ref BigInteger rewardHarvestAmt, ref bool isPostpone)
        {
            Program.WriteLineLog();
            Program.WriteLog("Getting farm reward count... ");
            try
            {
                rewardHarvestAmt = GetPendingRewardTask().Result;

                BigInteger rewardInGas =
                    _Router.GetAmountsOutTask(rewardHarvestAmt, _Settings.Farm.RewardContract, _Settings.WETH_Contract).Result;

                Program.WriteLineLog("{0:n10} ({1:n10} {2})",
                    (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(rewardHarvestAmt, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _Settings.Farm.RewardDecimals)),
                    (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(rewardInGas, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _Settings.Farm.RewardDecimals)),
                    _Settings.GasSymbol);

                if (rewardHarvestAmt.IsZero || ((rewardInGas - estimateGasUse) <= BigInteger.One))
                {
                    Program.WriteLineLog();
                    Program.WriteLineLog("Gas is greater then reward, postponing...");

                    isPostpone = true;

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Program.WriteLineLog();
                Program.WriteLineLog("Failed: Get farm reward count");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
        }

        public bool CalculateOptimalApy(BigInteger gasCostAmount, ref int compoundIntervalSecond)
        {
            Task<BigDecimal>
                ethToUsdTask,
                rewardValueEth,
                farmAllocWeightTask,
                userDepositSizeTask,
                userDepositAmtEthTask,
                valuePerLpEthTask,
                tokenValueAEthTask,
                tokenValueBEthTask,
                valuePerLpOffsetEthTask;

            Program.WriteLineLog();
            Program.WriteLineLog("Getting token values...");
            try
            {
                if (string.IsNullOrWhiteSpace(_Settings.USD_Contract))
                    ethToUsdTask = Task.Run(() => (BigDecimal)0);
                else
                    ethToUsdTask = _Router.
                        GetAmountsOutTask(
                            BigInteger.Pow(10, 18),
                            _Settings.WETH_Contract,
                            _Settings.USD_Contract).
                        ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Wei) /
                            BigDecimal.Pow(10, _Settings.USD_Decimals));

                rewardValueEth = _Router.
                    GetAmountsOutTask(
                        BigInteger.Pow(10, (int)_Settings.Farm.RewardDecimals),
                        _Settings.Farm.RewardContract,
                        _Settings.WETH_Contract).
                    ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Ether));

                if (_TokenA.Address.Equals(_Settings.WETH_Contract, StringComparison.OrdinalIgnoreCase))
                    tokenValueAEthTask = Task.Run(() => (BigDecimal)1);
                else
                    tokenValueAEthTask = _Router.
                        GetAmountsOutTask(
                            BigInteger.Pow(10, (int)_Settings.LiquidityPool.TokenA_Decimals),
                            _Settings.LiquidityPool.TokenA_Contract,
                            _Settings.WETH_Contract).
                        ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Ether));

                if (_TokenB.Address.Equals(_Settings.WETH_Contract, StringComparison.OrdinalIgnoreCase))
                    tokenValueBEthTask = Task.Run(() => (BigDecimal)1);
                else
                    tokenValueBEthTask = _Router.
                        GetAmountsOutTask(
                            BigInteger.Pow(10, (int)_Settings.LiquidityPool.TokenB_Decimals),
                            _Settings.LiquidityPool.TokenB_Contract,
                            _Settings.WETH_Contract).
                        ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Ether));

                Reward.ChainValue.Value = (decimal)rewardValueEth.Result;
                Reward.FiatValue.Value = (decimal)(rewardValueEth.Result * ethToUsdTask.Result);

                Program.WriteLineLog("Reward value: {0:n2} USD / {1:n10} {2}",
                    Reward.FiatValue.Value,
                    Reward.ChainValue.Value,
                    _Settings.GasSymbol);

                TokenA.ChainValue.Value = (decimal)tokenValueAEthTask.Result;
                TokenA.FiatValue.Value = (decimal)(tokenValueAEthTask.Result * ethToUsdTask.Result);

                Program.WriteLineLog("Token A value: {0:n2} USD / {1:n10} {2}",
                    TokenA.FiatValue.Value,
                    TokenA.ChainValue.Value,
                    _Settings.GasSymbol);

                TokenB.ChainValue.Value = (decimal)tokenValueBEthTask.Result;
                TokenB.FiatValue.Value = (decimal)(tokenValueBEthTask.Result * ethToUsdTask.Result);

                Program.WriteLineLog("Token B value: {0:n2} USD / {1:n10} {2}",
                    TokenB.FiatValue.Value,
                    TokenB.ChainValue.Value,
                    _Settings.GasSymbol);

                Program.WriteLog("Getting pending rewards... ");

                Task<BigDecimal> pendingReward =
                    GetPendingRewardTask().
                    ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Wei) /
                        BigDecimal.Pow(10, _Settings.Farm.RewardDecimals));

                CurrentPendingReward.Value.Value = (decimal)pendingReward.Result;
                CurrentPendingReward.ChainValue.Value = (decimal)(pendingReward.Result * rewardValueEth.Result);
                CurrentPendingReward.FiatValue.Value = (decimal)(pendingReward.Result * rewardValueEth.Result * ethToUsdTask.Result);

                Program.WriteLineLog("{0:n10} ({1:n2} USD / {2:n10} {3})",
                    CurrentPendingReward.Value.Value,
                    CurrentPendingReward.FiatValue.Value,
                    CurrentPendingReward.ChainValue.Value,
                    _Settings.GasSymbol);
            }
            catch (Exception ex)
            {
                Program.WriteLineLog();
                Program.WriteLineLog("Failed: Get token values");
                Program.WriteLineLog(ex.ToString());

                return false;
            }

            Program.WriteLog("Getting deposit value... ");
            try
            {
                userDepositSizeTask = GetUserInfoTask().
                    ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result.Amount, UnitConversion.EthUnit.Wei));

                Task<BigDecimal> tokenAInLPEthTask = _TokenA.
                    GetBalanceTask(_Settings.LiquidityPool.LP_Contract).
                    ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Wei) *
                        BigDecimal.Pow(10, _Settings.LiquidityPool.TokenA_Decimals)).
                    ContinueWith(t => t.Result * tokenValueAEthTask.Result);

                Task<BigDecimal> tokenBInLPEthTask = _TokenB.
                    GetBalanceTask(_Settings.LiquidityPool.LP_Contract).
                    ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Wei) *
                        BigDecimal.Pow(10, _Settings.LiquidityPool.TokenB_Decimals)).
                    ContinueWith(t => t.Result * tokenValueBEthTask.Result);

                Task<BigDecimal> totalLpSupplyTask = _LiquidityToken.
                    TotalSupplyTask().
                    ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Wei) *
                        BigDecimal.Pow(10, _Settings.LiquidityPool.LP_Decimals));

                valuePerLpEthTask = Task.Run(() => (tokenAInLPEthTask.Result + tokenBInLPEthTask.Result) / totalLpSupplyTask.Result);

                userDepositAmtEthTask =
                    Task.Run(() => userDepositSizeTask.Result / BigDecimal.Pow(10, _Settings.LiquidityPool.LP_Decimals) * valuePerLpEthTask.Result);

                valuePerLpOffsetEthTask =
                    Task.Run(() =>
                    {
                        BigDecimal tokenAOffsetEth =
                            tokenAInLPEthTask.Result + (tokenAInLPEthTask.Result * _Settings.LiquidityPool.TokenA_Offset / 100);

                        BigDecimal tokenBOffsetEth =
                            tokenBInLPEthTask.Result + (tokenBInLPEthTask.Result * _Settings.LiquidityPool.TokenB_Offset / 100);

                        return (tokenAOffsetEth + tokenBOffsetEth) / totalLpSupplyTask.Result;
                    });

                CurrentDeposit.Value.Value = (decimal)(userDepositSizeTask.Result / BigDecimal.Pow(10, _Settings.LiquidityPool.LP_Decimals));
                CurrentDeposit.ChainValue.Value = (decimal)userDepositAmtEthTask.Result;
                CurrentDeposit.FiatValue.Value = (decimal)(userDepositAmtEthTask.Result * ethToUsdTask.Result);

                Program.WriteLineLog("{0:n10} ({1:n2} USD / {2:n10} {3})",
                    CurrentDeposit.Value.Value,
                    CurrentDeposit.FiatValue.Value,
                    CurrentDeposit.ChainValue.Value,
                    _Settings.GasSymbol);

                BigDecimal underlyingTokenAEth =
                    (userDepositAmtEthTask.Result * tokenAInLPEthTask.Result / (tokenAInLPEthTask.Result + tokenBInLPEthTask.Result));

                BigDecimal underlyingTokenBEth =
                    (userDepositAmtEthTask.Result * tokenBInLPEthTask.Result / (tokenAInLPEthTask.Result + tokenBInLPEthTask.Result));

                UnderlyingTokenA_Deposit.Value.Value = (decimal)(underlyingTokenAEth / tokenValueAEthTask.Result);
                UnderlyingTokenA_Deposit.ChainValue.Value = (decimal)underlyingTokenAEth;
                UnderlyingTokenA_Deposit.FiatValue.Value = (decimal)(underlyingTokenAEth * ethToUsdTask.Result);

                Program.WriteLineLog("Underlying Token A value: {0:n10} ({1:n2} USD / {2:n10} {3})",
                    UnderlyingTokenA_Deposit.Value.Value,
                    UnderlyingTokenA_Deposit.FiatValue.Value,
                    UnderlyingTokenA_Deposit.ChainValue.Value,
                    _Settings.GasSymbol);

                UnderlyingTokenB_Deposit.Value.Value = (decimal)(underlyingTokenBEth / tokenValueBEthTask.Result);
                UnderlyingTokenB_Deposit.ChainValue.Value = (decimal)underlyingTokenBEth;
                UnderlyingTokenB_Deposit.FiatValue.Value = (decimal)(underlyingTokenBEth * ethToUsdTask.Result);

                Program.WriteLineLog("Underlying Token B value: {0:n10} ({1:n2} USD / {2:n10} {3})",
                    UnderlyingTokenB_Deposit.Value.Value,
                    UnderlyingTokenB_Deposit.FiatValue.Value,
                    UnderlyingTokenB_Deposit.ChainValue.Value,
                    _Settings.GasSymbol);
            }
            catch (Exception ex)
            {
                Program.WriteLineLog();
                Program.WriteLineLog("Failed: Get deposit value");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
            Program.WriteLog("Calculating APR... ");
            BigDecimal apr = 0;
            try
            {
                Task<BigDecimal> lpHoldingInFarmTask = _LiquidityToken.
                    GetBalanceTask(_Settings.Farm.FarmContract).
                    ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Wei) *
                        BigDecimal.Pow(10, _Settings.LiquidityPool.LP_Decimals)).
                    ContinueWith(t => t.Result * valuePerLpEthTask.Result);

                Task<BigDecimal> allocPointTask =
                    GetAllocPointTask().
                    ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Ether));

                Task<BigDecimal> totalAllocPointTask =
                    GetTotalAllocPointTask().
                    ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Ether));

                farmAllocWeightTask = Task.Run(() => allocPointTask.Result / totalAllocPointTask.Result);

                Task<BigDecimal> rewardPerYearTask =
                    GetRewardPerSecondTask().
                    ContinueWith(t => UnitConversion.Convert.FromWeiToBigDecimal(t.Result, UnitConversion.EthUnit.Wei) *
                        BigDecimal.Pow(10, _Settings.Farm.RewardDecimals)).
                    ContinueWith(t => t.Result * 60 * 60 * 24 * 365);

                Task<BigDecimal> farmWeightRewardPerYearTask =
                    Task.Run(() => farmAllocWeightTask.Result * rewardPerYearTask.Result);

                BigDecimal offset = valuePerLpOffsetEthTask.Result / valuePerLpEthTask.Result;

                apr = farmWeightRewardPerYearTask.Result / lpHoldingInFarmTask.Result * 100 * rewardValueEth.Result * offset;
                CurrentAPR.Value = (decimal)apr;

                if (apr > 0)
                {
                    Program.WriteLineLog("{0:n3} % ({1:n2} USD / {2:n10} {3})",
                        (decimal)apr,
                        (decimal)(userDepositAmtEthTask.Result * ethToUsdTask.Result * apr / 100),
                        (decimal)(userDepositAmtEthTask.Result * apr / 100),
                        _Settings.GasSymbol);
                }
                else
                {
                    Program.WriteLineLog();
                    Program.WriteLineLog("Failed: Calculate APR");

                    return false;
                }
            }
            catch (Exception ex)
            {
                Program.WriteLineLog();
                Program.WriteLineLog("Failed: Calculate APR");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
            Program.WriteLog("Calculating optimal APY... ");
            try
            {
                decimal optimalApy = 0;

                int compoundPerYear = 0;
                while (true)
                {
                    decimal tempApy = 100 * (decimal)(Math.Pow(1 + (double)apr / 100 / (compoundPerYear + 1), compoundPerYear + 1) - 1);
                    decimal pPerCompound = (decimal)userDepositAmtEthTask.Result * tempApy / 100 / (compoundPerYear + 1);

                    pPerCompound -=
                        (decimal)(UnitConversion.Convert.FromWeiToBigDecimal(gasCostAmount, UnitConversion.EthUnit.Ether) * (compoundPerYear + 1));

                    tempApy = pPerCompound / (decimal)userDepositAmtEthTask.Result * 100 * (compoundPerYear + 1);

                    if (tempApy <= optimalApy) break;

                    compoundPerYear++;
                    optimalApy = tempApy;
                }

                if (compoundPerYear > 0)
                {
                    OptimalAPY.Value = optimalApy;
                    OptimalCompoundsPerYear = compoundPerYear;

                    compoundIntervalSecond = 60 * 60 * 24 * 365 / compoundPerYear;

                    BigDecimal optimalEthPerYr = userDepositAmtEthTask.Result * optimalApy / 100;
                    BigDecimal optimalUsdPerYr = optimalEthPerYr * ethToUsdTask.Result;

                    Program.WriteLineLog(
                        (optimalApy < 1000 ? "{0:n2}" : "{0:n0}") +
                            " % ({1:n0} compounds / " +
                            (optimalUsdPerYr < 1000 ? "{2:n2}" : "{2:n0}") +
                            " USD / " +
                            (optimalEthPerYr < 1000 ? "{3:n10}" : "{3:n0}") +
                            " {4} per year)",
                        optimalApy,
                        compoundPerYear,
                        (decimal)optimalUsdPerYr,
                        (decimal)optimalEthPerYr,
                        _Settings.GasSymbol);
                }
                else
                {
                    OptimalAPY.Value = 0;
                    OptimalCompoundsPerYear = 0;

                    Program.WriteLineLog("Failed: Calculate optimal APY");

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Program.WriteLineLog();
                Program.WriteLineLog("Failed: Calculate optimal APY");
                Program.WriteLineLog(ex.ToString());

                return false;
            }
        }
    }
}
