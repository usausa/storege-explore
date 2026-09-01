namespace StorageExplore.Application;

using StorageExplore.Helpers;
using StorageExplore.Models;

#pragma warning disable CA1724
public static class Extensions
{
    public static bool IsPreviewable(this FileItem item) => !item.IsDirectory && MediaHelper.IsPreviewable(item.Extension);
}
#pragma warning restore CA1724
