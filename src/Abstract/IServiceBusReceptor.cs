using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.ServiceBus.Receptor.Abstract;

/// <summary>
/// An abstract Service Bus class meant to be derived by specific bus receptors. <para/>
/// Be sure to warm implementations of these <para/>
/// Singleton IoC
/// </summary>
public interface IServiceBusReceptor : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Must remain task
    /// </summary>
    Task Init(CancellationToken cancellationToken = default);

    ValueTask OnMessageReceived(string messageContent, Type? type, CancellationToken cancellationToken = default);
}