# M6 sharpening props

Original Blender models authored for this experiment; no downloaded assets or additional packages. Restrained matte enamel, rubber, abrasive and steel; separate wheel and rotation mark, tapered knife blade, bolster, handle pins and twelve visible edge sections. The workbench reuses the project's existing prefab.

Reproduce from the repository root using Blender's bundled Python:

```powershell
& 'Y:/SteamLibrary/steamapps/common/Blender/blender.exe' --background --python SourceArt/SharpeningTrial/build_sharpening.py
```

Source: `M6_Sharpening.blend`; exports: `Assets/Art/SharpeningTrial/M6_Grinder.fbx` and `M6_Knife.fbx`. Units are metres. The export compensates Unity's X handedness conversion. Keep the imported knife transform inside its separate Unity edge pivot: replacing the FBX root rotation would rotate the visible edge away from its contact geometry. Unity materials use URP Lit. These are experimental models requiring the owner's visual assessment.

`SharpeningTrialBuilder.Install` is a one-time scene installation tool; it refuses to overwrite an existing M6. The committed DevelopmentSandbox already contains the station. Do not reinstall it for normal play or verification.
