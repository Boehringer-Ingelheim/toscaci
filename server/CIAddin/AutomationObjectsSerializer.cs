using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
//using System.Text.Json;
using JsonProperty = Newtonsoft.Json.Serialization.JsonProperty;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace CIAddin
{
    public static class AutomationObjectsSerializer
    {
        #region Properties

        private static JsonSerializerSettings JsonSerializerSettings =>
                new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.All,
                    TypeNameHandling = TypeNameHandling.All,
                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                    ContractResolver = new AutomationObjectContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.None,
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                    Converters = new JsonConverter[] { new LockedImageJsonConverter() }
                };

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Convert a JSON String to a defined AutomationObject
        /// </summary>
        /// <typeparam name="T">Automation Object type expected</typeparam>
        /// <param name="jsonString">JSON that is deserialized</param>
        /// <returns>Requested Automation Object of Type <see cref="T"/> or null if not matching</returns>
        //public static T Deserialize<T>(string jsonString)
        //        where T : class
        //{
        //    object deserializeObject = JsonConvert.DeserializeObject(jsonString, typeof(T), JsonSerializerSettings);
        //    return deserializeObject as T;
        //}

        /// <summary>
        /// Takes a compressed JSON String and deserializes it to an Automation Object
        /// </summary>
        /// <typeparam name="T">Automation Object type expected</typeparam>
        /// <param name="compressedBytes">compressed JSON string as bytes</param>
        /// <returns>Requested Automation Object of Type <see cref="T"/> or null if not matching</returns>
        //public static T DeSerializeDecompress<T>(byte[] compressedBytes)
        //        where T : class
        //{
        //    T deserialize;
        //    using (MemoryStream inStream = new MemoryStream(compressedBytes))
        //    {
        //        using (GZipStream gZipStream = new GZipStream(inStream, CompressionMode.Decompress))
        //        {
        //            using (JsonTextReader jsonTextReader = new JsonTextReader(new StreamReader(gZipStream, Encoding.UTF8)))
        //            {
        //                deserialize = JsonSerializer.Create(JsonSerializerSettings).Deserialize<T>(jsonTextReader);
        //            }
        //        }
        //    }
        //    return deserialize;
        //}

        /// <summary>
        /// Loads Automation Objects from a FilePath
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath">The file path which is used for loading the object</param>
        /// <param name="createCryptoStream">Decrypts the read stream using the crypto stream reader</param>
        /// <returns>The loaded Object or null</returns>
        /// <exception cref="FileNotFoundException">If the file can not be found on the path</exception>
        public static T FromFile<T>(string filePath, Func<Stream, Stream> createCryptoStream)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The file with the Path \"{filePath}\" was not found", filePath);
            }

            try
            {
                return FromFileNoCrypto<T>(filePath);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("File might be encrypted. Exception: " + ex);
            }

            if (createCryptoStream == null)
            {
                throw new InvalidOperationException("Can not decrypt the File without crypto Stream");
            }

            try
            {
                return FromFileCrypto2<T>(filePath, createCryptoStream);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("File might be encrypted. Exception: " + ex);
            }

            return FromFileCrypto<T>(filePath, createCryptoStream);
        }

        /// <summary>
        /// Converts an Automation Object to a JSON string
        /// </summary>
        /// <typeparam name="T">Should be an Automation Object or a List of Automation Objects</typeparam>
        /// <param name="objectToSerialize">the object that is going to be serialized</param>
        /// <returns>a JSON string representing the object</returns>
        public static string Serialize<T>(T objectToSerialize)
        {
            return JsonConvert.SerializeObject(objectToSerialize, JsonSerializerSettings);
        }

        /// <summary>
        /// Uses <see cref="Serialize{T}"/> to create a JSON String and compresses it a ZIP Stream
        /// </summary>
        /// <typeparam name="T">Should be an Automation Object or a List of Automation Objects</typeparam>
        /// <param name="objectToSerialize">the object that is going to be serialized</param>
        /// <returns>ZIP Compressed JSON string</returns>
        public static byte[] SerializeCompress<T>(T objectToSerialize)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                using (GZipStream gZipStream = new GZipStream(outStream, CompressionLevel.Optimal))
                {
                    using (JsonTextWriter jsonTextWriter = new JsonTextWriter(new StreamWriter(gZipStream)))
                    {
                        JsonSerializer ser = JsonSerializer.Create(JsonSerializerSettings);
                        ser.Serialize(jsonTextWriter, objectToSerialize);
                        jsonTextWriter.Flush();
                    }
                }
                return outStream.ToArray();
            }
        }

        /// <summary>
        /// Save an Automation Object to a file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath">The path to serialize the object to</param>
        /// <param name="objectToSerialize">The object to serialize</param>
        /// <param name="createCryptoStream">uses the crypto stream to encrypt the file content</param>
        /// <exception cref="NotSupportedException">If a file on the <see cref="filePath"/> already exists</exception>
        public static void ToFile<T>(string filePath, T objectToSerialize, Func<Stream, Stream> createCryptoStream)
        {
            if (File.Exists(filePath))
            {
                throw new NotSupportedException($"The File with the Path {filePath} already exists");
            }

            if (createCryptoStream != null)
            {
                ToFileEncrypt(filePath, objectToSerialize, createCryptoStream);
                return;
            }

            ToFileNoEncrypt(filePath, objectToSerialize);
        }

        #endregion

        #region Methods

        private static T FromFileCrypto<T>(string filePath, Func<Stream, Stream> createCryptoStream)
        {
            T deserialize;
            using (FileStream infile = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                using (GZipStream zipStream = new GZipStream(infile, CompressionMode.Decompress))
                {
                    using (var cryptoStream = createCryptoStream(zipStream))
                    {
                        using (JsonTextReader jsonTextReader = new JsonTextReader(new StreamReader(cryptoStream, Encoding.UTF8)))
                        {
                            deserialize = JsonSerializer.Create(JsonSerializerSettings).Deserialize<T>(jsonTextReader);
                        }
                    }
                }
            }
            return deserialize;
        }

        private static T FromFileCrypto2<T>(string filePath, Func<Stream, Stream> createCryptoStream)
        {
            T deserialize;
            using (FileStream infile = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var cryptoStream = createCryptoStream(infile))
                {
                    using (GZipStream zipStream = new GZipStream(cryptoStream, CompressionMode.Decompress))
                    {
                        using (JsonTextReader jsonTextReader = new JsonTextReader(new StreamReader(zipStream, Encoding.UTF8)))
                        {
                            deserialize = JsonSerializer.Create(JsonSerializerSettings).Deserialize<T>(jsonTextReader);
                        }
                    }
                }
            }
            return deserialize;
        }

        private static T FromFileNoCrypto<T>(string filePath)
        {
            T deserialize;
            using (FileStream infile = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                //using (GZipStream zipStream = new GZipStream(infile, CompressionMode.Decompress))
                //{
                using (JsonTextReader jsonTextReader = new JsonTextReader(new StreamReader(infile, Encoding.UTF8)))
                {
                    deserialize = JsonSerializer.Create(JsonSerializerSettings).Deserialize<T>(jsonTextReader);
                }
                //}
            }
            return deserialize;
        }

        private static void ToFileEncrypt<T>(string filePath, T objectToSerialize, Func<Stream, Stream> createCryptoStream)
        {
            JsonSerializer ser = JsonSerializer.Create(JsonSerializerSettings);
            using (FileStream outfile = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                using (var cryptoStream = createCryptoStream(outfile))
                {
                    using (GZipStream zipStream = new GZipStream(cryptoStream, CompressionLevel.Optimal))
                    {
                        using (JsonTextWriter jsonTextWriter = new JsonTextWriter(new StreamWriter(zipStream)))
                        {
                            ser.Serialize(jsonTextWriter, objectToSerialize);
                            jsonTextWriter.Flush();
                        }
                    }
                }
            }
        }

        private static void ToFileNoEncrypt<T>(string filePath, T objectToSerialize)
        {
            JsonSerializer ser = JsonSerializer.Create(JsonSerializerSettings);
            using (FileStream outfile = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                using (GZipStream zipStream = new GZipStream(outfile, CompressionLevel.Optimal))
                {
                    using (JsonTextWriter jsonTextWriter = new JsonTextWriter(new StreamWriter(zipStream)))
                    {
                        ser.Serialize(jsonTextWriter, objectToSerialize);
                        jsonTextWriter.Flush();
                    }
                }
            }
        }

        #endregion

        private class AutomationObjectContractResolver : DefaultContractResolver
        {
            #region Methods

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty jProperty = base.CreateProperty(member, memberSerialization);
                PropertyInfo property = member as PropertyInfo;

                // based on this idea https://patrickdesjardins.com/blog/how-to-have-json-net-deserialize-using-private-constructor-and-private-setter
                if (jProperty.Writable)
                {
                    SetConverterIfEnum(property, jProperty);
                    return jProperty;
                }

                jProperty.Writable = property?.SetMethod != null;
                SetConverterIfEnum(property, jProperty);

                return jProperty;
            }

            private static void SetConverterIfEnum(PropertyInfo property, JsonProperty jProperty)
            {
                if (property != null && property.PropertyType.IsEnum)
                {
                    jProperty.Converter = new StringEnumConverter();
                }
            }

            #endregion
        }
    }
}
