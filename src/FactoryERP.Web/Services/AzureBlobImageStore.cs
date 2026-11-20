using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace FactoryERP.Web.Services;

/// <summary>
/// Abstracción para persistencia externa de imágenes en producción (Azure Blob Storage).
/// </summary>
public interface IExternalImageStore
{
    bool IsEnabled { get; }
    Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType)?> OpenReadAsync(string blobPath, CancellationToken ct = default);
}

public sealed class AzureBlobImageStore : IExternalImageStore
{
    private readonly BlobContainerClient? _containerClient;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _containerEnsured;

    public AzureBlobImageStore()
        : this(
            Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING"),
            Environment.GetEnvironmentVariable("AZURE_STORAGE_CONTAINER_NAME") ?? "uploads")
    {
    }

    public AzureBlobImageStore(string? connectionString, string containerName)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        string safeContainer = string.IsNullOrWhiteSpace(containerName) ? "uploads" : containerName.Trim().ToLowerInvariant();
        _containerClient = new BlobServiceClient(connectionString).GetBlobContainerClient(safeContainer);
    }

    public bool IsEnabled => _containerClient is not null;

    public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default)
    {
        if (_containerClient is null)
            throw new InvalidOperationException("Azure Blob Storage no está configurado.");

        await EnsureContainerAsync(ct);
        BlobClient blob = _containerClient.GetBlobClient(blobPath);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
    }

    public async Task<(Stream Stream, string ContentType)?> OpenReadAsync(string blobPath, CancellationToken ct = default)
    {
        if (_containerClient is null) return null;

        BlobClient blob = _containerClient.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(ct)) return null;

        var download = await blob.DownloadStreamingAsync(cancellationToken: ct);
        string contentType = download.Value.Details.ContentType ?? "application/octet-stream";
        return (download.Value.Content, contentType);
    }

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerEnsured || _containerClient is null) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_containerEnsured) return;
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            _containerEnsured = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
