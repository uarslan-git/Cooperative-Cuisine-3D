using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class JsonSubtypeConverter<T> : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return (objectType == typeof(T));
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        JObject item = (JObject)token;
        string type = item["category"].Value<string>();

        switch (type)
        {
            case "ItemCookingEquipment":
                return item.ToObject<CookingEquipmentState>(serializer);
            case "Item":
                return item.ToObject<ItemState>(serializer);
            default:
                return item.ToObject<ItemState>(serializer);
        }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}