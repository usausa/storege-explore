namespace StorageExplore.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using StorageExplore.Application;
using StorageExplore.Models;
using StorageExplore.Services;

public partial class FileBrowser : IAsyncDisposable
{
    //--------------------------------------------------------------------------------
    // State
    //--------------------------------------------------------------------------------

    private List<FileItem> items = [];
    private FileItem? selectedItem;
    private FileItem? previewItem;
    private bool isLoading;
    private ViewMode viewMode = ViewMode.List;
    private SortField sortField = SortField.Name;
    private bool sortDescending;

    // Upload state
    private bool isUploading;
    private int uploadedCount;
    private int uploadTotalCount;
    private long uploadedBytes;
    private long uploadTotalBytes;
    private string uploadCurrentFile = string.Empty;
    private string? uploadError;

    // New folder state
    private bool showNewFolder;
    private string newFolderName = string.Empty;
    private ElementReference newFolderInputRef;
    private bool focusNewFolderInput;

    // Delete state
    private bool showDeleteConfirm;

    // Rename state
    private FileItem? renamingItem;
    private string renameValue = string.Empty;
    private string? renameError;

    // Context menu state
    private bool showContextMenu;
    private double contextMenuX;
    private double contextMenuY;
    private FileItem? contextMenuItem;

    // Overwrite confirmation state
    private bool showOverwriteConfirm;
    private List<string> overwriteFileNames = [];
    private TaskCompletionSource<bool>? overwriteTcs;

    // JS interop
    private ElementReference dropZoneRef;
    private ElementReference fileInputRef;
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<FileBrowser>? dotNetRef;

    private string previousBucket = string.Empty;
    private bool isInitialized;

    //--------------------------------------------------------------------------------
    // Parameter
    //--------------------------------------------------------------------------------

    [Parameter]
    public string? Path { get; set; }

    [CascadingParameter(Name = "Bucket")]
    public string Bucket { get; set; } = string.Empty;

    [Inject]
    public FileStorageService Storage { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public IJSRuntime JS { get; set; } = default!;

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        dotNetRef?.Dispose();
        if (jsModule is not null)
        {
            return jsModule.DisposeAsync();
        }
        return ValueTask.CompletedTask;
    }

    protected override Task OnParametersSetAsync()
    {
        if (!isInitialized)
        {
            previousBucket = Bucket;
            isInitialized = true;
        }
        else if (previousBucket != Bucket)
        {
            previousBucket = Bucket;
            Path = null;
        }

        return LoadItems();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            dotNetRef = DotNetObjectReference.Create(this);
            jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/fileUpload.js");
            await jsModule.InvokeVoidAsync("initDropZone", dropZoneRef, fileInputRef, dotNetRef);
        }

        if (focusNewFolderInput)
        {
            focusNewFolderInput = false;
            await newFolderInputRef.FocusAsync();
        }
    }

    //--------------------------------------------------------------------------------
    // Load
    //--------------------------------------------------------------------------------

    private async Task LoadItems()
    {
        isLoading = true;
        selectedItem = null;
        try
        {
            var bucket = Bucket;
            var path = Path ?? string.Empty;
            items = await Task.Run(() => Storage.GetItems(bucket, path));
        }
        finally
        {
            isLoading = false;
        }
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    private void NavigateTo(string path)
    {
        selectedItem = null;
        previewItem = null;
        Navigation.NavigateTo(String.IsNullOrEmpty(path) ? "/" : $"/browse/{path}");
    }

    private void NavigateUp()
    {
        if (String.IsNullOrEmpty(Path))
        {
            return;
        }

        var lastSlash = Path.LastIndexOf('/');
        var parentPath = lastSlash > 0 ? Path[..lastSlash] : string.Empty;
        NavigateTo(parentPath);
    }

    //--------------------------------------------------------------------------------
    // Selection & Preview
    //--------------------------------------------------------------------------------

    private void SelectItem(FileItem item)
    {
        if (item.IsPreviewable())
        {
            selectedItem = item;
            previewItem = item;
        }
        else
        {
            selectedItem = selectedItem == item ? null : item;
        }
    }

    private void OpenItem(FileItem item)
    {
        if (item.IsPreviewable())
        {
            previewItem = item;
        }
        else
        {
            Navigation.NavigateTo(ApiRoutes.Download(Bucket, item.RelativePath), forceLoad: true);
        }
    }

    private void ClosePreview()
    {
        previewItem = null;
    }

    //--------------------------------------------------------------------------------
    // New folder
    //--------------------------------------------------------------------------------

    private void ShowNewFolderDialog()
    {
        newFolderName = string.Empty;
        showNewFolder = true;
        focusNewFolderInput = true;
    }

    private Task CreateFolder()
    {
        if (String.IsNullOrWhiteSpace(newFolderName))
        {
            return Task.CompletedTask;
        }

        var folderPath = String.IsNullOrEmpty(Path) ? newFolderName : $"{Path}/{newFolderName}";
        Storage.CreateDirectory(Bucket, folderPath);
        showNewFolder = false;
        newFolderName = string.Empty;
        return LoadItems();
    }

    private Task OnNewFolderKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            return CreateFolder();
        }
        if (e.Key == "Escape")
        {
            showNewFolder = false;
        }
        return Task.CompletedTask;
    }

    //--------------------------------------------------------------------------------
    // Delete
    //--------------------------------------------------------------------------------

    private void DeleteItem(FileItem item)
    {
        selectedItem = item;
        showDeleteConfirm = true;
    }

    private void DeleteSelected()
    {
        if (selectedItem is null)
        {
            return;
        }
        showDeleteConfirm = true;
    }

    private Task ConfirmDelete()
    {
        if (selectedItem is null)
        {
            return Task.CompletedTask;
        }

        Storage.Delete(Bucket, selectedItem.RelativePath, recursive: true);
        showDeleteConfirm = false;
        selectedItem = null;
        return LoadItems();
    }

    //--------------------------------------------------------------------------------
    // Rename
    //--------------------------------------------------------------------------------

    private void StartRename(FileItem item)
    {
        renamingItem = item;
        renameValue = item.Name;
        renameError = null;
    }

    private Task ConfirmRename()
    {
        if ((renamingItem is null) || String.IsNullOrWhiteSpace(renameValue))
        {
            return Task.CompletedTask;
        }

        if (renameValue == renamingItem.Name)
        {
            CancelRename();
            return Task.CompletedTask;
        }

        var newPath = Storage.Rename(Bucket, renamingItem.RelativePath, renameValue.Trim());
        if (newPath is null)
        {
            renameError = "Rename failed. Name may already exist or contain invalid characters.";
            return Task.CompletedTask;
        }

        renamingItem = null;
        renameValue = string.Empty;
        renameError = null;
        return LoadItems();
    }

    private void CancelRename()
    {
        renamingItem = null;
        renameValue = string.Empty;
        renameError = null;
    }

    private Task OnRenameKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            return ConfirmRename();
        }
        if (e.Key == "Escape")
        {
            CancelRename();
        }
        return Task.CompletedTask;
    }

    //--------------------------------------------------------------------------------
    // Context menu
    //--------------------------------------------------------------------------------

    private void OnContextMenu(MouseEventArgs e, FileItem item)
    {
        contextMenuItem = item;
        selectedItem = item;
        contextMenuX = e.ClientX;
        contextMenuY = e.ClientY;
        showContextMenu = true;
    }

    private void CloseContextMenu()
    {
        showContextMenu = false;
        contextMenuItem = null;
    }

    private void ContextMenuOpen()
    {
        if (contextMenuItem is null)
        {
            return;
        }
        var item = contextMenuItem;
        CloseContextMenu();

        if (item.IsDirectory)
        {
            NavigateTo(item.RelativePath);
        }
        else
        {
            OpenItem(item);
        }
    }

    private void ContextMenuRename()
    {
        if (contextMenuItem is null)
        {
            return;
        }
        var item = contextMenuItem;
        CloseContextMenu();
        StartRename(item);
    }

    private void ContextMenuDelete()
    {
        if (contextMenuItem is null)
        {
            return;
        }
        selectedItem = contextMenuItem;
        CloseContextMenu();
        showDeleteConfirm = true;
    }

    //--------------------------------------------------------------------------------
    // Upload
    //--------------------------------------------------------------------------------

    private void ShowUploadDialog()
    {
        _ = JS.InvokeVoidAsync("eval", "document.querySelector('.fb input[type=file]').click()").AsTask();
    }

    [JSInvokable]
#pragma warning disable CA1024
    public string GetCurrentPath() => Path ?? string.Empty;
#pragma warning restore CA1024

    [JSInvokable]
#pragma warning disable CA1024
    public string GetCurrentBucket() => Bucket;
#pragma warning restore CA1024

    [JSInvokable]
    public void OnUploadStarted(int totalCount, long totalBytes)
    {
        isUploading = true;
        uploadedCount = 0;
        uploadTotalCount = totalCount;
        uploadedBytes = 0;
        uploadTotalBytes = totalBytes;
        uploadCurrentFile = string.Empty;
        uploadError = null;
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnUploadByteProgress(int completedFiles, int totalFiles, long completedBytes, long totalBytes, string currentFile)
    {
        uploadedCount = completedFiles;
        uploadTotalCount = totalFiles;
        uploadedBytes = completedBytes;
        uploadTotalBytes = totalBytes;
        uploadCurrentFile = currentFile;
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnUploadCompleted()
    {
        isUploading = false;
        uploadCurrentFile = string.Empty;
        await LoadItems();
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnUploadError(string error)
    {
        uploadError = error;
        InvokeAsync(StateHasChanged);
    }

    //--------------------------------------------------------------------------------
    // Overwrite confirmation
    //--------------------------------------------------------------------------------

    [JSInvokable]
    public async Task<bool> CheckDuplicates(string[] fileNames)
    {
        var existingNames = items
            .Where(i => !i.IsDirectory)
            .Select(i => i.Name)
            .ToHashSet(StringComparer.Ordinal);

        var duplicates = fileNames.Where(existingNames.Contains).ToList();
        if (duplicates.Count == 0)
        {
            return true;
        }

        overwriteFileNames = duplicates;
        showOverwriteConfirm = true;
        overwriteTcs = new TaskCompletionSource<bool>();
        StateHasChanged();

        return await overwriteTcs.Task;
    }

    private void ConfirmOverwrite()
    {
        showOverwriteConfirm = false;
        overwriteTcs?.TrySetResult(true);
    }

    private void CancelOverwrite()
    {
        showOverwriteConfirm = false;
        overwriteTcs?.TrySetResult(false);
    }

    //--------------------------------------------------------------------------------
    // Data
    //--------------------------------------------------------------------------------

    private List<Breadcrumb> GetBreadcrumbs()
    {
        if (String.IsNullOrEmpty(Path))
        {
            return [];
        }

        var parts = Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var crumbs = new List<Breadcrumb>();
        var accumulated = string.Empty;

        for (var i = 0; i < parts.Length; i++)
        {
            accumulated = i == 0 ? parts[i] : $"{accumulated}/{parts[i]}";
            crumbs.Add(new Breadcrumb
            {
                Name = parts[i],
                Path = accumulated,
                Url = $"/browse/{accumulated}",
                IsLast = i == parts.Length - 1
            });
        }

        return crumbs;
    }

    private void SortBy(SortField field)
    {
        if (sortField == field)
        {
            sortDescending = !sortDescending;
        }
        else
        {
            sortField = field;
            sortDescending = false;
        }
    }

    private IEnumerable<FileItem> GetSortedItems()
    {
        var dirs = items.Where(i => i.IsDirectory);
        var files = items.Where(i => !i.IsDirectory);

        dirs = sortField switch
        {
            SortField.Name => sortDescending ? dirs.OrderByDescending(i => i.Name) : dirs.OrderBy(i => i.Name),
            SortField.Modified => sortDescending ? dirs.OrderByDescending(i => i.LastModified) : dirs.OrderBy(i => i.LastModified),
            _ => dirs.OrderBy(i => i.Name)
        };

        files = sortField switch
        {
            SortField.Name => sortDescending ? files.OrderByDescending(i => i.Name) : files.OrderBy(i => i.Name),
            SortField.Size => sortDescending ? files.OrderByDescending(i => i.Size) : files.OrderBy(i => i.Size),
            SortField.Modified => sortDescending ? files.OrderByDescending(i => i.LastModified) : files.OrderBy(i => i.LastModified),
            _ => files.OrderBy(i => i.Name)
        };

        return dirs.Concat(files);
    }

    //--------------------------------------------------------------------------------
    // Types
    //--------------------------------------------------------------------------------

    private sealed record Breadcrumb
    {
        public string Name { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public bool IsLast { get; init; }
    }

    private enum ViewMode
    {
        List,
        Grid
    }

    private enum SortField
    {
        Name,
        Size,
        Modified
    }
}
