#if ASSET_INVENTORY
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AIToolkit
{
    public static class AIAssetBridge
    {
        // ── Search ──────────────────────────────────────────────────────

        public static string Search(string query, string type = null, int maxResults = 20)
        {
            return SearchInternal(query, type, maxResults, null);
        }

        public static string SearchInPackage(string packageName, string query, string type = null, int maxResults = 20)
        {
            return SearchInternal(query, type, maxResults, packageName);
        }

        private static string SearchInternal(string query, string type, int maxResults, string packageFilter)
        {
            if (!EnsureInitialized()) return "[]";

            // CreateDefault() loads all reference data (tags, publishers, categories, asset names)
            // using internal methods we can't call directly from an external assembly
            var options = AssetInventory.AssetSearch.Options.CreateDefault();
            options.SearchPhrase = query ?? string.Empty;
            options.MaxResults = maxResults > 0 ? maxResults : 20;
            options.CurrentPage = 1;
            options.RawSearchType = type;

            if (!string.IsNullOrEmpty(packageFilter))
            {
                int packageIdx = Array.FindIndex(options.AssetNames,
                    n => n.IndexOf(packageFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                if (packageIdx > 0) options.SelectedAsset = packageIdx;
            }

            AssetInventory.AssetSearch.Result result = AssetInventory.AssetSearch.Execute(options);

            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < result.Files.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(FileToJson(result.Files[i], options.AllAssets));
            }
            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// Search for multiple terms at once. Terms are comma-separated.
        /// Returns JSON object with results grouped by term.
        /// </summary>
        public static string SearchMulti(string commaSeparatedTerms, string type = null, int maxPerTerm = 5)
        {
            if (!EnsureInitialized()) return "{}";
            if (string.IsNullOrEmpty(commaSeparatedTerms)) return "{}";

            string[] terms = commaSeparatedTerms.Split(',');

            var options = AssetInventory.AssetSearch.Options.CreateDefault();
            options.RawSearchType = type;

            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;

            foreach (string rawTerm in terms)
            {
                string term = rawTerm.Trim();
                if (string.IsNullOrEmpty(term)) continue;

                if (!first) sb.Append(",");
                first = false;

                options.SearchPhrase = term;
                options.MaxResults = maxPerTerm > 0 ? maxPerTerm : 5;
                options.CurrentPage = 1;

                AssetInventory.AssetSearch.Result result = AssetInventory.AssetSearch.Execute(options);

                sb.Append($"{JsonStr(term)}:[");
                for (int i = 0; i < result.Files.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(FileToJson(result.Files[i], options.AllAssets));
                }
                sb.Append("]");
            }
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Browse files inside a specific package, optionally filtered by path and type.
        /// </summary>
        public static string BrowsePackage(int assetId, string pathFilter = null, string type = null, int maxResults = 50)
        {
            if (!EnsureInitialized()) return "[]";

            var options = AssetInventory.AssetSearch.Options.CreateDefault();
            options.SearchPhrase = string.Empty;
            // Use high internal limit when path filtering, since filtering happens post-search
            int internalLimit = !string.IsNullOrEmpty(pathFilter) ? 1000 : (maxResults > 0 ? maxResults : 50);
            options.MaxResults = internalLimit;
            options.CurrentPage = 1;
            options.RawSearchType = type;

            var targetAsset = options.AllAssets.FirstOrDefault(a => a.AssetId == assetId);
            if (targetAsset == null) return "{\"error\":\"Package not found\"}";

            int packageIdx = Array.FindIndex(options.AssetNames,
                n => n.IndexOf(targetAsset.GetDisplayName(), StringComparison.OrdinalIgnoreCase) >= 0);
            if (packageIdx <= 0) return "{\"error\":\"Package not found in filter list\"}";
            options.SelectedAsset = packageIdx;

            AssetInventory.AssetSearch.Result result = AssetInventory.AssetSearch.Execute(options);

            var files = result.Files.AsEnumerable();
            if (!string.IsNullOrEmpty(pathFilter))
            {
                files = files.Where(f =>
                    f.Path != null && f.Path.IndexOf(pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            int limit = maxResults > 0 ? maxResults : 50;
            var fileList = files.Take(limit).ToList();
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < fileList.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(FileToJson(fileList[i], options.AllAssets));
            }
            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// Get a compact summary of search results grouped by package.
        /// </summary>
        public static string SearchSummary(string query, string type = null)
        {
            if (!EnsureInitialized()) return "{}";

            var options = AssetInventory.AssetSearch.Options.CreateDefault();
            options.SearchPhrase = query ?? string.Empty;
            options.MaxResults = 1000;
            options.CurrentPage = 1;
            options.RawSearchType = type;

            AssetInventory.AssetSearch.Result result = AssetInventory.AssetSearch.Execute(options);

            var groups = new Dictionary<int, PackageSummary>();
            foreach (var file in result.Files)
            {
                if (!groups.TryGetValue(file.AssetId, out var summary))
                {
                    var parent = options.AllAssets.FirstOrDefault(a => a.AssetId == file.AssetId);
                    summary = new PackageSummary
                    {
                        Name = parent?.DisplayName ?? file.GetDisplayName(),
                        Publisher = parent?.DisplayPublisher,
                        AssetId = file.AssetId,
                        IsDownloaded = parent?.IsDownloaded ?? false,
                        Count = 0
                    };
                    groups[file.AssetId] = summary;
                }
                summary.Count++;
            }

            int downloadedCount = 0;
            int notDownloadedCount = 0;
            foreach (var g in groups.Values)
            {
                if (g.IsDownloaded) downloadedCount += g.Count;
                else notDownloadedCount += g.Count;
            }

            var sorted = groups.Values.OrderByDescending(g => g.Count).ToList();

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"query\":{JsonStr(query)},");
            sb.Append($"\"totalResults\":{result.ResultCount},");
            sb.Append("\"packages\":[");
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var g = sorted[i];
                sb.Append("{");
                sb.Append($"\"name\":{JsonStr(g.Name)},");
                sb.Append($"\"publisher\":{JsonStr(g.Publisher)},");
                sb.Append($"\"count\":{g.Count},");
                sb.Append($"\"isDownloaded\":{Bool(g.IsDownloaded)},");
                sb.Append($"\"assetId\":{g.AssetId}");
                sb.Append("}");
            }
            sb.Append("],");
            sb.Append($"\"downloadedResults\":{downloadedCount},");
            sb.Append($"\"notDownloadedResults\":{notDownloadedCount}");
            sb.Append("}");
            return sb.ToString();
        }

        private class PackageSummary
        {
            public string Name;
            public string Publisher;
            public int AssetId;
            public bool IsDownloaded;
            public int Count;
        }

        public static string ListPackages(string filter = null)
        {
            if (!EnsureInitialized()) return "[]";

            var allAssets = AssetInventory.Assets.Load()
                .Where(p => p.IsIndexed && p.SafeName != AssetInventory.Asset.NONE)
                .ToList();

            if (!string.IsNullOrEmpty(filter))
            {
                allAssets = allAssets.Where(p =>
                    (p.DisplayName != null && p.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.SafePublisher != null && p.SafePublisher.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < allAssets.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var p = allAssets[i];
                sb.Append("{");
                sb.Append($"\"name\":{JsonStr(p.DisplayName)},");
                sb.Append($"\"publisher\":{JsonStr(p.DisplayPublisher)},");
                sb.Append($"\"category\":{JsonStr(p.DisplayCategory)},");
                sb.Append($"\"size\":{p.PackageSize},");
                sb.Append($"\"isIndexed\":{Bool(p.IsIndexed)},");
                sb.Append($"\"isDownloaded\":{Bool(p.IsDownloaded)},");
                sb.Append($"\"assetId\":{p.AssetId}");
                sb.Append("}");
            }
            sb.Append("]");
            return sb.ToString();
        }

        // ── Details ─────────────────────────────────────────────────────

        public static string GetFileDetails(int assetFileId)
        {
            if (!EnsureInitialized()) return "{}";

            var file = AssetInventory.DBAdapter.DB.Find<AssetInventory.AssetFile>(assetFileId);
            if (file == null) return "{\"error\":\"File not found\"}";

            var asset = AssetInventory.DBAdapter.DB.Find<AssetInventory.Asset>(file.AssetId);
            if (asset == null) return "{\"error\":\"Parent asset not found\"}";

            var allAssets = AssetInventory.Assets.Load().ToList();
            var assetInfo = allAssets.FirstOrDefault(a => a.AssetId == file.AssetId);

            file.CheckIfInProject();

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"name\":{JsonStr(file.FileName)},");
            sb.Append($"\"path\":{JsonStr(file.Path)},");
            sb.Append($"\"size\":{file.Size},");
            sb.Append($"\"type\":{JsonStr(file.Type)},");
            sb.Append($"\"width\":{file.Width},");
            sb.Append($"\"height\":{file.Height},");
            sb.Append($"\"aiCaption\":{JsonStr(file.AICaption)},");
            sb.Append($"\"inProject\":{Bool(file.InProject)},");
            sb.Append($"\"assetFileId\":{file.Id},");
            sb.Append($"\"assetId\":{file.AssetId},");
            sb.Append($"\"packageName\":{JsonStr(asset.DisplayName)},");
            sb.Append($"\"publisher\":{JsonStr(asset.DisplayPublisher)},");
            sb.Append($"\"isDownloaded\":{Bool(assetInfo?.IsDownloaded ?? false)},");
            sb.Append($"\"previewPath\":{JsonStr(GetPreviewFilePath(file.Id))}");
            sb.Append("}");
            return sb.ToString();
        }

        public static string GetPreviewPath(int assetFileId)
        {
            return GetPreviewFilePath(assetFileId) ?? "";
        }

        // ── Download ────────────────────────────────────────────────────

        private static readonly Dictionary<int, AssetInventory.AssetDownloader> _downloaders =
            new Dictionary<int, AssetInventory.AssetDownloader>();

        public static string StartDownload(int assetId)
        {
            if (!EnsureInitialized()) return "{\"status\":\"error\",\"message\":\"Not initialized\"}";

            var allAssets = AssetInventory.Assets.Load().ToList();
            var assetInfo = allAssets.FirstOrDefault(a => a.AssetId == assetId);
            if (assetInfo == null)
                return $"{{\"status\":\"error\",\"message\":\"Asset {assetId} not found\"}}";

            if (assetInfo.IsDownloaded)
                return "{\"status\":\"already_downloaded\"}";

            if (string.IsNullOrEmpty(assetInfo.OriginalLocation))
            {
                return "{\"status\":\"error\",\"message\":\"Download URL not available. Run Asset Store update in Asset Inventory Settings first.\"}";
            }

            var downloader = new AssetInventory.AssetDownloader(assetInfo);
            _downloaders[assetId] = downloader;
            downloader.Download(true);

            return $"{{\"status\":\"started\",\"assetId\":{assetId}}}";
        }

        public static string GetDownloadStatus(int assetId)
        {
            if (!EnsureInitialized()) return "{\"state\":\"Unknown\"}";

            if (_downloaders.TryGetValue(assetId, out var downloader))
            {
                downloader.RefreshState();
                var state = downloader.GetState();

                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"state\":{JsonStr(state.state.ToString())},");
                sb.Append($"\"progress\":{state.progress:F3},");
                sb.Append($"\"bytesDownloaded\":{state.bytesDownloaded},");
                sb.Append($"\"bytesTotal\":{state.bytesTotal}");
                sb.Append("}");

                if (state.state == AssetInventory.AssetDownloader.State.Downloaded ||
                    state.state == AssetInventory.AssetDownloader.State.Unavailable)
                {
                    _downloaders.Remove(assetId);
                }

                return sb.ToString();
            }

            var allAssets = AssetInventory.Assets.Load().ToList();
            var assetInfo = allAssets.FirstOrDefault(a => a.AssetId == assetId);
            if (assetInfo != null && assetInfo.IsDownloaded)
                return "{\"state\":\"Downloaded\",\"progress\":1.0,\"bytesDownloaded\":0,\"bytesTotal\":0}";

            return "{\"state\":\"Unknown\",\"progress\":0,\"bytesDownloaded\":0,\"bytesTotal\":0}";
        }

        // ── Import ──────────────────────────────────────────────────────

        private static int _nextJobId;

        private sealed class ImportJob
        {
            public string Id;
            public string State; // "InProgress", "Done", "Failed"
            public List<string> ImportedFiles = new List<string>();
            public List<string> Errors = new List<string>();
        }

        private static readonly Dictionary<string, ImportJob> _importJobs =
            new Dictionary<string, ImportJob>();

        public static string StartImport(int[] assetFileIds, string targetFolder = "Assets/ThirdParty")
        {
            if (!EnsureInitialized()) return "{\"error\":\"Not initialized\"}";
            if (assetFileIds == null || assetFileIds.Length == 0)
                return "{\"error\":\"No file IDs provided\"}";

            string jobId = $"import_{++_nextJobId}";
            var job = new ImportJob { Id = jobId, State = "InProgress" };
            _importJobs[jobId] = job;

            RunImportAsync(job, assetFileIds, targetFolder);

            return $"{{\"jobId\":{JsonStr(jobId)}}}";
        }

        public static string GetImportStatus(string jobId)
        {
            if (!_importJobs.TryGetValue(jobId, out var job))
                return "{\"state\":\"Unknown\",\"error\":\"Job not found\"}";

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"state\":{JsonStr(job.State)},");
            sb.Append("\"importedFiles\":[");
            for (int i = 0; i < job.ImportedFiles.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(JsonStr(job.ImportedFiles[i]));
            }
            sb.Append("],\"errors\":[");
            for (int i = 0; i < job.Errors.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(JsonStr(job.Errors[i]));
            }
            sb.Append("]}");

            if (job.State == "Done" || job.State == "Failed")
                _importJobs.Remove(jobId);

            return sb.ToString();
        }

        private static async void RunImportAsync(ImportJob job, int[] assetFileIds, string targetFolder)
        {
            try
            {
                var allAssets = AssetInventory.Assets.Load().ToList();

                foreach (int fileId in assetFileIds)
                {
                    try
                    {
                        var assetFile = AssetInventory.DBAdapter.DB.Find<AssetInventory.AssetFile>(fileId);
                        if (assetFile == null)
                        {
                            job.Errors.Add($"File {fileId} not found in database");
                            continue;
                        }

                        var parentAsset = allAssets.FirstOrDefault(a => a.AssetId == assetFile.AssetId);
                        if (parentAsset == null)
                        {
                            job.Errors.Add($"Parent asset for file {fileId} not found");
                            continue;
                        }

                        var searchOpt = AssetInventory.AssetSearch.Options.CreateDefault();
                        searchOpt.SearchPhrase = assetFile.FileName;
                        searchOpt.MaxResults = 1;
                        var searchResult = AssetInventory.AssetSearch.Execute(searchOpt);
                        var info = searchResult.Files.FirstOrDefault(f => f.Id == fileId);

                        if (info == null)
                        {
                            job.Errors.Add($"Could not resolve AssetInfo for file {fileId} ({assetFile.FileName})");
                            continue;
                        }

                        string resultPath = await AssetInventory.Assets.CopyTo(
                            info, targetFolder, withDependencies: true);

                        if (!string.IsNullOrEmpty(resultPath))
                        {
                            job.ImportedFiles.Add(resultPath);
                        }
                        else
                        {
                            job.Errors.Add($"Import returned no path for {assetFile.FileName}");
                        }
                    }
                    catch (Exception e)
                    {
                        job.Errors.Add($"Error importing file {fileId}: {e.Message}");
                    }
                }

                job.State = job.Errors.Count > 0 && job.ImportedFiles.Count == 0 ? "Failed" : "Done";
            }
            catch (Exception e)
            {
                job.State = "Failed";
                job.Errors.Add($"Import job failed: {e.Message}");
                Debug.LogError($"AIAssetBridge import job failed: {e}");
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static bool EnsureInitialized()
        {
            if (!AssetInventory.AI.IsInitialized)
            {
                AssetInventory.AI.Init();
                if (!AssetInventory.AI.IsInitialized)
                {
                    Debug.LogError("AIAssetBridge: Asset Inventory is not initialized.");
                    return false;
                }
            }
            return true;
        }

        private static string FileToJson(AssetInventory.AssetInfo file, List<AssetInventory.AssetInfo> allAssets)
        {
            var parent = allAssets.FirstOrDefault(a => a.AssetId == file.AssetId);
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"name\":{JsonStr(file.FileName)},");
            sb.Append($"\"path\":{JsonStr(file.Path)},");
            sb.Append($"\"packageName\":{JsonStr(parent?.DisplayName ?? file.GetDisplayName())},");
            sb.Append($"\"publisher\":{JsonStr(parent?.DisplayPublisher)},");
            sb.Append($"\"aiCaption\":{JsonStr(file.AICaption)},");
            sb.Append($"\"type\":{JsonStr(file.Type)},");
            sb.Append($"\"size\":{file.Size},");
            sb.Append($"\"width\":{file.Width},");
            sb.Append($"\"height\":{file.Height},");
            sb.Append($"\"isDownloaded\":{Bool(parent?.IsDownloaded ?? false)},");
            sb.Append($"\"assetFileId\":{file.Id},");
            sb.Append($"\"assetId\":{file.AssetId},");
            sb.Append($"\"previewPath\":{JsonStr(GetPreviewFilePath(file.Id))}");
            sb.Append("}");
            return sb.ToString();
        }

        private static string GetPreviewFilePath(int assetFileId)
        {
            string folder = AssetInventory.Paths.GetPreviewFolder(null, false, false);
            if (string.IsNullOrEmpty(folder)) return null;

            string path = System.IO.Path.Combine(folder, $"{assetFileId}.png");
            return System.IO.File.Exists(path) ? path : null;
        }

        private static string JsonStr(string value)
        {
            if (value == null) return "null";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
        }

        private static string Bool(bool value) => value ? "true" : "false";
    }
}
#endif
