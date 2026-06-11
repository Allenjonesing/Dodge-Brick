# Dodge-Brick 🧱

A multiplayer VR game for the **Oculus Quest** built with Unity, Photon Unity Networking (PUN 2), and the XR Interaction Toolkit. Players enter a shared gym arena and try to dodge bricks being thrown around. Getting hit triggers a ragdoll effect on the hit player's avatar.

---

## Table of Contents

1. [Game Overview](#game-overview)
2. [How to Play](#how-to-play)
3. [Requirements](#requirements)
4. [Project Structure](#project-structure)
5. [Scene Layout](#scene-layout)
6. [Scripts Reference](#scripts-reference)
   - [NetworkManager.cs](#networkmanagercs)
   - [NetworkPlayerSpawner.cs](#networkplayerspawnercs)
   - [NetworkPlayer.cs](#networkplayercs)
   - [XRGrabNetworkInteractable.cs](#xrgrabnetworkinteractablecs)
   - [ContinuousMovement.cs](#continuousmovementcs)
   - [VRRig.cs](#vrrigcs)
   - [VRAnimatorController.cs](#vranimatorcontrollercs)
   - [VRFootIK.cs](#vrfootikcs)
   - [HandPresence.cs](#handpresencecs)
   - [AvatarInfo.cs](#avatarinfocs)
   - [AvatarSelector.cs](#avatarselectorcs)
   - [LeaveRoomOnInput.cs](#leaveroomoninputcs)
   - [brick.cs](#brickcs)
   - [BrickSound.cs](#bricksoundcs)
   - [AvatarToRagdoll.cs](#avatartoragdollcs)
   - [AvatarToRagdollHead.cs](#avatartoragdollheadcs)
7. [Multiplayer Architecture](#multiplayer-architecture)
8. [Known Bugs Fixed](#known-bugs-fixed)
9. [Setup & Running](#setup--running)
10. [Photon Configuration Checklist](#photon-configuration-checklist)

---

## Game Overview

- Up to N players connect via Photon to the **MainGym** arena.
- Each player is represented by a networked avatar (the "Blue Avatar" prefab).
- Bricks are grabbable physics objects. Any player can pick one up (ownership transfers to them) and throw it.
- A brick collision on a player character triggers the **BzRagdoll** system, knocking them over.
- Players can leave the match by pressing the **Menu Button** on their left controller.

---

## How to Play

### Lobby Flow
- Start in `Lobby.unity`.
- Aim at **CONNECT** and press your controller trigger.
- Wait for the lobby status prompt to confirm the room list is ready.
- Aim at **PLAY** to join the `MainGym` room.

### MainGym Controls
- Reach out and grab bricks with your hands.
- Throw bricks at other players or stack them for cover.
- Use Photon Voice to talk with other players while you play.
- Press the **left controller Menu button** to leave the match and return to the lobby.

### Current In-Game Guidance
- `Lobby.unity` now spawns a floating instruction/status board beside the connect button.
- `MainGym.unity` now spawns a short-lived floating instruction board near the player spawn point.

---

## Requirements

| Dependency | Version |
|---|---|
| Unity | 2019.4 LTS (or the version used when this project was created) |
| XR Interaction Toolkit | ≥ 0.9 |
| Photon Unity Networking 2 (PUN 2) | included in `Assets/Photon/` |
| Photon Voice 2 | included in `Assets/Photon/PhotonVoice/` |
| Oculus Integration | included in `Assets/Oculus/` |
| BzKovSoft Ragdoll | included in `Assets/BzKovSoft/` |

---

## Project Structure

```
Assets/
├── Scripts/               # All custom game scripts (see Scripts Reference)
├── Photon/                # Photon PUN 2, Photon Voice, Photon Realtime libraries
├── Oculus/                # Oculus Integration SDK
├── BzKovSoft/             # Ragdoll / character physics library
├── Prefabs/               # Network Player, Blue Avatar, brick, etc.
├── Resources/             # Prefabs that must be loaded at runtime via PhotonNetwork.Instantiate
│   └── Network Player     # Player prefab (must live here for Photon to find it)
│   └── Blue Avatar        # Avatar prefab (must live here for Photon to find it)
├── Scenes/
│   ├── Lobby.unity        # Main menu / matchmaking scene (scene index 0)
│   └── MainGym.unity      # In-game arena scene
├── Animations/            # Avatar animation clips and controllers
├── Models/                # 3D character/environment models
├── Materials/             # Shared materials
├── Plugins/               # Native Android/IL2CPP plugins for Oculus Quest
├── brick.cs               # Brick collision → ragdoll handler (root level)
├── BrickSound.cs          # Plays a random sound on brick collision
├── AvatarToRagdoll.cs     # Body trigger zone → ragdoll
└── AvatarToRagdollHead.cs # Head trigger zone → instant ragdoll
```

---

## Scene Layout

### Lobby (`Assets/Scenes/Lobby.unity`)
- Hosts the **NetworkManager** GameObject which drives Photon connection/room creation.
- Contains a UI canvas for connection and room selection.
- Includes an XR rig and a runtime guidance/status board so the flow is usable in-headset.

### MainGym (`Assets/Scenes/MainGym.unity`)
- The play arena. Contains:
  - **XR Rig** – the local player's camera/controller GameObject hierarchy.
  - **NetworkPlayerSpawner** – spawns the `Network Player` prefab via Photon when a client joins.
  - **ContinuousMovement** – locomotion component attached to the XR Rig or Network Player.
  - **Brick prefabs** – physics objects with `XRGrabNetworkInteractable` + `PhotonView` + `PhotonTransformView`.
  - **Death/sound zones** – trigger volumes with `BrickSound`.
      - **Runtime instruction board** – explains the match controls on load.

---

## Scripts Reference

### `NetworkManager.cs`
Manages the full Photon connection lifecycle.

| Method | Description |
|---|---|
| `ConnectToServer()` | Initiates a connection to the Photon cloud using the app settings in `PhotonServerSettings`. |
| `OnConnectedToMaster()` | Callback fired when the client reaches the Master Server; automatically joins the lobby. |
| `OnJoinedLobby()` | Activates the room-selection UI. |
| `InitiliazeRoom(int)` | Stores the target scene index and calls `PhotonNetwork.JoinOrCreateRoom`. |
| `OnJoinedRoom()` | Called after the room is joined; loads the game scene with `PhotonNetwork.LoadLevel`. |
| `OnPlayerEnteredRoom(Player)` | Logs when a new remote player joins. |

**Key field:** `defaultRooms` – serialized list of `DefaultRoom` objects (name, scene index, max players).

---

### `NetworkPlayerSpawner.cs`
Attached to a persistent GameObject in the game scene. Spawns the local player's network prefab.

| Method | Description |
|---|---|
| `OnJoinedRoom()` | Spawns the `Network Player` prefab at this transform via `PhotonNetwork.Instantiate`. |
| `OnLeftRoom()` | Destroys the previously spawned prefab. |

---

### `NetworkPlayer.cs`
Attached to the `Network Player` prefab. Tracks the local XR Rig and drives hand animations.

| Field | Description |
|---|---|
| `head`, `leftHand`, `rightHand` | Transform targets on the prefab that follow the physical headset and controllers. These should each have a `PhotonTransformView` observed by the root `PhotonView`. |
| `leftHandAnimator`, `rightHandAnimator` | Set at runtime from the spawned avatar's `AvatarInfo`. |

| Method | Description |
|---|---|
| `Start()` | Finds the scene's `XRRig`, caches the head/hand rig transforms, then — only on the owner — fires the `LoadAvatar` RPC. |
| `LoadAvatar(int index)` [PunRPC] | Owner only: instantiates the `Blue Avatar` prefab via `PhotonNetwork.Instantiate` and parents avatar bones to the tracked transforms. |
| `Update()` | Owner only: calls `MapPosition` to mirror XR device positions to the network transforms, and updates hand grip/trigger animations. |

---

### `XRGrabNetworkInteractable.cs`
Extends `XRGrabInteractable` with Photon ownership transfer and position streaming.

This is the **core fix** for bricks not appearing to move for other players.

| Method | Description |
|---|---|
| `OnSelectEnter(XRBaseInteractor)` | Calls `photonView.RequestOwnership()` so the grabbing player becomes the physics authority, then calls the base grab logic. |
| `OnPhotonSerializeView(PhotonStream, PhotonMessageInfo)` | **IPunObservable** — streams `position`, `rotation`, `velocity`, and `angularVelocity` to all other clients every network tick. |

> **Inspector requirement**: Add this component to the **Observed Components** list of the `PhotonView` on every grabbable brick prefab. Without this, Photon will never call `OnPhotonSerializeView`.

---

### `ContinuousMovement.cs`
Provides smooth analogue-stick locomotion for the local player.

Movement is applied **locally** on the owning client only; Photon's `PhotonTransformView` (on the player prefab's `PhotonView`) propagates the new position to other clients.

| Field | Description |
|---|---|
| `speed` | Units per second. |
| `inputSource` | Which `XRNode` to read the joystick from (e.g. `LeftHand`). |
| `groundLayer` | Layer mask used to detect walls / obstacles via `Physics.CheckSphere`. |

---

### `VRRig.cs`
Drives the avatar's body root position and facing direction based on the headset's position/rotation, plus explicit `VRMap` targets for head and each hand.

`VRMap` (inner serialisable class): holds a `vrTarget` (XR device transform) and a `rigTarget` (avatar bone), with configurable position/rotation offsets.

---

### `VRAnimatorController.cs`
Computes the player's movement speed from headset world-space velocity and feeds `isMoving`, `DirectionX`, and `DirectionY` into the avatar's `Animator` for walk/idle blending.

---

### `VRFootIK.cs`
Uses Unity's `OnAnimatorIK` callback to raycast from each foot downward and snap it to the floor surface, keeping feet planted realistically on uneven terrain.

---

### `HandPresence.cs`
Initialises and toggles between a controller model and a hand model depending on the `showController` flag. Drives the hand `Animator` from trigger/grip input values.

---

### `AvatarInfo.cs`
Simple data container on the `Blue Avatar` prefab. Exposes references to the avatar's head/hand transforms and hand animators so `NetworkPlayer.LoadAvatar` can wire them up.

---

### `AvatarSelector.cs`
UI helper. Call `SetAvatarID(int)` (e.g. from a UI Button `OnClick`) to persist the chosen avatar index to `PlayerPrefs` before entering a room.

---

### `LeaveRoomOnInput.cs`
Watches a configurable XR button (default: left-hand Menu Button). When pressed, it disconnects from Photon and loads scene index 0 (the Lobby).

---

### `brick.cs`
Attached to every brick GameObject. On `OnCollisionEnter` with a `"Player"` tagged collider:
- Spawns a blood-particle effect at the contact point (capped at 5 active particles, with a 10 s cool-down).
- Calls `BzRagdoll.BrickCollisionDetected()` to start the ragdoll simulation.

---

### `BrickSound.cs`
Attach directly to any death zone or brick collider that has an `AudioSource`. Picks a random clip from `sounds` and plays it on every `OnCollisionEnter`.

---

### `AvatarToRagdoll.cs`
Trigger-based script on the avatar's **body** collider. When a `"Brick"` tagged object enters, it activates the ragdoll GameObject and triggers `BzRagdoll.BrickCollisionDetected()`.

---

### `AvatarToRagdollHead.cs`
Same as `AvatarToRagdoll` but attached to the avatar's **head** collider and calls `BrickCollisionDetected(true)` for an instant-kill head-shot.

---

## Multiplayer Architecture

```
Photon Cloud (Master Server)
        │
        ▼
  NetworkManager          ← scene 0 (Lobby)
  ConnectToServer()
        │
  OnConnectedToMaster()
        │ JoinLobby()
  OnJoinedLobby()  ──► show Room UI
        │
  InitiliazeRoom()
        │ JoinOrCreateRoom()
  OnJoinedRoom()   ──► PhotonNetwork.LoadLevel(gameScene)
        │
        ▼ scene 1 (MainGym)
  NetworkPlayerSpawner.OnJoinedRoom()
        │ PhotonNetwork.Instantiate("Network Player")
        ▼
  NetworkPlayer.Start()   [IsMine only]
        │ RPC("LoadAvatar", AllBuffered, avatarID)
        ▼
  LoadAvatar()            [IsMine only]
        │ PhotonNetwork.Instantiate("Blue Avatar")
        │ parent avatar bones → head/leftHand/rightHand transforms
        ▼
  NetworkPlayer.Update()  [IsMine only]
        │ MapPosition() mirrors XR rig → network transforms
        │ UpdateHandAnimation() reads controller input → Animator
        ▼
  PhotonTransformView syncs head/hand positions → all remote clients
```

### Grabbable Objects (Bricks)

```
Player grabs brick
  └─► XRGrabNetworkInteractable.OnSelectEnter()
        └─► photonView.RequestOwnership()   ← this client is now authority
              │
              ▼
  Owner's FixedUpdate moves brick via physics / XR interaction
              │
              ▼
  XRGrabNetworkInteractable.OnPhotonSerializeView() [IsWriting]
        └─► stream: position, rotation, velocity, angularVelocity
              │  (every Photon send rate tick, ~10 times/sec)
              ▼
  Remote clients' OnPhotonSerializeView() [IsReading]
        └─► apply position/rotation/velocity to their local copy
              └─► brick visually follows the owner's throw on all screens
```

---

## Known Bugs Fixed

The following bugs were present in the original codebase and have been corrected:

### 1. Bricks/objects don't move for other players *(PRIMARY)*
**File:** `XRGrabNetworkInteractable.cs`  
**Root cause:** The class called `RequestOwnership()` when an object was grabbed (correct), but never implemented `IPunObservable`. Photon therefore never transmitted the object's new position/rotation/velocity to any other client.  
**Fix:** Implemented `IPunObservable.OnPhotonSerializeView` to stream position, rotation, and Rigidbody velocity on every network tick.  
**⚠️ Action required in the Unity Editor:** Open each brick prefab, select its `PhotonView` component, and add `XRGrabNetworkInteractable` to the **Observed Components** list. Without this, Photon will not invoke `OnPhotonSerializeView`.

### 2. Player movement broken for remote clients
**File:** `ContinuousMovement.cs`  
**Root cause:** Movement was applied inside a `[PunRPC] MoveSelf()` method sent to `RpcTarget.AllBuffered` every `FixedUpdate`. The method referenced `rig` — the local scene's `XRRig` — so every remote client would move *their own* XR rig instead of the moving player's network transform, causing chaotic teleportation for everyone.  
**Fix:** Removed the RPC. Movement is now applied locally only when `photonView.IsMine`; Photon's `PhotonTransformView` propagates the result to all clients.

### 3. `LoadAvatar` RPC crashes with a serialization exception
**File:** `NetworkPlayer.cs`  
**Root cause:** The `LoadAvatar` RPC included an `XRRig rig` parameter. Photon cannot serialise MonoBehaviour objects and throws a `System.Exception` before the RPC is ever sent, so no avatar was ever loaded in a real multiplayer session. Additionally, calling `PhotonNetwork.Instantiate` inside an `AllBuffered` RPC caused every connected client to try to spawn its own copy of the avatar, creating one avatar per player per client.  
**Fix:** Removed the `XRRig` parameter from the RPC signature; the rig is now found locally via `FindObjectOfType<XRRig>()`. `PhotonNetwork.Instantiate` is gated behind `photonView.IsMine` so only the owning client spawns the avatar (Photon replicates it to all clients automatically).

### 4. Scene loaded before room was joined
**File:** `NetworkManager.cs`  
**Root cause:** `InitiliazeRoom` called `PhotonNetwork.LoadLevel` *before* `PhotonNetwork.JoinOrCreateRoom`. `PhotonNetwork.LoadLevel` requires the client to already be inside a room; calling it earlier silently fell back to a local scene load with no network sync.  
**Fix:** The scene index is stored in `pendingSceneIndex` and `PhotonNetwork.LoadLevel` is called inside `OnJoinedRoom`, after the room join is confirmed.

---

## Setup & Running

1. **Clone / open the project** in Unity (match the Unity version used by the project).
2. **Configure Photon:** go to `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings` and paste your Photon App ID.
3. **Enable scene sync:** in `PhotonServerSettings`, tick **Auto Join Lobby** and **Automatically Sync Scene**.
4. **Build settings:** add `Lobby` (index 0) and `MainGym` (index 1) to the build scene list.
5. **Android / Oculus Quest build:**
   - Switch platform to Android.
   - Set the minimum API level to Android 10 (API 29) or as required by the Oculus SDK.
   - Enable IL2CPP scripting backend.
   - Sign with the included `user.keystore` (or provide your own).
6. **Test in Editor:** hit Play in the Lobby scene; click a room button; the MainGym scene should load and a network player spawned.

---

## Photon Configuration Checklist

- [ ] App ID set in `PhotonServerSettings`
- [ ] **Automatically Sync Scene** enabled in `PhotonServerSettings`
- [ ] `Network Player` prefab is inside `Assets/Resources/`
- [ ] `Blue Avatar` prefab is inside `Assets/Resources/`
- [ ] Every brick prefab's `PhotonView` → **Observed Components** includes `XRGrabNetworkInteractable`
- [ ] Every brick prefab's `PhotonView` → **Ownership Transfer** set to `Takeover` (so `RequestOwnership` works)
- [ ] `NetworkPlayer` head/hand child transforms each have a `PhotonTransformView` observed by the root `PhotonView`

---

## Living Room Pirates

> Converting the dodgeball prototype into a room-scale pirate ship experience. The player's only in-game locomotion is physical walking inside their real Meta Quest Guardian boundary.

### New Scripts (`Assets/Scripts/LivingRoomPirates/`)

| Script | Purpose |
|---|---|
| `BoundaryShipGenerator.cs` | Reads the Meta Guardian play-area via `OVRBoundary`, selects a `ShipTier`, and instantiates the ship from prefabs at runtime. Exposes `RegenerateShip()` for editor testing. |
| `DisableLocomotion.cs` | Finds and disables all artificial locomotion on `Start()`: `ContinuousMovement`, `ContinuousMoveProvider`, `ContinuousTurnProvider`, `SnapTurnProvider`, `TeleportationProvider`, `OVRPlayerController`. |
| `ShipStationNetwork.cs` | Photon custom-event hub (codes 10–15). Provides `SendCannonFired`, `SendCannonballHit`, `SendRepairAction`, `SendAnchorAction`, `SendSailAction`, `SendAvatarSync` helpers and handles incoming events. |
| `CannonController.cs` | Single-cannon script: fires a local `Rigidbody` cannonball, plays VFX/SFX, then broadcasts via `ShipStationNetwork.SendCannonFired`. |
| `PirateNetworkPlayer.cs` | Replaces/extends `NetworkPlayer.cs` for the pirate game. Streams head/hand transforms via `IPunObservable` and the `ShipStationNetwork` avatar-sync event. |

### ShipTier Enum

| Tier | Condition (smaller dimension) |
|---|---|
| `Dinghy` | < 1.5 m or no valid Guardian boundary |
| `Rowboat` | 1.5 – 2.2 m |
| `Sloop` | 2.2 – 3.0 m |
| `Brig` | 3.0 – 4.0 m |
| `Galleon` | 4.0 m + |

### Scene Hierarchy (recommended)

```
LivingRoomPiratesRoot          ← BoundaryShipGenerator, DisableLocomotion
  ShipGeneratedRoot            ← all spawned deck tiles, railings, stations
  PlayerRig                    ← XR Rig (head-tracking only; no locomotion)
  NetworkedPropsRoot           ← ShipStationNetwork (PhotonView required)
  BoundaryDebugRoot            ← optional boundary visualiser objects
```

### Startup Flow

```
Scene loads
  └─► DisableLocomotion.Start()
        └─► disables ContinuousMovement, XRI providers, OVRPlayerController
  └─► BoundaryShipGenerator.Start()  →  RegenerateShip()
        ├─► OVRManager.trackingOriginType = Stage
        ├─► OVRBoundary.GetDimensions(PlayArea)  →  width × depth in metres
        │       (falls back to editorFallbackWidth × editorFallbackDepth in Editor)
        ├─► usableWidth = width  - 2 × safetyMargin (default 0.35 m)
        ├─► SelectTier(minDim)  →  ShipTier
        ├─► SpawnDeck()         →  flat deck tiles
        ├─► SpawnRailings()     →  railings around usable rectangle (colliders disabled)
        └─► SpawnStations()     →  tier-specific interactables logged to console
```

### Network Events (ShipStationNetwork)

| Event Code | Event | Reliable? |
|---|---|---|
| `10` | `CannonFired` (side, index, aimYaw, firePower) | Yes |
| `11` | `CannonballHit` (hitType, position) | Yes |
| `12` | `RepairAction` (station, amount) | Yes |
| `13` | `AnchorAction` (isAnchored) | Yes |
| `14` | `SailAction` (sailIndex, openAmount) | Yes |
| `15` | `AvatarSync` (head/hand poses) | No (unreliable, high-frequency) |

### Inspector Setup Checklist

**BoundaryShipGenerator** (on `LivingRoomPiratesRoot`):
- [ ] Assign `shipGeneratedRoot` Transform
- [ ] Assign `boundaryDebugRoot` Transform (optional)
- [ ] Assign structural prefabs: `DeckTile`, `RailingStraight`, `RailingCorner`
- [ ] Assign station prefabs: `SteeringWheel`, `CannonForward`, `CannonSide`, `AnchorLever`, `SailRope`, `Spyglass`, `Oar`, `RepairBucket`, `AmmoCrate`, `TreasureChest`, `MastSmall`

**CannonController** (on each cannon prefab):
- [ ] Assign `firePoint` Transform (barrel mouth)
- [ ] Assign `cannonballPrefab` (Rigidbody prefab)
- [ ] Optionally assign `fireVFX` (ParticleSystem) and `fireAudio` (AudioSource)
- [ ] Set `shipSide` ("Forward", "Port", or "Starboard") and `stationIndex`

**ShipStationNetwork** (on `NetworkedPropsRoot`):
- [ ] GameObject must have a `PhotonView` component

**PirateNetworkPlayer** (on `Network Player` prefab):
- [ ] Assign `head`, `leftHand`, `rightHand` avatar bone Transforms
- [ ] Assign `leftHandAnimator`, `rightHandAnimator`
- [ ] Add `PirateNetworkPlayer` to `PhotonView` → **Observed Components**

### Multiplayer Design Principle

Each player's ship fits their own real room — ship geometry is **not** networked. Only gameplay intent is sent:

```
Player fires port cannon #1  →  SendCannonFired("Port", 1, aimYaw, firePower)
                                       │
                               All remote clients receive EVENT_CANNON_FIRED
                                       │
                               Each client plays VFX on their own local ship layout
```
