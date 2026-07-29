import { createHash } from 'node:crypto'
import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { CATALOG_ITEMS } from '../packages/editor/src/components/ui/item-catalog/catalog-items'

type CatalogEntry = (typeof CATALOG_ITEMS)[number]

type UnityCatalogEntry = CatalogEntry & {
  localPath: string
  sha256: string
  bytes: number
}

type UnityCatalogManifest = {
  schemaVersion: 1
  source: 'pascal-editor-catalog'
  sourceSha256: string
  generatedAt: string
  entries: UnityCatalogEntry[]
}

const workspaceRoot = resolve(import.meta.dir, '..')
const unityLibraryRoot = resolve(
  workspaceRoot,
  'Unity/MapEditor/Assets/PascalScene/Library',
)
const modelsRoot = resolve(unityLibraryRoot, 'Models')
const manifestPath = resolve(unityLibraryRoot, 'pascal-catalog.json')
const reportPath = resolve(unityLibraryRoot, 'pascal-catalog-report.json')
const sourceCatalogPath = resolve(
  workspaceRoot,
  'packages/editor/src/components/ui/item-catalog/catalog-items.tsx',
)
const checkOnly = process.argv.includes('--check')

function sha256(bytes: Uint8Array): string {
  return createHash('sha256').update(bytes).digest('hex')
}

function localPathFor(assetId: string): string {
  return `Assets/PascalScene/Library/Models/${assetId}.glb`
}

function absolutePathFor(assetId: string): string {
  return resolve(modelsRoot, `${assetId}.glb`)
}

function isGlb(bytes: Uint8Array): boolean {
  return (
    bytes.length >= 12 &&
    bytes[0] === 0x67 &&
    bytes[1] === 0x6c &&
    bytes[2] === 0x54 &&
    bytes[3] === 0x46
  )
}

async function download(sourceUrl: string): Promise<Uint8Array> {
  const response = await fetch(sourceUrl)
  if (!response.ok) {
    throw new Error(`Download failed (${response.status}) for ${sourceUrl}`)
  }

  return new Uint8Array(await response.arrayBuffer())
}

async function readModel(assetId: string): Promise<Uint8Array> {
  return new Uint8Array(await readFile(absolutePathFor(assetId)))
}

async function sync(): Promise<UnityCatalogManifest> {
  await mkdir(modelsRoot, { recursive: true })
  const downloadedByUrl = new Map<string, Uint8Array>()
  const entries: UnityCatalogEntry[] = []

  for (const item of CATALOG_ITEMS) {
    let bytes = downloadedByUrl.get(item.src)
    if (!bytes) {
      bytes = await download(item.src)
      if (!isGlb(bytes)) {
        throw new Error(`Catalog model '${item.id}' is not a binary GLB.`)
      }

      downloadedByUrl.set(item.src, bytes)
    }

    await writeFile(absolutePathFor(item.id), bytes)
    entries.push({
      ...item,
      localPath: localPathFor(item.id),
      sha256: sha256(bytes),
      bytes: bytes.length,
    })
  }

  const sourceSha256 = sha256(await readFile(sourceCatalogPath))
  const manifest: UnityCatalogManifest = {
    schemaVersion: 1,
    source: 'pascal-editor-catalog',
    sourceSha256,
    generatedAt: new Date().toISOString(),
    entries,
  }
  await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`)
  await writeFile(
    reportPath,
    `${JSON.stringify(
      {
        catalogEntryCount: entries.length,
        uniqueSourceModelCount: downloadedByUrl.size,
        totalBytes: entries.reduce((sum, entry) => sum + entry.bytes, 0),
        duplicateSourceModelIds: entries
          .filter((entry, index) => entries.findIndex((other) => other.src === entry.src) !== index)
          .map((entry) => entry.id),
      },
      null,
      2,
    )}\n`,
  )
  return manifest
}

async function verify(): Promise<void> {
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8')) as UnityCatalogManifest
  if (manifest.schemaVersion !== 1 || manifest.entries.length !== CATALOG_ITEMS.length) {
    throw new Error('Catalog manifest is missing entries or has an unsupported schema version.')
  }

  for (const entry of manifest.entries) {
    const bytes = await readModel(entry.id)
    if (!isGlb(bytes) || sha256(bytes) !== entry.sha256 || bytes.length !== entry.bytes) {
      throw new Error(`Catalog model '${entry.id}' does not match the static manifest.`)
    }
  }
}

if (checkOnly) {
  await verify()
  console.log(`Verified ${CATALOG_ITEMS.length} local Pascal catalog entries.`)
} else {
  const manifest = await sync()
  console.log(
    `Synced ${manifest.entries.length} Pascal catalog entries to ${unityLibraryRoot}.`,
  )
}
