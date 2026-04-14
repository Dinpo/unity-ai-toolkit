#if ASSET_INVENTORY
/// <summary>
/// Global shorthand for AIToolkit.AIAssetBridge.
/// Call directly from MCP execute_code: return AIT.Search("tent", "Prefabs", 10);
/// </summary>
public static class AIT
{
    public static string Search(string query, string type = null, int maxResults = 20)
        => AIToolkit.AIAssetBridge.Search(query, type, maxResults);

    public static string SearchMulti(string terms, string type = null, int maxPerTerm = 5)
        => AIToolkit.AIAssetBridge.SearchMulti(terms, type, maxPerTerm);

    public static string SearchSummary(string query, string type = null)
        => AIToolkit.AIAssetBridge.SearchSummary(query, type);

    public static string SearchInPackage(string packageName, string query, string type = null, int maxResults = 20)
        => AIToolkit.AIAssetBridge.SearchInPackage(packageName, query, type, maxResults);

    public static string BrowsePackage(int assetId, string pathFilter = null, string type = null, int maxResults = 50)
        => AIToolkit.AIAssetBridge.BrowsePackage(assetId, pathFilter, type, maxResults);

    public static string ListPackages(string filter = null)
        => AIToolkit.AIAssetBridge.ListPackages(filter);

    public static string GetFileDetails(int assetFileId)
        => AIToolkit.AIAssetBridge.GetFileDetails(assetFileId);

    public static string GetPreviewPath(int assetFileId)
        => AIToolkit.AIAssetBridge.GetPreviewPath(assetFileId);

    public static string GetBounds(string[] prefabPaths)
        => AIToolkit.AIAssetBridge.GetBounds(prefabPaths);

    public static string ImportAndWait(int[] assetFileIds, string targetFolder = "Assets/ThirdParty")
        => AIToolkit.AIAssetBridge.ImportAndWait(assetFileIds, targetFolder);

    public static string InspectPrefab(string prefabPath)
        => AIToolkit.AIAssetBridge.InspectPrefab(prefabPath);

    public static string StartDownload(int assetId)
        => AIToolkit.AIAssetBridge.StartDownload(assetId);

    public static string GetDownloadStatus(int assetId)
        => AIToolkit.AIAssetBridge.GetDownloadStatus(assetId);

    public static string StartImport(int[] assetFileIds, string targetFolder = "Assets/ThirdParty")
        => AIToolkit.AIAssetBridge.StartImport(assetFileIds, targetFolder);

    public static string GetImportStatus(string jobId)
        => AIToolkit.AIAssetBridge.GetImportStatus(jobId);
}
#endif
