using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TinyJsonSer
{
    public class JsonDeserializer
    {
        private static readonly JsonParser _parser = new JsonParser();

        public T Deserialize<T>(string json)
        {
            return (T)Deserialize(typeof(T), json);
        }

        public object Deserialize(Type type, string json)
        {
            var jsonValue = _parser.Parse(json);
            return Deserialize(type, jsonValue);
        }

        public T Deserialize<T>(StreamReader jsonTextStream)
        {
            return (T)Deserialize(typeof(T), jsonTextStream);
        }

        public object Deserialize(Type type, StreamReader jsonTextStream)
        {
            var jsonValue = _parser.Parse(jsonTextStream);
            return Deserialize(type, jsonValue);
        }

        private object Deserialize(Type type, JsonValue jsonValue)
        {
            if (jsonValue is JsonString str) return DeserializeString(type, str);
            if (jsonValue is JsonObject obj) return DeserializeObject(type, obj);
            if (jsonValue is JsonArray array)  return DeserializeArray(type, array);
            if (jsonValue is JsonNumber number) return DeserializeNumber(type, number);
            if (jsonValue is JsonNull)   return DeserializeNull(type);
            if (jsonValue is JsonTrue)   return DeserializeBoolean(type, true);
            if (jsonValue is JsonFalse)  return DeserializeBoolean(type, false);

            throw new JsonException($"No deserializer for {jsonValue.GetType().Name}");
        }

        private object DeserializeNumber(Type type, JsonNumber jsonNumber)
        {
            try
            {
                if (type == typeof(int)) return int.Parse(jsonNumber.StringRepresentation);
                if (type == typeof(long)) return long.Parse(jsonNumber.StringRepresentation);
                if (type == typeof(decimal)) return decimal.Parse(jsonNumber.StringRepresentation);
                if (type == typeof(float)) return float.Parse(jsonNumber.StringRepresentation);
                if (type == typeof(double)) return double.Parse(jsonNumber.StringRepresentation);
                if (type == typeof(uint)) return uint.Parse(jsonNumber.StringRepresentation);
                if (type == typeof(ulong)) return ulong.Parse(jsonNumber.StringRepresentation);
                if (type == typeof(byte)) return byte.Parse(jsonNumber.StringRepresentation);
            }
            catch (FormatException)
            {
                throw new JsonException($"Malformed {type.Name}: '{jsonNumber.StringRepresentation}'");
            }

            // Fallback
            var tc = TypeDescriptor.GetConverter(type);
            if(tc.CanConvertFrom(typeof(string))) return tc.ConvertFromString(jsonNumber.StringRepresentation);
            
            throw new JsonException($"Could not map {jsonNumber.StringRepresentation} to {type.Name}");
        }

        private object DeserializeArray(Type type, JsonArray jsonArray)
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var length = jsonArray.Items.Count;
                var array = Array.CreateInstance(elementType, length);
                for (var i = 0; i < length; i++)
                {
                    array.SetValue(Deserialize(elementType, jsonArray.Items[i]), i);
                }
                return array;
            }
            throw new JsonException($"Could not map json array to {type.Name}");
        }

        private object DeserializeObject(Type type, JsonObject jsonObject)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) return CreateDictionary(type, jsonObject);
            if (type.IsClass) return InstatiateObject(type, jsonObject);

            throw new JsonException($"Could not map {jsonObject.GetType().Name} to {type.Name}");
        }

        private object DeserializeNull(Type type)
        {
            if (!type.IsValueType) return null;
            throw new JsonException($"Could not map JsonNull to a value type '{type.Name}'");
        }

        private object DeserializeBoolean(Type type, bool value)
        {
            if (type == typeof(bool)) return value;
            throw new JsonException($"Could not map '{value}' to {type.Name}");
        }

        private object CreateDictionary(Type type, JsonObject jsonValue)
        {
            var keyType = type.GetGenericArguments()[0];
            var valueType = type.GetGenericArguments()[1];
            var add = type.GetMethod("Add", new[] { keyType, valueType });
            var dictionary = Activator.CreateInstance(type);
            foreach (var member in jsonValue.Members)
            {
                var key = DeserializeFromString(keyType, member.Name);
                var value = Deserialize(valueType, member.Value);
                add.Invoke(dictionary, new[] { key, value });
            }
            return dictionary;
        }

        private object DeserializeFromString(Type type, string str)
        {
            if (type == typeof(string)) return str;

            if (type == typeof(DateTime)) return DateTime.Parse(str, null, System.Globalization.DateTimeStyles.RoundtripKind);

            var tc = TypeDescriptor.GetConverter(type);
            if (tc.CanConvertFrom(typeof(string))) return tc.ConvertFromString(str);

            throw new JsonException($"Could not map string to {type.Name}");
        }

        private object DeserializeString(Type type, JsonString jsonString)
        {
            return DeserializeFromString(type, jsonString.Value);
        }

        private object InstatiateObject(Type type, JsonObject jsonObject)
        {
            var activationPlan = GetObjectActivationPlan(type, jsonObject);

            var ctorParams = activationPlan.ConstructorParameterMap
                                           .Select(pair => Deserialize(pair.Type, pair.JsonValue))
                                           .ToArray();


            var obj = ctorParams.Any() 
                ? Activator.CreateInstance(type, ctorParams)
                : Activator.CreateInstance(type);

            var remainingJsonMembers =
                jsonObject.Members.Where(m => activationPlan.ConstructorParameterMap.All(pair => pair.JsonValue != m.Value));

            foreach (var member in remainingJsonMembers)
            {
                var exactProperty = type.GetProperty(member.Name, BindingFlags.Instance | BindingFlags.Public);
                if (exactProperty != null)
                {
                    var propertyValue = Deserialize(exactProperty.PropertyType, member.Value);
                    exactProperty.SetValue(obj, propertyValue, null);
                    continue;
                }

                var property = type.GetProperty(member.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property != null)
                {
                    var propertyValue = Deserialize(property.PropertyType, member.Value);
                    property.SetValue(obj, propertyValue, null);
                    continue;
                }

                var exactField = type.GetField(member.Name, BindingFlags.Instance | BindingFlags.Public);
                if (exactField != null)
                {
                    var fieldValue = Deserialize(exactField.FieldType, member.Value);
                    exactField.SetValue(obj, fieldValue);
                    continue;
                }

                var field = type.GetField(member.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    var fieldValue = Deserialize(field.FieldType, member.Value);
                    field.SetValue(obj, fieldValue);
                }
            }

            return obj;
        }

        private ObjectActivationPlan GetObjectActivationPlan(Type type, JsonObject jsonObject)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            var settableProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                         .Where(p => p.CanWrite)
                                         .ToArray();

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            var settableMemberNames = fields.Select(f => f.Name).Union(settableProperties.Select(p => p.Name))
                                        .Select(name => name.ToLowerInvariant())
                                        .ToArray();

            var jsonMemberNamesLower = jsonObject.Members.Select(m => m.Name.ToLowerInvariant()).ToArray();

            bool CanSatisfy(ConstructorInfo ctor)
            {
                var ctorParamNames = ctor.GetParameters().Select(p => p.Name.ToLowerInvariant()).ToArray();
                var paramsNotInCtor = jsonMemberNamesLower.Except(ctorParamNames).ToArray();
                return ctorParamNames.All(p => jsonMemberNamesLower.Contains(p)) &&
                       paramsNotInCtor.All(p => settableMemberNames.Contains(p));
            }

            var constructor = constructors.Where(CanSatisfy)
                                          .OrderByDescending(ctor => ctor.GetParameters().Length)
                                          .FirstOrDefault();

            if(constructor == null) throw new JsonException($"Could not find a suitable constructor for {type.Name}.");


            var constructorParameterMap = constructor
                                          .GetParameters()
                                          .Select(p => new JsonValueWithType(jsonObject.Members.First(m => m.Name.Equals(p.Name,
                                                                                                                         StringComparison.InvariantCultureIgnoreCase))
                                                                                       .Value,
                                                                             p.ParameterType))
                                          .ToArray();



            return new ObjectActivationPlan(constructor, constructorParameterMap);
        }
    }

    internal class ObjectActivationPlan
    {
        public ConstructorInfo Constructor { get; }
        public JsonValueWithType[] ConstructorParameterMap { get; }

        public ObjectActivationPlan(ConstructorInfo constructor,
                                    JsonValueWithType[] constructorParameterMap)
        {
            Constructor = constructor;
            ConstructorParameterMap = constructorParameterMap;
        }
    }

    internal class JsonValueWithType
    {
        internal JsonValue JsonValue { get; }
        internal Type Type { get; }

        public JsonValueWithType(JsonValue jsonValue, Type type)
        {
            JsonValue = jsonValue;
            Type = type;
        }
    }
}
