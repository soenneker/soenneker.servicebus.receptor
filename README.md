[![](https://img.shields.io/nuget/v/Soenneker.ServiceBus.Receptor.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.ServiceBus.Receptor/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.servicebus.receptor/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.servicebus.receptor/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.ServiceBus.Receptor.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.ServiceBus.Receptor/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.servicebus.receptor/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.servicebus.receptor/actions/workflows/codeql.yml)

# Soenneker.ServiceBus.Receptor

An abstract Service Bus class meant to be derived by specific bus receptors. Be sure to warm implementations of these Singleton IoC.

## Install

```bash
dotnet add package Soenneker.ServiceBus.Receptor
```

## Quick start

```csharp
using Soenneker.ServiceBus.Receptor.Registrars;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var result = services.AddServiceBusReceptorAsSingleton();
```

Does not add ServiceBusReceptor (since it's abstract), but adds `IServiceBusQueueUtil` (and dependencies).

## What you get

- `IServiceBusReceptor` — An abstract Service Bus class meant to be derived by specific bus receptors. Be sure to warm implementations of these Singleton IoC.
- `ServiceBusReceptorRegistrar` — An abstract Service Bus class meant to be derived by specific bus receptors. A 'Receptor' is a specific class for a particular message type.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `IServiceBusReceptor.Init(cancellationToken)` | Must remain task. | A task that completes when the init operation is complete. |
| `IServiceBusReceptor.OnMessageReceived(messageContent, type, cancellationToken)` | Handles an incoming message with the specified content and type. | A ValueTask that represents the asynchronous handling of the message. |
| `ServiceBusReceptorRegistrar.AddServiceBusReceptorAsSingleton(services)` | Does not add ServiceBusReceptor (since it's abstract), but adds `IServiceBusQueueUtil` (and dependencies). | The same service collection, so additional registrations can be chained. |
| `ServiceBusReceptorRegistrar.AddServiceBusReceptorAsScoped(services)` | Does not add ServiceBusReceptor (since it's abstract), but adds `IServiceBusQueueUtil` (and dependencies). | The same service collection, so additional registrations can be chained. |

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.
- Calls that return a cached or singleton value reuse the same instance until the owning service is disposed.
- Dispose instances you own when their scope ends so held resources can be released.
