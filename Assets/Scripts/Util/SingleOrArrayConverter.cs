using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

// This converter allows a JSON property to be either a single object, an array of objects, or null.
public class SingleOrArrayConverter<T> : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return (objectType == typeof(List<T>));
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        if (token.Type == JTokenType.Null)
        {
            return new List<T>(); // Return an empty list if the token is null
        }
        if (token.Type == JTokenType.Array)
        {
            return token.ToObject<List<T>>();
        }
        // If it's a single object, wrap it in a list
        return new List<T> { token.ToObject<T>() };
    }

    public override bool CanWrite
    {
        get { return false; }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}