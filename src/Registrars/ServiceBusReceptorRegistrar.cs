using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.ServiceBus.Queue.Registrars;
using Soenneker.ServiceBus.Receptor.Abstract;

namespace Soenneker.ServiceBus.Receptor.Registrars;

/// <summary>
/// A utility library for receiving Azure Service Bus messages
/// </summary>
public static class ServiceBusReceptorRegistrar
{
    /// <summary>
    /// As Singleton
    /// </summary>
    public static void AddServiceBusReceptor(this IServiceCollection services)
    {
        services.AddServiceBusQueueUtil();
        services.TryAddSingleton<IServiceBusReceptor, ServiceBusReceptor>();
    }
}