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
using System.Numerics;

namespace Crypto_LP_Compounder.Settings
{
    internal class CompounderSettings
    {
        private static readonly AddressUtil _AddressUtil = new();

        private static CancellationTokenSource _CancelToken;

        public static CompounderSettings LoadSettings(string filePath)
        {
            Program.LogLineConsole();
            Program.LogConsole($"Loading settings from '{filePath}'... ");

            CompounderSettings settings = null;

            if (File.Exists(filePath))
            {
                settings = Json.DeserializeFromFile<CompounderSettings>(filePath);
                Program.LogLineConsole("Done!");
            }
            else
            {
                Program.LogLineConsole("File not found!");
                Program.ExitWithErrorCode(11);
            }

            CheckSettings(settings, out bool isSettingChanged);

            if (isSettingChanged)
            {
                Program.LogLineConsole();
                Program.LogLineBreakConsole();

                if (Json.SerializeToFile(settings, filePath))
                    Program.LogLineConsole("Settings saved");
                else
                {
                    Program.LogLineConsole("Failed to save settings");
                    Program.ExitWithErrorCode(1);
                }

                Program.LogConsole("Press any key to continue...");
                Program.FlushConsoleLogsAsync().Wait();
                Console.Read();

                Console.Clear();
            }
            return settings;
        }

        private static void CheckSettings(CompounderSettings settings, out bool isSettingChanged)
        {
            isSettingChanged = false;
            Program.LogLineConsole();
            Program.LogLineConsole("Checking settings...");
            try
            {
                bool isInstanceNameCheck = false;

                while (!isInstanceNameCheck)
                {
                    if (string.IsNullOrWhiteSpace(settings.Name))
                    {
                        Program.LogLineConsole();
                        Program.LogLineConsole("Instance name cannot be empty!");
                        Program.LogConsole("New instance name > ");
                        Program.FlushConsoleLogsAsync().Wait();
                        Program.EnableQuickEdit();

                        settings.Name = Console.ReadLine();

                        Program.DisableQuickEdit();
                        isSettingChanged = true;
                    }
                    else if (Path.GetInvalidFileNameChars().Any(invalidChar => settings.Name.Contains(invalidChar)))
                    {
                        Program.LogLineConsole();
                        Program.LogLineConsole("Instance name contains invalid character(s)!");
                        Program.LogConsole("New instance name > ");
                        Program.FlushConsoleLogsAsync().Wait();
                        Program.EnableQuickEdit();

                        settings.Name = Console.ReadLine();

                        Program.DisableQuickEdit();
                        isSettingChanged = true;
                    }
                    else
                    {
                        isInstanceNameCheck = true;
                    }
                }

                while (string.IsNullOrWhiteSpace(settings.Name))
                {
                    Program.LogLineConsole();
                    Program.LogLineConsole("Instance name cannot be empty!");
                    Program.LogConsole("New instance name > ");
                    Program.FlushConsoleLogsAsync().Wait();
                    Program.EnableQuickEdit();

                    settings.Name = Console.ReadLine();

                    Program.DisableQuickEdit();
                    isSettingChanged = true;
                }

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
                    Program.LogLineConsole();
                    Program.LogLineConsole("Invalid fixed gas price! Must be >= 0.0 (0 for disable)");
                    Program.LogConsole("New fixed gas price (Gwei) > ");
                    Program.FlushConsoleLogsAsync().Wait();
                    Program.EnableQuickEdit();

                    if (float.TryParse(Console.ReadLine(), out float gasPrice))
                        settings.FixedGasPriceGwei = gasPrice;

                    Program.DisableQuickEdit();
                    isSettingChanged = true;
                }

                while (settings.MinGasPriceGwei < 0.0f)
                {
                    Program.LogLineConsole();
                    Program.LogLineConsole("Invalid minimum gas price! Must be >= 0.0");
                    Program.LogConsole("New minimum gas price (Gwei) > ");
                    Program.FlushConsoleLogsAsync().Wait();
                    Program.EnableQuickEdit();

                    if (float.TryParse(Console.ReadLine(), out float minGasPrice))
                        settings.MinGasPriceGwei = minGasPrice;

                    Program.DisableQuickEdit();
                    isSettingChanged = true;
                }

                while (string.IsNullOrWhiteSpace(settings.GasSymbol))
                {
                    Program.LogLineConsole();
                    Program.LogLineConsole("Gas symbol is empty!");
                    Program.LogConsole("New gas symbol (e.g. ETH) > ");
                    Program.FlushConsoleLogsAsync().Wait();
                    Program.EnableQuickEdit();

                    settings.GasSymbol = Console.ReadLine();

                    Program.DisableQuickEdit();
                    isSettingChanged = true;
                }

                while (settings.LiquidityPool.Slippage < 0.1f)
                {
                    Program.LogLineConsole();
                    Program.LogLineConsole("Invalid slippage! Must be >= 0.1");
                    Program.LogConsole("New slippage (%) > ");
                    Program.FlushConsoleLogsAsync().Wait();
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
                    Program.LogLineConsole();
                    Program.LogLineConsole("Private key field is " + keyIssue);
                    Program.LogConsole("New private key > ");
                    Program.FlushConsoleLogsAsync().Wait();
                    Program.EnableQuickEdit();

                    settings.Wallet.PrivateKeyCrypt = Crypto_Crypt.Factory.Instance.Encrypt(Console.ReadLine().Trim());

                    Program.DisableQuickEdit();
                    isSettingChanged = true;
                }

                if (isSettingChanged) Program.LogLineConsole();
                Program.LogLineConsole("Settings checked");
            }
            catch (Exception ex)
            {
                Program.LogLineConsole();
                Program.LogLineConsole("Checking settings failed");
                Program.LogLineConsole(ex.ToString());
                Program.ExitWithErrorCode(2);
            }
        }

        private static void CheckAddress(CompounderSettings settings, string parameter, ref bool isSettingChange, bool isOptional = false)
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
                    Program.LogLineConsole();
                    Program.LogLineConsole($"{parameter}: Invalid address provided, ensure address is 42 characters long (including '0x')");

                    if (isOptional) Program.LogLineConsole("Or leave blank to disable");

                    Program.LogConsole("Enter new address > ");
                    Program.FlushConsoleLogsAsync().Wait();
                    Program.EnableQuickEdit();

                    address = Console.ReadLine().Trim();

                    Program.DisableQuickEdit();
                    Program.LogLineConsole();

                    if (isOptional && string.IsNullOrWhiteSpace(address)) return;
                }

                parentObject.GetType().GetProperty(name).SetValue(parentObject, address);

                isSettingChange = true;
            }

            if (!_AddressUtil.IsChecksumAddress(address))
            {
                address = _AddressUtil.ConvertToChecksumAddress(address);

                parentObject.GetType().GetProperty(name).SetValue(parentObject, address);

                Program.LogLineConsole();
                Program.LogLineConsole($"{parameter}: Address updated due to checksum ({address})");

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

        public string Name { get; set; } = "Instance_1";

        public bool IsLogAll { get; set; } = true;

        public uint DeleteLogsAfterDays { get; set; } = 60;

        public string RPC_URL { get; set; } = string.Empty;

        public uint RPC_Timeout { get; set; } = 120;

        public ulong? RPC_ChainID { get; set; } = null;

        public float GasPriceOffsetGwei { get; set; } = 0.0f;

        public float FixedGasPriceGwei { get; set; } = 0.0f;

        public float MinGasPriceGwei { get; set; } = 0.0f;

        public string GasSymbol { get; set; } = "ETH";

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
