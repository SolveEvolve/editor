// @ts-expect-error — bun:test is provided by the Bun runtime; viewer does not
// depend on @types/bun so the import type is unresolved at compile time.
import { describe, expect, test } from 'bun:test'
import { BoxGeometry, Mesh, MeshBasicMaterial, Object3D, PerspectiveCamera, Scene } from 'three'
import { mergedOutline } from './merged-outline-node'

describe('merged outline rendering', () => {
  for (const group of ['primaryObjects', 'secondaryObjects'] as const) {
    test(`${group} depth pass skips non-depth-writing geometry but retains solid occluders`, () => {
      const geometry = new BoxGeometry()
      const solidMaterial = new MeshBasicMaterial()
      const transparentMaterial = new MeshBasicMaterial({ transparent: true, depthWrite: false })
      const selected = new Mesh(geometry, solidMaterial)
      const solid = new Mesh(geometry, solidMaterial)
      const transparent = new Mesh(geometry, transparentMaterial)
      const scene = new Scene()
      scene.add(selected, solid, transparent)
      const outline = mergedOutline(scene, new PerspectiveCamera(), { [group]: [selected] })
      const drawn: Object3D[] = []
      type RenderObject = (
        object: Mesh,
        scene: Scene,
        camera: PerspectiveCamera,
        geometry: BoxGeometry,
        material: MeshBasicMaterial,
      ) => void
      let renderObject: RenderObject | null = null
      const depthComplete = new Error('depth pass complete')
      const renderer = {
        getRenderTarget: () => null,
        getActiveCubeFace: () => 0,
        getActiveMipmapLevel: () => 0,
        getRenderObjectFunction: () => null,
        getPixelRatio: () => 1,
        getMRT: () => null,
        getClearColor: (value: unknown) => value,
        getClearAlpha: () => 0,
        getScissorTest: () => false,
        setMRT: () => {},
        setClearColor: () => {},
        setRenderTarget: () => {},
        setRenderObjectFunction: (callback: RenderObject | null) => {
          renderObject = callback
        },
        getDrawingBufferSize: () => ({ width: 64, height: 64 }),
        renderObject: (object: Object3D) => {
          drawn.push(object)
        },
        render: () => {
          for (const mesh of [selected, solid, transparent]) {
            renderObject?.(mesh, scene, outline.camera, geometry, mesh.material)
          }
          throw depthComplete
        },
      }

      try {
        expect(() => outline.updateBefore({ renderer })).toThrow(depthComplete.message)
        expect(drawn).toEqual([solid])
        expect(transparent.material.depthWrite).toBe(false)
      } finally {
        outline.dispose()
        geometry.dispose()
        solidMaterial.dispose()
        transparentMaterial.dispose()
      }
    })
  }

  test('keeps selected outlines active during camera interaction', () => {
    const cameraInteractionActive = true
    const params = {
      enabled: () => !cameraInteractionActive,
      primaryObjects: [new Object3D()],
    }
    const outline = mergedOutline(new Scene(), new PerspectiveCamera(), params)
    const frame = {
      get renderer(): never {
        throw new Error('outline renderer was reached')
      },
    }

    expect(() => outline.updateBefore(frame)).toThrow('outline renderer was reached')
    outline.dispose()
  })
})
