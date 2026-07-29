import type { Collection, CollectionId } from './schema/collections'
import type { SceneMaterial, SceneMaterialId } from './schema/scene-material'
import type { AnyNode, AnyNodeId } from './schema/types'

export const PASCAL_BUILD_SCHEMA_VERSION = 1 as const
export const PASCAL_CATALOG_VERSION = 'catalog-2026-07-29'
export const PASCAL_MATERIAL_LIBRARY_VERSION = 'materials-2026-07-29'

export type PascalBuildDocument = {
  schemaVersion: typeof PASCAL_BUILD_SCHEMA_VERSION
  catalogVersion: string
  materialLibraryVersion: string
  nodes: Record<AnyNodeId, AnyNode>
  rootNodeIds: AnyNodeId[]
  collections: Record<CollectionId, Collection>
  materials: Record<SceneMaterialId, SceneMaterial>
  installedPlugins: string[]
}

export function createPascalBuildDocument(input: {
  nodes: Record<AnyNodeId, AnyNode>
  rootNodeIds: AnyNodeId[]
  collections: Record<CollectionId, Collection>
  materials: Record<SceneMaterialId, SceneMaterial>
  installedPlugins: string[]
}): PascalBuildDocument {
  return {
    schemaVersion: PASCAL_BUILD_SCHEMA_VERSION,
    catalogVersion: PASCAL_CATALOG_VERSION,
    materialLibraryVersion: PASCAL_MATERIAL_LIBRARY_VERSION,
    nodes: input.nodes,
    rootNodeIds: input.rootNodeIds,
    collections: input.collections,
    materials: input.materials,
    installedPlugins: input.installedPlugins,
  }
}
