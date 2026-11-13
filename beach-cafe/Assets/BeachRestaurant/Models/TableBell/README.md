# Table bell model

Downloaded archive: `table-bell.zip`, source `source/Table bell.7z/Table Bell.fbx`.
Original mesh and supplied PBR textures retained. Unity URP materials use base color, metallic, and occlusion textures with scalar smoothness. Authored roughness maps are retained for further material work.
The prefab is approximately 16 cm wide. HoverPressButton shows a white, depth-tested URP outline for either hand and invokes its configurable On Pressed list of UnityEvents once per press. The first event is assigned to the spatial AudioSource.Play action using Audio/ServiceBell.wav. The plunger animates on press. Additional event entries can be assigned in the Inspector.
