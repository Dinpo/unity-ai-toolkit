---
name: prefab-builder
description: Compose imported assets into positioned prefabs using MCP tools. Use when building environment compositions like campsites, market stalls, debris piles, or any arrangement of props.
user_invocable: true
---

# Prefab Builder — Compose Props into Prefabs

Use this skill when you need to arrange multiple imported assets into a composed prefab. Works with any assets already in the project — pair with the `asset-search` skill to find and import assets first.

## Prerequisites

- MCP for Unity connected
- Assets already imported into the project (use `asset-search` skill if needed)

## Workflow

### 1. Plan the composition

Before touching anything, plan:
- **What you're building** — a campsite, a market stall, a debris pile, etc.
- **Center point** — what's the anchor? (e.g., a firepit, a table, a flag)
- **Props list** — what goes around it? (e.g., tents, sleeping bags, crates)
- **Arrangement pattern** — circle, grid, scatter, layered?

### 2. Understand your props

**CRITICAL: Always measure bounds and check orientation before placing.**

**Get bounds for spacing:**
```csharp
return AIT.GetBounds(new string[]{
    "Assets/ThirdParty/.../SM_Bld_Saloon_01.prefab",
    "Assets/ThirdParty/.../SM_Bld_Jail_01.prefab"
});
```
Returns exact width/height/depth for each prefab. Use these for spacing calculations.

**Inspect orientation with gizmos:**
```csharp
return AIT.InspectPrefab("Assets/ThirdParty/.../SM_Bld_Saloon_01.prefab");
```
Returns bounds + a 4-angle contact sheet (front/right/back/left) captured from the Scene View with gizmos and XYZ axis visible. Read the contact sheet image to understand which direction the building faces.

**Lesson learned:** In a test building a western town, a Saloon was 3x wider than other buildings but was placed at equal 15m spacing because the LLM had no bounds data. Always use actual dimensions.

### 3. Create the prefab hierarchy

Use MCP `manage_gameobject` to create the structure:

```python
# Create parent
manage_gameobject(action="create", name="Campsite_Survivor")

# Add children as prefab instances at positions
manage_gameobject(action="create", name="FirePit", parent="Campsite_Survivor",
    prefab_path="Assets/ThirdParty/Props/SM_Prop_FirePit_01.prefab",
    position={"x": 0, "y": 0, "z": 0})

manage_gameobject(action="create", name="Tent", parent="Campsite_Survivor",
    prefab_path="Assets/ThirdParty/Props/SM_Prop_Tent_01.prefab",
    position={"x": -3, "y": 0, "z": 1},
    rotation={"x": 0, "y": 135, "z": 0})
```

### 4. Common arrangement patterns

**Circle (around center point):**
```
Firepit at (0, 0, 0)
Tent at (-3, 0, 1)      — behind-left
Sleeping bag at (1.5, 0, 0.5) — right
Camp chair at (0, 0, 2)  — in front
Crate at (2, 0, -1)     — right-back
```
Offset 2-4m from center. Rotate to face the center point.

**Street layout (using actual bounds):**
```
Given: Saloon=12m wide, Jail=4m, Store=8m
Gap between buildings: 2m

South side (facing +Z):
  Saloon at X=6 (center, spans 0 to 12)
  Jail at X=12+2+2=16 (center, spans 14 to 18)
  Store at X=18+2+4=24 (center, spans 20 to 28)

North side (facing -Z, rotated Y=180):
  Hotel at X=6, Z=streetWidth+depth
  Bank at X=16, Z=streetWidth+depth
```
Always calculate positions from actual bounds width, never guess spacing.

**Grid (for market stalls, storage areas):**
```
Stall_1 at (0, 0, 0)
Stall_2 at (4, 0, 0)    — 4m spacing
Stall_3 at (0, 0, 4)
Stall_4 at (4, 0, 4)
```

**Random scatter (debris, foliage):**
Use small random offsets from a base position. Vary Y-rotation randomly (0-360). Keep minimum 0.5m between items.

**Layered (desk with items):**
```
Desk at (0, 0, 0)
Lamp at (0.3, 0.8, 0.1)   — on the desk surface
Book at (-0.2, 0.8, 0.15) — on the desk surface
Chair at (0, 0, -0.6)      — in front of desk, on ground
```

### 5. Add atmosphere

- **Point lights** — warm orange (color: 1, 0.6, 0.3) for fires, range 5-8m, intensity 1-2
- **Particle effects** — if fire prefabs are available, add them at the fire source
- **Audio sources** — ambient fire crackle, wind, etc. if audio assets are available

### 6. Screenshot and verify

**ALWAYS take a screenshot after placement:**

```python
manage_camera(action="screenshot", include_image=True)
```

Check:
- Are props the right scale relative to each other?
- Are rotations correct? (doors/openings facing the right way)
- Are items floating or buried in the ground?
- Does the overall composition look natural?

If something's wrong, fix it before saving as a prefab.

### 7. Save as prefab

```python
manage_prefabs(action="create",
    source_path="Campsite_Survivor",
    prefab_path="Assets/Prefab/Environment/Campsite_Survivor.prefab")
```

## Tips

- **Scale matters** — most asset packs use 1 unit = 1 meter but some don't. Check if a tent is 2m or 20m.
- **Y=0 is ground** — most props have their pivot at the bottom. Set Y=0 for ground-level items.
- **Face the center** — when arranging in a circle, rotate each prop to face the center point. Calculate Y rotation from the position offset.
- **Variants add variety** — if a pack has `_01`, `_02`, `_03` variants, use different ones for a natural look.
- **Layer from big to small** — place large items first (tents, vehicles), then medium (crates, chairs), then small (debris, trash).
