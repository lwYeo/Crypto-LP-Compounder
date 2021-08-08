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
using Nethereum.Web3;
using System;
using System.Threading.Tasks;

namespace Crypto_LP_Compounder.Contract.UniswapV2
{
    internal class Factory
    {
        private readonly Log _Log;
        private readonly Settings _Settings;
        private readonly Web3 _Web3;
        private readonly ContractHandler _ContractHandler;

        public Factory(Log log, Settings settings, Web3 web3)
        {
            _Log = log;
            _Settings = settings;
            _Web3 = web3;
            _ContractHandler = _Web3.Eth.GetContractHandler(_Settings.LiquidityPool.FactoryContract);
        }

        public async Task<string> GetPairAddress()
        {
            DTO.Uniswap.V2Factory.GetPairFunction getPairFunction = new()
            {
                TokenA = _Settings.LiquidityPool.TokenA_Contract,
                TokenB = _Settings.LiquidityPool.TokenB_Contract,
                FromAddress = _Settings.Wallet.Address
            };
            return await _ContractHandler.
                QueryAsync<DTO.Uniswap.V2Factory.GetPairFunction, string>(getPairFunction);
        }

        public async Task HandleCheckLiquidityPool(string lpContract)
        {
            _Log.WriteLine();
            _Log.Write("Checking liquidity pair address... ");
            try
            {
                string pairAddress = await GetPairAddress();

                if (!pairAddress.Equals(lpContract, StringComparison.OrdinalIgnoreCase))
                {
                    _Log.WriteLine("Failed");
                    _Log.WriteLine($"Invalid LP address from token pair: {pairAddress}");
                    Program.ExitWithErrorCode(4);
                }

                _Log.WriteLine("Done");
            }
            catch (Exception ex)
            {
                _Log.WriteLine("Failed");
                _Log.WriteLine(ex.ToString());
                Program.ExitWithErrorCode(5);
            }
        }
    }
}
