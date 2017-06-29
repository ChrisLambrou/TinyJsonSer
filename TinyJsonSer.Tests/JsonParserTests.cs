﻿using System;
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
            var array = parser.Parse("[ '1', '2' ,'3' ]") as JsonArray;
            Assert.NotNull(array);
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, array.Items.OfType<JsonString>().Select(s => s.Value));
        }
    }
}
