using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class SingleOrArrayOfSubtypeConverter<T> : JsonConverter where T : class
{
    public override bool CanConvert(Type objectType)
    {
        return (objectType == typeof(List<T>));
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token.Type == JTokenType.Array)
        {
            return token.ToObject<List<T>>(serializer);
        }
        
        JObject item = (JObject)token;
        string type = item["category"].Value<string>();

        switch (type)
        {
            case "ItemCookingEquipment":
                return new List<T> { item.ToObject<CookingEquipmentState>() as T };
            case "Item":
                return new List<T> { item.ToObject<ItemState>() as T };
            default:
                return new List<T> { item.ToObject<ItemState>() as T };
        }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}