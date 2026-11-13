# Circular workstation integration — 2026-11-13

`main` incorporates `origin/dev-rmb` at fdd17a4. This document records the integrated layout, interaction fixes and validation.

## Combined scene

- Preserves the circular workstation, grouped grill/fryer/drinks stations, centered XR origin, incoming cooking replay improvements and bundled Android XR restore package.
- Uses the functional tray and bell prefabs in the new layout, with obsolete blockout duplicates removed.
- Food, cup, tray and bell use 85% of their previous dimensions. Tray slots, hints and colliders scale with the tray. The setup uses absolute scales so it is safe to repeat.
- Includes the previously pending soda serving and shared interaction outlines, including runtime burger outlines, socket outline exclusions and preview/bounds filtering.
- Soda placement clamps against rotated circular counter surfaces and aligns the actual collider bottom to the counter.
- Camera movement during validation was confined to Play Mode; the saved camera remains centered at local position/rotation zero, with XR origin at (0, 0, -0.45).
- Setup targets the functional prefabs, preserves their dimensions, avoids recreating existing ring geometry, corrects the order label orientation, and places the ASSEMBLY label on the counter face.

## Validation

Passed in Unity 6000.6.5f1 in the isolated integration worktree:

- Recorded keyboard/mouse burger replay: preview, snap, assembled-body pickup and rotated top-bun alignment.
- Full tray input replay: burger/fries/soda previews, typed slots, removal/replacement, held outlines, socketed outline clearing, live contents and count events.
- Fryer input replay: oil immersion, both-hand hover, basket retrieval into either hand, free dynamic release and stationary player root.
- Stove input replay: raw-to-ready-to-overcooked transitions, steam/smoke, audio and pickup stopping cooking.
- Bell input replay: both hands, hover clearing, press/release, one invocation per press, ordered UnityEvents, audio and plunger animation.
- No compile errors or runtime console errors at the end of the suite. Simulator haptic and XR validation warnings remain environment-specific.

Recorded burger replay reloads a saved scene before Play Mode because the project's disabled scene/domain reload can otherwise retain simulator state between runs. Save scene edits before running the replay. Tray navigation uses parent space, approaches from above and corrects held-item placement after dynamic attach settles. Fryer free-release validation drops over the supply counter so a tray socket does not take selection.

Final verification in the original `main` project also passed: `unity recompile`, `RPVRPickupSmokeTest.Run()` and `console_status` (zero console/compile errors). Scene is saved in Edit Mode with camera local position and rotation zero. `git diff --check` passes.

## Preservation and hygiene

The pre-integration authored state is preserved by a Git stash named `Authored WIP before dev-rmb layout integration` and a backup archive under `.git/rpvr-integration-backups/`. That directory also contains the merge plan, integration patch, replay results and a completed-order screenshot. Generated .utmp, recovery, build and crash files are excluded from the integration and remain uncommitted.

Generated artifacts stay outside version control. Every staged file must remain below 50 MB.

## Editor performance follow-up

A controlled foreground Play Mode comparison on this Linux/OpenGL machine measured about 60 FPS with the separate Project Settings window open, and 86–93 FPS after closing it (CPU profiler enabled in both cases). Game PlayerLoop cost was about 4 ms. Offscreen desktop windows also produce misleading one-second frame samples; keep the Game view onscreen for performance comparisons. Keep Project Settings closed during simulator play; reopening the original project can restore this extra window from the local editor layout.

Closed the Project Settings window and restored all temporary profiling preferences/window sizing. CPU profiling is disabled again. Disabled GPU Resident Drawer in PC_RPAsset because OpenGL cannot support its required raw-buffer path. The mobile renderer and gameplay quality settings are unchanged. The duplicate XRSimulation descriptor messages occur during startup with domain reload disabled; they were not the sustained frame-time bottleneck. The user confirmed performance is normal after the window fix.

Recompile and diff checks passed; final console status reports no compile/runtime errors. Existing gameplay interactions were left untouched during profiling cleanup.

## Simulator release fix

Reproduced a stuck Patty by releasing Space (right-hand manipulation) before releasing the left mouse button. The stock XRDeviceSimulator preserves button state on an unmanipulated hand, leaving XRI Select pressed. Normal mouse release while still manipulating the hand passed.

Added CafeXRDeviceSimulator, which forwards trigger/grip release edges to unmanipulated simulated controllers. The main scene uses this derived simulator with all inherited simulator configuration copied from the original component; sample/package files and input action bindings are unchanged.

RPVRSimulatorReleaseSmokeTest failed before the fix and passed afterwards for normal release, right-hand release after Space, left-hand release after Shift, and Grip release after the manipulation modifier. Recompile, pickup smoke test and diff checks passed. Final console status has zero errors. Scene is saved in Edit Mode.

## Full project retest before commit

All 10 project checks passed on 2026-11-13 against the current saved main scene with CafeXRDeviceSimulator and the reduced prop sizes. Each check started from a fresh scene load. There are no project NUnit/UnityTest suites under Assets; these are the project smoke checks and input replays.

| Check | Result | Seconds |
| --- | --- | ---: |
| pickup | PASS | 5.53 |
| orders | PASS | 7.27 |
| fryer-physics | PASS | 6.14 |
| grill-contact | PASS | 6.14 |
| simulator-release | PASS | 32.98 |
| burger-input | PASS | 40.82 |
| stove-input | PASS | 88.17 |
| fryer-input | PASS | 29.56 |
| bell-input | PASS | 12.27 |
| tray-input | PASS | 116.74 |

Compilation and diff checks passed, and every test finished without console errors. Full results are preserved locally under `.git/rpvr-integration-backups/full-suite/`. The Editor is left in Edit Mode.
