using System;

class FakeTestAttribute : Attribute { }
class TestMethodAttribute : Attribute { }

class UnitLike
{
    [TestMethod]
    public void ShouldBeAllowedByConfig()
    {
        // No MNA0003/MNA0003A due to allow_in_tests = true
        Console.WriteLine("[APP] In tests");
    }
}
