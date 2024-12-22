using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.ServiceBus.Receptor.Tests;

[Collection("Collection")]
public class ServiceBusReceptorTests : FixturedUnitTest
{
    public ServiceBusReceptorTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public void Default()
    {

    }
}
