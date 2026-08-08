namespace StorageExplore.Endpoints;

using StorageExplore.Helpers;
using StorageExplore.Services;

public static class FileEndpoint
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapFileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/files");

        group.MapGet("/download/{bucket}/{**path}", HandleDownload);
        group.MapGet("/preview/{bucket}/{**path}", HandlePreview);
        group.MapGet("/thumbnail/{bucket}/{**path}", HandleThumbnail);
        group.MapPost("/upload/{bucket}/{**path}", HandleUpload)
            .DisableAntiforgery()
            .WithRequestTimeout(TimeSpan.FromHours(1));
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static IResult HandleDownload(
        HttpContext context,
        string bucket,
        string path,
        FileStorageService storage)
    {
        var fileInfo = storage.GetFileInfo(bucket, path);
        if (fileInfo is null)
        {
            return Results.NotFound();
        }

#pragma warning disable CA2000
        var stream = storage.OpenRead(bucket, path);
#pragma warning restore CA2000
        if (stream is null)
        {
            return Results.NotFound();
        }

        context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileInfo.Name}\"";
        return Results.Stream(stream, "application/octet-stream", enableRangeProcessing: false);
    }

    private static IResult HandlePreview(
        string bucket,
        string path,
        FileStorageService storage)
    {
        var fileInfo = storage.GetFileInfo(bucket, path);
        if (fileInfo is null)
        {
            return Results.NotFound();
        }

#pragma warning disable CA2000
        var stream = storage.OpenRead(bucket, path);
#pragma warning restore CA2000
        if (stream is null)
        {
            return Results.NotFound();
        }

        return Results.Stream(stream, MediaHelper.GetContentType(fileInfo.Extension), enableRangeProcessing: true);
    }

    private static IResult HandleThumbnail(
        string bucket,
        string path,
        FileStorageService storage)
    {
        var fileInfo = storage.GetFileInfo(bucket, path);
        if (fileInfo is null)
        {
            return Results.NotFound();
        }

        if (!MediaHelper.HasThumbnail(fileInfo.Extension))
        {
            return Results.NoContent();
        }

#pragma warning disable CA2000
        var stream = storage.OpenRead(bucket, path);
#pragma warning restore CA2000
        if (stream is null)
        {
            return Results.NotFound();
        }

        return Results.Stream(stream, MediaHelper.GetContentType(fileInfo.Extension), enableRangeProcessing: true);
    }

    private static async ValueTask<IResult> HandleUpload(
        HttpContext context,
        string bucket,
        string? path,
        FileStorageService storage)
    {
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (form.Files.Count == 0)
        {
            return Results.BadRequest("No files uploaded.");
        }

        var uploaded = new List<object>();
        foreach (var file in form.Files)
        {
            var fileName = GetFileName(file.FileName);
            if (fileName.Length == 0)
            {
                return Results.BadRequest("Invalid file name.");
            }

            var targetPath = string.IsNullOrEmpty(path) ? fileName : $"{path}/{fileName}";

            await using var stream = file.OpenReadStream();
            await storage.SaveFileAsync(bucket, targetPath, stream);

            uploaded.Add(new { name = fileName, size = file.Length, path = targetPath });
        }

        return Results.Ok(new { uploaded = uploaded.Count, files = uploaded });
    }

    private static string GetFileName(string value)
    {
        var index = value.LastIndexOfAny(['/', '\\']);
        var name = index >= 0 ? value[(index + 1)..] : value;
        return (name is "." or "..") ? String.Empty : name;
    }
}
