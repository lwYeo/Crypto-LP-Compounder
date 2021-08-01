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

namespace Crypto_LP_Compounder.DTO.Uniswap
{
    public class V2Factory
    {
        [Function("getPair", "address")]
        public class GetPairFunction : FunctionMessage
        {
            [Parameter("address", "tokenA", 1)]
            public string TokenA { get; set; }

            [Parameter("address", "tokenB", 2)]
            public string TokenB { get; set; }
        }
    }
}
