using Azure;

namespace DI.Validation.CronJob.Components.Blob;

public interface IBlobClient
{
    Task<Response> UpdateBlobAsync(string blobName, string content, CancellationToken cancellationToken);
}