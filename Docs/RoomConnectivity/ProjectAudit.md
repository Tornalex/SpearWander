# Project Audit — Room Connectivity (Phase 0)

Scope: only the systems needed to implement room identity and the later room-connectivity phases.
No implementation code was modified during this audit.

Project: Unity `6000.4.0f1` (from `ProjectSettings/ProjectVersion.txt`).
No assembly definition files exist; all gameplay scripts compile into `Assembly-CSharp`, editor scripts into `Assembly-CSharp-Editor`.

---

## 1. GameSceneManager architecture and scene loading

File: `Assets/Scripts/SceneManagement/GameSceneManager.cs`

- `GameSceneManager` is a `MonoBehaviour` singleton (`Instance`, first instance wins).
- In `Awake` it detaches itself from its parent, calls `DontDestroyOnLoad(gameObject)` and is destroyed if a second instance exists.
- Serialized fields (as configured in `Assets/Scenes/Master.unity`):

| Field | Value / purpose |
|---|---|
| `playerTransform` | Player prefab instance in Master |
| `firstRoomSceneName` | `Playground` |
| `firstRoomDoorID` | `Spawn` |
| `cameraConfiner` | `CinemachineConfiner2D` on the camera rig |
| `transitionCanvasGroup` | Fade canvas group in Master |
| `fadeOutDuration` | `0.15` |
| `fadeInDuration` | `0.35` |

- Runtime state: `_currentRoomScene` (scene name string), `_targetDoorID`, `_isTransitioning`.
- Public API: `CurrentRoom`, `TransitionToRoom(sceneName, targetDoorID)`, `LoadInitialRoom(sceneName, doorID)`, `ReloadCurrentRoom(resetState = true, overrideSpawn = null)`, `FadeToBlack()`, `FadeFromBlack()`.

### Start behaviour

`Start` counts real loaded scenes (ignoring `DontDestroyOnLoad`):

- More than one real scene: the first real scene that is not the active scene becomes `_currentRoomScene` and is set active.
- Exactly one real scene:
  - if it is not named `Master`, it is used as the current room (allows opening a room scene directly);
  - if it is `Master`, `LoadInitialRoom(firstRoomSceneName, firstRoomDoorID)` is called.

### Transition flow (`TransitionRoutine`)

1. Reject if `_isTransitioning`.
2. Empty scene name → error and abort.
3. `_isTransitioning = true`, store target door, `SetPlayerControl(false)` (rigidbody becomes `Kinematic`, velocity zeroed, `Player.ControlReason.Transition` added).
4. Fade to black.
5. Unload `_currentRoomScene` additively (`UnloadSceneAsync`) and wait.
6. `_currentRoomScene = sceneName`; `LoadSceneAsync(..., LoadSceneMode.Additive)`; wait.
   - A null operation logs an error mentioning Build Settings and aborts.
7. Set the new scene active, `Physics2D.SyncTransforms()`, wait one frame.
8. `FocusTargetDoor()`, `UpdateCameraBounds()`, force the Cinemachine camera to the player position.
9. Wait three frames, fade in, restore player control, wait `0.1s` real time, clear `_isTransitioning`.

`ReloadCurrentRoom` uses the same unload/load sequence, optionally applying a checkpoint room/position, and optionally calls `Player.ResetState()`.

**Room identity is currently the Unity scene name** (`_currentRoomScene` is a `string`).

---

## 2. How Master is kept persistent

- `Assets/Scenes/Master.unity` is an enabled Build Settings scene and is the intended startup scene. In the current enabled build list it is the first enabled scene (the only scene before it is disabled `SampleScene`).
- Rooms are loaded additively; `GameSceneManager` only unloads `_currentRoomScene`. Master is never unloaded in the current flow, so everything authored in Master stays alive for the whole session.
- Master contains the player prefab instance (source prefab `Assets/Prefabs/Utilities/Player.prefab`, GUID `7a951fb9d51b6314db5ebd114d340b49`), camera rig, HUD, textbox, game-over screen, transition canvas and the manager objects:
  - `GameSceneManager` (GUID `657090c6114ed3441994765402473a49`)
  - `CheckpointManager`
  - `AbilityManager`
  - `SFX Manager`
  - `VFX Manager`
  - `HUDManager`
  - `GameOverUIManager`
  - `EventSystem`
- `GameSceneManager`, `CheckpointManager` and `AbilityManager` also call `DontDestroyOnLoad` on their own GameObjects. This is redundant while Master remains loaded but does not change the current flow.
- No room scene contains a `Player` component. The player exists only in Master and is repositioned by name-based door lookup.

---

## 3. How gameplay rooms are loaded and unloaded

- Rooms are normal scenes under `Assets/Scenes/Rooms/` and are loaded with `LoadSceneAsync(LoadSceneMode.Additive)`.
- Only one gameplay room is loaded at a time: the current room is unloaded before the target room is loaded.
- After loading, the new scene is made active (`SceneManager.SetActiveScene`).
- `UpdateCameraBounds` finds a single object tagged `CameraBounds` via `GameObject.FindGameObjectWithTag` and assigns its `Collider2D` to the Cinemachine confiner. Each room has one such object.
- Runtime loading by name requires the scene to be in Build Settings (or otherwise loadable by name). Build Settings membership is **not** needed for editor-side scene GUID resolution.
- There is no runtime registry of loaded rooms; `_currentRoomScene` is the only record.

---

## 4. Existing player positioning and spawn logic

The player is persistent in Master. Its position is set only by `GameSceneManager`:

- After a normal door transition: `FocusTargetDoor()` searches `FindObjectsByType<RoomTransition>(FindObjectsInactive.Exclude)`, finds the door whose `DoorID` equals `_targetDoorID`, and moves the player to that door's `SpawnPoint.position`.
  - If the door has no spawn point, a warning is logged and the position is left unchanged.
  - If no door ID matches, a warning is logged and the position is left unchanged.
- After a reload with `overrideSpawn`: the player is placed directly at `overrideSpawn` and rigidbody velocity is zeroed.
- After a reload without `overrideSpawn`: `FocusSpawnDoor()` matches `firstRoomDoorID` (`Spawn`); if not found, it falls back to the first active door.
  - `FocusSpawnDoor` uses `firstRoomDoorID` even when the reloaded scene is not the initial room.
- In both paths, player rigidbody velocity is zeroed when the position is assigned.

Unknowns that require runtime testing:

- Whether the player can overlap geometry or enemies in a newly loaded room before `FocusTargetDoor` runs (see section 9).
- Whether inactive doors are ever needed as arrival points (`FindObjectsInactive.Exclude` excludes them; the only inactive doors found are the three duplicate `Right` doors in `R_Spear_Advanced_1`).

---

## 5. RoomTransition implementation

File: `Assets/Scripts/SceneManagement/RoomTransition.cs`
Prefab: `Assets/Prefabs/Props/Door.prefab` (GUID `8a8a628d417d19b43a0149b49ff4753c`)

Serialized fields (exact names):

| Field | Meaning |
|---|---|
| `doorID` | ID of this door inside the room |
| `_spawnPoint` | Child transform used as arrival position |
| `targetSceneName` | Exact Unity scene name to load |
| `targetDoorID` | Door ID to find in the target scene |

Read-only properties: `DoorID`, `SpawnPoint`, `TargetSceneName`, `TargetDoorID`.

Behaviour: `OnTriggerEnter2D` ignores empty `targetSceneName`; when the collider has tag `Player` and `GameSceneManager.Instance` exists, it calls `TransitionToRoom(targetSceneName, targetDoorID)`. It does not check player control state.

The Door prefab has a kinematic `Rigidbody2D`, a trigger `BoxCollider2D` (size `1 x 4` by default) and a `Spawn` child at local `(-1, 1)`. Scenes place Door prefab instances and override `doorID`, `targetSceneName`, `targetDoorID`, transform and sometimes collider size/active state.

---

## 6. Input and interaction integration points

File: `Assets/Scripts/Player/PlayerInputHandler.cs`

- `[DefaultExecutionOrder(-101)]`.
- Exposes `MoveInput`, `AimInput`, `NavigateInput`, `InteractTriggered`, `CancelTriggered`, `JumpTriggered`, `DashTriggered`, `FireTriggered`, `RecallTriggered`, `DownTriggered`, `MouseFire*`, `IsGamepad`, held-state helpers.
- Trigger flags are reset at the start of every `Update` and read from the Input System action asset.
- `InteractTriggered` can be suppressed for one frame via `SwitchToPlayer()` (used after closing the textbox).
- `SwitchToUI()` / `SwitchToPlayer()` swap the `Player` and `UI` action maps.
- Input is not disabled by room transitions; gameplay consumers check `Player.HasControl`.

File: `Assets/Scripts/Player/Player.cs`

- `ControlReason` enum: `Death`, `Respawn`, `Cutscene`, `Transition`, `Pickup`, `Pause`, `TextBox`.
- `HasControl` is true only when no reason is active.
- `GameSceneManager.SetPlayerControl` uses `ControlReason.Transition` and also removes physics control (kinematic rigidbody).

File: `Assets/Scripts/Player/PlayerInteract.cs`

- Trigger-based list of nearby `IInteractable`.
- Uses the last interactable in the list.
- Only interacts when `_player.HasControl && _player.Input.InteractTriggered`.
- Handles destroyed interactables explicitly (interface null checks).
- Optional prompt root/text; interaction works without a prompt.
- Public `RemoveInteractableFromList(IInteractable)`.

File: `Assets/Scripts/Interfaces/IInteractable.cs`

```csharp
void Interact(Player player);
string GetInteractPrompt();
```

Existing implementers include `EssenceWell`, `TextInteractable`, pickups, and other props. `PlayerInteract` finds them with `collision.GetComponent<IInteractable>()`, so the interactable must be on the collider GameObject (or a parent collider lookup would not find it).

---

## 7. Systems that use scene names as room identity

### CheckpointManager
`Assets/Scripts/Checkpoint/CheckpointManager.cs`

- Singleton, `DontDestroyOnLoad`.
- Stores `_checkpointRoom` (string), `_checkpointPosition` and `HasCheckpoint`.
- `SetCheckpoint(string roomName, Vector3 position)`, `GetCheckpoint()`.

### EssenceWell
`Assets/Scripts/Checkpoint/EssenceWell.cs`

- Implements `IInteractable`.
- On interact: reads `GameSceneManager.Instance.CurrentRoom` (scene name), stores it as the checkpoint room, heals/refills, calls `DeadEnemyTracker.ClearAll()`, then `ReloadCurrentRoom(resetState: false, overrideSpawn: transform.position)`.
- Because the checkpoint room is a scene name, death/retry reloads by scene name.

### DeadEnemyTracker
`Assets/Scripts/Abilities/DeadEnemyTracker.cs`

- Static `Dictionary<string, HashSet<string>>`, keyed by room name (currently scene name).
- API: `MarkDead(roomName, stableIdentity)`, `IsDead(roomName, stableIdentity)`, `ClearRoom(roomName)`, `ClearAll()`.
- In-memory only; reset on play session start.

### BaseEnemy
`Assets/Scripts/Enemies/BaseEnemy.cs`

- Fields: `enemyData`, `currentHealth`, hidden `stableIdentity`, public `roomName`, `isDead`.
- `Start` sets `roomName = GameSceneManager.Instance.CurrentRoom` (or `"Unknown"` if no manager), then destroys the object when a non-empty `stableIdentity` is already marked dead.
- `Die` marks `DeadEnemyTracker` when `stableIdentity` is non-empty, then destroys the object.
- `stableIdentity` is assigned by `Assets/Editor/EnemyIdentityEditor.cs` from `GlobalObjectId` (scene GUID + local file ID), repaired on scene open/save and validated before Play Mode.
- `roomName` is serialized but overwritten at runtime; it is not an authored room identity.

### PlayerHealth
`Assets/Scripts/Player/PlayerHealth.cs`

- Health lives on the persistent player; it is not stored per room.
- Uses `GameSceneManager.Instance.CurrentRoom` only when the player dies, for a debug log and `DeadEnemyTracker.ClearAll()`.
- `ResetState()` restores health, i-frames and collider state.

**Conclusion:** checkpointing, enemy-death persistence and room reloads all currently key off the scene name string. Phase 1 introduces stable room identity but must not change these systems yet.

---

## 8. Current room and door inventory

### Scenes

| Scene | In Build Settings | Door prefab instances | Notes |
|---|---|---|---|
| `Assets/Scenes/Master.unity` | yes | 0 | Persistent managers/player/UI; must not be registered |
| `Assets/Scenes/Rooms/Playground.unity` | yes | 1 | Current initial room (`firstRoomSceneName = Playground`) |
| `Assets/Scenes/Rooms/BaseRoom.unity` | yes | 0 | Empty room template (tiles + camera bounds only) |
| `Assets/Scenes/Rooms/Parallax Testing.unity` | yes | 1 | Test room |
| `Legacy/1. Movement_Tutorial/R_Beginning.unity` | yes | 2 (1 inactive) | Inactive `Spawn Point` |
| `Legacy/1. Movement_Tutorial/R_Movement_Tutorial_1.unity` | yes | 2 | |
| `Legacy/1. Movement_Tutorial/R_Movement_Tutorial_2.unity` | yes | 2 | |
| `Legacy/1. Movement_Tutorial/R_Movement_Tutorial_3.unity` | yes | 2 | |
| `Legacy/2. Spear/R_Spear_1.unity` | yes | 2 | Contains EssenceWell prefab instances |
| `Legacy/2. Spear/R_Spear_2.unity` | yes | 2 | |
| `Legacy/2. Spear/R_Spear_3.unity` | yes | 2 | |
| `Legacy/2. Spear/R_Spear_4.unity` | yes | 3 | |
| `Legacy/2. Spear/R_Spear_5.unity` | yes | 3 | |
| `Legacy/2. Spear/R_Spear_6.unity` | yes | 2 | |
| `Legacy/2. Spear/R_Spear_7.unity` | yes | 1 | Contains EssenceWell prefab instances |
| `Legacy/2.1 Spear_Advanced/R_Spear_Advanced_1.unity` | yes | 4 (3 inactive) | Three inactive duplicate `Right` doors |
| `Legacy/2.1 Spear_Advanced/R_Spear_Advanced_2.unity` | **no** | 2 | Not in Build Settings |
| `Legacy/3. Dash/R_Dash_1.unity` | yes | 1 | |
| `Yellow Forest/YellowForest_BardoHouse.unity` | yes | 1 | Scene name contains a space |
| `Yellow Forest/YellowForest_TreeBranch 1.unity` | yes | 1 | Duplicate copy; scene name contains a space |
| `Yellow Forest/YellowForest_TreeBranch.unity` | yes | 2 | Scene name contains no space |
| `Assets/Scenes/Old.unity` | **no** | 1 | Legacy scene, not Build Settings |

Other `.unity` files exist under `Assets/VFX/_Recovery`, `Assets/Imports/LeanTween` and `Assets/TextMesh Pro`; they are not gameplay rooms and were not audited further.

### Doors (by scene)

| Scene | Door object | `doorID` | State | Target scene | Target door |
|---|---|---|---|---|---|
| Playground | Door | `Spawn` | active | *(empty)* | *(empty)* |
| Parallax Testing | Door | `Spawn` | active | `_` | `_` |
| R_Beginning | Door (1) | `Right` | active | `R_Movement_Tutorial_1` | `Left` |
| R_Beginning | Spawn Point | `Spawn` | **inactive** | *(empty)* | *(empty)* |
| R_Movement_Tutorial_1 | Beginning | `Left` | active | `R_Beginning` | `Right` |
| R_Movement_Tutorial_1 | Movement_Tutorial_2 | `Right` | active | `R_Movement_Tutorial_2` | `Left` |
| R_Movement_Tutorial_2 | Movement_Tutorial_3 | `Right` | active | `R_Movement_Tutorial_3` | `Left` |
| R_Movement_Tutorial_2 | Movement_Tutorial_1 | `Left` | active | `R_Movement_Tutorial_1` | `Right` |
| R_Movement_Tutorial_3 | Movement_Tutorial_2 | `Left` | active | `R_Movement_Tutorial_2` | `Right` |
| R_Movement_Tutorial_3 | Spear_1 | `Top` | active | `R_Spear_1` | `Bottom` |
| R_Spear_1 | Spear_2 | `Right` | active | `R_Spear_2` | `Left` |
| R_Spear_1 | Movement_Tutorial_3 | `Bottom` | active | `R_Movement_Tutorial_3` | `Top` |
| R_Spear_2 | Spear_3 | `Right` | active | `R_Spear_3` | `Left` |
| R_Spear_2 | Spear_1 | `Left` | active | `R_Spear_1` | `Right` |
| R_Spear_3 | Spear_4 | `Right` | active | `R_Spear_4` | `Left` |
| R_Spear_3 | Spear_2 | `Left` | active | `R_Spear_2` | `Right` |
| R_Spear_4 | Spear_5 | `Top` | active | `R_Spear_5` | `Bottom` |
| R_Spear_4 | Spear_Advanced_1 | `Right` | active | `R_Spear_Advanced_1` | `Left` |
| R_Spear_4 | Spear_3 | `Left` | active | `R_Spear_3` | `Right` |
| R_Spear_5 | Dash_1 | `Top` | active | `R_Dash_1` | `Bottom` |
| R_Spear_5 | Spear_6 | `Left` | active | `R_Spear_6` | `Right` |
| R_Spear_5 | Spear_4 | `Bottom` | active | `R_Spear_4` | `Top` |
| R_Spear_6 | Spear_5 | `Right` | active | `R_Spear_5` | `Left` |
| R_Spear_6 | Spear_7 | `Left` | active | `R_Spear_7` | `Right` |
| R_Spear_7 | Spear_6 | `Right` | active | `R_Spear_6` | `Left` |
| R_Spear_Advanced_1 | Spear_Advanced_2 | `Right` | **inactive** | `R_Spear_Advanced_2` | `Left` |
| R_Spear_Advanced_1 | Spear_4 | `Left` | active | `R_Spear_4` | `Right` |
| R_Spear_Advanced_1 | Spear_Advanced_2 (2) | `Right` | **inactive** | `R_Spear_Advanced_2` | `Left` |
| R_Spear_Advanced_1 | Spear_Advanced_2 (1) | `Right` | **inactive** | `R_Spear_Advanced_2` | `Left` |
| R_Spear_Advanced_2 | Spear_Advanced_2 | `Right` | active | `R_Spear_Advanced_2` | `Left` |
| R_Spear_Advanced_2 | Spear_Advanced_1 | `Left` | active | `R_Spear_4` | `Right` |
| R_Dash_1 | Door | `Bottom` | active | `R_Spear_5` | `Top` |
| YellowForest_BardoHouse | To YellowForest_TreeBranch | `Entrance` | active | `YellowForest_TreeBranch` | `BardoHouse` |
| YellowForest_TreeBranch 1 | To YellowForest_- | `TopLeft` | active | `YellowForest_` | `TopRight` |
| YellowForest_TreeBranch | To YellowForest_- | `TopLeft` | active | `YellowForest_` | `TopRight` |
| YellowForest_TreeBranch | To YellowForest_BardoHouse | `BardoHouse` | active | `YellowForest_BardoHouse` | `Entrance` |

Inventory observations (not fixed in Phase 0/1):

- `R_Spear_Advanced_1` has three duplicate inactive doors with `doorID = Right`; the active `Left` door is the only arrival point.
- `R_Spear_Advanced_2` is not in Build Settings and its `Right` door targets itself.
- `R_Spear_Advanced_2`'s `Left` door targets `R_Spear_4`, while `R_Spear_4`'s door targets `R_Spear_Advanced_1` (not `R_Spear_Advanced_2`).
- Both Yellow Forest scenes reference a missing scene name `YellowForest_`.
- `Parallax Testing` targets a placeholder scene name `_`.
- `Playground`'s only door has an empty target; it is a spawn marker, not a transition.
- `R_Beginning`'s `Spawn` marker is inactive.
- `BaseRoom` has no doors.
- `Old.unity` is not in Build Settings; it contains a door targeting `R_Movement_Tutorial_3`.

---

## 9. Does the current transition flow prevent unwanted gameplay during additive transitions?

What is guaranteed today:

- `_isTransitioning` blocks a second transition.
- Before unload, the player loses control (`ControlReason.Transition`), its rigidbody becomes kinematic and velocity is zeroed.
- The screen is fully black while the old room unloads and the new room loads.
- The new room is positioned/focused and the camera forced before the fade-in.
- Player interaction (`PlayerInteract`) and combat input use `Player.HasControl`; they are blocked during the fade.
- The previous room is unloaded before the new one loads, so two gameplay rooms are never simultaneously loaded.
- `PlayerCombat` reacts to `SceneManager.sceneUnloaded` and destroys the active spear.

What is not guaranteed today (from code inspection):

- Loaded room content is not disabled or paused. `Awake`, `Start`, `Update`, `FixedUpdate` and physics of the newly loaded room run as soon as the additive load completes, while the screen is still black and player control is off. Enemies, moving platforms, falling platforms and other room objects are live during the remainder of the transition.
- `PlayerHealth` collision damage is not gated by `Player.HasControl`. Its `OnCollisionEnter2D`/`OnCollisionStay2D` paths can apply damage while control is disabled.
- The player keeps its previous room position until `FocusTargetDoor` runs after the load. If that position overlaps new-room colliders/enemies, a collision callback could occur before the player is repositioned.
- `RoomTransition.OnTriggerEnter2D` does not check `HasControl` or `_isTransitioning`; the guard is only inside `GameSceneManager.TransitionToRoom`.

Requires runtime test:

- Whether the overlap described above actually occurs in current rooms (depends on geometry and spawn positions).
- Whether the kinematic player receives collision callbacks from dynamic enemies during the black screen.
- Whether any room's gameplay state changes during the black portion of a transition in a way that matters for future multi-room loading.

---

## 10. Phase 1 implementation-relevant facts

- Scene GUIDs are available through `.meta` files and `AssetDatabase.AssetPathToGUID`.
- Build Settings membership is independent of scene GUID resolution; `R_Spear_Advanced_2` and `Old.unity` are valid discoverable scenes outside Build Settings.
- Door prefab instances can contain inactive `RoomTransition` components (`R_Beginning`, `R_Spear_Advanced_1`), so discovery must include inactive objects.
- `RoomTransition` has no reference back to a room asset and no unique door identity beyond its serialized `doorID`.
- Registration cannot rely on the door prefab GUID because doors are prefab instances with per-instance overrides.
- Opening scenes additively for discovery can trigger `EnemyIdentityEditor.OnSceneOpened`, which repairs missing enemy identities and may mark a scene dirty. Discovery must therefore avoid saving/closing-with-save and should be tested for scene side effects.
- No `RoomDefinition`, `RoomRegistry`, registration menu or room-connectivity code exists yet.
