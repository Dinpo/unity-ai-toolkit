---
name: asset-search
description: Search, download, and import assets from your Asset Store library via Asset Inventory 4. Use when you need to find props, textures, audio, or other assets across all purchased packages — even ones not yet downloaded or imported.
---

# Asset Search — Find and Import Assets from Your Library

Use this skill when you need to find and bring assets into the project from the user's Asset Store purchases. Requires Asset Inventory 4 to be installed and indexed.

## Prerequisites

- Asset Inventory 4 installed and indexed (check for `ASSET_INVENTORY` define)
- AI Toolkit package installed (`com.dino.ai-toolkit`)
- MCP for Unity connected

## API Reference

All methods are called via MCP `execute_code`. They return JSON strings.

### Search

```csharp
// Search all indexed assets
AIToolkit.AIAssetBridge.Search("zombie tent fire", "Prefabs", 20)

// Search within a specific package
AIToolkit.AIAssetBridge.SearchInPackage("POLYGON Apocalypse", "prop fire", "Prefabs", 10)

// List all indexed packages
AIToolkit.AIAssetBridge.ListPackages("Synty")
```

**Search syntax:**
- `fire tent` — matches files containing both words (any order)
- `~fire tent` — exact phrase match only
- `fire +tent` — must contain "tent", may contain "fire"
- `fire -broken` — contains "fire", excludes "broken"

**Type filters:** `"Prefabs"`, `"Images"`, `"Audio"`, `"Materials"`, `"3D Models"`, `"Animations"`, or `null` for all types.

**Search results include:**
- `name`, `path` — file name and path within the package
- `packageName`, `publisher` — which package it's from
- `aiCaption` — AI-generated description (if available). USE THIS to understand mesh shape, size, and orientation before placing.
- `type`, `size`, `width`, `height` — file metadata
- `isDownloaded` — whether the package is on disk. If false, you must download before importing.
- `assetFileId` — unique ID for import/details calls
- `assetId` — package ID for download calls
- `previewPath` — absolute path to preview PNG. Read this with the Read tool to visually inspect the asset.

### Details

```csharp
// Get full details for one file
AIToolkit.AIAssetBridge.GetFileDetails(48291)

// Get preview image path
AIToolkit.AIAssetBridge.GetPreviewPath(48291)
```

### Download

```csharp
// Start downloading a package (required only if isDownloaded is false)
AIToolkit.AIAssetBridge.StartDownload(1027)

// Poll until state is "Downloaded"
AIToolkit.AIAssetBridge.GetDownloadStatus(1027)
```

States: `"Downloading"`, `"Downloaded"`, `"Paused"`, `"Unavailable"`, `"UpdateAvailable"`, `"Unknown"`

### Import

```csharp
// Start importing files + dependencies into the project
AIToolkit.AIAssetBridge.StartImport(new int[]{48291, 48292}, "Assets/ThirdParty")

// Poll until state is "Done"
AIToolkit.AIAssetBridge.GetImportStatus("import_1")
```

States: `"InProgress"`, `"Done"`, `"Failed"`

## Workflow

Follow these steps in order:

### 1. Search for what you need

```csharp
var results = AIToolkit.AIAssetBridge.Search("campfire tent sleeping bag", "Prefabs", 30);
Debug.Log(results);
```

Review the results. Look at `aiCaption` for descriptions of what each asset looks like.

### 2. Inspect promising candidates

Read preview images to understand mesh shape and orientation:

```csharp
var previewPath = AIToolkit.AIAssetBridge.GetPreviewPath(48291);
Debug.Log(previewPath);
```

Then read that file path with the Read tool to see the preview image.

**CRITICAL:** Always inspect previews before placing assets. The last encampment build had a log rotated wrong because the mesh was vertical (a stump) but was assumed to be horizontal. AI captions and previews prevent this.

### 3. Download if needed

Check `isDownloaded` in the search results. If false:

```csharp
AIToolkit.AIAssetBridge.StartDownload(assetId);
// Wait and poll:
AIToolkit.AIAssetBridge.GetDownloadStatus(assetId);
```

Poll every few seconds until state is `"Downloaded"`. Unity downloads the full `.unitypackage` — this is a Unity limitation, but only the files you import go into the project.

### 4. Import selected files

```csharp
AIToolkit.AIAssetBridge.StartImport(new int[]{id1, id2, id3}, "Assets/ThirdParty");
// Poll:
AIToolkit.AIAssetBridge.GetImportStatus("import_1");
```

This imports only the specified files + their dependencies (materials, textures, etc.). The rest of the package stays out of the project.

### 5. Verify

After import completes, check the console for errors and verify the files landed:

```csharp
read_console(types=["error"], count=5)
```

## Tips

- **Search broadly first, then narrow.** Start with general terms, review results, then add `+required` terms.
- **Prefer already-downloaded packages** when multiple options exist — saves download time.
- **Read AI captions carefully** — they describe shape, size, and orientation which you need for correct placement.
- **Preview images are your eyes** — always look at them before deciding rotation and scale.
- **Default import folder is `Assets/ThirdParty`** — this keeps imported assets separate from project code.
