'use client'

import {
  type AnyNodeId,
  type ShureMicrophoneNode,
  ShureMicrophoneTargetNode,
  useScene,
} from '@pascal-app/core'
import { SliderControl } from '@pascal-app/editor'
import { useState } from 'react'
import { getShureMicrophoneTargetIds } from './target-ids'

const MAX_CONES = 8

function targetPosition(node: ShureMicrophoneNode, index: number, count: number) {
  const angle = (index - (count - 1) / 2) * 0.25
  return [
    node.position[0] + Math.sin(angle) * 2,
    node.position[1],
    node.position[2] + Math.cos(angle) * 2,
  ] as [number, number, number]
}

export function ShureMicrophoneConeCountEditor({
  node,
}: {
  node: ShureMicrophoneNode
  onUpdate: (patch: Partial<ShureMicrophoneNode>) => void
}) {
  const currentIds = getShureMicrophoneTargetIds(node)
  const count = Math.max(1, currentIds.length)
  const [draftCount, setDraftCount] = useState<number | null>(null)
  const value = draftCount ?? count

  const commit = (rawCount: number) => {
    const nextCount = Math.max(1, Math.min(MAX_CONES, Math.round(rawCount)))
    setDraftCount(null)
    if (nextCount === count) return

    const scene = useScene.getState()
    if (nextCount < count) {
      const retained = currentIds.slice(0, nextCount)
      scene.updateNode(node.id, { targetIds: retained.slice(1) })
      scene.deleteNodes(currentIds.slice(nextCount) as AnyNodeId[])
      return
    }

    const added = Array.from({ length: nextCount - count }, (_, offset) =>
      ShureMicrophoneTargetNode.parse({
        name: `Microphone target ${count + offset + 1}`,
        parentId: node.parentId,
        microphoneId: node.id,
        position: targetPosition(node, count + offset, nextCount),
      }),
    )
    if (!node.parentId) return
    scene.createNodes(
      added.map((target) => ({ node: target, parentId: node.parentId as AnyNodeId })),
    )
    scene.updateNode(node.id, {
      targetIds: [...currentIds.slice(1), ...added.map((target) => target.id)],
    })
  }

  return (
    <SliderControl
      label="Cones"
      max={MAX_CONES}
      min={1}
      onChange={setDraftCount}
      onCommit={commit}
      precision={0}
      step={1}
      value={value}
    />
  )
}
