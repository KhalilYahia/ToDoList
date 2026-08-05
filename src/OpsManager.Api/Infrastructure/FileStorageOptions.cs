namespace OpsManager.Api.Infrastructure;

public enum FileStorageProvider
{
    Local = 0,
    S3 = 1,
    AzureBlob = 2,
}

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public FileStorageProvider Provider { get; set; } = FileStorageProvider.Local;

    public string BaseUrl { get; set; } = string.Empty;

    public LocalFileStorageOptions Local { get; set; } = new();

    public S3FileStorageOptions S3 { get; set; } = new();

    public AzureBlobStorageOptions AzureBlob { get; set; } = new();
}

public sealed class S3FileStorageOptions
{
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string ServiceUrl { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string CdnCustomDomain { get; set; } = string.Empty;
    public long MaximumFileBytes { get; set; } = 10 * 1024 * 1024;
}

public sealed class AzureBlobStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "attachments";
    public string CustomDomain { get; set; } = string.Empty;
    public long MaximumFileBytes { get; set; } = 10 * 1024 * 1024;
}
