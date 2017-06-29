using System;
using System.Collections.Generic;

namespace TinyJsonSer
{
    internal sealed class JsonParser
    {
        private static char[] _null = new char[] { 'n', 'u', 'l', 'l' };
        private static char[] _true = new char[] { 't', 'r', 'u', 'e' };
        private static char[] _false = new char[] { 'f', 'a', 'l', 's', 'e' };

        public JsonValue Parse(string json)
        {
            var reader = new StringCharReader(json);
            return ParseNextJsonValue(reader);
        }

        private JsonValue ParseNextJsonValue(ICharReader charReader)
        {
            var leadingCharacter = charReader.Peek();
            if (!leadingCharacter.HasValue) throw new JsonParseException("Unexpected end of stream");
            var valueType = IdentifyValueType(leadingCharacter.Value);

            switch (valueType)
            {
                case JsonValueType.String:
                case JsonValueType.Number:
                case JsonValueType.Object:
                case JsonValueType.Array:
                case JsonValueType.True:
                    return ParseTrue(charReader);
                case JsonValueType.False:
                    return ParseFalse(charReader);
                case JsonValueType.Null:
                    return ParseNull(charReader);
                case JsonValueType.Unrecognised:
                    throw new JsonParseException($"Unexpected character '{leadingCharacter.Value}'");
                default:
                    throw new ArgumentOutOfRangeException(nameof(valueType), valueType, null);
            }
        }

        private JsonValue ParseTrue(ICharReader charReader)
        {
            foreach (char c in _true)
            {
                if (c != charReader.Read()) throw new JsonParseException($"Expected character '{c}' whilst parsing 'true'");
            }
            return new JsonTrue();
        }

        private JsonValue ParseFalse(ICharReader charReader)
        {
            foreach (char c in _false)
            {
                if (c != charReader.Read()) throw new JsonParseException($"Expected character '{c}' whilst parsing 'false'");
            }
            return new JsonFalse();
        }

        private JsonValue ParseNull(ICharReader charReader)
        {
            foreach (char c in _null)
            {
                if (c != charReader.Read()) throw new JsonParseException($"Expected character '{c}' whilst parsing 'null'");
            }
            return new JsonNull();
        }

        private static JsonValueType IdentifyValueType(char leadingCharacter)
        {
            switch (leadingCharacter)
            {
                case '\"': return JsonValueType.String;
                case '\'': return JsonValueType.String;
                case '{': return JsonValueType.Object;
                case '[': return JsonValueType.Array;
                case 't': return JsonValueType.True;
                case 'f': return JsonValueType.False;
                case 'n': return JsonValueType.Null;
            }

            if (Char.IsDigit(leadingCharacter)) return JsonValueType.Number;
            if (leadingCharacter == '-') return JsonValueType.Number;

            return JsonValueType.Unrecognised;
        }
    }

    internal class JsonParseException : Exception
    {
        public JsonParseException(string message) : base(message)
        {
        }
    }


    internal class JsonObject : JsonValue
    {
        public IList<JsonNameValuePair> Members { get; }

        public JsonObject(IList<JsonNameValuePair> members)
        {
            Members = members;
        }
    }

    internal class JsonNameValuePair
    {
        public string Name { get; }
        public JsonValue Value { get; }

        public JsonNameValuePair(string name, JsonValue value)
        {
            Name = name;
            Value = value;
        }
    }

    internal class JsonTrue : JsonValue
    {

    }

    internal class JsonFalse : JsonValue
    {

    }

    internal class JsonNull : JsonValue
    {

    }

    enum JsonValueType { String, Number, Object, Array, True, False, Null, Unrecognised }

    internal class JsonValue
    {
    }

    internal interface ICharReader
    {
        char? Peek();
        char? Read();
    }

    internal class StringCharReader : ICharReader
    {
        private readonly string _string;
        private int _position = 0;

        public StringCharReader(string s)
        {
            _string = s;
        }

        public char? Peek()
        {
            return Read(_position);
        }

        public char? Read()
        {
            return Read(_position++);
        }

        private char? Read(int position)
        {
            if (position > _string.Length - 1) return null;
            return _string[position];
        }
    }
}
