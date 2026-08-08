namespace StorageExplore;

using System.Runtime;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Service start.")]
    public static partial void InfoServiceStart(this ILogger log);

    [LoggerMessage(Level = LogLevel.Information, Message = "Runtime: os=[{OsDescription}], framework=[{FrameworkDescription}], rid=[{RuntimeIdentifier}]")]
    public static partial void InfoServiceSettingsRuntime(this ILogger log, string osDescription, string frameworkDescription, string runtimeIdentifier);

    [LoggerMessage(Level = LogLevel.Information, Message = "Environment: version=[{Version}], directory=[{Directory}]")]
    public static partial void InfoServiceSettingsEnvironment(this ILogger log, Version? version, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "GCSettings: serverGC=[{IsServerGC}], latencyMode=[{LatencyMode}], largeObjectHeapCompactionMode=[{LargeObjectHeapCompactionMode}]")]
    public static partial void InfoServiceSettingsGC(this ILogger log, bool isServerGC, GCLatencyMode latencyMode, GCLargeObjectHeapCompactionMode largeObjectHeapCompactionMode);

    // Storage

    [LoggerMessage(Level = LogLevel.Information, Message = "Bucket directory created. name=[{Name}], path=[{Path}]")]
    public static partial void InfoBucketDirectoryCreated(this ILogger log, string name, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Path traversal attempt. bucket=[{Bucket}], path=[{Path}]")]
    public static partial void WarnPathTraversal(this ILogger log, string bucket, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "File saved. bucket=[{Bucket}], path=[{Path}], size=[{Size}]")]
    public static partial void InfoFileSaved(this ILogger log, string bucket, string path, long size);

    [LoggerMessage(Level = LogLevel.Information, Message = "Directory created. bucket=[{Bucket}], path=[{Path}]")]
    public static partial void InfoDirectoryCreated(this ILogger log, string bucket, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "File deleted. bucket=[{Bucket}], path=[{Path}]")]
    public static partial void InfoFileDeleted(this ILogger log, string bucket, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Directory deleted. bucket=[{Bucket}], path=[{Path}]")]
    public static partial void InfoDirectoryDeleted(this ILogger log, string bucket, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bucket root delete rejected. bucket=[{Bucket}]")]
    public static partial void WarnBucketRootDeleteRejected(this ILogger log, string bucket);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory is not empty. bucket=[{Bucket}], path=[{Path}]")]
    public static partial void WarnDirectoryNotEmpty(this ILogger log, string bucket, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid rename name. name=[{Name}]")]
    public static partial void WarnInvalidRenameName(this ILogger log, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rename path traversal attempt. bucket=[{Bucket}], path=[{Path}], name=[{Name}]")]
    public static partial void WarnRenamePathTraversal(this ILogger log, string bucket, string path, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rename target already exists. path=[{Path}]")]
    public static partial void WarnRenameTargetExists(this ILogger log, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "File renamed. bucket=[{Bucket}], path=[{Path}], name=[{Name}]")]
    public static partial void InfoFileRenamed(this ILogger log, string bucket, string path, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Directory renamed. bucket=[{Bucket}], path=[{Path}], name=[{Name}]")]
    public static partial void InfoDirectoryRenamed(this ILogger log, string bucket, string path, string name);
}
