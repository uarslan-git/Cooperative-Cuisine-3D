using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// This converter handles a common backend issue where a property can be either a single object
// or an array of objects. It also handles deserializing to the correct subtype based on a "category" field.
public class SingleOrArrayOfSubtypeConverter<T> : JsonConverter where T : class
{
    public override bool CanWrite => false;

    public override bool CanConvert(Type objectType)
    {
        // This converter is designed for List<T> properties.
        return (objectType == typeof(List<T>));
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);

        // Handle null tokens
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        // Handle the case where the token is an array
        if (token.Type == JTokenType.Array)
        {
            var list = new List<T>();
            foreach (var itemToken in token.Children<JObject>())
            {
                list.Add(ReadItem(itemToken, serializer));
            }
            return list;
        }

        // Handle the case where the token is a single object
        if (token.Type == JTokenType.Object)
        {
            return new List<T> { ReadItem((JObject)token, serializer) };
        }

        return null; // Should not happen with valid JSON
    }

    private T ReadItem(JObject item, JsonSerializer serializer)
    {
        // Determine the subtype based on the "category" field
        if (item.TryGetValue("category", StringComparison.OrdinalIgnoreCase, out JToken typeToken))
        {
            string type = typeToken.Value<string>();
            switch (type)
            {
                case "ItemCookingEquipment":
                    return item.ToObject<CookingEquipmentState>(serializer) as T;
                // Add other subtypes here if necessary
                case "Item":
                default:
                    return item.ToObject<ItemState>(serializer) as T;
            }
        }
        
        // Fallback if no category is specified
        return item.ToObject<ItemState>(serializer) as T;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        // This will never be called because CanWrite is false.
        throw new NotImplementedException("This converter is read-only and should not be used for serialization.");
    }
}