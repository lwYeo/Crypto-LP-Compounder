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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Text;

namespace Crypto_LP_Compounder
{
    internal static class Json
    {
        private const int MAX_TIMEOUT = 10;

        private static readonly object _SerializeFromObjectLock = new();
        private static readonly object _DeserializeFromUrlLock = new();
        private static readonly object _DeserializeTFromFileLock = new();
        private static readonly object _SerializeToFileLock = new();
        private static readonly object _CloneTObjectLock = new();

        private static readonly HttpClient _HttpClient = new();

        public static readonly JsonSerializerSettings BaseClassFirstSettings =
            new() { ContractResolver = BaseFirstContractResolver.Instance };

        public static string SerializeFromObject(object obj, JsonSerializerSettings settings = null)
        {
            lock (_SerializeFromObjectLock)
            {
                try
                {
                    return (settings == null) ?
                        JsonConvert.SerializeObject(obj, Formatting.Indented) :
                        JsonConvert.SerializeObject(obj, Formatting.Indented, settings);
                }
                catch { }
                return string.Empty;
            }
        }

        public static JObject DeserializeFromURL(string url)
        {
            lock (_DeserializeFromUrlLock)
            {
                string sJSON = string.Empty;

                using (var oClient = new HttpClient())
                {
                    oClient.Timeout = new TimeSpan(0, 0, MAX_TIMEOUT);
                    using (HttpResponseMessage oResponse = oClient.GetAsync(url).Result)
                    {
                        using (HttpContent oContent = oResponse.Content)
                        {
                            sJSON = oContent.ReadAsStringAsync().Result;
                        }
                    }
                }
                return (JObject)JsonConvert.DeserializeObject(sJSON);
            }
        }

        public static T DeserializeFromURL<T>(string url, bool throwException = false)
        {
            return _HttpClient.
                GetAsync(url).
                ContinueWith(t =>
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<T>(t.Result.EnsureSuccessStatusCode().Content.ReadAsStringAsync().Result);
                    }
                    catch
                    {
                        if (throwException) throw;
                        return (T)Activator.CreateInstance(typeof(T));
                    }
                }).
                Result;
        }

        public static T DeserializeFromFile<T>(string filePath)
        {
            lock (_DeserializeTFromFileLock)
            {
                string sJSON = File.ReadAllText(filePath);
                T jObject = (T)Activator.CreateInstance(typeof(T));
                try
                {
                    jObject = JsonConvert.DeserializeObject<T>(sJSON);
                }
                catch { }
                return jObject;
            }
        }

        public static bool SerializeToFile(object objToSerialize, string filePath, JsonSerializerSettings settings = null)
        {
            lock (_SerializeToFileLock)
            {
                try
                {
                    if (File.Exists(filePath)) File.Delete(filePath);

                    File.WriteAllText(filePath, (settings == null) ?
                        JsonConvert.SerializeObject(objToSerialize, Formatting.Indented) :
                        JsonConvert.SerializeObject(objToSerialize, Formatting.Indented, settings));
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static JObject InvokeJObjectRPC(string url, JObject obj, JsonSerializerSettings settings = null)
        {
            return _HttpClient.
                PostAsync(url, new StringContent(SerializeFromObject(obj, settings), Encoding.UTF8, "application/json")).
                Result.
                EnsureSuccessStatusCode().
                Content.
                ReadAsStringAsync().
                ContinueWith(t => JsonConvert.DeserializeObject<JObject>(t.Result)).
                Result;
        }

        public static T CloneObject<T>(T objectToClone)
        {
            lock (_CloneTObjectLock)
            {
                if (objectToClone == null) { return default(T); }
                try
                {
                    return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(objectToClone),
                                                            new JsonSerializerSettings() { ObjectCreationHandling = ObjectCreationHandling.Replace });
                }
                catch { return default(T); }
            }
        }

        public class ClassNameContractResolver : DefaultContractResolver
        {
            private ClassNameContractResolver()
            {
            }

            static ClassNameContractResolver() => Instance = new ClassNameContractResolver();

            public static ClassNameContractResolver Instance { get; private set; }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization).
                            Select(p =>
                            {
                                p.PropertyName = p.UnderlyingName;
                                return p;
                            }).
                            ToList();
            }
        }

        public class BaseFirstContractResolver : DefaultContractResolver
        {
            private BaseFirstContractResolver()
            {
            }

            static BaseFirstContractResolver() => Instance = new BaseFirstContractResolver();

            public static BaseFirstContractResolver Instance { get; private set; }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization).
                            OrderBy(p => p.DeclaringType.BaseTypesAndSelf().Count()).
                            ToList();
            }
        }
    }

    public static class TypeExtensions
    {
        public static IEnumerable<Type> BaseTypesAndSelf(this Type type)
        {
            while (type != null)
            {
                yield return type;
                type = type.BaseType;
            }
        }
    }
}
