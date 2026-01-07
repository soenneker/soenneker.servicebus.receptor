using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.ServiceBus.Client.Abstract;
using Soenneker.ServiceBus.Queue.Abstract;
using Soenneker.ServiceBus.Receptor.Abstract;

namespace Soenneker.ServiceBus.Receptor;

/// <inheritdoc cref="IServiceBusReceptor"/>
public abstract class ServiceBusReceptor : IServiceBusReceptor
{
    protected ILogger<ServiceBusReceptor> Logger { get; }

    protected string Queue { get; }

    protected IConfiguration Config { get; }

    private ServiceBusProcessor? _processor;

    private Func<ProcessMessageEventArgs, Task>? _messageHandler;
    private Func<ProcessErrorEventArgs, Task>? _errorHandler;

    private readonly bool _log;
    private readonly IServiceBusClientUtil _serviceBusClientUtil;
    private readonly IServiceBusQueueUtil _serviceBusQueueUtil;

    // Avoid closure by storing the init token here.
    private CancellationToken _initToken;

    // Type.GetType is expensive; cache by the string payload.
    private static readonly ConcurrentDictionary<string, Type?> _typeCache = new(StringComparer.Ordinal);

    private static readonly ServiceBusProcessorOptions _processorOptions = new()
    {
        MaxConcurrentCalls = 1,
        AutoCompleteMessages = false
    };

    protected ServiceBusReceptor(string queue, ILogger<ServiceBusReceptor> logger, IServiceBusClientUtil serviceBusClientUtil,
        IServiceBusQueueUtil serviceBusQueueUtil, IConfiguration config)
    {
        Logger = logger;
        Queue = queue;
        _serviceBusClientUtil = serviceBusClientUtil;
        _serviceBusQueueUtil = serviceBusQueueUtil;
        Config = config;

        _log = config.GetValue<bool>("Azure:ServiceBus:Log");
    }

    public async Task Init(CancellationToken cancellationToken = default)
    {
        // Capture once; used by the message handler without closures.
        _initToken = cancellationToken;

        await _serviceBusQueueUtil.CreateQueueIfDoesNotExist(Queue, cancellationToken)
                                  .NoSync();

        ServiceBusClient client = await _serviceBusClientUtil.Get(cancellationToken)
                                                             .NoSync();

        _processor = client.CreateProcessor(Queue, _processorOptions);

        // Method groups (no closure alloc). Store references for unsub.
        _messageHandler = ProcessMessageAsync;
        _errorHandler = ProcessErrorAsync;

        _processor.ProcessMessageAsync += _messageHandler;
        _processor.ProcessErrorAsync += _errorHandler;

        await _processor.StartProcessingAsync(cancellationToken)
                        .NoSync();
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        CancellationToken cancellationToken = _initToken;

        var messageStr = args.Message.Body.ToString();

        if (_log && Logger.IsEnabled(LogLevel.Debug))
            Logger.LogDebug("Received message: {message}", messageStr);

        Type? runtimeType = null;

        if (args.Message.ApplicationProperties.TryGetValue("type", out object? typeObj))
        {
            if (typeObj is string typeStr)
            {
                if (typeStr.IsNullOrEmpty())
                {
                    Logger.LogError("ServiceBus message was not properly formed");
                }
                else
                {
                    // Cache the resolution result (including null if it can't be resolved).
                    runtimeType = _typeCache.GetOrAdd(typeStr, static s => Type.GetType(s, throwOnError: false));
                }
            }
            else if (typeObj != null)
            {
                Logger.LogError("Type was not of type string during {handler}", nameof(ProcessMessageAsync));
            }
        }

        Logger.LogInformation("Received {queue} queue message with content: {content} - type: {type}", Queue, messageStr, runtimeType?.Name);

        await OnMessageReceived(messageStr, runtimeType, cancellationToken)
            .NoSync();

        // Complete the message (delete from queue)
        await args.CompleteMessageAsync(args.Message, cancellationToken)
                  .NoSync();
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        Logger.LogError(args.Exception, "Error processing message");
        return Task.CompletedTask;
    }

    public abstract ValueTask OnMessageReceived(string messageContent, Type? type, CancellationToken cancellationToken = default);

    public ValueTask DisposeAsync() => DisposeInternal();

    public void Dispose()
    {
        // If you can, prefer only IAsyncDisposable and avoid sync-over-async.
        DisposeInternal()
            .NoSync()
            .GetAwaiter()
            .GetResult();
    }

    private async ValueTask DisposeInternal()
    {
        ServiceBusProcessor? processor = _processor;

        if (processor is null)
            return;

        try
        {
            await processor.StopProcessingAsync()
                           .NoSync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while stopping the processor.");
        }

        try
        {
            if (_messageHandler is not null)
            {
                processor.ProcessMessageAsync -= _messageHandler;
                _messageHandler = null;
            }

            if (_errorHandler is not null)
            {
                processor.ProcessErrorAsync -= _errorHandler;
                _errorHandler = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while unsubscribing event handlers.");
        }

        try
        {
            await processor.DisposeAsync()
                           .NoSync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while disposing the processor.");
        }

        _processor = null;
    }
}