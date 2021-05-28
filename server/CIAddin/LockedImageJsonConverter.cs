using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Tricentis.Automation.Contract.ImageProcessing;

namespace CIAddin
{
    public class LockedImageJsonConverter : JsonConverter
    {
        //private readonly TricentisLogger logger = TricentisLogManager.GetLogger();

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(LockedImage);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                LockedImage image = new LockedImage(null);
                JObject lockedImageJObject = JObject.Load(reader);
                if (!(lockedImageJObject.GetValue("ImageString") is JValue imageStringJValue))
                {
                    throw new NullReferenceException("Property \"ImageString\" not found in serialized representation of LockedImage.");
                }
                image.ImageString = imageStringJValue.Value.ToString();
                return image;
            }
            catch (Exception e)
            {
                string errorMessage = $"Exception during Json deserialization when trying to map \"Tricentis.Common.MiscHelper.LockedImage\" to \"{typeof(LockedImage).FullName}\".";
                //logger.Error(errorMessage, e);
                throw new InvalidOperationException(errorMessage, e);
            }
        }
    }
}