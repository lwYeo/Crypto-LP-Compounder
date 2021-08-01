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

using Nethereum.Util;
using Nethereum.Web3;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Crypto_LP_Compounder
{
    internal class Settings
    {
        private const string FileName = "settings.json";

        private static readonly AddressUtil _AddressUtil = new();

        private static CancellationTokenSource _CancelToken;

        public static Settings LoadSettings()
        {
            Settings settings;

            Program.WriteLog("Loading settings... ");

            string filePath = System.IO.Path.Combine(AppContext.BaseDirectory, FileName);

            if (System.IO.File.Exists(filePath))
            {
                settings = Json.DeserializeFromFile<Settings>(filePath);
                Program.WriteLineLog("Done!");
            }
            else
            {
                Program.WriteLineLog("File not found!");
                settings = new();
            }

            CheckSettings(settings, out bool isSettingChanged);

            if (isSettingChanged)
            {
                Program.CreateLineBreak();

                if (Json.SerializeToFile(settings, FileName))
                    Program.WriteLineLog("Settings saved");
                else
                    Program.ExitWithErrorMessage(1, "Failed to save settings");

                Program.WriteLog("Press any key to continue...");
                Console.Read();

                Console.Clear();
            }
            return settings;
        }

        private static void CheckSettings(Settings settings, out bool isSettingChanged)
        {
            isSettingChanged = false;
            Program.WriteLineLog();
            Program.WriteLineLog("Checking settings...");
            Program.WriteLineLog();
            try
            {
                CheckAddress(settings, "WETH_Contract", ref isSettingChanged);
                CheckAddress(settings, "USD_Contract", ref isSettingChanged);
                CheckAddress(settings, "LiquidityPool.TokenA_Contract", ref isSettingChanged);
                CheckAddress(settings, "LiquidityPool.TokenB_Contract", ref isSettingChanged);
                CheckAddress(settings, "LiquidityPool.LP_Contract", ref isSettingChanged);
                CheckAddress(settings, "LiquidityPool.FactoryContract", ref isSettingChanged);
                CheckAddress(settings, "LiquidityPool.RouterContract", ref isSettingChanged);
                CheckAddress(settings, "LiquidityPool.ZapContract", ref isSettingChanged, isOptional: true);
                CheckAddress(settings, "LiquidityPool.TaxFreeContract", ref isSettingChanged, isOptional: true);
                CheckAddress(settings, "Farm.FarmContract", ref isSettingChanged);
                CheckAddress(settings, "Farm.RewardContract", ref isSettingChanged);

                while (settings.FixedGasPriceGwei < 0.0f)
                {
                    Program.WriteLineLog("Invalid fixed gas price! Must be >= 0.0 (0 for disable)");
                    Program.WriteLog("New fixed gas price (Gwei) > ");
                    Program.EnableQuickEdit();

                    if (float.TryParse(Console.ReadLine(), out float gasPrice))
                        settings.FixedGasPriceGwei = gasPrice;

                    Program.DisableQuickEdit();
                    isSettingChanged = true;
                }

                while (settings.MinGasPriceGwei < 0.0f)
                {
                    Program.WriteLineLog("Invalid minimum gas price! Must be >= 0.0");
                    Program.WriteLog("New minimum gas price (Gwei) > ");
                    Program.EnableQuickEdit();

                    if (float.TryParse(Console.ReadLine(), out float minGasPrice))
                        settings.MinGasPriceGwei = minGasPrice;

                    Program.DisableQuickEdit();
                    isSettingChanged = true;
                }

                while (settings.LiquidityPool.Slippage < 0.1f)
                {
                    Program.WriteLineLog("Invalid slippage! Must be >= 0.1");
                    Program.WriteLog("New slippage (%) > ");
                    Program.EnableQuickEdit();

                    if (float.TryParse(Console.ReadLine(), out float slippage))
                        settings.LiquidityPool.Slippage = slippage;

                    Program.DisableQuickEdit();
                    isSettingChanged = true;
                }

                CheckAddress(settings, "Wallet.Address", ref isSettingChanged);

                string keyIssue = string.Empty;
                try
                {
                    if (string.IsNullOrWhiteSpace(settings.Wallet.PrivateKeyCrypt)) keyIssue = "empty";
                    else Crypto_Crypt.Factory.Instance.Decrypt(settings.Wallet.PrivateKeyCrypt);
                }
                catch
                {
                    keyIssue = "invalid";
                }

                if (!string.IsNullOrWhiteSpace(keyIssue))
                {
                    Program.WriteLineLog("Private key field is " + keyIssue);
                    Program.WriteLog("New private key > ");
                    Program.EnableQuickEdit();

                    settings.Wallet.PrivateKeyCrypt = Crypto_Crypt.Factory.Instance.Encrypt(Console.ReadLine().Trim());

                    Program.DisableQuickEdit();
                    isSettingChanged = true;
                }

                Program.WriteLineLog("Settings checked");
                Program.WriteLineLog();
            }
            catch (Exception ex)
            {
                Program.WriteLineLog("Checking settings failed");

                Program.ExitWithErrorMessage(2, ex.ToString());
            }
        }

        private static void CheckAddress(Settings settings, string parameter, ref bool isSettingChange, bool isOptional = false)
        {
            string name = string.Empty;
            string address = string.Empty;
            object parentObject = settings;

            string[] propertyLevel = parameter.Split('.');

            foreach (string propertyName in propertyLevel)
            {
                if (propertyName.Equals(propertyLevel.Last()))
                {
                    name = propertyName;
                    address = (string)parentObject.GetType().GetProperty(propertyName).GetValue(parentObject);
                }
                else
                    parentObject = parentObject.GetType().GetProperty(propertyName).GetValue(parentObject);
            }

            if (isOptional && string.IsNullOrWhiteSpace(address)) return;

            if ((!_AddressUtil.IsValidAddressLength(address) || !_AddressUtil.IsValidEthereumAddressHexFormat(address)))
            {
                while (!_AddressUtil.IsValidAddressLength(address) || !_AddressUtil.IsValidEthereumAddressHexFormat(address))
                {
                    Program.WriteLineLog();
                    Program.WriteLineLog("{0}: Invalid address provided, ensure address is 42 characters long (including '0x')", parameter);

                    if (isOptional) Program.WriteLineLog("Or leave blank to disable");

                    Program.WriteLog("Enter new address > ");
                    Program.EnableQuickEdit();

                    address = Console.ReadLine().Trim();

                    Program.DisableQuickEdit();
                    Program.WriteLineLog();

                    if (isOptional && string.IsNullOrWhiteSpace(address)) return;
                }

                parentObject.GetType().GetProperty(name).SetValue(parentObject, address);

                isSettingChange = true;
            }

            if (!_AddressUtil.IsChecksumAddress(address))
            {
                address = _AddressUtil.ConvertToChecksumAddress(address);

                parentObject.GetType().GetProperty(name).SetValue(parentObject, address);

                Program.WriteLineLog("{0}: Address updated due to checksum ({1})", parameter, address);
                Program.WriteLineLog();

                isSettingChange = true;
            }
        }

        public BigInteger GetUserGasPriceOffset()
        {
            return UnitConversion.Convert.ToWei(GasPriceOffsetGwei, UnitConversion.EthUnit.Gwei);
        }

        public BigInteger? GetUserFixedGasPrice()
        {
            return (FixedGasPriceGwei <= 0.0f) ? null : UnitConversion.Convert.ToWei(FixedGasPriceGwei, UnitConversion.EthUnit.Gwei);
        }

        public BigInteger GetUserMinGasPrice()
        {
            return (MinGasPriceGwei <= 0.0f) ? BigInteger.Zero : UnitConversion.Convert.ToWei(MinGasPriceGwei, UnitConversion.EthUnit.Gwei);
        }

        public BigInteger GetTimeoutEpoch()
        {
            return new BigInteger(DateTimeOffset.Now.AddSeconds(RPC_Timeout).ToUnixTimeSeconds());
        }

        public CancellationTokenSource GetCancelTokenByTimeout()
        {
            if (_CancelToken?.IsCancellationRequested ?? true)
            {
                _CancelToken?.Dispose();
                _CancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(RPC_Timeout + 1));
            }

            return _CancelToken;
        }

        public BigInteger GetGasPrice(Web3 web3)
        {
            return GetUserFixedGasPrice() ??
                new[]
                {
                    web3.Eth.GasPrice.SendRequestAsync().Result.Value + GetUserGasPriceOffset(),
                    GetUserMinGasPrice()
                }.
                Max();
        }

        #region Properties and Subtypes

        public string RPC_URL { get; set; } = string.Empty;
        public uint RPC_Timeout { get; set; } = 120;
        public float GasPriceOffsetGwei { get; set; } = 0.0f;
        public float FixedGasPriceGwei { get; set; } = 0.0f;
        public float MinGasPriceGwei { get; set; } = 0.0f;
        public string WETH_Contract { get; set; } = string.Empty;
        public string USD_Contract { get; set; } = string.Empty;
        public uint USD_Decimals { get; set; } = 6;
        public WalletParams Wallet { get; set; } = new();
        public LiquidityPoolParams LiquidityPool { get; set; } = new();
        public FarmParams Farm { get; set; } = new();

        public class WalletParams
        {
            public string Address { get; set; } = string.Empty;
            public string PrivateKeyCrypt { get; set; } = string.Empty;
        }

        public class LiquidityPoolParams
        {
            public string TokenA_Contract { get; set; } = string.Empty;
            public uint TokenA_Decimals { get; set; } = 18;
            public string TokenB_Contract { get; set; } = string.Empty;
            public uint TokenB_Decimals { get; set; } = 18;
            public string LP_Contract { get; set; } = string.Empty;
            public uint LP_Decimals { get; set; } = 18;
            public string FactoryContract { get; set; } = string.Empty;
            public string RouterContract { get; set; } = string.Empty;
            public float Slippage { get; set; } = 1.0f;
            public float TokenA_Offset { get; set; } = 0.0f;
            public float TokenB_Offset { get; set; } = 0.0f;
            public string ZapContract { get; set; } = string.Empty;
            public string TaxFreeContract { get; set; } = string.Empty;
        }

        public class FarmParams
        {
            public string FarmType { get; set; } = string.Empty;
            public string FarmContract { get; set; } = string.Empty;
            public string RewardContract { get; set; } = string.Empty;
            public uint RewardDecimals { get; set; } = 18;
            public uint FarmPoolID { get; set; } = 0;
            public bool ProcessAllRewards { get; set; } = false;
        }

        #endregion Properties and Subtypes
    }
}
