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

using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Collections.Generic;
using System.Numerics;

namespace Crypto_LP_Compounder.DTO.Uniswap
{
    public class V2Router
    {
        [FunctionOutput]
        public class AddLiquidityOutputDTO : IFunctionOutputDTO
        {
            [Parameter("uint256", "amountA", 1)]
            public BigInteger AmountA { get; set; }

            [Parameter("uint256", "amountB", 2)]
            public BigInteger AmountB { get; set; }

            [Parameter("uint256", "liquidity", 3)]
            public BigInteger Liquidity { get; set; }
        }

        [Function("addLiquidity", typeof(AddLiquidityOutputDTO))]
        public class AddLiquidityFunction : FunctionMessage
        {
            [Parameter("address", "tokenA", 1)]
            public string TokenA { get; set; }

            [Parameter("address", "tokenB", 2)]
            public string TokenB { get; set; }

            [Parameter("uint256", "amountADesired", 3)]
            public BigInteger AmountADesired { get; set; }

            [Parameter("uint256", "amountBDesired", 4)]
            public BigInteger AmountBDesired { get; set; }

            [Parameter("uint256", "amountAMin", 5)]
            public BigInteger AmountAMin { get; set; }

            [Parameter("uint256", "amountBMin", 6)]
            public BigInteger AmountBMin { get; set; }

            [Parameter("address", "to", 7)]
            public string To { get; set; }

            [Parameter("uint256", "deadline", 8)]
            public BigInteger Deadline { get; set; }
        }

        [Function("swapExactETHForTokens", "uint256[]")]
        public class SwapExactETHForTokensFunction : FunctionMessage
        {
            [Parameter("uint256", "amountOutMin", 1)]
            public BigInteger AmountOutMin { get; set; }

            [Parameter("address[]", "path", 2)]
            public List<string> Path { get; set; }

            [Parameter("address", "to", 3)]
            public string To { get; set; }

            [Parameter("uint256", "deadline", 4)]
            public BigInteger Deadline { get; set; }
        }

        [Function("swapExactTokensForETH", "uint256[]")]
        public class SwapExactTokensForETHFunction : FunctionMessage
        {
            [Parameter("uint256", "amountIn", 1)]
            public BigInteger AmountIn { get; set; }

            [Parameter("uint256", "amountOutMin", 2)]
            public BigInteger AmountOutMin { get; set; }

            [Parameter("address[]", "path", 3)]
            public List<string> Path { get; set; }

            [Parameter("address", "to", 4)]
            public string To { get; set; }

            [Parameter("uint256", "deadline", 5)]
            public BigInteger Deadline { get; set; }
        }

        [Function("swapExactTokensForTokens", "uint256[]")]
        public class SwapExactTokensForTokensFunction : FunctionMessage
        {
            [Parameter("uint256", "amountIn", 1)]
            public BigInteger AmountIn { get; set; }

            [Parameter("uint256", "amountOutMin", 2)]
            public BigInteger AmountOutMin { get; set; }

            [Parameter("address[]", "path", 3)]
            public List<string> Path { get; set; }

            [Parameter("address", "to", 4)]
            public string To { get; set; }

            [Parameter("uint256", "deadline", 5)]
            public BigInteger Deadline { get; set; }
        }

        [Function("getAmountsOut", "uint256[]")]
        public class GetAmountsOutFunction : FunctionMessage
        {
            [Parameter("uint256", "amountIn", 1)]
            public BigInteger AmountIn { get; set; }

            [Parameter("address[]", "path", 2)]
            public List<string> Path { get; set; }
        }
    }
}
