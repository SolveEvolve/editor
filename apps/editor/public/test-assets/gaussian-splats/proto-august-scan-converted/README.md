# Proto August Scan — converted Gaussian splats

These assets were converted from the PortalCam Gaussian PLY export in:

`C:\Users\SolveEvolve\Downloads\Proto August Scan\Proto August Scan\ply\ply-result\point_cloud\iteration_100`

Use the files as `scan` nodes with `assetFormat: 'spz'`.

| Asset | Splats | Size | Editor URL |
| --- | ---: | ---: | --- |
| Main downsampled scan | 464,114 | 15,279,381 bytes | `/test-assets/gaussian-splats/proto-august-scan-converted/proto-august-scan-464k.spz` |
| Environment cloud | 1,741 | 70,781 bytes | `/test-assets/gaussian-splats/proto-august-scan-converted/proto-august-environment.spz` |

The main source was `point_cloud_3.ply` (115,102,298 bytes). It was selected because it is the
largest supplied Gaussian PLY below the editor's 200 MB upload limit. The higher-resolution PLYs
are 230 MB, 461 MB, and 931 MB.

Both files are SPZ v4, produced with `gsply 0.4.6`. Coordinates were preserved so the PLY and SPZ
paths have matching placement in the current renderer. Three.js `SPZLoader` validation recovered
all splats; the largest absolute difference across the main cloud's axis-aligned bounds was
0.000117 scene units.

## Checksums

```text
673DD9661845AF0CDFB6219E09ED55E24686374C896156387F976217DA6A2AE1  proto-august-scan-464k.spz
E197A5A29995ECB1C6A027AF1C2D1F816D7D3FAFE01DA5C791FBE5A823DC5705  proto-august-environment.spz
77A097844890F83780DFBB26AA247B047CDDC1B03607ADEE8120B7058E32EF01  preview.jpg
```
