# Unity AI Toolkit

LLM-driven asset search, download, import, and prefab composition for Unity.

Bridges [Asset Inventory 4](https://assetstore.unity.com/packages/tools/utilities/asset-inventory-4-349582) so that AI assistants (Claude Code, etc.) can search your entire Asset Store library, download missing packages, import individual files with dependencies, and compose prefabs — all programmatically via MCP.

## Requirements

- Unity 6+
- [Asset Inventory 4](https://assetstore.unity.com/packages/tools/utilities/asset-inventory-4-349582) (must be installed and indexed)
- [MCP for Unity](https://github.com/CoplayDev/unity-mcp) (for LLM communication)

## Installation

Add to your project's `Packages/manifest.json`:

```json
"com.dino.ai-toolkit": "https://github.com/dinpo/unity-ai-toolkit.git"
```

Then in Unity: **Tools > AI Toolkit > Install Skills** to symlink the Claude skills into your project.

## What's Included

### Editor Scripts
- **AIAssetBridge** — Static methods for searching, downloading, and importing assets. Called by the LLM via MCP `execute_code`.
- **AISkillInstaller** — Menu item to symlink skills into `.claude/skills/`.

### Skills
- **asset-search** — Teaches the LLM to search, download, and import assets from your library
- **prefab-builder** — Teaches the LLM to compose imported assets into prefabs
- **unity-dev** — C# correctness reference for Unity
- **unity-mcp-skill** — MCP orchestration guide
- **coherence-workflow** — coherence SDK networking reference
