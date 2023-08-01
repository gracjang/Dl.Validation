using Azure.Storage.Blobs;

namespace DI.Validation.CronJob.Components.Blob;

public interface IBlobClientFactory
{
    BlobServiceClient GetBlobServiceClient(string connectionString);
}