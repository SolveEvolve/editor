// @ts-expect-error — bun:test is provided by the Bun runtime.
import { describe, expect, test } from 'bun:test'
import { localizeBundledCatalogAssetUrl, resolveCdnUrl } from './asset-url'

const catalogUrl =
  'https://byrpxoiotywskoojsrzd.supabase.co/storage/v1/object/public/items/system/cactus/model.glb'

describe('bundled asset URLs', () => {
  test('maps legacy catalog URLs to their same-origin copies', () => {
    expect(localizeBundledCatalogAssetUrl(catalogUrl)).toBe('/items/cactus/model.glb')
    expect(resolveCdnUrl(catalogUrl)).toBe('/items/cactus/model.glb')
  })

  test('keeps root-relative public assets on the active deployment', () => {
    expect(resolveCdnUrl('/assets/production/hard-light.glb')).toBe(
      '/assets/production/hard-light.glb',
    )
  })
})
