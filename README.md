[![](https://img.shields.io/nuget/v/Soenneker.ServiceBus.Receptor.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.ServiceBus.Receptor/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.servicebus.receptor/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.servicebus.receptor/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.ServiceBus.Receptor.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.ServiceBus.Receptor/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.servicebus.receptor/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.servicebus.receptor/actions/workflows/codeql.yml)

# Soenneker.ServiceBus.Receptor

An abstract Azure Service Bus queue processor that provisions its queue, dispatches body and type data to a derived receptor, and completes successfully handled messages.

## Installation

```bash
dotnet add package Soenneker.ServiceBus.Receptor
```

## Implement a receptor

Derive from `ServiceBusReceptor`, choose the queue in the base constructor, and implement the application handler:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soenneker.ServiceBus.Client.Abstract;
using Soenneker.ServiceBus.Queue.Abstract;
using Soenneker.ServiceBus.Receptor;

public sealed class OrderReceptor : ServiceBusReceptor
{
    public OrderReceptor(
        ILogger<ServiceBusReceptor> logger,
        IServiceBusClientUtil clientUtil,
        IServiceBusQueueUtil queueUtil,
        IConfiguration configuration)
        : base("orders", logger, clientUtil, queueUtil, configuration)
    {
    }

    public override async ValueTask OnMessageReceived(
        string messageContent,
        string type,
        CancellationToken cancellationToken = default)
    {
        switch (type)
        {
            case "order.created.v1":
                await HandleOrderCreated(messageContent, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"Unsupported message type: {type}");
        }
    }
}
```

The body is supplied as a string. The type comes from the Service Bus message's `ApplicationProperties["type"]` value and is expected to be a non-empty string. Producers using `Soenneker.ServiceBus.Message` receive this property automatically.

## Register and start

The base registrar adds queue, administration, and client dependencies; it cannot register your abstract-derived type for you:

```csharp
using Soenneker.ServiceBus.Receptor.Registrars;

services.AddServiceBusReceptorAsSingleton();
services.AddSingleton<OrderReceptor>();
```

Resolve the receptor during application startup and initialize it once:

```csharp
OrderReceptor receptor = services.GetRequiredService<OrderReceptor>();
await receptor.Init(cancellationToken);
```

Simply registering the receptor does not start it. `Init` creates the queue with Azure defaults when absent, creates a processor, attaches handlers, and starts processing. The connection-string credential therefore needs both queue-management and receive permissions.

Registering the base dependencies as scoped is also supported with `AddServiceBusReceptorAsScoped()`. The scoped queue utility retains singleton administration and data-plane clients.

## Processing and settlement

The processor uses peek-lock mode, disables automatic completion, and handles one message at a time. After `OnMessageReceived` completes successfully, the receptor explicitly completes the message.

If the handler or completion throws, the exception flows back to the Azure processor and the message is not completed by this class. Configure queue retry and dead-letter behavior for your workload, and make handlers idempotent because a message may be delivered again.

`Azure:ServiceBus:Log=true` enables full message-body logging at debug level. Bodies may contain credentials or personal data, so leave this disabled unless the log destination and retention policy are suitable. Information logs identify the queue and message type without recording the body.

Dispose the receptor during application shutdown. Disposal stops processing, removes its event handlers, and disposes the processor; it does not dispose the shared top-level Service Bus client.
