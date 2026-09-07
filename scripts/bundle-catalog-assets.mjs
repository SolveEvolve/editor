import { createHash } from 'node:crypto'
import { mkdir, readFile, stat, writeFile } from 'node:fs/promises'
import { existsSync } from 'node:fs'
import { basename, dirname, resolve } from 'node:path'

const root = resolve(import.meta.dirname, '..')
const catalogPath = resolve(root, 'packages/editor/src/components/ui/item-catalog/catalog-items.tsx')
const publicItems = resolve(root, 'apps/editor/public/items')
const manifestPath = resolve(publicItems, 'manifest.json')
const source = await readFile(catalogPath, 'utf8')
const urls = [...source.matchAll(/https:\/\/byrpxoiotywskoojsrzd\.supabase\.co\/storage\/v1\/object\/public\/items\/(?:system|users\/[^/]+)\/[^']+/g)].map(
  (match) => match[0],
)

function targetFor(url) {
  const match = url.match(/\/items\/(?:system|users\/[^/]+)\/([^/]+)\//)
  if (!match) throw new Error(`Cannot derive catalog item id from ${url}`)
  const id = match[1]
  if (url.endsWith('.glb')) return { id, path: resolve(publicItems, id, 'model.glb') }
  if (url.endsWith('thumbnail.png')) return { id, path: resolve(publicItems, id, 'thumbnail.png') }
  if (url.endsWith('floor-plan.png')) return { id, path: resolve(publicItems, id, 'floor-plan.png') }
  throw new Error(`Unsupported catalog asset ${url}`)
}

const entries = []
for (const url of urls) {
  const target = targetFor(url)
  const preferred = target.path.endsWith('thumbnail.png')
    ? target.path.replace(/\.png$/, '.webp')
    : target.path
  if (!existsSync(preferred)) {
    await mkdir(dirname(target.path), { recursive: true })
    const response = await fetch(url)
    if (!response.ok) throw new Error(`${response.status} while downloading ${url}`)
    const buffer = Buffer.from(await response.arrayBuffer())
    if (target.path.endsWith('.glb') && buffer.subarray(0, 4).toString() !== 'glTF') {
      throw new Error(`Invalid GLB response for ${url}`)
    }
    if (target.path.endsWith('.png') && !buffer.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))) {
      throw new Error(`Invalid PNG response for ${url}`)
    }
    await writeFile(target.path, buffer)
  }
  const localPath = existsSync(preferred) ? preferred : target.path
  const buffer = await readFile(localPath)
  entries.push({
    source: url,
    localPath: `/${localPath.slice(resolve(root, 'apps/editor/public').length + 1).replaceAll('\\', '/')}`,
    bytes: buffer.byteLength,
    sha256: createHash('sha256').update(buffer).digest('hex'),
    attribution: null,
    license: null,
  })
}

for (const runtimeAsset of [
  {
    source: 'https://www.gstatic.com/draco/versioned/decoders/1.5.5/draco_decoder.js',
    localPath: '/decoders/draco/draco_decoder.js',
  },
  {
    source: 'https://www.gstatic.com/draco/versioned/decoders/1.5.5/draco_decoder.wasm',
    localPath: '/decoders/draco/draco_decoder.wasm',
  },
  {
    source: 'https://www.gstatic.com/draco/versioned/decoders/1.5.5/draco_wasm_wrapper.js',
    localPath: '/decoders/draco/draco_wasm_wrapper.js',
  },
  {
    source: 'https://cdn.jsdelivr.net/gh/pmndrs/drei-assets@master/basis/basis_transcoder.js',
    localPath: '/decoders/basis/basis_transcoder.js',
  },
  {
    source: 'https://cdn.jsdelivr.net/gh/pmndrs/drei-assets@master/basis/basis_transcoder.wasm',
    localPath: '/decoders/basis/basis_transcoder.wasm',
  },
]) {
  const localPath = resolve(root, 'apps/editor/public', `.${runtimeAsset.localPath}`)
  const buffer = await readFile(localPath)
  entries.push({
    ...runtimeAsset,
    bytes: buffer.byteLength,
    sha256: createHash('sha256').update(buffer).digest('hex'),
    attribution: 'three.js examples',
    license: 'MIT',
  })
}

await writeFile(manifestPath, `${JSON.stringify({ version: 1, assets: entries }, null, 2)}\n`)
console.log(`Verified ${entries.length} catalog assets; manifest: ${basename(manifestPath)}`)
