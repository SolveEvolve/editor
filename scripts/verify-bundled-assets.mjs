import { createHash } from 'node:crypto'
import { readFile, stat } from 'node:fs/promises'
import { resolve } from 'node:path'

const root = resolve(import.meta.dirname, '..')
const publicRoot = resolve(root, 'apps/editor/public')
const manifest = JSON.parse(await readFile(resolve(publicRoot, 'items/manifest.json'), 'utf8'))

for (const asset of manifest.assets) {
  const path = resolve(publicRoot, `.${asset.localPath}`)
  const file = await readFile(path)
  const metadata = await stat(path)
  const checksum = createHash('sha256').update(file).digest('hex')
  if (metadata.size !== asset.bytes || checksum !== asset.sha256) {
    throw new Error(`Asset integrity mismatch: ${asset.localPath}`)
  }
  if (path.endsWith('.glb') && file.subarray(0, 4).toString() !== 'glTF') {
    throw new Error(`Invalid GLB: ${asset.localPath}`)
  }
  if (path.endsWith('.png') && !file.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))) {
    throw new Error(`Invalid PNG: ${asset.localPath}`)
  }
  if (path.endsWith('.webp') && (file.subarray(0, 4).toString() !== 'RIFF' || file.subarray(8, 12).toString() !== 'WEBP')) {
    throw new Error(`Invalid WebP: ${asset.localPath}`)
  }
}

console.log(`Verified ${manifest.assets.length} bundled catalog assets.`)
