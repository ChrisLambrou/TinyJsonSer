using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }
}
