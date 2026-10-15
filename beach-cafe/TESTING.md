# Testing VR item pickup

## XR Device Simulator

1. Open `Assets/BeachRestaurant/Scenes/BeachRestaurant_Blockout.unity` and press Play.
2. In the XR Device Simulator overlay, press `Y` to target the right controller or `T` to target the left controller. Hold `W`, `A`, `S`, or `D` to move it horizontally and `Q`/`E` to move it vertically. The overlay shows the current bindings.
3. Move a controller close to the Patty, either bun, or Cup. Hold the left mouse button (simulated trigger) to pick it up, move the controller, then release to drop it.
4. Confirm the item follows the controller while held and falls under gravity after release. Repeat with another item or the other controller if desired.

`I`, `J`, `K`, and `L` move the player body in the keyboard locomotion controls so `WASD` remains available for controller simulation. Arrow keys remain available for player movement too.

## Automated grab/release smoke check

With the scene running in Play Mode, run:

```sh
unity command eval --project-path /path/to/rpvr/beach-cafe 'RPVRPickupSmokeTest.Run(); return "passed";'
```

The smoke test selects Patty through the Right Controller's XRI interactor, checks that its Rigidbody stays dynamic with velocity-tracked movement and continuous collision detection while held, releases it, and checks that selection clears and gravity remains enabled. It restores Patty's transform and physics state afterward. This checks the XRI grab/release path independently of simulated trigger input.
