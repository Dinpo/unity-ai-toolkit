# Unity AI Toolkit

LLM-driven asset search, download, import, and prefab composition for Unity. Lets AI assistants (Claude Code, etc.) search your entire Asset Store library, download missing packages, import individual files with dependencies, inspect meshes, and compose prefabs — all programmatically via MCP.

## Requirements

- Unity 6+
- [Asset Inventory 4](https://assetstore.unity.com/packages/tools/utilities/asset-inventory-4-349582) — must be installed and indexed
- [MCP for Unity](https://github.com/CoplayDev/unity-mcp) — for LLM communication

## Installation

Add to `Packages/manifest.json`:

```json
"com.dino.ai-toolkit": "https://github.com/Dinpo/unity-ai-toolkit.git"
```

Or for local development:

```json
"com.dino.ai-toolkit": "file:../path/to/unity-ai-toolkit"
```

Then in Unity: **Tools > AI Toolkit > Install Skills** to symlink the Claude Code skills into your project's `.claude/skills/`.

## API

All methods are available via the `AIT` shorthand, callable directly from MCP `execute_code`:

### Search

```csharp
// Search for multiple asset types at once (preferred)
return AIT.SearchMulti("tent, firepit, crate, sleeping bag", "Prefabs", 5);

// Get a package-level overview
return AIT.SearchSummary("prop", "Prefabs");

// Single search
return AIT.Search("fire", "Prefabs", 10);

// Browse a specific package's contents
return AIT.BrowsePackage(59, "Props", "Prefabs", 20);

// Search within a named package
return AIT.SearchInPackage("POLYGON War", "barrel", "Prefabs", 10);

// List all indexed packages
return AIT.ListPackages("Synty");
```

### Inspect

```csharp
// Get exact dimensions for imported prefabs
return AIT.GetBounds(new string[]{
    "Assets/ThirdParty/Buildings/SM_Bld_Saloon_01.prefab",
    "Assets/ThirdParty/Buildings/SM_Bld_Jail_01.prefab"
});
// Returns: width, height, depth, center, min, max for each

// 4-angle contact sheet to see mesh shape and orientation
return AIT.InspectPrefab("Assets/ThirdParty/Buildings/SM_Bld_Saloon_01.prefab");
// Returns: bounds + paths to front/right/back/left screenshots
```

### Download & Import

```csharp
// Import and wait (preferred — blocks, returns paths + bounds)
return AIT.ImportAndWait(new int[]{48291, 48292}, "Assets/ThirdParty");

// Or async: start + poll
return AIT.StartImport(new int[]{48291}, "Assets/ThirdParty");
return AIT.GetImportStatus("import_1");

// Download a package not yet on disk
return AIT.StartDownload(1027);
return AIT.GetDownloadStatus(1027);
```

### Details

```csharp
return AIT.GetFileDetails(48291);   // Full metadata for one file
return AIT.GetPreviewPath(48291);   // Asset Inventory preview image path
```

## Typical Workflow

```
1. AIT.SearchMulti("tent, fire, crate", "Prefabs", 5)     — find candidates
2. AIT.ImportAndWait(selectedIds, "Assets/ThirdParty")     — import + get bounds
3. AIT.InspectPrefab(prefabPath)                           — see orientation from 4 angles
4. Place via MCP manage_gameobject / batch_execute          — using actual dimensions
5. Screenshot to verify                                     — fix any issues
```

## Skills

Installed via **Tools > AI Toolkit > Install Skills** (creates symlinks in `.claude/skills/`).

| Skill | Purpose |
|-------|---------|
| **asset-search** | Search, download, and import assets from your Asset Store library |
| **prefab-builder** | Compose imported assets into positioned prefabs with bounds-aware spacing |
| **unity-dev** | C# correctness reference for Unity (lifecycle, serialization, null safety) |
| **unity-mcp-skill** | MCP for Unity orchestration guide and tool reference |
| **coherence-workflow** | coherence SDK networking reference |

Project-specific skills (not from the toolkit) coexist alongside these symlinks.

## Editor Scripts

| Script | Purpose |
|--------|---------|
| **AIAssetBridge.cs** | All search, import, inspect, download methods. Wrapped in `#if ASSET_INVENTORY`. |
| **AIT.cs** | Global no-namespace shim so `execute_code` can call `AIT.Search(...)` without reflection. |
| **AISkillInstaller.cs** | Menu items to install/uninstall skill symlinks. |

## Development

For local development, use the `file:` path in `manifest.json`. Changes to editor scripts trigger Unity auto-recompile. Changes to skills are picked up immediately by Claude Code (they're symlinked).

```bash
cd ~/CodeProjects/Unity/unity-ai-toolkit
# edit, test in Unity...
git add -A && git commit -m "description" && git push
```

Tag releases for other projects to pin:

```bash
git tag v0.1.0 && git push origin v0.1.0
```

Then reference as `"https://github.com/Dinpo/unity-ai-toolkit.git#v0.1.0"`.
