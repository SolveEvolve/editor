# Static QSF2 conversion

This folder contains the offline still-image conversion path used by the Proto VR scene. It converts one standard binary Gaussian PLY to the fixed Quest profile used by the bundled renderer: Morton-ordered chunks, Norm16 positions, Norm11 scales, Norm8 RGBA, and SH0 only.

From the repository root:

```powershell
python Unity/MapEditor/Tools/QSF2/convert_static_ply_to_qsf2.py `
  "path/to/point_cloud.ply" `
  Unity/MapEditor/Assets/ProtoVR/Scans/proto-august-scan-464k.qsf
```

The converter refuses to replace an existing output unless `--force` is passed. It writes a neighboring `.conversion.json` file with input/output hashes, compression ratio, splat count, and measured quantization errors.

The Unity importer accepts QSF2 version 2 only and verifies the fixed profile and every payload length before creating a `StaticQsfAsset`. Scene loading is completely offline; no source PLY or Pascal server is needed in the player build.
