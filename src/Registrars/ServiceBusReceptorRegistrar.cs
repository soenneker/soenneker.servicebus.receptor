using Microsoft.Extensions.DependencyInjection;
using Soenneker.ServiceBus.Queue.Abstract;
using Soenneker.ServiceBus.Queue.Registrars;

namespace Soenneker.ServiceBus.Receptor.Registrars;

/// <summary>
/// An abstract Service Bus class meant to be derived by specific bus receptors. A 'Receptor' is a specific class for a particular message type.
/// </summary>
public static class ServiceBusReceptorRegistrar
{
    /// <summary>
    /// Does not add ServiceBusReceptor (since it's abstract), but adds <see cref="IServiceBusQueueUtil"/> (and dependencies)
    /// </summary>
    public static IServiceCollection AddServiceBusReceptorAsSingleton(this IServiceCollection services)
    {
        services.AddServiceBusQueueUtilAsSingleton();

        return services;
    }

    /// <summary>
    /// Does not add ServiceBusReceptor (since it's abstract), but adds <see cref="IServiceBusQueueUtil"/> (and dependencies)
    /// </summary>
    public static IServiceCollection AddServiceBusReceptorAsScoped(this IServiceCollection services)
    {
        services.AddServiceBusQueueUtilAsScoped();

        return services;
    }
}