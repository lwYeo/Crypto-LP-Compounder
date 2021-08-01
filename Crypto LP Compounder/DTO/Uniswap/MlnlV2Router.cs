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
using System.Numerics;

namespace Crypto_LP_Compounder.DTO.Uniswap
{
    public class MlnlV2Router : V2Router
    {
        [Function("zapInToken")]
        public class ZapInTokenFunction : FunctionMessage
        {
            [Parameter("address", "_from", 1)]
            public string From { get; set; }

            [Parameter("uint256", "amount", 2)]
            public BigInteger Amount { get; set; }

            [Parameter("address", "_to", 3)]
            public string To { get; set; }

            [Parameter("address", "routerAddr", 4)]
            public string RouterAddr { get; set; }

            [Parameter("address", "_recipient", 5)]
            public string Recipient { get; set; }
        }
    }
}
