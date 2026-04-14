---
name: unity-dev
description: Use when writing or modifying C# scripts for Unity — covers lifecycle, serialization, null safety, physics, coroutines, and common API mistakes that compile but break at runtime
---

# Unity C# Correctness Reference

Target audience: competent C# developer who doesn't know Unity's quirks. Every item here compiles but breaks at runtime.

---

## 1. Lifecycle

Unity bypasses normal C# construction. Constructors, static init order, and deterministic startup don't work the way you expect.

**No constructors on MonoBehaviour.** Unity deserializes fields directly — constructors run at unpredictable times (including in the Editor).

```csharp
// ❌ WRONG
public class Enemy : MonoBehaviour {
    public Enemy() { health = 100; } // runs at wrong time, may run multiple times
}

// ✅ RIGHT
public class Enemy : MonoBehaviour {
    void Awake() { health = 100; }
}
```

**Awake vs Start.** Awake runs on instantiation, even if the component is disabled — but NOT if the GameObject itself is inactive. Start runs before the first Update, only if enabled. Use Awake for self-init, Start for cross-object references.

```csharp
// ❌ WRONG — other object may not exist yet in Awake
void Awake() { target = FindFirstObjectByType<Player>(); }

// ✅ RIGHT — Awake for self, Start for cross-references
void Awake() { rb = GetComponent<Rigidbody>(); }
void Start() { target = FindFirstObjectByType<Player>(); }
```

**OnEnable/OnDisable must mirror each other.** Subscribe in OnEnable, unsubscribe in OnDisable. If you subscribe in Awake but unsubscribe in OnDisable, re-enabling the object won't resubscribe.

```csharp
// ✅ RIGHT
void OnEnable() { EventBus.OnWave += HandleWave; }
void OnDisable() { EventBus.OnWave -= HandleWave; }
```

**Execution order between scripts is non-deterministic** unless set in Project Settings > Script Execution Order.

**DontDestroyOnLoad creates duplicates on scene reload.** Guard with a singleton check:

```csharp
void Awake() {
    if (Instance != null) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject);
}
```

---

## 2. Null Safety

Unity overrides `==` on UnityEngine.Object. Standard C# null patterns silently break.

**`obj == null` catches destroyed objects. `obj is null` and `?.` do NOT** — they check CLR null only, so a destroyed-but-not-GC'd object passes the check.

```csharp
// ❌ WRONG — misses destroyed objects
if (target is null) return;
target?.TakeDamage(10);

// ✅ RIGHT — catches both null and destroyed
if (target == null) return;
if (!target) return; // implicit bool, also works
```

**Use `TryGetComponent` over `GetComponent` + null check:**

```csharp
// ❌ WRONG
var rb = GetComponent<Rigidbody>();
if (rb != null) rb.AddForce(Vector3.up);

// ✅ RIGHT
if (TryGetComponent<Rigidbody>(out var rb)) rb.AddForce(Vector3.up);
```

---

## 3. Serialization

AIs write properties and dictionaries expecting them to appear in the Inspector. They won't.

- **Public fields** serialize by default. **Properties never serialize.**
- **Private fields** need `[SerializeField]` to serialize.
- **Interfaces, dictionaries, and most generic types** don't serialize.
- Custom classes/structs need `[System.Serializable]`.

```csharp
// ❌ WRONG — won't appear in Inspector
public int Health { get; set; } = 100;
private float speed = 5f;
public Dictionary<string, int> inventory; // won't serialize
public IWeapon weapon; // interfaces don't serialize

// ✅ RIGHT
public int health = 100;
[SerializeField] private float speed = 5f;
public List<ItemEntry> inventory; // List serializes, Dictionary does not
public WeaponData weapon; // concrete ScriptableObject reference
```

**Custom classes need `[System.Serializable]`:**

```csharp
// ❌ WRONG — appears as empty in Inspector
public class ItemEntry { public string name; public int count; }

// ✅ RIGHT
[System.Serializable]
public class ItemEntry { public string name; public int count; }
```

**ScriptableObject** is for shared data assets (weapon stats, config), not per-instance runtime state.

**Inspector values override code defaults.** Changing `public int health = 200;` in code won't update existing instances that already serialized `100`. Reset the component or delete the serialized data.

---

## 4. Physics

AIs put physics code in Update and move Rigidbodies with transform. Both produce jittery, non-deterministic results.

**Rigidbody movement goes in FixedUpdate, never Update.**

```csharp
// ❌ WRONG
void Update() { rb.MovePosition(rb.position + dir * speed * Time.deltaTime); }

// ✅ RIGHT
void FixedUpdate() { rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime); }
```

**Never set `transform.position` on a Rigidbody** — it teleports past the physics engine, skipping collision detection entirely.

```csharp
// ❌ WRONG — teleports through walls
transform.position = newPos;

// ✅ RIGHT — physics-aware movement
rb.MovePosition(newPos);
// or for continuous movement:
rb.linearVelocity = direction * speed; // Unity 6+; use rb.velocity in older versions
```

**Trigger vs Collision callbacks:**
- `OnTriggerEnter` — requires `isTrigger = true` on at least one collider
- `OnCollisionEnter` — requires `isTrigger = false` on both colliders
- Both need colliders on both objects, at least one Rigidbody

**Collisions "don't work"?** Check the Layer Collision Matrix in Project Settings > Physics.

---

## 5. Coroutines & Async

AIs write coroutines that silently die or can't be stopped.

**Coroutines stop when the GameObject is disabled or destroyed.** No error, no callback — they just vanish. If the coroutine must survive, run it on a manager object that stays active.

Common yield instructions:
- `yield return null` — resume next frame
- `yield return new WaitForFixedUpdate()` — resume next physics step
- `yield return new WaitForEndOfFrame()` — resume after rendering

**Store the Coroutine reference to stop it.** `StopCoroutine("MethodName")` only works with `StartCoroutine("MethodName")`. Prefer the reference pattern:

```csharp
// ✅ RIGHT
private Coroutine flashRoutine;

void StartFlash() {
    if (flashRoutine != null) StopCoroutine(flashRoutine);
    flashRoutine = StartCoroutine(Flash());
}
```

**WaitForSeconds uses Time.timeScale.** Use `WaitForSecondsRealtime` for pause menus, UI timers, etc.

**async/await with `Task` can leave the main thread.** Unity API calls (transform, GameObject, etc.) must run on the main thread. If using async, ensure you return to the Unity synchronization context or use a library like UniTask.

```csharp
// ❌ WRONG — may resume on thread pool, crashing on Unity API access
async void Start() {
    await Task.Delay(1000);
    transform.position = Vector3.zero; // potential crash: not on main thread
}

// ✅ RIGHT — use coroutine for simple delays
IEnumerator Start() {
    yield return new WaitForSeconds(1f);
    transform.position = Vector3.zero; // safe: always on main thread
}
```

---

## 6. Common API Mistakes

These compile and look correct but produce wrong results.

**Quaternion multiplication order:** `parent * child`, not `child * parent`.

```csharp
// ❌ WRONG — rotates in wrong space
Quaternion result = localRot * parentRot;

// ✅ RIGHT
Quaternion result = parentRot * localRot;
```

**TransformPoint vs TransformDirection.** `TransformPoint` converts a position (affected by position, rotation, scale). `TransformDirection` converts a direction (rotation only, not affected by scale).

**Never read-modify-write eulerAngles.** Internal representation is quaternion; reading back introduces error and gimbal lock.

```csharp
// ❌ WRONG
transform.eulerAngles = new Vector3(transform.eulerAngles.x + 5, 0, 0);

// ✅ RIGHT
transform.Rotate(5, 0, 0);
// or: transform.rotation = Quaternion.Euler(pitch, yaw, 0);
```

**Destroy targets — know what you're targeting:**

```csharp
Destroy(this);       // removes only this component
Destroy(gameObject); // removes the entire GameObject
// Common bug: writing Destroy(this) when you meant to destroy the whole object
```

Both are valid operations. The pitfall is using one when you meant the other.

**Destroy is deferred** to end of frame — the object still exists for the rest of the current frame. Use `DestroyImmediate` only in Editor scripts (never at runtime).

**Be explicit about Instantiate placement.** Without arguments, the object spawns at the prefab's stored position at the scene root. That's fine for projectiles or world-space VFX, but a common bug when you meant to parent it:

```csharp
// Spawns at scene root with prefab's default transform — intentional for projectiles, VFX, etc.
var obj = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

// Spawns as child of a container — use when you need hierarchy placement
var obj = Instantiate(prefab, spawnPoint.position, Quaternion.identity, parent);
```

---

## 7. Special Folders

Putting scripts in the wrong folder causes build failures or missing code at runtime.

| Folder | Behavior |
|---|---|
| `Editor/` | Excluded from builds. Runtime scripts must NOT go here. |
| `Resources/` | Everything included in build. Loaded via `Resources.Load()`. Don't dump large assets here. |
| `StreamingAssets/` | Raw files copied to build. Platform-dependent path at runtime. |

**`Plugins/`** — native plugins go here. Has a special compilation order (compiled before other scripts).

**Assembly definitions (`.asmdef`):** Scripts inside an asmdef can't reference scripts outside it without an explicit assembly reference. If your new script "can't see" existing code, check for asmdef boundaries. Common symptom: "type or namespace not found" errors that only appear in certain assemblies.

---

## 8. Performance

Correct but slow code in hot paths. These matter when called every frame.

**Cache GetComponent results.** Never call GetComponent in Update — it's a search operation.

```csharp
// ❌ WRONG — searches every frame
void Update() { GetComponent<Rigidbody>().AddForce(Vector3.up); }

// ✅ RIGHT — cached once
private Rigidbody rb;
void Awake() { rb = GetComponent<Rigidbody>(); }
void Update() { rb.AddForce(Vector3.up); }
```

**Never use Find/FindObjectOfType in Update.** These scan the entire scene. Cache the reference in Awake or Start.

```csharp
// ❌ WRONG — O(n) scene scan every frame
void Update() { var player = FindFirstObjectByType<Player>(); }

// ✅ RIGHT
private Player player;
void Start() { player = FindFirstObjectByType<Player>(); }
```

**`CompareTag("X")` not `tag == "X"`** — the `tag` property getter allocates a new string every call. CompareTag does not.

```csharp
// ❌ WRONG — GC allocation every call
if (other.tag == "Enemy") { }

// ✅ RIGHT — no allocation
if (other.CompareTag("Enemy")) { }
```

**Pool frequently spawned/destroyed objects** (bullets, particles, hit effects) instead of calling Instantiate/Destroy each frame. Instantiation is expensive and fragments memory.

**`foreach` on generic collections (`List<T>`, arrays) is fine in modern Unity** — no GC allocation. Only non-generic collections like `ArrayList` cause boxing. No need to replace `foreach` with `for` on typed collections.
