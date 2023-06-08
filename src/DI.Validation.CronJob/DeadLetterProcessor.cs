using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DI.Validation.CronJob.Components.Blob;
using DI.Validation.CronJob.Configurations;
using DI.Validation.CronJob.Models;

namespace DI.Validation.CronJob;

public class DeadLetterProcessor : IDeadLetterProcessor
{
    private const string QueueName = "subscriptionoutbox";

    private readonly ILogger<IDeadLetterProcessor> _logger;
    private readonly IBlobClient _blobClient;
    private readonly ServiceBusConfiguration _serviceBusConfiguration;

    public DeadLetterProcessor(ILogger<IDeadLetterProcessor> logger, ServiceBusConfiguration serviceBusConfiguration, IBlobClient blobClient)
    {
        _logger = logger;
        _serviceBusConfiguration = serviceBusConfiguration;
        _blobClient = blobClient;
    }

    public async Task Execute(CancellationToken cancellationToken)
    {
        try
        {
            await using var client = new ServiceBusClient(_serviceBusConfiguration.QueueConnectionString, new ServiceBusClientOptions()
            {
                RetryOptions = new ServiceBusRetryOptions()
                {
                    Mode = ServiceBusRetryMode.Exponential,
                    MaxRetries = 3,
                    MaxDelay = TimeSpan.FromSeconds(10),
                },
            });
            var receiver = client.CreateReceiver(QueueName, new ServiceBusReceiverOptions()
            {
                SubQueue = SubQueue.DeadLetter
            });

            var receiveMessages = receiver.ReceiveMessagesAsync(cancellationToken: cancellationToken);
            var processedMessages = new ConcurrentDictionary<string, List<ServiceBusReceivedMessage>>();

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var messageCount = 0;
            var invalidMessages = new List<ServiceBusReceivedMessage>();
            var validMessages = new List<ServiceBusReceivedMessage>();
            await foreach (var message in receiveMessages.WithCancellation(cancellationToken))
            {
                if (messageCount >= 200)
                {
                    _logger.LogInformation("Skip message: {count}", ++messageCount);
                    break;
                }

                if (!string.IsNullOrEmpty(message.DeadLetterReason) && message.DeliveryCount >= 3)
                {
                    invalidMessages.Add(message);
                    _logger.LogInformation("Processed {count} messages.", ++messageCount);
                    continue;
                }

                validMessages.Add(message);
                _logger.LogInformation("Processed {count} messages.", ++messageCount);
            }

            processedMessages.TryAdd(ProcessingType.Valid, validMessages);
            processedMessages.TryAdd(ProcessingType.Invalid, invalidMessages);

            _logger.LogInformation("Saved messages to concurrentDictionary in {elapsedTime} ms.", stopWatch.ElapsedMilliseconds);

            var sender = client.CreateSender(QueueName);
            await ProcessValidMessages(processedMessages, sender, cancellationToken);

            await ProcessInValidMessages(processedMessages, receiver, cancellationToken);
            await client.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing messages from deadletter. See exception for details.");
            throw;
        }
    }

    private async Task ProcessInValidMessages(ConcurrentDictionary<string, List<ServiceBusReceivedMessage>> messages, ServiceBusReceiver receiver, CancellationToken cancellationToken)
    {
        try
        {
            var blobPath = $"{DateTime.Today:ddMMyyyy}/{DateTimeOffset.Now.ToUnixTimeSeconds()}.txt";
            var logLines = messages
                .Where(x => x.Key == ProcessingType.Invalid)
                .SelectMany(x => x.Value)
                .Select(x => $"MessageId:{x.MessageId};DeadLetterReason:{x.DeadLetterReason};DeadLetterErrorDescription:{x.DeadLetterErrorDescription};DeliveryCount:{x.DeliveryCount}")
                .Aggregate((current, next) => current + Environment.NewLine + next);

            var result = await _blobClient.UpdateBlobAsync(blobPath, logLines, cancellationToken);
            if (result.Status != 201)
            {
                _logger.LogError("Error while updating blob. Status code: {status}, Content: {content}", result.Status, JsonSerializer.Serialize(result.Content));
            }

            var count = 0;
            await Parallel.ForEachAsync(messages.Where(x => x.Key == ProcessingType.Invalid).SelectMany(x => x.Value), new ParallelOptions()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = -1
            }, async (message, token) =>
            {
                await receiver.CompleteMessageAsync(message, token);
                _logger.LogInformation("Completed message with id {messageId}. Count {count}", message.MessageId, ++count);
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while processing invalid messages. See exception for details.");
            throw;
        }
        finally
        {
            await receiver.DisposeAsync();
        }
    }

    private async Task ProcessValidMessages(ConcurrentDictionary<string, List<ServiceBusReceivedMessage>> messages, ServiceBusSender sender, CancellationToken cancellationToken)
    {
        try
        {
            var serviceBusMessages = messages.Where(x => x.Key == ProcessingType.Valid).SelectMany(x => x.Value).Select(x => new ServiceBusMessage(x)).ToList();
            await sender.SendMessagesAsync(serviceBusMessages, cancellationToken);

            _logger.LogInformation("Sent {count} messages to queue.", serviceBusMessages.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error while processing valid messages. See exception for details.");
            throw;
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }
}