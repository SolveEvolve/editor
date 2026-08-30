'use client'

import {
  emitter,
  type GridEvent,
  ShureMicrophoneNode,
  ShureMicrophoneTargetNode,
  useScene,
} from '@pascal-app/core'
import { isGridSnapActive, useEditor } from '@pascal-app/editor'
import { useViewer } from '@pascal-app/viewer'
import { useEffect, useRef } from 'react'
import {
  type FloorPlacementClickTriggerEvent,
  getLevelLocalSnappedPosition,
  stopPlacementCommitPropagation,
  subscribeFloorPlacementClicks,
} from '../shared/floor-placement'

const ShureMicrophoneTool = () => {
  const activeLevelId = useViewer((state) => state.selection.levelId)
  const lastPosition = useRef<[number, number, number] | null>(null)

  useEffect(() => {
    if (!activeLevelId) return
    const onMove = (event: GridEvent) => {
      const step = useEditor.getState().gridSnapStep
      const snap = isGridSnapActive()
      lastPosition.current = [
        snap ? Math.round(event.localPosition[0] / step) * step : event.localPosition[0],
        event.localPosition[1],
        snap ? Math.round(event.localPosition[2] / step) * step : event.localPosition[2],
      ]
    }
    const onCommit = (event: FloorPlacementClickTriggerEvent) => {
      const position =
        lastPosition.current ??
        getLevelLocalSnappedPosition(
          activeLevelId,
          event,
          useEditor.getState().gridSnapStep,
          !isGridSnapActive(),
        )
      const microphone = ShureMicrophoneNode.parse({
        name: 'Shure Microphone',
        parentId: activeLevelId,
        position: [position[0], position[1] + 2, position[2]],
        rotation: [0, 0, 0],
      })
      const target = ShureMicrophoneTargetNode.parse({
        name: 'Microphone target',
        parentId: activeLevelId,
        microphoneId: microphone.id,
        position: [position[0], position[1] + 2, position[2] + 2],
      })
      const linkedMicrophone = ShureMicrophoneNode.parse({
        ...microphone,
        targetId: target.id,
        targetIds: [],
      })
      useScene.getState().createNodes([
        { node: linkedMicrophone, parentId: activeLevelId },
        { node: target, parentId: activeLevelId },
      ])
      useViewer.getState().setSelection({ selectedIds: [linkedMicrophone.id] })
      useEditor.getState().setTool(null)
      stopPlacementCommitPropagation(event)
    }
    emitter.on('grid:move', onMove)
    const unsubscribe = subscribeFloorPlacementClicks(onCommit)
    return () => {
      emitter.off('grid:move', onMove)
      unsubscribe()
    }
  }, [activeLevelId])
  return null
}

export default ShureMicrophoneTool
