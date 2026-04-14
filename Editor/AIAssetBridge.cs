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

            var options = new AssetInventory.AssetSearch.Options
            {
                SearchPhrase = query ?? string.Empty,
                MaxResults = maxResults > 0 ? maxResults : 20,
                CurrentPage = 1,
                RawSearchType = type
            };

            var allAssets = AssetInventory.Assets.Load().ToList();
            options.AllAssets = allAssets;

            var tags = AssetInventory.DBAdapter.DB.Table<AssetInventory.Tag>().ToList();
            options.Tags = tags;
            options.TagNames = AssetInventory.Assets.ExtractTagNames(tags);
            options.PublisherNames = AssetInventory.Assets.ExtractPublisherNames(allAssets);
            options.CategoryNames = AssetInventory.Assets.ExtractCategoryNames(allAssets);
            options.AssetNames = AssetInventory.Assets.ExtractAssetNames(allAssets, true);

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
                sb.Append(FileToJson(result.Files[i], allAssets));
            }
            sb.Append("]");
            return sb.ToString();
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
