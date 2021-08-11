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

using Nethereum.Contracts.ContractHandlers;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using System.Numerics;

namespace Crypto_LP_Compounder.Contract
{
    internal class ERC20
    {
        private readonly Log _Log;
        private readonly Settings.CompounderSettings _Settings;
        private readonly Web3 _Web3;
        private readonly ContractHandler _ContractHandler;

        public string Address { get; }

        public ERC20(Log log, Settings.CompounderSettings settings, Web3 web3, string tokenAddress)
        {
            _Log = log;
            Address = tokenAddress;
            _Settings = settings;
            _Web3 = web3;
            _ContractHandler = _Web3.Eth.GetContractHandler(tokenAddress);
        }

        public async Task<BigInteger> TotalSupplyTask()
        {
            DTO.ERC20.TotalSupplyFunction totalSupplyFunction = new()
            {
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.ERC20.TotalSupplyFunction, BigInteger>(totalSupplyFunction);
        }

        public async Task<BigInteger> GetBalanceTask(string ownerAddress)
        {
            DTO.ERC20.BalanceOfFunction balanceOfFunction = new()
            {
                Owner = ownerAddress,
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.ERC20.BalanceOfFunction, BigInteger>(balanceOfFunction);
        }

        public async Task<BigInteger> GetAllowanceTask(string spendingContract)
        {
            DTO.ERC20.AllowanceFunction allowanceFunction = new()
            {
                Owner = _Settings.Wallet.Address,
                Spender = spendingContract,
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.ERC20.AllowanceFunction, BigInteger>(allowanceFunction);
        }

        public async Task<TransactionReceipt> ApproveSpend(string contract, BigInteger amount, BigInteger gasPrice)
        {
            DTO.ERC20.ApproveFunction approveFunction = new()
            {
                Spender = contract,
                Value = amount,
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _ContractHandler.
                SendRequestAndWaitForReceiptAsync(approveFunction);
        }

        public async Task<TransactionReceipt> Transfer(string address, BigInteger amount, BigInteger gasPrice)
        {
            DTO.ERC20.TransferFunction transferFunction = new()
            {
                Recipient = address,
                Value = amount,
                FromAddress = _Settings.Wallet.Address,
                GasPrice = gasPrice
            };
            return await _ContractHandler.
                SendRequestAndWaitForReceiptAsync(transferFunction);
        }

        public bool HandleApproveSpend(string contract, uint decimals, BigInteger amount)
        {
            BigInteger approveAmt = amount * 2; // Allow edge cases
            BigInteger currentApproveAmt = GetAllowanceTask(contract).Result;

            if (currentApproveAmt > approveAmt) return true;
            try
            {
                approveAmt = amount * 5; // Minimize future unnecessary approvals

                _Log.WriteLine(
                    $"Approving spend of" +
                    $" {(decimal)(UnitConversion.Convert.FromWeiToBigDecimal(approveAmt, UnitConversion.EthUnit.Wei) / BigDecimal.Pow(10, decimals)):n10}" +
                    $" to {contract}...");

                BigInteger gasPrice = _Settings.GetGasPrice(_Web3);

                TransactionReceipt approveResult = ApproveSpend(contract, approveAmt, gasPrice).Result;

                while (approveResult.Status == null)
                {
                    approveResult =
                        _Web3.Eth.Transactions.GetTransactionReceipt.
                        SendRequestAsync(approveResult.TransactionHash).Result;
                }

                if (approveResult.Succeeded())
                {
                    _Log.WriteLine(
                        $"Success: Approve spend" +
                        $" (gas: {UnitConversion.Convert.FromWei(approveResult.GasUsed * gasPrice, UnitConversion.EthUnit.Ether):n10}" +
                        $" {_Settings.GasSymbol}, txn ID: {approveResult.TransactionHash})");

                    return true;
                }
                else
                {
                    _Log.WriteLine(
                        $"Failed: Approve spend," +
                        $" gas: {UnitConversion.Convert.FromWei(approveResult.GasUsed * gasPrice, UnitConversion.EthUnit.Ether):n10}" +
                        $" {_Settings.GasSymbol}, txn ID: {approveResult.TransactionHash}");

                    if (approveResult.HasLogs())
                        _Log.WriteLine(approveResult.Logs.ToString());

                    return false;
                }
            }
            catch (Exception ex)
            {
                _Log.WriteLine("Failed: Approve spend rewards");
                _Log.WriteLine(ex.ToString());

                return false;
            }
        }
    }
}
