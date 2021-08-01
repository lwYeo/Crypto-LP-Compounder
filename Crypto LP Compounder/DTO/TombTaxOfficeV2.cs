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
    public class TombTaxOfficeV2
    {
        [FunctionOutput]
        public class AddLiquidityTaxFreeOutputDTO : IFunctionOutputDTO
        {
            [Parameter("uint256", "", 1)]
            public BigInteger ResultAmtTomb { get; set; }

            [Parameter("uint256", "", 2)]
            public BigInteger ResultAmtToken { get; set; }

            [Parameter("uint256", "", 3)]
            public BigInteger Liquidity { get; set; }
        }

        [FunctionOutput]
        public class AddLiquidityETHTaxFreeOutputDTO : IFunctionOutputDTO
        {
            [Parameter("uint256", "", 1)]
            public BigInteger ResultAmtTomb { get; set; }

            [Parameter("uint256", "", 2)]
            public BigInteger ResultAmtFtm { get; set; }

            [Parameter("uint256", "", 3)]
            public BigInteger Liquidity { get; set; }
        }

        [Function("addLiquidityTaxFree", typeof(AddLiquidityTaxFreeOutputDTO))]
        public class AddLiquidityTaxFreeFunction : FunctionMessage
        {
            [Parameter("address", "token", 1)]
            public string Token { get; set; }

            [Parameter("uint256", "amtTomb", 2)]
            public BigInteger AmtTomb { get; set; }

            [Parameter("uint256", "amtToken", 3)]
            public BigInteger AmtToken { get; set; }

            [Parameter("uint256", "amtTombMin", 4)]
            public BigInteger AmtTombMin { get; set; }

            [Parameter("uint256", "amtTokenMin", 5)]
            public BigInteger AmtTokenMin { get; set; }
        }

        [Function("addLiquidityETHTaxFree", typeof(AddLiquidityETHTaxFreeOutputDTO))]
        public class AddLiquidityETHTaxFreeFunction : FunctionMessage
        {
            [Parameter("uint256", "amtTomb", 1)]
            public BigInteger AmtTomb { get; set; }

            [Parameter("uint256", "amtTombMin", 2)]
            public BigInteger AmtTombMin { get; set; }

            [Parameter("uint256", "amtFtmMin", 3)]
            public BigInteger AmtFtmMin { get; set; }
        }
    }
}
