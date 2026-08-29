# @pascal-app/nodes

Built-in node definitions for the Pascal viewer and editor.

## Installation

```bash
npm install @pascal-app/core @pascal-app/viewer @pascal-app/editor @pascal-app/nodes
```

The package declares the remaining React, Next.js, Three.js, and UI libraries it needs as peer
dependencies. Install any peers reported by your package manager.

### Gaussian splat dependency pin

Gaussian scan references use Three.js's native WebGPU `GaussianSplat` implementation. Until
Three.js r186 is published, applications consuming this package must resolve `three` to commit
`444f238c63b594fbaf1d5adde301fa7e10c29a83`, matching this workspace's root override. The scan
path supports SPZ, SPLAT, KSPLAT, and Gaussian PLY assets; SPZ is recommended for larger captures.

Upload hosts should preserve the original filename long enough to set the node format:

```typescript
import { detectScanAssetFormat } from '@pascal-app/core'

const assetFormat = detectScanAssetFormat(file.name) ?? 'mesh'
```

After stable r186 and matching `@types/three` declarations are available, replace the commit
override with the stable release and remove `src/three-gaussian-splat.d.ts`.

## Usage

Load `builtinPlugin` once before mounting a Pascal viewer or editor:

```typescript
import { loadPlugin } from '@pascal-app/core'
import { builtinPlugin } from '@pascal-app/nodes'

await loadPlugin(builtinPlugin)
```

The plugin registers the built-in schemas, renderers, geometry builders, tools, and systems. Hosts
can load additional plugins through the same `loadPlugin` API.

See the
[`@pascal-app/viewer` quick start](https://github.com/pascalorg/editor/tree/main/packages/viewer#usage)
for bootstrap ordering in a React application.

## License

MIT
