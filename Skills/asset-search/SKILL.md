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

## Quick Reference

All methods are called via MCP `execute_code` using the `AIT` shorthand. They return JSON strings.

```csharp
// Multi-term search (PREFERRED — searches for each term separately)
return AIT.SearchMulti("tent, firepit, sleeping bag, crate", "Prefabs", 5);

// Quick overview — how many results, which packages
return AIT.SearchSummary("tent", "Prefabs");

// Single search
return AIT.Search("fire", "Prefabs", 10);

// Browse a specific package's contents
return AIT.BrowsePackage(59, "Props", "Prefabs", 20);

// Search within a named package
return AIT.SearchInPackage("POLYGON War", "prop", "Prefabs", 10);

// List all indexed packages
return AIT.ListPackages("Synty");

// Get details / preview for one file
return AIT.GetFileDetails(48291);
return AIT.GetPreviewPath(48291);

// Download a package (only if isDownloaded is false)
return AIT.StartDownload(1027);
return AIT.GetDownloadStatus(1027);

// Import and wait (PREFERRED — blocks, returns paths + bounds)
return AIT.ImportAndWait(new int[]{48291, 48292}, "Assets/ThirdParty");

// Get bounds for already-imported prefabs
return AIT.GetBounds(new string[]{"Assets/ThirdParty/path/to/prefab.prefab"});

// Inspect prefab orientation (4-angle contact sheet with gizmos + XYZ axis)
return AIT.InspectPrefab("Assets/ThirdParty/path/to/prefab.prefab");

// Import files into the project
return AIT.StartImport(new int[]{48291, 48292}, "Assets/ThirdParty");
return AIT.GetImportStatus("import_1");
```

## Recommended Workflow

### 1. Get an overview

Start with `SearchSummary` to understand what's available:

```csharp
return AIT.SearchSummary("tent", "Prefabs");
```

This returns total result count, a breakdown by package (with counts), and how many results are from downloaded vs. not-downloaded packages. Use this to decide which packages to focus on.

### 2. Search for multiple props at once

Use `SearchMulti` to find candidates for all the props you need in one call:

```csharp
return AIT.SearchMulti("tent, firepit, sleeping bag, crate, chair", "Prefabs", 5);
```

Results are grouped by search term. Review the results — look at `aiCaption` for descriptions and `packageName` to pick a cohesive art style.

### 3. Explore a promising package

If a package has lots of good results, browse its full contents:

```csharp
return AIT.BrowsePackage(59, "Props", "Prefabs", 30);
```

Use `pathFilter` to narrow: `"Props"`, `"Buildings"`, `"Vehicles"`, `"Characters"`, etc.

### 4. Inspect candidates

Read preview images to understand mesh shape and orientation:

```csharp
return AIT.GetPreviewPath(48291);
```

Then read that file path with the Read tool to see the preview image.

**CRITICAL:** Always inspect previews before placing assets. A previous build had a log rotated wrong because the mesh was a vertical stump, not a horizontal log. AI captions and previews prevent this.

### 5. Download if needed

Check `isDownloaded` in the search results. If false:

```csharp
return AIT.StartDownload(1027);
// Poll every few seconds:
return AIT.GetDownloadStatus(1027);
```

Wait until state is `"Downloaded"`. Unity downloads the full `.unitypackage` — only the files you import go into the project.

### 6. Import selected files

```csharp
return AIT.ImportAndWait(new int[]{id1, id2, id3}, "Assets/ThirdParty");
```

This blocks until all files are imported and returns their project paths + renderer bounds (width/height/depth). No polling needed. Use the bounds data for placement spacing.

To visually inspect a key asset's orientation:
```csharp
return AIT.InspectPrefab("Assets/ThirdParty/path/to/prefab.prefab");
```
Returns a 4-angle contact sheet with gizmos and XYZ axis visible. Read the image to understand which direction the asset faces.

### 7. Verify

```csharp
read_console(types=["error"], count=5)
```

## Search Syntax

- `fire tent` — matches files containing both words (any order)
- `~fire tent` — exact phrase match only
- `fire +tent` — must contain "tent", may contain "fire"
- `fire -broken` — contains "fire", excludes "broken"

## Type Filters

`"Prefabs"`, `"Images"`, `"Audio"`, `"Materials"`, `"3D Models"`, `"Animations"`, or `null` for all.

## Tips

- **Start with SearchSummary** to understand the landscape before diving into details.
- **Use SearchMulti** as your primary search — it's how you naturally think about props.
- **Pick a cohesive style** — prefer assets from the same package for visual consistency.
- **Prefer downloaded packages** when multiple options exist — saves time.
- **Read AI captions** for shape, size, and orientation info.
- **Preview images are your eyes** — always look before deciding rotation and scale.
- **Default import folder is `Assets/ThirdParty`**.
- **BrowsePackage is great for discovery** — "what else is in this pack?"
