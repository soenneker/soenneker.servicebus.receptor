using Soenneker.Tests.HostedUnit;

namespace Soenneker.ServiceBus.Receptor.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class ServiceBusReceptorTests : HostedUnitTest
{
    public ServiceBusReceptorTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {

    }
}
