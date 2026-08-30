import { describe, expect, test } from 'bun:test'
import { BufferAttribute, BufferGeometry } from 'three'
import { deserializeSplatGeometry, serializeSplatGeometry } from './spz-worker-protocol'

describe('SPZ worker geometry transport', () => {
  test('round-trips every Gaussian splat attribute without changing its representation', () => {
    const source = new BufferGeometry()
    source.setAttribute('position', new BufferAttribute(new Float32Array([1, 2, 3, 4, 5, 6]), 3))
    source.setAttribute(
      'covariance',
      new BufferAttribute(new Float32Array([1, 0, 0, 1, 0, 1, 2, 0, 0, 2, 0, 2]), 6),
    )
    source.setAttribute(
      'color',
      new BufferAttribute(new Uint8ClampedArray([10, 20, 30, 40, 50, 60, 70, 80]), 4, true),
    )
    source.setAttribute(
      'sphericalHarmonics1',
      new BufferAttribute(new Uint32Array([1, 2, 3, 4, 5, 6]), 3),
    )

    const serialized = serializeSplatGeometry(source)
    const restored = deserializeSplatGeometry(serialized.geometry)

    expect(serialized.transfer).toHaveLength(4)
    for (const name of ['position', 'covariance', 'color', 'sphericalHarmonics1']) {
      const before = source.getAttribute(name)
      const after = restored.getAttribute(name)
      expect(after.itemSize).toBe(before.itemSize)
      expect(after.normalized).toBe(before.normalized)
      expect(after.array.constructor).toBe(before.array.constructor)
      expect(Array.from(after.array)).toEqual(Array.from(before.array))
    }
  })
})
