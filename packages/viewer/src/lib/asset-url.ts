import { loadAssetUrl } from '@pascal-app/core'

export const ASSETS_CDN_URL =
  process.env.NEXT_PUBLIC_ASSETS_CDN_URL ||
  (typeof window === 'undefined' ? 'http://localhost' : window.location.origin)

const BUNDLED_CATALOG_PREFIX =
  'https://byrpxoiotywskoojsrzd.supabase.co/storage/v1/object/public/items/'

/** Converts a legacy built-in catalog URL to its committed, same-origin copy. */
export function localizeBundledCatalogAssetUrl(url: string | undefined | null): string | null {
  if (!url?.startsWith(BUNDLED_CATALOG_PREFIX)) return null
  const match = url.match(/\/items\/(?:system|users\/[^/]+)\/([^/]+)\//)
  if (!match) return null
  const id = match[1]
  if (url.endsWith('.glb')) return `/items/${id}/model.glb`
  if (url.endsWith('thumbnail.png')) return `/items/${id}/thumbnail.webp`
  if (url.endsWith('floor-plan.png')) return `/items/${id}/floor-plan.png`
  return null
}

/**
 * Resolves an asset URL to the appropriate format:
 * - If URL starts with http:// or https://, return as-is (external URL)
 * - If URL starts with asset://, resolve from IndexedDB storage
 * - If URL starts with /, prepend CDN URL (absolute path)
 * - Otherwise, prepend CDN URL (relative path)
 */
export async function resolveAssetUrl(url: string | undefined | null): Promise<string | null> {
  if (!url) return null

  const bundled = localizeBundledCatalogAssetUrl(url)
  if (bundled) return bundled

  // External URL - use as-is
  if (url.startsWith('http://') || url.startsWith('https://')) {
    return url
  }

  // IndexedDB asset - resolve from storage
  if (url.startsWith('asset://')) {
    return loadAssetUrl(url)
  }

  return url.startsWith('/') ? url : `/${url}`
}

/**
 * Synchronous version for URLs that don't need IndexedDB resolution
 * Only use this if you're sure the URL is not an asset:// URL
 */
export function resolveCdnUrl(url: string | undefined | null): string | null {
  if (!url) return null

  const bundled = localizeBundledCatalogAssetUrl(url)
  if (bundled) return bundled

  // External URL - use as-is
  if (url.startsWith('http://') || url.startsWith('https://')) {
    return url
  }

  // Don't use this for asset:// URLs - use resolveAssetUrl instead
  if (url.startsWith('asset://')) {
    console.warn('Use resolveAssetUrl() for asset:// URLs, not resolveCdnUrl()')
    return null
  }

  return url.startsWith('/') ? url : `/${url}`
}
