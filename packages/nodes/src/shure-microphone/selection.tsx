'use client'

import {
  type AnyNodeId,
  type Cursor,
  sceneRegistry,
  useScene,
} from '@pascal-app/core'
import { swallowNextClick } from '@pascal-app/editor'
import { useViewer } from '@pascal-app/viewer'
import { createPortal, type ThreeEvent, useThree } from '@react-three/fiber'
import { useEffect, useState } from 'react'
import { type Object3D, OrthographicCamera, Plane, Raycaster, Vector2, Vector3 } from 'three'
import { MoveChevron } from '../shared/selection-handles'

type Point = [number, number, number]
type SelectableGuide = {
  id: AnyNodeId
  position: Point
  rotation?: Point
  type: 'shure-microphone' | 'shure-microphone-target'
}

const ShureMicrophoneSelection = () => {
  const selectedIds = useViewer((state) => state.selection.selectedIds)
  const node = useScene((state) => {
    if (selectedIds.length !== 1) return null
    const selected = state.nodes[selectedIds[0] as AnyNodeId]
    if (selected?.type !== 'shure-microphone' && selected?.type !== 'shure-microphone-target') {
      return null
    }
    return selected as SelectableGuide
  })
  const [target, setTarget] = useState<Object3D | null>(null)

  useEffect(() => {
    if (!node) {
      setTarget(null)
      return
    }
    let frame = 0
    const resolve = () => {
      const next = sceneRegistry.nodes.get(node.id) ?? null
      setTarget((current) => (current === next ? current : next))
      if (!next) frame = window.requestAnimationFrame(resolve)
    }
    resolve()
    return () => window.cancelAnimationFrame(frame)
  }, [node])

  if (!node || !target) return null
  return createPortal(<VerticalMoveHandles node={node} />, target.parent ?? target)
}

function VerticalMoveHandles({ node }: { node: SelectableGuide }) {
  const { camera, gl, events, setEvents } = useThree()
  const [dragging, setDragging] = useState(false)
  const zoom = camera instanceof OrthographicCamera ? 1 / camera.zoom : 1
  const gap = zoom * 0.4

  const startDrag = (cursor: Cursor, event: ThreeEvent<PointerEvent>) => {
    event.stopPropagation()
    const initialPosition: Point = [...node.position]
    const raycaster = new Raycaster()
    const pointer = new Vector2()
    const origin = new Vector3(...initialPosition)
    const cameraForward = camera.getWorldDirection(new Vector3())
    cameraForward.y = 0
    if (cameraForward.lengthSq() < 1e-6) cameraForward.set(0, 0, 1)
    const dragPlane = new Plane().setFromNormalAndCoplanarPoint(cameraForward.normalize(), origin)
    const hit = new Vector3()
    const sampleY = (pointerEvent: PointerEvent) => {
      const rect = gl.domElement.getBoundingClientRect()
      pointer.set(
        ((pointerEvent.clientX - rect.left) / rect.width) * 2 - 1,
        -((pointerEvent.clientY - rect.top) / rect.height) * 2 + 1,
      )
      raycaster.setFromCamera(pointer, camera)
      return raycaster.ray.intersectPlane(dragPlane, hit) ? hit.y : null
    }
    const startY = sampleY(event.nativeEvent)
    if (startY === null) return

    useScene.temporal.getState().pause()
    useViewer.getState().setInputDragging(true)
    const eventsEnabledBeforeDrag = events.enabled
    // The drag samples its own plane from native pointer events. Keeping R3F
    // events enabled would still raycast the dense splat on every move.
    setEvents({ enabled: false })
    setDragging(true)
    document.body.style.cursor = cursor
    let nextPosition: Point | null = null
    const onMove = (pointerEvent: PointerEvent) => {
      const currentY = sampleY(pointerEvent)
      if (currentY === null) return
      const y = initialPosition[1] + currentY - startY
      if (nextPosition?.[1] === y) return
      nextPosition = [initialPosition[0], y, initialPosition[2]]
      // This is a drag-local presentation transform. Publishing it through
      // Zustand makes R3F reconcile the full scene every frame, including a
      // dense splat; the Shure system reads this group pose for the guide.
      sceneRegistry.nodes.get(node.id)?.position.fromArray(nextPosition)
    }
    const cleanup = () => {
      window.removeEventListener('pointermove', onMove)
      window.removeEventListener('pointerup', onUp)
      window.removeEventListener('pointercancel', onUp)
      setEvents({ enabled: eventsEnabledBeforeDrag })
      useViewer.getState().setInputDragging(false)
      setDragging(false)
      if (document.body.style.cursor === cursor) document.body.style.cursor = ''
    }
    const onUp = () => {
      swallowNextClick()
      cleanup()
      useScene.temporal.getState().resume()
      if (nextPosition) useScene.getState().updateNode(node.id, { position: nextPosition })
      useScene.temporal.getState().pause()
    }

    window.addEventListener('pointermove', onMove)
    window.addEventListener('pointerup', onUp)
    window.addEventListener('pointercancel', onUp)
  }

  if (dragging) return null
  const [x, y, z] = node.position
  return (
    <group>
      <MoveChevron
        cursor="ns-resize"
        onPointerDown={(event) => startDrag('ns-resize', event)}
        position={[x, y + gap, z]}
        vertical="up"
      />
      <MoveChevron
        cursor="ns-resize"
        onPointerDown={(event) => startDrag('ns-resize', event)}
        position={[x, y - gap, z]}
        vertical="down"
      />
    </group>
  )
}

export default ShureMicrophoneSelection
