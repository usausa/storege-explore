namespace StorageExplore.Services;

using Microsoft.Extensions.Options;

using StorageExplore.Models;

#pragma warning disable CA3003
public sealed class FileStorageService
{
    private readonly ILogger<FileStorageService> log;

    private readonly Dictionary<string, string> resolvedBuckets;

    public IReadOnlyDictionary<string, string> Buckets => resolvedBuckets;

    public FileStorageService(ILogger<FileStorageService> log, IOptions<FileStorageSetting> options)
    {
        this.log = log;
        resolvedBuckets = options.Value.Buckets.ToDictionary(
            kvp => kvp.Key,
            kvp => Path.GetFullPath(kvp.Value));
    }

    public void Initialize()
    {
        foreach (var (name, path) in resolvedBuckets)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                log.InfoBucketDirectoryCreated(name, path);
            }
        }
    }

    public string? GetBucketPath(string bucketName)
    {
        return resolvedBuckets.GetValueOrDefault(bucketName);
    }

    public string? ResolvePath(string bucketName, string relativePath)
    {
        var bucketPath = GetBucketPath(bucketName);
        if (bucketPath is null)
        {
            return null;
        }

        if (String.IsNullOrWhiteSpace(relativePath))
        {
            return bucketPath;
        }

        var combined = Path.GetFullPath(Path.Combine(bucketPath, relativePath));
        if (!IsUnderBucket(bucketPath, combined) || ContainsLink(bucketPath, combined))
        {
            log.WarnPathTraversal(bucketName, relativePath);
            return null;
        }
        return combined;
    }

    private static bool IsUnderBucket(string bucketPath, string fullPath)
    {
        if (String.Equals(fullPath, bucketPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var bucketRoot = Path.EndsInDirectorySeparator(bucketPath)
            ? bucketPath
            : bucketPath + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(bucketRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsLink(string bucketPath, string fullPath)
    {
        var current = fullPath;
        while ((current is not null) && (current.Length > bucketPath.Length))
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists && (info.LinkTarget is not null))
            {
                return true;
            }

            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    public List<FileItem> GetItems(string bucketName, string relativePath)
    {
        var fullPath = ResolvePath(bucketName, relativePath);
        if (fullPath is null || !Directory.Exists(fullPath))
        {
            return [];
        }

        var bucketPath = GetBucketPath(bucketName)!;
        var items = new List<FileItem>();

        foreach (var dir in Directory.EnumerateDirectories(fullPath))
        {
            var info = new DirectoryInfo(dir);
            if (info.LinkTarget is not null)
            {
                continue;
            }

            items.Add(new FileItem
            {
                Name = info.Name,
                RelativePath = Path.GetRelativePath(bucketPath, dir).Replace('\\', '/'),
                IsDirectory = true,
                LastModified = info.LastWriteTime
            });
        }

        foreach (var file in Directory.EnumerateFiles(fullPath))
        {
            var info = new FileInfo(file);
            if (info.LinkTarget is not null)
            {
                continue;
            }

            items.Add(new FileItem
            {
                Name = info.Name,
                RelativePath = Path.GetRelativePath(bucketPath, file).Replace('\\', '/'),
                IsDirectory = false,
                Size = info.Length,
                LastModified = info.LastWriteTime
            });
        }

        return items;
    }

    public FileItem? GetFileInfo(string bucketName, string relativePath)
    {
        var fullPath = ResolvePath(bucketName, relativePath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return null;
        }

        var info = new FileInfo(fullPath);
        return new FileItem
        {
            Name = info.Name,
            RelativePath = relativePath,
            IsDirectory = false,
            Size = info.Length,
            LastModified = info.LastWriteTime
        };
    }

    public Stream? OpenRead(string bucketName, string relativePath)
    {
        var fullPath = ResolvePath(bucketName, relativePath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return null;
        }

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task<string?> ReadTextAsync(string bucketName, string relativePath, int maxLength = 100_000)
    {
        var fullPath = ResolvePath(bucketName, relativePath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return null;
        }

        var info = new FileInfo(fullPath);
        if (info.Length > maxLength)
        {
            return null;
        }

        return await File.ReadAllTextAsync(fullPath);
    }

    public async Task SaveFileAsync(string bucketName, string relativePath, Stream content)
    {
        var fullPath = ResolvePath(bucketName, relativePath);
        if (fullPath is null)
        {
            throw new InvalidOperationException("Invalid path.");
        }

        var directory = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs);
        log.InfoFileSaved(bucketName, relativePath, fs.Length);
    }

    public void CreateDirectory(string bucketName, string relativePath)
    {
        var fullPath = ResolvePath(bucketName, relativePath);
        if (fullPath is null)
        {
            throw new InvalidOperationException("Invalid path.");
        }

        Directory.CreateDirectory(fullPath);
        log.InfoDirectoryCreated(bucketName, relativePath);
    }

    public void Delete(string bucketName, string relativePath, bool recursive = false)
    {
        var fullPath = ResolvePath(bucketName, relativePath);
        if (fullPath is null)
        {
            throw new InvalidOperationException("Invalid path.");
        }

        if (String.Equals(fullPath, GetBucketPath(bucketName), StringComparison.OrdinalIgnoreCase))
        {
            log.WarnBucketRootDeleteRejected(bucketName);
            throw new InvalidOperationException("Bucket root can not be deleted.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            log.InfoFileDeleted(bucketName, relativePath);
        }
        else if (Directory.Exists(fullPath))
        {
            if (!recursive && Directory.EnumerateFileSystemEntries(fullPath).Any())
            {
                log.WarnDirectoryNotEmpty(bucketName, relativePath);
                throw new InvalidOperationException("Directory is not empty.");
            }

            Directory.Delete(fullPath, recursive);
            log.InfoDirectoryDeleted(bucketName, relativePath);
        }
    }

    public string? Rename(string bucketName, string relativePath, string newName)
    {
        if (String.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            log.WarnInvalidRenameName(newName);
            return null;
        }

        var fullPath = ResolvePath(bucketName, relativePath);
        if (fullPath is null)
        {
            return null;
        }

        var parentDir = Path.GetDirectoryName(fullPath)!;
        var newFullPath = Path.GetFullPath(Path.Combine(parentDir, newName));

        var bucketPath = GetBucketPath(bucketName)!;
        if (!IsUnderBucket(bucketPath, newFullPath) || String.Equals(newFullPath, bucketPath, StringComparison.OrdinalIgnoreCase))
        {
            log.WarnRenamePathTraversal(bucketName, relativePath, newName);
            return null;
        }

        if (File.Exists(newFullPath) || Directory.Exists(newFullPath))
        {
            log.WarnRenameTargetExists(newFullPath);
            return null;
        }

        if (File.Exists(fullPath))
        {
            File.Move(fullPath, newFullPath);
            log.InfoFileRenamed(bucketName, relativePath, newName);
        }
        else if (Directory.Exists(fullPath))
        {
            Directory.Move(fullPath, newFullPath);
            log.InfoDirectoryRenamed(bucketName, relativePath, newName);
        }
        else
        {
            return null;
        }

        return Path.GetRelativePath(bucketPath, newFullPath).Replace('\\', '/');
    }

    public bool Exists(string bucketName, string relativePath)
    {
        var fullPath = ResolvePath(bucketName, relativePath);
        if (fullPath is null)
        {
            return false;
        }
        return File.Exists(fullPath) || Directory.Exists(fullPath);
    }

    public (long TotalBytes, long FreeBytes) GetStorageInfo(string bucketName)
    {
        var bucketPath = GetBucketPath(bucketName);
        if (bucketPath is null)
        {
            return (0, 0);
        }

        var driveInfo = new DriveInfo(Path.GetPathRoot(bucketPath)!);
        return (driveInfo.TotalSize, driveInfo.AvailableFreeSpace);
    }
}
