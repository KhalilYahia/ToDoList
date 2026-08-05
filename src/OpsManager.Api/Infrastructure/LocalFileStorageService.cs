using OpsManager.Service.Abstractions;

namespace OpsManager.Api.Infrastructure;

public sealed class LocalFileStorageOptions
{
    public const string SectionName = "FileStorage";
    public string RootPath { get; set; } = "wwwroot/uploads";
    public long MaximumFileBytes { get; set; } = 10 * 1024 * 1024;
}

public sealed class LocalFileStorageService(
    IWebHostEnvironment environment,
    LocalFileStorageOptions options) : IFileStorageService
{
    private readonly string _root = ResolveRoot(environment.ContentRootPath, options.RootPath);

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        string safeFileName = Path.GetFileName(fileName);
        string extension = Path.GetExtension(safeFileName);
        string storedName = $"{Guid.NewGuid():N}{extension}";
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, storedName);

        await using FileStream destination = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);
        await content.CopyToAsync(destination, cancellationToken);
        if (destination.Length > options.MaximumFileBytes)
        {
            destination.Close();
            File.Delete(path);
            throw new InvalidOperationException("The uploaded file exceeds the configured size limit.");
        }

        return new StoredFile($"/uploads/{storedName}", safeFileName, contentType, destination.Length);
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fileName = Path.GetFileName(url);
        string path = Path.GetFullPath(Path.Combine(_root, fileName));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The file path is outside the configured storage root.");
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public string ResolveUrl(string? storagePathOrKey)
    {
        if (string.IsNullOrWhiteSpace(storagePathOrKey)) return string.Empty;
        if (storagePathOrKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storagePathOrKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return storagePathOrKey;
        }

        string relative = storagePathOrKey.StartsWith('/') ? storagePathOrKey : $"/{storagePathOrKey}";

        return string.IsNullOrWhiteSpace(options.RootPath)
            ? relative
            : relative;
    }

    private static string ResolveRoot(string contentRootPath, string configuredRoot)
    {
        string root = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(contentRootPath, configuredRoot);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }
}
