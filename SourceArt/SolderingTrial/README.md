# M2 workshop props

Editable Blender source: `M2_SolderingProps.blend`. Prop roots are staged for the source preview; exported FBX models use a zero origin at the working end (iron and solder) or repair contact (flashlight). Metres, fixed functional anchors; no gameplay scripts or colliders in the imported models.

Created locally in Blender 5.2.3 LTS, without downloaded assets, paid APIs or extra Python packages. `build_soldering_props.py` uses Blender's Python, bmesh and bundled NumPy. It creates shaped meshes with bevelled edges, a ribbed iron grip, wound solder, an open housing, cable curves and a shared UV atlas. The table and dynamic solder bridge remain the existing prototype geometry.

From the repository root:

```powershell
& 'Y:/SteamLibrary/steamapps/common/Blender/blender.exe' --background --python SourceArt/SolderingTrial/build_soldering_props.py
```

Outputs:

- `Assets/Art/SolderingTrial/Models/`: three FBX files.
- `Assets/Art/SolderingTrial/Textures/`: 512 × 512 base colour and metallic/smoothness atlas (metal in R, smoothness in A), plus a 64 × 64 soft smoke sprite.
- `M2_SolderingProps.blend`: source with packed surface textures, meshes and preview camera.
- `Logs/M2-Blender-props.png`, `Logs/M2-Blender-assets.json`: local preview and mesh counts.

Unity: `Tools/Development Sandbox/Apply M2 Blender Visuals` imports the maps, updates a shared URP Lit material, creates prefab wrappers and binds them to the existing sandbox trial. Stop Play Mode and preserve unsaved scene work before applying. Model pivots and functional markers stay at the original contact positions. `HotTip` gets the trial's animated heat material; other surfaces retain textures. FBX handedness conversion is accounted for explicitly in the source.

Triangle counts: iron 3,644; spool 4,476; open flashlight 4,600 (12,720 total). These are a first styled set for the focused sandbox experiment, not an accepted final art package. Art and usability require the owner's assessment. M3 disassembly/assembly and city integration are separate work.
