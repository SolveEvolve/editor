import { describe, expect, test } from 'bun:test'
import { detectScanAssetFormat, SCAN_ASSET_FORMATS, ScanNode } from './scan'

describe('ScanNode', () => {
  test('defaults legacy scans to mesh assets', () => {
    const scan = ScanNode.parse({ url: '/scans/site.glb' })
    expect(scan.assetFormat).toBe('mesh')
  })

  test.each(SCAN_ASSET_FORMATS)('accepts the %s asset format', (assetFormat) => {
    expect(ScanNode.parse({ url: `/scan.${assetFormat}`, assetFormat }).assetFormat).toBe(
      assetFormat,
    )
  })

  test('rejects unsupported asset formats', () => {
    expect(ScanNode.safeParse({ url: '/scan.xyz', assetFormat: 'xyz' }).success).toBe(false)
  })
})

describe('detectScanAssetFormat', () => {
  test.each([
    ['room.glb', 'mesh'],
    ['room.GLTF', 'mesh'],
    ['capture.SPZ', 'spz'],
    ['capture.splat?download=1', 'splat'],
    ['https://cdn.example.com/capture.ksplat#asset', 'ksplat'],
    ['/project/capture.PLY?version=2#view', 'ply'],
  ] as const)('detects %s as %s', (input, expected) => {
    expect(detectScanAssetFormat(input)).toBe(expected)
  })

  test.each([
    'asset://opaque-id',
    'scan.xyz',
    '',
    'https://example.com/no-extension',
  ])('returns null for %s', (input) => {
    expect(detectScanAssetFormat(input)).toBeNull()
  })
})
