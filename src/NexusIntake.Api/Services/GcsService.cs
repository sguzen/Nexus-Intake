using Google.Cloud.Storage.V1;

namespace NexusIntake.Api.Services;

public interface IGcsService
{
    Task<string> UploadAsync(string objectName, byte[] data, string contentType, CancellationToken ct = default);
}

public class GcsService : IGcsService
{
    private readonly StorageClient _storageClient;
    private readonly string _bucketName;

    public GcsService(StorageClient storageClient, IConfiguration configuration)
    {
        _storageClient = storageClient;
        _bucketName = configuration["Gcs:BucketName"] ?? "nexus-intake-raw";
    }

    public async Task<string> UploadAsync(string objectName, byte[] data, string contentType, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(data);
        var obj = await _storageClient.UploadObjectAsync(
            bucket: _bucketName,
            objectName: objectName,
            contentType: contentType,
            source: stream,
            cancellationToken: ct);

        return $"gs://{_bucketName}/{objectName}";
    }
}
