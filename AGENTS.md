# Agent notes

## Repository and Unity project

- The Git root is this directory (`rpvr`); the Unity project is in `beach-cafe/`.
- The project has been running with Unity `6000.6.5f1`. Check the connected Editor before acting with `unity status`, and pass `--project-path /home/huji/dev/rpvr/beach-cafe` to Editor commands.
- When the Editor is connected, use the Unity CLI to inspect and change scenes/assets. Stop Play Mode before making saved scene changes; Play Mode edits can be lost when the Editor exits it.
- Useful commands: `unity command editor_play`, `unity command editor_stop`, `unity command eval '…'`, `unity recompile`, and `unity command console_status`. Include the explicit project path on each command. Save scene edits through `UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes()`.
- In XRI 3.4, the grab interactable type is `UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable`; interactor types are under `UnityEngine.XR.Interaction.Toolkit.Interactors`.

## VR scene and interaction setup

- Main scene: `beach-cafe/Assets/BeachRestaurant/Scenes/BeachRestaurant_Blockout.unity`.
- The scene has four grab items: `Patty`, `Top Bun`, `Bottom Bun`, and `Cup`. They use convex mesh colliders and Rigidbodies. When replacing a collider, keep the interactable's `colliders` list pointing at the new collider.
- Grabbables use dynamic attach to preserve their orientation offset at grab time, `VelocityTracking` movement, `ContinuousDynamic` collision detection, and Rigidbody interpolation. Keep them dynamic while held so they collide with static scene colliders; XRI's Kinematic movement can let a held object pass through colliders without Rigidbodies.
- Only `Patty` has the `SlipperyItem` physics material (`beach-cafe/Assets/BeachRestaurant/Materials/SlipperyItem.physicMaterial`): static/dynamic friction `0.05`, friction combine `Minimum`. Buns and Cup use default friction.
- `beach-cafe/Assets/Editor/RPVRPickupSmokeTest.cs` checks trigger bindings and the grab/release physics path. Run it in Play Mode with:

  ```sh
  unity command eval --project-path /home/huji/dev/rpvr/beach-cafe 'RPVRPickupSmokeTest.Run();'
  ```

- The XR Device Simulator overlay is the source of truth for its current key labels. In the current setup, `W/A/S/D` and `Q/E` move the simulated controller, `T`/`Y` select controllers, and the left mouse button simulates the trigger. Player movement was moved to `I/J/K/L` so `W/A/S/D` remain available to the simulator.
- Both hands' XRI Select button and value actions are bound to their trigger controls in `beach-cafe/Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/XRI Default Input Actions.inputactions`.

## Validation and repository hygiene

- Validate changes with `unity recompile --project-path /home/huji/dev/rpvr/beach-cafe`, the pickup smoke test, and `unity command console_status --project-path /home/huji/dev/rpvr/beach-cafe`. Inspect actual console errors separately from warnings.
- The simulated controller can log a haptic-capability warning; that is expected with XR Device Simulator. OpenXR editor validation has also logged a `DllNotFoundException` for `UnityOpenXR` on this machine; distinguish it from compile errors and runtime interaction failures.
- Avoid committing generated build/recovery files such as `beach-cafe/.utmp/`, `beach-cafe/Assets/_Recovery/`, `beach-cafe/build/outputs/logs/`, and `beach-cafe/mono_crash*.blob`.
- The user requested that committed files stay below 50 MB. Check the size of every staged file before committing, and leave generated artifacts out even when they are smaller than that limit.
