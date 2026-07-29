import { describe, expect, test } from 'bun:test'
import {
  createPascalBuildDocument,
  PASCAL_BUILD_SCHEMA_VERSION,
  PASCAL_CATALOG_VERSION,
  PASCAL_MATERIAL_LIBRARY_VERSION,
} from './build-document'

describe('createPascalBuildDocument', () => {
  test('creates a complete versioned document', () => {
    const document = createPascalBuildDocument({
      nodes: {} as never,
      rootNodeIds: [] as never,
      collections: {} as never,
      materials: {} as never,
      installedPlugins: [],
    })

    expect(document.schemaVersion).toBe(PASCAL_BUILD_SCHEMA_VERSION)
    expect(document.catalogVersion).toBe(PASCAL_CATALOG_VERSION)
    expect(document.materialLibraryVersion).toBe(PASCAL_MATERIAL_LIBRARY_VERSION)
    expect(document.collections).toEqual({})
    expect(document.materials).toEqual({})
  })
})
