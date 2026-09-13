using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Audit;

public class ReceptorLifecycleTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    [Test]
    public async Task ProcessorLifecycleIsIdempotentAndFailedStartupIsDisposed()
    {
        var client = new FakeClient();
        var receptor = new TestReceptor(new ClientUtil(client));
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => receptor.Init()));
        Check(client.Processors.Count == 1 && client.Processors[0].Starts == 1, "Init leaked duplicate processors");
        await Task.WhenAll(receptor.DisposeAsync().AsTask(), receptor.DisposeAsync().AsTask());
        Check(client.Processors[0].Disposals == 1, "Processor was not disposed exactly once");
        try { await receptor.Init(); throw new Exception("Reinitialized disposed receptor"); }
        catch (ObjectDisposedException) { }

        var failing = new FakeClient { FailFirst = true };
        await using var retry = new TestReceptor(new ClientUtil(failing));
        try { await retry.Init(); throw new Exception("Expected startup failure"); }
        catch (InvalidOperationException) { }
        Check(failing.Processors[0].Disposals == 1, "Failed startup leaked processor");
        await retry.Init();
        Check(failing.Processors.Count == 2, "Failed initialization could not retry");
    }
    private sealed class TestReceptor(ClientUtil client) : Soenneker.ServiceBus.Receptor.ServiceBusReceptor("audit",
        NullLogger<Soenneker.ServiceBus.Receptor.ServiceBusReceptor>.Instance, client, new NoopQueues(), Fixture.Config())
    {
        public override ValueTask OnMessageReceived(string content, string type, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
    private sealed class NoopQueues : Soenneker.ServiceBus.Queue.Abstract.IServiceBusQueueUtil
    {
        public ValueTask CreateQueueIfDoesNotExist(string queue, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask EmptyQueue(string queue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class ClientUtil(FakeClient client) : Soenneker.ServiceBus.Client.Abstract.IServiceBusClientUtil
    {
        public ValueTask<ServiceBusClient> Get(CancellationToken cancellationToken = default) => ValueTask.FromResult<ServiceBusClient>(client);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class FakeClient : ServiceBusClient
    {
        public bool FailFirst;
        public List<FakeProcessor> Processors { get; } = [];
        public override ServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions options)
        {
            var processor = new FakeProcessor { FailStart = FailFirst && Processors.Count == 0 };
            Processors.Add(processor);
            return processor;
        }
    }
    private sealed class FakeProcessor : ServiceBusProcessor
    {
        public int Starts; public int Disposals; public bool FailStart;
        public override async Task StartProcessingAsync(CancellationToken cancellationToken = default)
        {
            Starts++; await Task.Yield();
            if (FailStart) throw new InvalidOperationException("start failed");
        }
        public override Task StopProcessingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task CloseAsync(CancellationToken cancellationToken = default) { Disposals++; return Task.CompletedTask; }
    }
}

internal static class Fixture
{
    public static Microsoft.Extensions.Configuration.IConfiguration Config(bool logging = false, bool counts = true) =>
        new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Azure:ServiceBus:Enable"] = "true", ["Azure:ServiceBus:TransmitterLogging"] = logging.ToString(),
            ["Background:QueueLength"] = "32", ["Background:LockCounts"] = counts.ToString()
        }).Build();
}
