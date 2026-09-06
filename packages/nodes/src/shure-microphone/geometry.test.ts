import { describe, expect, test } from 'bun:test'
import type { AnyNodeId, GeometryContext } from '@pascal-app/core'
import { FrontSide, Mesh, MeshBasicMaterial, SpotLight } from 'three'
import { buildShureMicrophoneGeometry } from './geometry'
import { ShureMicrophoneNode, ShureMicrophoneTargetNode } from './schema'

describe('buildShureMicrophoneGeometry', () => {
  test('builds a depth-tested front-sided cone without a spotlight', () => {
    const target = ShureMicrophoneTargetNode.parse({
      id: 'shure-microphone-target_demo',
      position: [0, 0, 2],
    })
    const microphone = ShureMicrophoneNode.parse({
      id: 'shure-microphone_demo',
      targetId: target.id,
    })
    const ctx: GeometryContext = {
      resolve: <N,>(id: AnyNodeId) => (id === target.id ? (target as N) : undefined),
      children: [],
      siblings: [],
      parent: null,
    }

    const group = buildShureMicrophoneGeometry(microphone, ctx)
    const cone = group.getObjectByName(`direction-cone:${target.id}`) as Mesh
    const material = cone.material as MeshBasicMaterial
    let spotLightCount = 0
    group.traverse((child) => {
      if (child instanceof SpotLight) spotLightCount += 1
    })

    expect(material.opacity).toBe(0.1)
    expect(material.depthTest).toBe(true)
    expect(material.depthWrite).toBe(false)
    expect(material.side).toBe(FrontSide)
    expect(spotLightCount).toBe(0)
  })
})
