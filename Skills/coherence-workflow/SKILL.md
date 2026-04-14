---
name: coherence-workflow
description: coherence SDK reference for Unity - networking, sync, commands, authority, and best practices
---

# coherence SDK Reference (Unity)

Complete reference for the coherence networking SDK v2.x for Unity.

> **Naming convention:** The product/brand is always lowercase **coherence**. C# class names use PascalCase (`CoherenceSync`, `CoherenceBridge`, etc.) because that's C# convention. In prose, menus, and docs, always write "coherence" with a lowercase "c".

## Core Concepts

**CoherenceSync** — The core component. Attach to any GameObject to make it networked. One per GameObject.

**CoherenceBridge** — The connection manager. One per scene. Manages server connection, entity replication, and time sync.

## Authority Model

Every networked entity has two independent authority types:

| Authority | Who | Purpose |
|-----------|-----|---------|
| **State Authority** | Owner of entity state | Can modify [Sync] fields, changes propagate to all clients |
| **Input Authority** | Owner of entity input | Can produce inputs via `CoherenceInput`, forwarded to state authority |

Check authority at runtime:
```csharp
if (sync.HasStateAuthority) { /* Can modify synced state */ }
if (sync.HasInputAuthority) { /* Can send inputs */ }
```

### SimulationType (set on CoherenceSync)
- `ClientSide` (default) — Creating client keeps both state + input authority
- `ServerSide` — Both authorities transfer to Simulator
- `ServerSideWithClientInput` — State to Simulator, input stays with client

### Authority Transfer
```csharp
// Request authority (async with callback)
sync.RequestAuthority(AuthorityType.State, result => {
    if (result == RequestAuthorityResult.Success) { /* Got it */ }
});

// Or async/await
var result = await sync.RequestAuthorityAsync(AuthorityType.State);

// Transfer to specific client
sync.TransferAuthority(clientID, AuthorityType.State);

// Give up authority (entity becomes orphaned)
sync.AbandonAuthority();

// Adopt an orphaned entity
sync.Adopt();
```

Transfer config options on CoherenceSync:
- `NotTransferable` — Cannot be transferred
- `Request` — Owner approves/denies via `OnAuthorityRequest` event
- `Steal` — Always accepted (first come first served)

## [Sync] — Synced Properties

Mark fields for automatic network replication:

```csharp
[Sync] public float health;
[Sync] public int state;
[Sync] public Vector3 targetPosition;
[Sync] public string playerName;
```

### Supported Types
`bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `char`, `string`, `Vector2`, `Vector3`, `Quaternion`, `Color`, `byte[]`, `enum` types, `CoherenceSync` (entity references), `ClientID`

NOT supported: Dictionaries, Lists, custom structs, GameObjects. Use `byte[]` serialization or multiple fields as workaround.

### Sync Modes
- `SyncMode.Always` — Every frame when changed (default)
- `SyncMode.CreationOnly` — Only when entity first created
- `SyncMode.Manual` — Only when `MarkForSyncing()` called explicitly

### Value Change Callbacks
Fires on NON-authority side only (remote clients receiving updates):

```csharp
[Sync]
[OnValueSynced(nameof(OnHealthChanged))]
public int health;

// Must be public, same component, two params of matching type
public void OnHealthChanged(int oldValue, int newValue) {
    // React to remote health change
}
```

## [Command] — Network Commands (RPCs)

Fire-and-forget remote procedure calls. Cannot return values.

```csharp
[Command]
public void NetworkTakeDamage(float damage) {
    health -= damage;
}
```

### Sending Commands

```csharp
// Basic: type + method name + target + params
sync.SendCommand<MyComponent>(
    nameof(MyComponent.NetworkTakeDamage),
    MessageTarget.All,
    25f
);

// To children in hierarchy
sync.SendCommandToChildren<MyComponent>(
    "NetworkTakeDamage", MessageTarget.All, 25f);

// With null parameters (use tuple)
sync.SendCommand<MyComponent>(
    "MyMethod", MessageTarget.All, (typeof(string), null));
```

### MessageTarget

| Target | Executes On | Use For |
|--------|-------------|---------|
| `All` | Everyone including sender | State changes everyone needs |
| `Other` | Everyone except sender | Visual effects sender already plays locally |
| `StateAuthorityOnly` | State authority (host/owner) | State modification requests |
| `InputAuthorityOnly` | Input authority holder | Owner-only operations |

Note: `AuthorityOnly` is a legacy alias for `StateAuthorityOnly`.

### Command Parameter Types
Same as [Sync] supported types. Up to 6 typed params via Action overloads, unlimited via `params object[]`.

### Command Metadata
Access sender info inside a command handler:

```csharp
[Command(UseMeta = true)]  // Must opt-in explicitly
public void MyCommand(int value) {
    var meta = CoherenceSync.CurrentCommandMeta;
    ClientID sender = meta.Sender;
    AbsoluteSimulationFrame frame = meta.Frame;
}
```

## CoherenceSync Configuration

### Lifetime
- `SessionBased` (default) — Destroyed when authority owner disconnects
- `Persistent` — Survives disconnection, can be adopted by others

### Orphaned Behavior (for persistent entities)
- `DoNothing` — Stays orphaned until adopted
- `AutoAdopt` — Another client automatically adopts it

### Uniqueness
- `AllowDuplicates` (default) — Multiple instances allowed
- `NoDuplicates` — Only one instance per UUID; duplicates destroyed

### Key Events on CoherenceSync
```csharp
sync.OnStateAuthority       // Gained state authority
sync.OnStateRemote          // Lost state authority
sync.OnInputAuthority       // Gained input authority
sync.OnInputRemote          // Lost input authority
sync.OnNetworkedInstantiation   // Entity on network, authority resolved
sync.OnNetworkedDestruction     // About to be destroyed by network
sync.OnAuthorityRequest         // Another client requested authority
```

### Component Actions (auto-configure by authority)
- `KinematicRigidbodyComponentAction` — isKinematic=true on remote, false on authority
- `DisableBehaviourComponentAction` — Enable/disable MonoBehaviours by authority
- `DisableColliderComponentAction` — Enable/disable colliders by authority

### Rigidbody Modes
- `Direct` — Sets position/rotation directly (supports parenting)
- `Interpolated` — Uses MovePosition/MoveRotation (preserves velocity)
- `Manual` — Calls user callbacks for custom physics

## CoherenceBridge

### Connecting
```csharp
bridge.Connect(endpoint, connectionSettings);
bridge.ConnectAsHost(endpoint, connectionSettings);
bridge.Disconnect();
```

### Key Properties
```csharp
bridge.IsConnected
bridge.IsSimulatorOrHost
bridge.ClientID
bridge.NetworkTime
bridge.Ping
```

### Events
```csharp
bridge.onConnected         // Connected to server
bridge.onDisconnected      // Disconnected (with reason)
bridge.onConnectionError   // Connection error
bridge.onLiveQuerySynced   // Initial entities synced
```

## Queries and Relevance

### CoherenceLiveQuery
Position-based entity filtering. Attach to a moving GameObject (player/camera).
- `Extent` — Half-length of visibility cube (0 = all entities)
- `Buffer` — Extra range to prevent pop-in/pop-out

### CoherenceGlobalQuery
Receives all entities with `isGlobal = true` on their CoherenceSync. No position filtering.

### CoherenceTagQuery
String-based filtering via `CoherenceTag` on CoherenceSync.

## Interpolation and Prediction

### InterpolationSettings (ScriptableObject per binding)
- `LinearInterpolator` — Linear interpolation between samples
- `SplineInterpolator` — Smooth spline-based interpolation
- `Interpolator` (base) — Snap to latest (no blending)

### Interpolation Loop
When interpolation runs: `Update`, `LateUpdate`, `FixedUpdate`, or combinations.

### Prediction Modes (per binding)
- `Never` (default) — Always apply incoming network samples
- `Always` — Fully predicted, never apply network samples
- `InputAuthority` — Predict only when entity has input authority

### LOD System
Distance-based optimization in CoherenceSync "Optimize" window:
- Configure LOD distance thresholds
- Each step can disable bindings or reduce sample rate
- Default sample rate: 20 Hz

## Network Instantiation

### Prefab Providers
- `DirectReferenceProvider` — Direct prefab reference (default)
- `ResourcesProvider` — Load from Resources folder
- `AddressablesProvider` — Load via Addressables

### Object Pooling
Use `CoherenceObjectPool` / `NetworkPool` as instantiator for pooling.

### Hierarchy Sync
- `CoherenceNode` syncs parent-child relationships via sibling indices
- Both parent and child need CoherenceSync
- `PrefabSyncGroup` for nesting prefabs at edit time

## Baking (Schema Generation)

Generates C# serialization code in `Assets/coherence/baked/`.

**When to rebake:**
- After adding/removing [Sync] or [Command] attributes
- After adding/removing CoherenceSync prefabs
- After changing prefab hierarchy
- Schema must match between all clients and server

**How:** Menu > coherence > Bake

## Advanced Features

### Floating Origin (Large Worlds)
```csharp
bridge.FloatingOriginManager.SetFloatingOrigin(new Vector3d(x, y, z));
```
Max range: `float.MaxValue / 2`.

### Client Connections
`CoherenceClientConnection` represents each connected client. Auto-instantiates a connection prefab per client for client-to-client messaging.

### CoherenceInput (Prediction/Rollback)
For client-side prediction with server reconciliation:
- Max 32 input fields per CoherenceInput
- `SetButton()` / `GetButton()` / `SetAxis()` / `GetAxis()`
- `InitialInputDelay` (default 3)
- Check `IsReadyToProcessInputs` before sending

## Common Patterns

### Authority-Gated State Modification
```csharp
[Command]
public void NetworkSetValue(float value) {
    if (sync.HasStateAuthority) {
        this.syncedValue = value;  // Propagates to all
    }
    PlayEffect();  // Visual effects on ALL clients
}
```

### Local + Remote Execution (avoid latency on sender)
```csharp
public void DoAction(float param) {
    sync.SendCommand<MyComp>(nameof(NetworkDoAction), MessageTarget.Other, param);
    OnDoAction(param);  // Execute locally immediately
}

[Command]
public void NetworkDoAction(float param) {
    OnDoAction(param);  // Remote clients execute
}
```

### Host-Only Spawning
```csharp
if (bridge.IsSimulatorOrHost) {
    var obj = Instantiate(prefab, pos, rot);
}
```

### Request-Before-Modify
```csharp
async void TryPickup() {
    var result = await item.sync.RequestAuthorityAsync(AuthorityType.State);
    if (result == RequestAuthorityResult.Success) {
        item.carriedBy = mySync;
    }
}
```

## Local Debugging (Replication Server)

For local multiplayer testing without coherence Cloud, use the **local Replication Server**:

1. Start the local server: `coherence > Local Replication Server` in Unity menu
2. This avoids the "schema not uploaded to Cloud" errors
3. Connect via `localhost` — no Cloud login needed
4. Schema only needs to match locally (just rebake if bindings change)

Use this workflow during development. Cloud upload is only needed for remote/production multiplayer.

## Correct Patterns (Not Bugs)

When auditing coherence code, these patterns are **correct** and should NOT be flagged as bugs:

### Writing [Sync] inside [Command] is valid
Commands execute on the authority. Writing [Sync] properties inside a [Command] handler is the intended pattern — the authority modifies synced state, which then replicates.
```csharp
[Command]
public void NetworkSetOpen(bool isOpen) {
    this.isOpen = isOpen;  // ✅ Correct: [Command] runs on authority, [Sync] propagates
}
```

### LocalPlayer writing to its own proxy's [Sync] properties
The local player controller writing to its own NetworkPlayerProxy's synced fields every frame is correct — it's writing to its OWN proxy where it has authority.
```csharp
// In LocalPlayer.Update() — this is CORRECT, not a desync bug
localPlayerProxy.animMoving = math.saturate(input.magnitude);
localPlayerProxy.health = health;
```

### MessageTarget.AuthorityOnly for interaction requests
Client sends a command to the entity's authority (host), who then modifies the [Sync] property. The change propagates to all clients via sync. This is the standard client→authority→sync flow.
```csharp
// Client interaction → command to authority → authority sets [Sync] → replicates
sync.SendCommand<Door>(nameof(NetworkSetOpen), MessageTarget.AuthorityOnly, !isOpen);
```

### Dual local+network execution with OnHit pattern
A common pattern: the shooting player calls OnHit locally for instant feedback AND sends a command so other clients also run OnHit. This is not "double execution" — it's intentional for responsiveness.
```csharp
public void TriggerLocalHit(float damage) {
    sync.SendCommand<Enemy>(nameof(NetworkHit), MessageTarget.Other, damage);
    OnHit(damage);  // ✅ Local immediate feedback
}

[Command]
public void NetworkHit(float damage) {
    OnHit(damage);  // Remote clients execute
}
```

### Non-[Sync] health with authority-gated state transitions
Enemy health being non-synced is intentional when both clients calculate it independently from the same damage commands. The host controls state transitions (dead/alive) via [Sync] aiState. Clients may have slightly different health values but agree on the authoritative state.

## Gotchas and Limitations

1. **Must rebake after binding changes** — Schema mismatch causes silent failures
2. **Commands cannot return values** — Fire-and-forget only
3. **[OnValueSynced] only fires on non-authority** — Never on the entity owner
4. **[Sync] and [Command] must be public** — Private/protected not supported
5. **No Dictionary/List sync** — Serialize as byte[] or use multiple fields
6. **Entity references only via CoherenceSync** — Cannot reference raw GameObjects
7. **Authority operations are async** — RequestAuthority can timeout (5s default)
8. **String size limits** — Large data should use byte[]
9. **CoherenceNode uses sibling indices** — Hierarchy must match across clients
10. **Command metadata requires opt-in** — `[Command(UseMeta = true)]` or throws
11. **Position precision** — float-based; large worlds need FloatingOrigin
12. **Prefabs need CoherenceSyncConfig** — Auto-created during bake, must be in registry
13. **One mainBridge only** — For DontDestroyOnLoad scene transitions
