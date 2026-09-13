using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.ServiceBus.Receptor.Abstract;

/// <summary>
/// Defines an Azure Service Bus receptor that must be initialized before it can dispatch messages.
/// </summary>
public interface IServiceBusReceptor : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Provisions the configured queue when necessary, creates the processor, and starts message processing. Repeated calls share the running processor.
    /// </summary>
    /// <remarks>
    /// Reads <c>Azure:ServiceBus:MaxConcurrentCalls</c> (default 1) and <c>Azure:ServiceBus:PrefetchCount</c> (default 0) at construction.
    /// Increasing concurrency permits overlapping handlers; prefetched messages consume memory and their broker locks continue to age.
    /// Initialization is serialized with disposal. Failed startup is cleaned up and can be retried; initialization after disposal throws.
    /// </remarks>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the init operation is complete.</returns>
    Task Init(CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles an incoming message with the specified content and type.
    /// </summary>
    /// <param name="messageContent">The content of the received message. Cannot be null.</param>
    /// <param name="type">The type or category of the message. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that completes when application handling has finished. Successful completion allows the receptor to complete the broker message.</returns>
    ValueTask OnMessageReceived(string messageContent, string type, CancellationToken cancellationToken = default);
}
