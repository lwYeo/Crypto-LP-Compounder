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

namespace Crypto_LP_Compounder.DTO
{
    public class ERC20
    {
        [Function("totalSupply", "uint256")]
        public class TotalSupplyFunction : FunctionMessage
        {
        }

        [Function("balanceOf", "uint256")]
        public class BalanceOfFunction : FunctionMessage
        {
            [Parameter("address", "owner", 1)]
            public string Owner { get; set; }
        }

        [Function("allowance", "uint256")]
        public class AllowanceFunction : FunctionMessage
        {
            [Parameter("address", "owner", 1)]
            public string Owner { get; set; }

            [Parameter("address", "spender", 2)]
            public string Spender { get; set; }
        }

        [Function("approve", "bool")]
        public class ApproveFunction : FunctionMessage
        {
            [Parameter("address", "spender", 1)]
            public string Spender { get; set; }

            [Parameter("uint256", "value", 2)]
            public BigInteger Value { get; set; }
        }

        [Function("transfer", "bool")]
        public class TransferFunction : FunctionMessage
        {
            [Parameter("address", "recipient", 1)]
            public string Recipient { get; set; }

            [Parameter("uint256", "amount", 2)]
            public BigInteger Value { get; set; }
        }

        [Event("Transfer")]
        public class TransferEventDTO : IEventDTO
        {
            [Parameter("address", "from", 1, indexed: true)]
            public string From { get; set; }

            [Parameter("address", "to", 2, indexed: true)]
            public string To { get; set; }

            [Parameter("uint256", "value", 3)]
            public BigInteger Value { get; set; }
        }
    }
}
