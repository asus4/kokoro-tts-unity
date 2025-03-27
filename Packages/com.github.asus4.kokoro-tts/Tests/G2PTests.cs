using NUnit.Framework;
using Unity.Mathematics;

namespace Kokoro.Tests
{
    [TestFixture]
    public class G2PTests
    {
        [TestCase(1000, 560, 1920, 1080, 1000, 8)] // landscape
        [TestCase(560, 1000, 1080, 1920, 1000, 8)] // portrait
        public void TestResizeToMaxSize(int expectedX, int expectedY, int inputX, int inputY, int maxSize, int alignmentSize)
        {

        }
    }
}
