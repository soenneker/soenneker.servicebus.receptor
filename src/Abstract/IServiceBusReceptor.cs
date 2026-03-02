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

    /// <summary>
    /// Handles an incoming message with the specified content and type.
    /// </summary>
    /// <param name="messageContent">The content of the received message. Cannot be null.</param>
    /// <param name="type">The type or category of the message. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A ValueTask that represents the asynchronous handling of the message.</returns>
    ValueTask OnMessageReceived(string messageContent, string type, CancellationToken cancellationToken = default);
}