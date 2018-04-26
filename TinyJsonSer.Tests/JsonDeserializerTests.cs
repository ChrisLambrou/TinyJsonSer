using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TinyJsonSer.Tests
{
    [TestFixture]
    public class JsonDeserializerTests
    {
        [Test]
        public void Deserialization()
        {
            var deserialiser = new JsonDeserializer();
            var testClass = deserialiser.Deserialize<TestClass>(TestClass.Json());
            Assert.NotNull(testClass);
            Assert.AreEqual("TheValue", testClass.StringProperty);
            Assert.AreEqual(false, testClass.BoolF);
            Assert.AreEqual(true, testClass.BoolT);
            Assert.Null(testClass.Child);
            CollectionAssert.AreEqual(new[] {1,2,3,4,-5}, testClass.Int32s);
            var apples = testClass.StringCounts["Apples"];
            var bananas = testClass.StringCounts["Bananas"];
            Assert.AreEqual(2, apples);
            Assert.AreEqual(3, bananas);
        }

        [TestCase(@"{""BoolT"" : null }")]
        [TestCase(@"{""Int32"" : null }")]
        public void CannotDeserializeNullToValueType(string nullToValueTypeJson)
        {
            var deserialiser = new JsonDeserializer();
            Assert.That(() =>
                deserialiser.Deserialize<TestClass>(nullToValueTypeJson),
                Throws.Exception.TypeOf<JsonException>().With.Message.Contain("value type"));
        }

        public enum TestEnum { Red, Green, Blue }
        [TestCase("\"Red\"", TestEnum.Red)]
        [TestCase("\"green\"", TestEnum.Green)]
        [TestCase("\"BLUE\"", TestEnum.Blue)]
        [TestCase("\"0\"", TestEnum.Red)]
        [TestCase("\"1\"", TestEnum.Green)]
        [TestCase("\"2\"", TestEnum.Blue)]
        [TestCase("0", TestEnum.Red)]
        [TestCase("1", TestEnum.Green)]
        [TestCase("2", TestEnum.Blue)]
        public void EnumDeserialization(string json, TestEnum expectedValue)
        {
            var deserialiser = new JsonDeserializer();
            var val = deserialiser.Deserialize<TestEnum>(json);
            Assert.AreEqual(expectedValue, val);
        }

        [Test]
        public void CanDeserialiseIso8601()
        {
            var deserialiser = new JsonDeserializer();
            var val = deserialiser.Deserialize<DateTime>("\"2017-06-30T14:15:57Z\"");
            Assert.AreEqual(2017, val.Year);
            Assert.AreEqual(6, val.Month);
            Assert.AreEqual(30, val.Day);
            Assert.AreEqual(14, val.Hour);
            Assert.AreEqual(15, val.Minute);
            Assert.AreEqual(57, val.Second);
            Assert.AreEqual(DateTimeKind.Utc, val.Kind);
        }

        [Test]
        public void CanDeserialiseImmutableClass()
        {
            var deserialiser = new JsonDeserializer();
            var obj = deserialiser.Deserialize<ImmutableClass>(ImmutableClass.Json());
            Assert.AreEqual(10, obj.IntValue);
            Assert.AreEqual("Hello", obj.StrValue);
        }

        [Test]
        public void CanDeserialiseMixedMutabilityClass()
        {
            var deserialiser = new JsonDeserializer();
            var obj = deserialiser.Deserialize<MixedMutabilityClass>(MixedMutabilityClass.Json());
            Assert.AreEqual(10, obj.IntValue);
            Assert.AreEqual("Hello", obj.StrValue);
        }
    }

    public class ImmutableClass
    {
        public int IntValue { get; }
        public string StrValue { get; }

        public ImmutableClass()
        {
            IntValue = -1;
            StrValue = "bad";
        }

        public ImmutableClass(int intValue, string strValue)
        {
            IntValue = intValue;
            StrValue = strValue;
        }

        public ImmutableClass(int intValue)
        {
            IntValue = intValue;
            StrValue = "bad";
        }

        public ImmutableClass(string strValue)
        {
            IntValue = -1;
            StrValue = strValue;
        }

        public static string Json()
        {
            return @"{""IntValue"" : 10,""StrValue"" : ""Hello""}";
        }
    }

    public class MixedMutabilityClass
    {
        public int IntValue { get; }

        public string StrValue { get; set; }

        public MixedMutabilityClass(int intValue)
        {
            IntValue = intValue;
        }

        public static string Json()
        {
            return @"{""IntValue"" : 10,""StrValue"" : ""Hello""}";
        }
    }

    public class TestClass
    {
        public string StringProperty { get; set; }
        public Dictionary<string, int> StringCounts { get; set; }
        public int[] Int32s { get; set; }
        public bool BoolT { get; set; }
        public bool BoolF { get; set; }
        public int Int32 { get; set; }
        public TestClass Child { get; set; }

        public static string Json()
        {
            return @"
{
    ""StringProperty"" : ""TheValue"",
    ""Int32s"" : [ 1 ,2, 3, 4, -5 ], 
    ""BoolT"" : true, 
    ""BoolF"" : false ,
    ""Child"" : null,
    ""StringCounts"" : { ""Apples"" : 2, ""Bananas"" : 3 }
}";
        }
    }
}
