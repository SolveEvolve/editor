# ITV Scan — converted Gaussian splats

Converted using the same workflow as the Proto August scan: PortalCam Gaussian PLY to SPZ v4 with gsply 0.4.6 and default conversion settings, preserving coordinates.

Source directory: `C:\Users\SolveEvolve\Downloads\ITV\ITV\ply\ply-result\point_cloud\iteration_100`.

| Source | Output | Splats | Bytes |
| --- | --- | ---: | ---: |
| point_cloud_3.ply | itv-scan.spz | 621,496 | 21,614,596 |
| environment.ply | itv-environment.spz | 2,714 | 116,575 |

As with Proto, point_cloud_3.ply is the largest supplied Gaussian PLY below 200 MB (154,133,031 bytes). No additional downsampling or transform was applied.

Use as scan nodes with `assetFormat: 'spz'`. Asset URLs and SHA-256 checksums are in manifest.json. The environment is a separate optional scan; use matching transforms to keep it aligned with the main scan.

Validation with the editor's Three.js GaussianSplatPLYLoader and SPZLoader recovered every splat and all three spherical-harmonic bands. Every decoded position was finite and matched its source within 0.0001220703125 scene units. Details are in validation.json. Visual scene placement has not been configured.

To reproduce with gsply[spz]==0.4.6:

```python
from gsply import plyread, write_spz
write_spz('itv-scan.spz', plyread('point_cloud_3.ply'), version=4)
write_spz('itv-environment.spz', plyread('environment.ply'), version=4)
```
