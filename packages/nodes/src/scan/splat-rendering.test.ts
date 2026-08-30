import { describe, expect, test } from 'bun:test'
import { BufferAttribute, InstancedBufferGeometry } from 'three'
import NodeMaterial from 'three/src/materials/nodes/NodeMaterial.js'
import { vec4 } from 'three/tsl'
import { applySplatColorManagement, repairSplatBillboardGeometry } from './splat-rendering'

describe('Gaussian splat rendering adapters', () => {
  test('wraps encoded colour exactly once while preserving the source RGBA node', () => {
    const material = new NodeMaterial()
    const encodedColor = vec4(0.5, 0.25, 0.75, 0.4)
    material.colorNode = encodedColor

    expect(applySplatColorManagement(material)).toBe(true)
    const converted = material.colorNode as typeof material.colorNode & {
      colorNode: unknown
      source: string
      target: string
    }
    expect(converted.colorNode).toBe(encodedColor)
    expect(converted.source).toBe('srgb')
    expect(converted.target).toBe('WorkingColorSpace')
    expect(applySplatColorManagement(material)).toBe(false)
    expect(material.colorNode).toBe(converted)
  })

  test('expands the indexed quad to six valid vertices without changing instance count', () => {
    const geometry = new InstancedBufferGeometry()
    geometry.setAttribute(
      'position',
      new BufferAttribute(new Float32Array([-2, -2, 0, 2, -2, 0, 2, 2, 0, -2, 2, 0]), 3),
    )
    geometry.setIndex([0, 1, 2, 0, 2, 3])
    geometry.instanceCount = 42

    expect(repairSplatBillboardGeometry(geometry)).toBe(true)
    expect(geometry.getIndex()).toBeNull()
    expect(geometry.getAttribute('position').count).toBe(6)
    expect(Array.from(geometry.getAttribute('position').array)).toEqual([
      -2, -2, 0, 2, -2, 0, 2, 2, 0, -2, -2, 0, 2, 2, 0, -2, 2, 0,
    ])
    expect(geometry.instanceCount).toBe(42)
    expect(repairSplatBillboardGeometry(geometry)).toBe(false)
  })
})
