using System;
using System.Threading.Tasks;

namespace Soenneker.ServiceBus.Receptor.Abstract;

/// <summary>
/// A utility library for receiving Azure Service Bus messages <para/>
/// Be sure to warm implementations of these <para/>
/// Singleton IoC
/// </summary>
public interface IServiceBusReceptor : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Must remain task
    /// </summary>
    Task Init();

    ValueTask OnMessageReceived(string messageContent, Type? type);
}