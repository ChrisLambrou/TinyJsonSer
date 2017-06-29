using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            return ParseJsonValue(reader);
        }

        private JsonValue ParseJsonValue(ICharReader charReader)
        {
            AdvanceWhitespace(charReader);
            var leadingCharacter = charReader.Peek();
            if (!leadingCharacter.HasValue) throw new JsonParseException("Unexpected end of stream");
            var valueType = IdentifyValueType(leadingCharacter.Value);

            switch (valueType)
            {
                case JsonValueType.String:
                    return ParseString(charReader);
                case JsonValueType.Number:
                    return ParseNumber(charReader);
                case JsonValueType.Object:
                    return ParseObject(charReader);
                case JsonValueType.Array:
                    return ParseArray(charReader);
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

        private JsonValue ParseObject(ICharReader charReader)
        {
            var members = new List<JsonObjectMember>();

            charReader.Read(); // {
            AdvanceWhitespace(charReader);

            var peek = charReader.Peek();
            if (!peek.HasValue) throw new JsonParseException("Unterminated object");

            while (peek.Value != '}')
            {
                if (members.Any())
                {
                    if(peek.Value != ',') throw new JsonParseException("Missing comma separater between object members.");
                    charReader.Read();
                    AdvanceWhitespace(charReader);
                }
                var member = ParseObjectMember(charReader);
                members.Add(member);
                peek = charReader.Peek();
                if(!peek.HasValue) throw new JsonParseException("Unterminated object");
            }
            charReader.Read(); // }

            return new JsonObject(members);
        }

        private JsonObjectMember ParseObjectMember(ICharReader charReader)
        {
            var name = ParseString(charReader);
            AdvanceWhitespace(charReader);
            var separator = charReader.Read();
            if (separator != ':') throw new JsonParseException("expected ':' after member name definition");
            AdvanceWhitespace(charReader);
            var value = ParseJsonValue(charReader);
            AdvanceWhitespace(charReader);
            return new JsonObjectMember(name.Value, value);
        }

        private static readonly char[] Numerics = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', 'e' ,'E', '-'};

        private JsonValue ParseNumber(ICharReader charReader)
        {
            var sb = new StringBuilder();
            var peek = charReader.Peek();

            while (peek.HasValue && Numerics.Contains(peek.Value))
            {
                sb.Append(charReader.Read());
                peek = charReader.Peek();
            }

            return new JsonNumber(sb.ToString());
        }

        private JsonValue ParseArray(ICharReader charReader)
        {
            charReader.Read(); // [
            AdvanceWhitespace(charReader);
            var items = new List<JsonValue>();
            while (charReader.Peek() != ']')
            {
                items.Add(ParseJsonValue(charReader));
                AdvanceWhitespace(charReader);
                if (charReader.Peek() != ',') break;
                charReader.Read();
                AdvanceWhitespace(charReader);
            }
            charReader.Read(); // ]
            return new JsonArray(items);
        }

        private JsonString ParseString(ICharReader charReader)
        {
            var delimiter = charReader.Read();
            if (delimiter != '\'' && delimiter != '"') throw new JsonParseException("Strings must be delimiated with either single or double quotes");

            var sb = new StringBuilder();
            var c = GetNextStringCharacter(charReader);
            while (c != delimiter)
            {
                sb.Append(c);
                c = GetNextStringCharacter(charReader);
            }

            return new JsonString(sb.ToString());
        }

        private char? GetNextStringCharacter(ICharReader charReader)
        {
            var c = charReader.Read();
            if (!c.HasValue) throw new JsonParseException("Unterminated string");
            if (CharRequiresEscapeInString(c.Value)) throw new JsonParseException($"Unescaped '{c}' in string");
            if (c != '\\') return c;

            c = charReader.Read();
            if (!c.HasValue) throw new JsonParseException("Unterminated string");

            if (ShortEscapeDecodables.TryGetValue(c.Value, out char fromShortCode))
            {
                return fromShortCode;
            }

            if (c == 'u') return GetUnicodeSymbol(charReader);

            throw new JsonParseException("Unrecognised escape sequence '\\{c}'");
        }

        private char? GetUnicodeSymbol(ICharReader charReader)
        {
            var sb = new StringBuilder(8);
            for (var i = 0; i < 4; i++)
            {
                var c = charReader.Read();
                if (!c.HasValue) throw new JsonParseException("Unterminated string");
                if (char.IsDigit(c.Value) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
                {
                    sb.Append(c.Value);
                }
                else
                {
                    throw new JsonParseException($"Invalid character '{c.Value}' in hexidecimal unicode notation");
                }
            }

            var hexString = sb.ToString();
            var sChar = (ushort)(Convert.ToUInt16(hexString.Substring(0, 2), 16) << 8);
            sChar += Convert.ToUInt16(hexString.Substring(2, 2), 16);
            return Convert.ToChar(sChar);
        }

        private static readonly Dictionary<char, char> ShortEscapeDecodables
            = new Dictionary<char, char>
            {
                {'\"', '"'},
                {'\\', '\\'},
                {'b', '\b'},
                {'f', '\f'},
                {'n', '\n'},
                {'r', '\r'},
                {'t', '\t'}
            };

        private bool CharRequiresEscapeInString(char c)
        {
            return c <= '\u001f';
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

        private void AdvanceWhitespace(ICharReader charReader)
        {
            var peek = charReader.Peek();
            while (peek.HasValue && char.IsWhiteSpace(peek.Value))
            {
                charReader.Read();
                peek = charReader.Peek();
            }
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
        public IEnumerable<JsonObjectMember> Members { get; }

        public JsonObject(IEnumerable<JsonObjectMember> members)
        {
            Members = members;
        }

        public JsonValue this[string index]
        {
            get { return Members.FirstOrDefault(m => m.Name == index)?.Value; }
        }
    }

    internal class JsonObjectMember
    {
        public string Name { get; }
        public JsonValue Value { get; }

        public JsonObjectMember(string name, JsonValue value)
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

    internal class JsonString : JsonValue
    {
        public string Value { get; }

        public JsonString(string value)
        {
            Value = value;
        }
    }

    internal class JsonNumber : JsonValue
    {
        public string StringRepresentation { get; }

        public JsonNumber(string stringRepresentation)
        {
            StringRepresentation = stringRepresentation;
        }
    }

    internal class JsonArray : JsonValue
    {
        public IList<JsonValue> Items { get; }

        public JsonArray(IList<JsonValue> items)
        {
            Items = items;
        }
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
