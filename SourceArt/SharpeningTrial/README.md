# M6 sharpening props

Original Blender models authored for this experiment; no downloaded assets or additional packages. Restrained matte enamel, rubber, abrasive and steel. The kitchen knife has one continuous curved blade, a thin cutting bevel, a thicker spine and a contoured three-rivet handle. The grinder has a lathed ribbed motor casing, ventilation grille, pressed wheel guard with an open front contact sector, flange/nut, tool rest, clear eye shield, switches, cast base, fasteners and power lead. The workbench reuses the project's existing prefab.

Reproduce from the repository root using Blender's bundled Python:

```powershell
& 'Y:/SteamLibrary/steamapps/common/Blender/blender.exe' --background --python SourceArt/SharpeningTrial/build_sharpening.py
```

Source: `M6_Sharpening.blend`; exports: `Assets/Art/SharpeningTrial/M6_Grinder.fbx` and `M6_Knife.fbx`. Units are metres. The export compensates Unity's X handedness conversion. Keep the imported knife transform inside its separate Unity edge pivot: replacing the FBX root rotation would rotate the visible edge away from its contact geometry. Unity materials use URP Lit. These are experimental models requiring the owner's visual assessment.

Current coating is a single continuous UV surface using the original generated transparent spray mask `Assets/Art/SharpeningTrial/M6PowderSpray.png` (built-in imagegen; [exact prompt](powder_prompt.txt)). The native URP shader `M6PowderCoating.shader` combines two randomized mask placements, varying offsets/scale on each trial reset via a local System.Random. It smoothly interpolates actual progress along the blade; no twelve strip meshes or rectangular visible divisions remain. Cyan is visual guidance, not a new consumable. No bloom/lighting bake or additional renderer package is required.

`CuttingEdge_00` through `CuttingEdge_11` are invisible Blender-authored marker transforms, verified against the gameplay curve. The cutting edge faces +Z toward the wheel; the thick spine faces -Z toward the player. The curve rises toward the pointed tip. Motion is constrained against the actual wheel circumference along 96 intervals of the curved edge, including tilt. Exports retain the existing FBX GUIDs. Use `-- --knife-only` after the script argument when only the knife has changed. `Tools/Development Sandbox/Apply M6 Kitchen Models` rebinds the existing station after an export; it does not reinstall it or move adjacent stations.

Visual references only (not downloaded or incorporated into assets): [Robert Welch kitchen-knife profile](https://www.robertwelch.com/blogs/news/professional-knives), [Kastforce bench grinder components](https://kastforce.com/products/2-8amp-1-2hp-6-inch-high-speed-bench-grinder-csa-listed-table-grinder-for-sharpening). No brand/logo was copied. Models and mask remain experimental artwork requiring the owner's assessment.

`SharpeningTrialBuilder.Install` is a one-time scene installation tool; it refuses to overwrite an existing M6. The committed DevelopmentSandbox already contains the station. Do not reinstall it for normal play or verification.
