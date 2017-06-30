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
            var bananas = testClass.StringCounts["Bananas"];
            var apples = testClass.StringCounts["Apples"];
            Assert.AreEqual(2, apples);
            Assert.AreEqual(3, bananas);
        }

        [TestCase(@"{""BoolT"" : null }")]
        [TestCase(@"{""Int32"" : null }")]
        public void CantDeserializeNullToValueType(string nullToValueTypeJson)
        {
            var deserialiser = new JsonDeserializer();
            Assert.That(() =>
                deserialiser.Deserialize<TestClass>(nullToValueTypeJson),
                Throws.Exception.TypeOf<JsonException>().With.Message.Contain("value type"));
        }

        enum TestEnum { Red, Green, Blue }
        [Test]
        public void EnumDeserialization()
        {
            var deserialiser = new JsonDeserializer();
            var val = deserialiser.Deserialize<TestEnum>("\"Green\"");
            Assert.AreEqual(TestEnum.Green, val);
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
    ""Int32s"" : [ 1 ,2, 3,4,-5 ], 
    ""BoolT"" : true, 
    ""BoolF"" : false ,
    ""Child"" : null,
    ""StringCounts"" : { ""Apples"" : 2, ""Bananas"" : 3 }
}";
        }
    }
}
