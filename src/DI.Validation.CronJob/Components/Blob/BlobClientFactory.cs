using System.Collections.Concurrent;
using Azure.Storage.Blobs;

namespace DI.Validation.CronJob.Components.Blob;

public class BlobClientFactory : IBlobClientFactory
{
    private readonly ConcurrentDictionary<string, Lazy<BlobServiceClient>> _blobClientsHolder = new();

    public BlobServiceClient GetBlobServiceClient(string connectionString)
    {
        var lazyBlobServiceClient = _blobClientsHolder.GetOrAdd(connectionString, new Lazy<BlobServiceClient>(() => new BlobServiceClient(connectionString)));
        return lazyBlobServiceClient.Value;
    }
}