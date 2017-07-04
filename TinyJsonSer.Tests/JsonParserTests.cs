using System;
using System.Linq;
using NUnit.Framework;

namespace TinyJsonSer.Tests
{
    [TestFixture]
    public class JsonParserTests
    {
        [TestCase("true", typeof(JsonTrue), TestName = "true")]
        [TestCase("false", typeof(JsonFalse), TestName = "false")]
        [TestCase("null", typeof(JsonNull), TestName = "null")]
        public void JsonLiteralValues(string json, Type type)
        {
            var parser = new JsonParser();
            var obj = parser.Parse(json);
            Assert.IsInstanceOf(type, obj, $"Expected '{json}' to parse to type {type.Name}");
        }

        [TestCase("\\u0100", "Ā")]
        [TestCase("\\uD835\\uDC9C", "𝒜")]
        public void UnicodeParses(string input, string expectedOutput)
        {
            var parser = new JsonParser();
            var str = parser.Parse($"\"{input}\"") as JsonString;
            Assert.NotNull(str);
            Assert.AreEqual(expectedOutput, str.Value);
        }

        [Test]
        public void ArrayParsing()
        {
            var parser = new JsonParser();
            var array = parser.Parse("[ '1', '2' ,\"3\" ]") as JsonArray;
            Assert.NotNull(array);
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, array.Items.OfType<JsonString>().Select(s => s.Value));
        }

        [Test]
        public void NumberParsing()
        {
            var parser = new JsonParser();
            var number = parser.Parse("-15.2") as JsonNumber;
            Assert.NotNull(number);
            Assert.AreEqual("-15.2", number.StringRepresentation);
        }

        [Test]
        public void ObjectParsing()
        {
            var parser = new JsonParser();
            var obj = parser.Parse("{ 'Field1' : 2, \"Field2\" : 'payload' }") as JsonObject;
            Assert.NotNull(obj);
            var field1 = obj["Field1"] as JsonNumber;
            var field2 = obj["Field2"] as JsonString;
            Assert.NotNull(field1);
            Assert.NotNull(field2);
            Assert.AreEqual("2", field1.StringRepresentation);
            Assert.AreEqual("payload", field2.Value);
        }
        
        [Test]
        public void EscapedChars()
        {
            var str = "\"\\\"{}\\/\\uD835\\uDC9C  \\b\\t\\r\\n\"";
            var parser = new JsonParser();
            var parsed = parser.Parse(str) as JsonString;
            Assert.NotNull(parsed);
            Assert.AreEqual("\"{}/𝒜  \b\t\r\n", parsed.Value);
        }
    }
}
