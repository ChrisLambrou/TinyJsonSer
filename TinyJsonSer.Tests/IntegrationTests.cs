using System.Collections.Generic;
using NUnit.Framework;

namespace TinyJsonSer.Tests
{
    public class IntegrationTests
    {
        [Test]
        public void RoundTrip()
        {
            var serialiser = new JsonSerializer(true);
            var deserialiser = new JsonDeserializer();

            var testClass = new TestClass
            {
                BoolF = false,
                BoolT = true,
                Child = new TestClass { Int32 = 30, Int32s = new[] { 1, -2, 3, -4, 5 } },
                StringCounts = new Dictionary<string, int> { { "blah", 6 } }
            };

            var serialised = serialiser.Serialize(testClass);
            var returned = deserialiser.Deserialize<TestClass>(serialised);

            Assert.AreEqual(testClass.BoolF, returned.BoolF);
            Assert.AreEqual(testClass.BoolT, returned.BoolT);
            Assert.AreEqual(6, returned.StringCounts["blah"]);
            Assert.AreEqual(30, returned.Child.Int32);
            CollectionAssert.AreEqual(testClass.Child.Int32s, returned.Child.Int32s);
        }
    }
}
