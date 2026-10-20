# Workshop worker source

Base model and hairstyle: **Quaternius**, Universal Base Characters, **Standard (free)** edition.

- Author's page: https://quaternius.com/packs/universalbasecharacters.html
- Official download: https://quaternius.itch.io/universal-base-characters
- License: **CC0 1.0 Universal**, https://creativecommons.org/publicdomain/zero/1.0/
- Original author's included notice: `Original/License_Standard.txt`.
- Downloaded 2026-10-20, free Standard archive, SHA-256: `FDBF1804C90DFC1EA03E992BFF7DA2DFD1A79318E13270A660180F9308455F40`.

`Original/Quaternius_SuperheroMale.fbx` and `Original/Quaternius_HairSimpleParted.fbx` are the downloaded base and hairstyle. No Source-edition purchase, account, paid API, or software installation was used.

`WorkshopWorker.blend` is the editable adaptation made in the already installed **Blender 5.2.2 LTS**. The downloaded realistic head and eyes were rejected by the owner and removed from the active model. The final character reuses the skeleton, reshaped hands/forearms, trouser topology and fitted hairstyle from Quaternius. The elongated head, simple white eyes and pupils, nose/mouth, clean open jacket, undershirt and shaped shoes were made in Blender for this sample. There are no photographic skin/iris textures. A quiet idle uses the actual skeleton. Preview cameras/lights are excluded from the saved source and FBX. The original realistic candidate is not the delivered model.

`build_worker.py` reconstructs this adaptation from the downloaded originals. Run from the project with Blender `--background --factory-startup --python-exit-code 1 --python SourceArt/ArtDirectionTest/Worker/build_worker.py`. It overwrites the adaptation and exported FBX; preserve manual changes to the blend before reconstructing. For normal hand editing, edit the blend and export the selected armature/meshes to `Assets/ArtDirectionTest/Worker/WorkshopWorker.fbx` with -Z forward/Y up and no leaf bones.

In Unity, `Tools > Art Direction > Import Blender Worker` configures the FBX, creates/remaps the URP materials, connects the looping idle, and replaces only the rejected art-test prefab's contents while preserving its GUID. The downloaded base is third-party CC0 content, not an original Hustle & Scale sculpt or an asset from Schedule I. Its suitability remains subject to the owner's visual review.
