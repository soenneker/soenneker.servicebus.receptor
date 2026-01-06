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

///<inheritdoc cref="IServiceBusReceptor"/>
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

    protected ServiceBusReceptor(string queue, ILogger<ServiceBusReceptor> logger, IServiceBusClientUtil serviceBusClientUtil, IServiceBusQueueUtil serviceBusQueueUtil, IConfiguration config)
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
        await _serviceBusQueueUtil.CreateQueueIfDoesNotExist(Queue, cancellationToken).NoSync();

        ServiceBusClient client = await _serviceBusClientUtil.Get(cancellationToken).NoSync();

        var options = new ServiceBusProcessorOptions {MaxConcurrentCalls = 1, AutoCompleteMessages = false};

        _processor = client.CreateProcessor(Queue, options);

        _messageHandler = args => MessageHandler(args, cancellationToken);
        _errorHandler = ErrorHandler;

        _processor.ProcessMessageAsync += _messageHandler;
        _processor.ProcessErrorAsync += _errorHandler;

        await _processor.StartProcessingAsync(cancellationToken).NoSync();
    }

    /// <summary>
    /// Must remain Task for handler hook
    /// </summary>
    private async Task MessageHandler(ProcessMessageEventArgs args, CancellationToken cancellationToken = default)
    {
        var messageStr = args.Message.Body.ToString();

        if (_log)
            Logger.LogDebug("Received message: {message}", messageStr);

        Type? runtimeType = null;

        args.Message.ApplicationProperties.TryGetValue("type", out object? type);

        // ServiceBus doesn't support direct usage of Type :(
        if (type != null)
        {
            if (type is string value)
            {
                if (value.IsNullOrEmpty())
                    Logger.LogError("ServiceBus message was not properly formed");
                else
                    runtimeType = Type.GetType(value);
            }
            else
                Logger.LogError("Type was not of type string during {handler}", nameof(MessageHandler));
        }

        PreOnMessageReceived(messageStr, runtimeType);

        await OnMessageReceived(messageStr, runtimeType, cancellationToken).NoSync();

        // complete the message. messages are deleted from the queue. 
        await args.CompleteMessageAsync(args.Message, cancellationToken).NoSync();
    }

    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        Logger.LogError(args.Exception, "Error processing message");
        return Task.CompletedTask;
    }

    public abstract ValueTask OnMessageReceived(string messageContent, Type? type, CancellationToken cancellationToken = default);

    private void PreOnMessageReceived(string messageContent, Type? type)
    {
        Logger.LogInformation("Received {queue} queue message with content: {content} - type: {type}", Queue, messageContent, type?.Name);
    }

    public ValueTask DisposeAsync()
    {
        return DisposeInternal();
    }

    public void Dispose()
    {
        DisposeInternal().NoSync().GetAwaiter().GetResult();
    }

    private async ValueTask DisposeInternal()
    {
        if (_processor == null)
            return;

        try
        {
            await _processor.StopProcessingAsync().NoSync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while stopping the processor.");
        }

        try
        {
            if (_messageHandler != null)
            {
                _processor.ProcessMessageAsync -= _messageHandler;
                _messageHandler = null;
            }

            if (_errorHandler != null)
            {
                _processor.ProcessErrorAsync -= _errorHandler;
                _errorHandler = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while unsubscribing event handlers.");
        }

        try
        {
            await _processor.DisposeAsync().NoSync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while disposing the processor.");
        }

        _processor = null;
    }
}