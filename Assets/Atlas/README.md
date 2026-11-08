# Atlas UI: Unity scripts for Team Headset

Drop-in C# for the AtlasXR Unity project. It builds every panel from code (world-space uGUI +
TextMeshPro, 1 canvas pixel = 1 mm), so nobody has to hand-make a prefab, and two people never
edit the same one. Everything runs in the editor with a mouse against a **fake backend** that
uses the same contract as SpacetimeDB. When B1's SpacetimeManager is ready, the UI keeps working
without changes.

## Install (5 minutes)

1. Copy the `Assets/Atlas` folder (and optionally `Assets/Resources/architecture.json`) into
   `AtlasXR/Assets/`.
2. If Unity asks, import **TMP Essential Resources** (Window → TextMeshPro → Import TMP Essential Resources).
3. Open the main scene (H1), then **Atlas → Create Atlas Rig In Scene**.
4. Press Play.

## Try it in the editor

| Do this | What happens |
|---|---|
| Click a role on the role picker | `join` runs; the panels appear around the table; Atlas speaks a first mission |
| Click a node | Node glows in your role color; the info card opens above it |
| Click again or click empty space | Clears the selection |
| Drag a node | Moves it (`move_node` on release). Drag it more than 12 cm and Atlas explains its blast radius |
| Info card → Tell me more / What depends on this? / Modernize this | Sends a question through `add_transcript` |
| Question buttons (right) | Demo questions → reasoning steps → answer caption → scene commands |
| Layer switcher (behind the map) | `set_layer`: code / business / architecture layouts animate |
| Hold **Space** | Push-to-talk (needs a speech-to-text provider, see Voice) |
| **1** highlight_path · **2** animate_flow · **3** blast radius · **4** next layer · **5** focus_node · **6** ghost · **0** clear · **T** fake question · **R** re-place map | Fake scene commands, no agent needed |

Tip: on `Atlas UI`, tick **Auto Join In Editor** to skip the role picker while iterating.

## What's in here

```
Scripts/Core/   AtlasModels (contract types) · IAtlasBackend (the seam) · LocalFakeBackend
                SpacetimeBackendTemplate (for B1) · AtlasUtil (graph BFS, JSON, input) · SampleGraph
Scripts/UI/     AtlasUIRig (creates + places everything) · RolePickerPanel · InfoCardPanel
                ReasoningPanel · ChecklistPanel · QuestionButtonsPanel · LayerSwitcherPanel
                AgentOrb + CaptionPanel · UIFactory · AtlasTheme · WorldLabel · FaceCamera
Scripts/Map/    MapBuilder (spawn + 3 layouts + focus) · NodeView · EdgeView
                SceneCommandRenderer (every scene command kind) · EditorMouseInteractor
Scripts/Voice/  PushToTalkController · AtlasVoice (STT/TTS interfaces)
Editor/         Atlas menu
```

Owners, following the Build Plan: **H1** owns Map/, RolePickerPanel and LayerSwitcherPanel.
**H2** owns everything else in UI/, plus SceneCommandRenderer and Voice/.

## On the headset (Meta XR SDK)

- **Pointing at panels:** the panels already exist in edit mode after *Create Atlas Rig*. Select
  each panel GameObject under `Atlas UI`, then right-click → **Interaction SDK → Add Ray
  Interaction to Canvas** (or Poke). If you change panel code later, run **Atlas → Rebuild UI
  Panels**; you may need to redo the quick action.
- **Grabbing nodes:** make a node prefab with Meta's Grab building block and a collider, and assign
  it to **MapBuilder → Node Prefab**. In the prefab, add an *InteractableUnityEventWrapper* and wire
  it like this:
  - **When Select** → `NodeView.NotifyGrabbed`
  - **When Unselect** → `NodeView.NotifyReleased` (this calls `move_node`)
  - a pinch or poke → `NodeView.NotifySelected` (this calls `select_node`)
- **Push-to-talk:** wire a pinch pose's ActiveStateUnityEventWrapper: **WhenActivated** →
  `PushToTalkController.BeginTalk`, **WhenDeactivated** → `EndTalk`. The microphone permission is
  requested on start.

## Hand-off #2: switching to SpacetimeDB (B1)

1. Generate the bindings into `Assets/module_bindings/` and install the SDK.
2. Add `ATLAS_SPACETIME` to *Player → Scripting Define Symbols*.
3. Finish `SpacetimeBackend` in `SpacetimeBackendTemplate.cs`. It maps the rows to Atlas types and
   raises the events, and has one method per reducer.
4. Add it to the `Atlas` object and **disable `LocalFakeBackend`**. You don't need to touch any UI code.

## Voice (H2)

Implement `ISpeechToText` / `ITextToSpeech` for the provider you pick, then set
`AtlasVoice.SpeechToText` and `AtlasVoice.TextToSpeech` at startup. Without them:
- push-to-talk logs a warning; use the question buttons instead
- answers still appear as a caption over the orb, so the narrator can read them aloud

## Data

`MapBuilder` uses B2's `Assets/Resources/architecture.json` (copied from `data/`). Until that file
exists, it uses the built-in CardDemo-style sample, which you can write out with **Atlas → Copy
Sample architecture.json To Resources**.

The ghost architecture is built from the hints on **SceneCommandRenderer → Billing Example**:
- which nodes move into the new service
- what the service still connects to

You can also assign a prepared prefab under **Ghost Prefabs**.
