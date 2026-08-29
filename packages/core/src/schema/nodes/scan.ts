import { z } from 'zod'
import { AssetUrl } from '../asset-url'
import { BaseNode, nodeType, objectId } from '../base'

export const SCAN_ASSET_FORMATS = ['mesh', 'spz', 'splat', 'ksplat', 'ply'] as const
export const ScanAssetFormat = z.enum(SCAN_ASSET_FORMATS)
export type ScanAssetFormat = z.infer<typeof ScanAssetFormat>

const EXTENSION_FORMATS: Readonly<Record<string, ScanAssetFormat>> = {
  glb: 'mesh',
  gltf: 'mesh',
  ksplat: 'ksplat',
  ply: 'ply',
  splat: 'splat',
  spz: 'spz',
}

export function detectScanAssetFormat(fileNameOrUrl: string): ScanAssetFormat | null {
  const path = fileNameOrUrl.trim().split(/[?#]/, 1)[0]?.toLowerCase()
  const extension = path?.match(/\.([a-z0-9]+)$/)?.[1]
  return extension ? (EXTENSION_FORMATS[extension] ?? null) : null
}

export const ScanNode = BaseNode.extend({
  id: objectId('scan'),
  type: nodeType('scan'),
  url: AssetUrl,
  assetFormat: ScanAssetFormat.default('mesh'),
  position: z.tuple([z.number(), z.number(), z.number()]).default([0, 0, 0]),
  rotation: z.tuple([z.number(), z.number(), z.number()]).default([0, 0, 0]),
  scale: z.number().default(1),
  opacity: z.number().min(0).max(100).default(100),
})

export type ScanNode = z.infer<typeof ScanNode>
