import type { NodeDefinition } from '@pascal-app/core'
import { buildShureMicrophoneFloorplan, buildShureMicrophoneTargetFloorplan } from './floorplan'
import { buildShureMicrophoneGeometry, buildShureMicrophoneTargetGeometry } from './geometry'
import { ShureMicrophoneConeCountEditor } from './inspector-editors'
import { ShureMicrophoneNode, ShureMicrophoneTargetNode } from './schema'
import { getShureMicrophoneTargetIds } from './target-ids'

export const shureMicrophoneDefinition: NodeDefinition<typeof ShureMicrophoneNode> = {
  kind: 'shure-microphone',
  schemaVersion: 2,
  schema: ShureMicrophoneNode,
  category: 'furnish',
  snapProfile: 'item',
  defaults: () => ({
    object: 'node',
    parentId: null,
    visible: true,
    metadata: {},
    position: [0, 0, 0],
    rotation: [0, 0, 0],
    targetIds: [],
  }),
  capabilities: {
    selectable: { hitVolume: 'bbox' },
    movable: { axes: ['x', 'y', 'z'], gridSnap: true },
    rotatable: { axes: ['x', 'y', 'z'] },
    duplicable: true,
    deletable: true,
  },
  geometry: buildShureMicrophoneGeometry,
  floorplan: buildShureMicrophoneFloorplan,
  geometryKey: (n) => JSON.stringify([n.position, n.rotation, n.targetId, n.targetIds]),
  system: { module: () => import('./system') },
  affordanceTools: { selection: () => import('./selection') },
  parametrics: {
    groups: [
      {
        label: 'Direction guides',
        fields: [{ key: 'coneCount', kind: 'custom', component: ShureMicrophoneConeCountEditor }],
      },
    ],
    onDeleteCascade: getShureMicrophoneTargetIds,
  },
  tool: () => import('./tool'),
  presentation: {
    label: 'Shure Microphone',
    description: 'A visual microphone direction guide.',
    icon: { kind: 'url', src: '/icons/couch.webp' },
    paletteSection: 'furnish',
    paletteOrder: 35,
  },
}

export const shureMicrophoneTargetDefinition: NodeDefinition<typeof ShureMicrophoneTargetNode> = {
  kind: 'shure-microphone-target',
  schemaVersion: 2,
  schema: ShureMicrophoneTargetNode,
  category: 'furnish',
  snapProfile: 'item',
  defaults: () => ({
    object: 'node',
    parentId: null,
    visible: true,
    metadata: {},
    position: [0, 0, 2],
  }),
  capabilities: {
    selectable: { hitVolume: 'bbox' },
    movable: { axes: ['x', 'y', 'z'], gridSnap: true },
    deletable: false,
  },
  geometry: buildShureMicrophoneTargetGeometry,
  floorplan: buildShureMicrophoneTargetFloorplan,
  geometryKey: (n) => JSON.stringify([n.position, n.microphoneId]),
  affordanceTools: { selection: () => import('./selection') },
  parametrics: { groups: [] },
  presentation: {
    label: 'Microphone target',
    description: 'Aim point for a Shure Microphone.',
    icon: { kind: 'url', src: '/icons/couch.webp' },
    paletteSection: 'furnish',
    paletteOrder: 36,
  },
}
