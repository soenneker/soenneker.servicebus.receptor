using System;
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

public abstract class ServiceBusReceptor : IServiceBusReceptor
{
    protected ILogger<ServiceBusReceptor> Logger { get; }

    protected string Queue { get; }

    protected IConfiguration Config { get; }

    private static readonly Action<ILogger, string, string?, Exception?> _received =
        LoggerMessage.Define<string, string?>(LogLevel.Information, new EventId(1, "Received"), "Received {queue} queue message - type: {type}");

    private ServiceBusProcessor? _processor;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private bool _disposed;

    private Func<ProcessMessageEventArgs, Task>? _messageHandler;
    private Func<ProcessErrorEventArgs, Task>? _errorHandler;

    private readonly bool _log;
    private readonly IServiceBusClientUtil _serviceBusClientUtil;
    private readonly IServiceBusQueueUtil _serviceBusQueueUtil;

    private readonly ServiceBusProcessorOptions _processorOptions;

    protected ServiceBusReceptor(string queue, ILogger<ServiceBusReceptor> logger, IServiceBusClientUtil serviceBusClientUtil,
        IServiceBusQueueUtil serviceBusQueueUtil, IConfiguration config)
    {
        Logger = logger;
        Queue = queue;
        _serviceBusClientUtil = serviceBusClientUtil;
        _serviceBusQueueUtil = serviceBusQueueUtil;
        Config = config;

        _log = config.GetValue<bool>("Azure:ServiceBus:Log");
        _processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = config.GetValue<int?>("Azure:ServiceBus:MaxConcurrentCalls") ?? 1,
            PrefetchCount = config.GetValue<int?>("Azure:ServiceBus:PrefetchCount") ?? 0,
            AutoCompleteMessages = false
        };
    }

    public async Task Init(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).NoSync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_processor is not null)
                return;

            await _serviceBusQueueUtil.CreateQueueIfDoesNotExist(Queue, cancellationToken).NoSync();
            ServiceBusClient client = await _serviceBusClientUtil.Get(cancellationToken).NoSync();
            ServiceBusProcessor processor = client.CreateProcessor(Queue, _processorOptions);
            _messageHandler ??= ProcessMessageAsync;
            _errorHandler ??= ProcessErrorAsync;
            try
            {
                processor.ProcessMessageAsync += _messageHandler;
                processor.ProcessErrorAsync += _errorHandler;
                await processor.StartProcessingAsync(cancellationToken).NoSync();
                _processor = processor;
            }
            catch
            {
                // A failed startup must not retain a processor or prevent a later retry.
                await DisposeProcessor(processor).NoSync();
                throw;
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        CancellationToken cancellationToken = args.CancellationToken;

        var messageStr = args.Message.Body.ToString();

        if (_log && Logger.IsEnabled(LogLevel.Debug))
            Logger.LogDebug("Received message: {message}", messageStr);

        string? type = null!;

        if (args.Message.ApplicationProperties.TryGetValue("type", out object? typeObj))
        {
            if (typeObj is string typeStr)
            {
                if (typeStr.IsNullOrEmpty())
                {
                    Logger.LogError("ServiceBus message was not properly formed (type is missing)");
                }
                else
                {
                    type = typeStr;
                }
            }
            else if (typeObj != null)
            {
                Logger.LogError("Type was not of type string during {handler}", nameof(ProcessMessageAsync));
            }
        }

        _received(Logger, Queue, type, null);

        await OnMessageReceived(messageStr, type, cancellationToken)
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

    public abstract ValueTask OnMessageReceived(string messageContent, string type, CancellationToken cancellationToken = default);
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
        await _lifecycle.WaitAsync().NoSync();
        try
        {
            if (_disposed)
                return;
            _disposed = true;
            ServiceBusProcessor? processor = _processor;
            _processor = null;
            if (processor is not null)
                await DisposeProcessor(processor).NoSync();
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async ValueTask DisposeProcessor(ServiceBusProcessor processor)
    {
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
            }

            if (_errorHandler is not null)
            {
                processor.ProcessErrorAsync -= _errorHandler;
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

    }
}
