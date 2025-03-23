using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace InControl
{
    public static class JsonUtils
    {
        public static readonly JsonSerializerSettings PrettyJsonFormat = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            Converters = new JsonConverter[]
            {
                new StringEnumConverter(),
                new ItemConverter(),
                new MapItemConverter()
            }
        };
        public static readonly JsonSerializerSettings CompactJsonFormat = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
            Converters = new JsonConverter[]
            {
                new StringEnumConverter(),
                new ItemConverter(),
                new MapItemConverter()
            }
        };

        public static void AddJsonConverter(JsonConverter converter)
        {
            PrettyJsonFormat.Converters.Add(converter);
            CompactJsonFormat.Converters.Add(converter);
        }

        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, PrettyJsonFormat);
        }

        public static string Serialize(object obj, bool pretty = true)
        {
            return JsonConvert.SerializeObject(obj, pretty ? PrettyJsonFormat : CompactJsonFormat);
        }

        private class ItemConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value is Item_Base item)
                    writer.WriteValue(item.name);
                else
                    throw new JsonSerializationException($"Invalid value {value} for ItemConverter");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var name = (string)reader.Value;
                var o = ItemUtils.BestItemMatch(name);
                if (o == null)
                    throw new JsonSerializationException($"Invalid item name: " + name);
                return o;
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(Item_Base).IsAssignableFrom(objectType);
            }
        }

        private class MapItemConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                if (value is System.Collections.IEnumerable obj)
                {
                    var it = obj.GetEnumerator();
                    if (it.MoveNext())
                    {
                        var keyAccess = it.Current.GetType().GetProperty("Key");
                        var valueAccess = it.Current.GetType().GetProperty("Value");
                        if (keyAccess != null && valueAccess != null)
                            foreach (var e in obj)
                                if (keyAccess.GetValue(e) is Item_Base key)
                                {
                                    writer.WritePropertyName(key.name);
                                    serializer.Serialize(writer, valueAccess.GetValue(e));
                                }
                    }
                }
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var valueType = objectType.GetGenericArguments()[1];
                var o = Activator.CreateInstance(objectType);
                var add = objectType.GetMethod("Add");
                if (add == null) return o;
                foreach (var property in JObject.Load(reader).Properties())
                {
                    var key = ItemUtils.BestItemMatch(property.Name);
                    if (key == null)
                        throw new JsonSerializationException($"Invalid item name: " + property.Name);
                    var value = property.Value.ToObject(valueType, serializer);
                    add.Invoke(o, new object[]{key, value});
                }
                return o;
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Dictionary<,>) && objectType.GetGenericArguments()[0] == typeof(Item_Base);
            }
        }
    }
}