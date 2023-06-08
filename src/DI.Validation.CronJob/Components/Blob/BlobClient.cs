using System.Text;
using Azure;
using Azure.Storage.Blobs;
using DI.Validation.CronJob.Configurations;

namespace DI.Validation.CronJob.Components.Blob;

public class BlobClient : IBlobClient
{
    private readonly BlobContainerClient _blobContainerClient;

    public BlobClient(IBlobClientFactory blobClientFactory, ContainerConfiguration containerConfiguration)
    {
        _blobContainerClient = blobClientFactory.GetBlobServiceClient(containerConfiguration.BlobConnectionString)
            .GetBlobContainerClient(containerConfiguration.BlobContainerName);
    }

    public async Task<Response> UpdateBlobAsync(string blobName, string content, CancellationToken cancellationToken)
    {
        await _blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var bytes = Encoding.UTF8.GetBytes(content);
        using var uploadStream = new MemoryStream(bytes);
        var result = await _blobContainerClient.UploadBlobAsync(blobName, uploadStream, cancellationToken: cancellationToken);
        return result.GetRawResponse();
    }
}