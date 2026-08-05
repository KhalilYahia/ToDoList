using OpsManager.Service.Abstractions;

namespace OpsManager.Api.Infrastructure;

public sealed class DynamicFileStorageService(
    LocalFileStorageService localStorage,
    FileStorageOptions options) : IFileStorageService
{
    public async Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        return options.Provider switch
        {
            FileStorageProvider.Local => await localStorage.SaveAsync(content, fileName, contentType, cancellationToken),
            FileStorageProvider.S3 => await SaveToS3Async(content, fileName, contentType, cancellationToken),
            FileStorageProvider.AzureBlob => await SaveToAzureAsync(content, fileName, contentType, cancellationToken),
            _ => await localStorage.SaveAsync(content, fileName, contentType, cancellationToken)
        };
    }

    public async Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        switch (options.Provider)
        {
            case FileStorageProvider.Local:
                await localStorage.DeleteAsync(url, cancellationToken);
                break;
            case FileStorageProvider.S3:
                await DeleteFromS3Async(url, cancellationToken);
                break;
            case FileStorageProvider.AzureBlob:
                await DeleteFromAzureAsync(url, cancellationToken);
                break;
            default:
                await localStorage.DeleteAsync(url, cancellationToken);
                break;
        }
    }

    public string ResolveUrl(string? storagePathOrKey)
    {
        if (string.IsNullOrWhiteSpace(storagePathOrKey))
        {
            return string.Empty;
        }

        if (storagePathOrKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storagePathOrKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return storagePathOrKey;
        }

        string cleanKey = storagePathOrKey.TrimStart('/');

        switch (options.Provider)
        {
            case FileStorageProvider.S3:
                if (!string.IsNullOrWhiteSpace(options.S3.CdnCustomDomain))
                {
                    return $"{options.S3.CdnCustomDomain.TrimEnd('/')}/{cleanKey}";
                }
                if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                {
                    return $"{options.BaseUrl.TrimEnd('/')}/{cleanKey}";
                }
                if (!string.IsNullOrWhiteSpace(options.S3.BucketName))
                {
                    return $"https://{options.S3.BucketName}.s3.{options.S3.Region}.amazonaws.com/{cleanKey}";
                }
                break;

            case FileStorageProvider.AzureBlob:
                if (!string.IsNullOrWhiteSpace(options.AzureBlob.CustomDomain))
                {
                    return $"{options.AzureBlob.CustomDomain.TrimEnd('/')}/{cleanKey}";
                }
                if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                {
                    return $"{options.BaseUrl.TrimEnd('/')}/{cleanKey}";
                }
                break;

            case FileStorageProvider.Local:
            default:
                if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                {
                    return $"{options.BaseUrl.TrimEnd('/')}/{cleanKey}";
                }
                break;
        }

        return storagePathOrKey.StartsWith('/') ? storagePathOrKey : $"/{storagePathOrKey}";
    }

    private async Task<StoredFile> SaveToS3Async(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        string safeFileName = Path.GetFileName(fileName);
        string extension = Path.GetExtension(safeFileName);
        string key = $"uploads/{Guid.NewGuid():N}{extension}";
        long length = content.Length;

        await Task.CompletedTask;

        string publicUrl = ResolveUrl(key);
        return new StoredFile(publicUrl, safeFileName, contentType, length);
    }

    private static Task DeleteFromS3Async(string url, CancellationToken cancellationToken)
    {
        _ = url;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private async Task<StoredFile> SaveToAzureAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        string safeFileName = Path.GetFileName(fileName);
        string extension = Path.GetExtension(safeFileName);
        string blobName = $"{Guid.NewGuid():N}{extension}";
        long length = content.Length;

        await Task.CompletedTask;

        string publicUrl = ResolveUrl(blobName);
        return new StoredFile(publicUrl, safeFileName, contentType, length);
    }

    private static Task DeleteFromAzureAsync(string url, CancellationToken cancellationToken)
    {
        _ = url;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
