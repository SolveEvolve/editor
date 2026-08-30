'use client'

import { sceneRegistry, useLiveNodeOverrides, useLiveTransforms, useScene } from '@pascal-app/core'
import { useFrame } from '@react-three/fiber'
import type { Group } from 'three'
import { applyShureMicrophoneConePose } from './geometry'
import { getShureMicrophoneTargetIds } from './target-ids'

function asPoint(value: unknown): [number, number, number] | null {
  return Array.isArray(value) &&
    value.length === 3 &&
    value.every((part) => typeof part === 'number')
    ? (value as [number, number, number])
    : null
}

const ShureMicrophoneSystem = () => {
  useFrame(() => {
    const { nodes } = useScene.getState()
    const transforms = useLiveTransforms.getState()
    const overrides = useLiveNodeOverrides.getState()
    for (const node of Object.values(nodes)) {
      if (node.type !== 'shure-microphone') continue
      const microphoneTransform = transforms.get(node.id)
      const microphoneOverride = overrides.get(node.id)
      const microphonePosition =
        microphoneTransform?.position ?? asPoint(microphoneOverride?.position) ?? node.position
      const overrideRotation = asPoint(microphoneOverride?.rotation)
      const microphoneRotation: [number, number, number] = [
        overrideRotation?.[0] ?? node.rotation[0],
        microphoneTransform?.rotation ?? overrideRotation?.[1] ?? node.rotation[1],
        overrideRotation?.[2] ?? node.rotation[2],
      ]
      const group = sceneRegistry.nodes.get(node.id) as Group | undefined
      if (!group) continue
      for (const targetId of getShureMicrophoneTargetIds(node)) {
        const target = nodes[targetId]
        if (target?.type !== 'shure-microphone-target') continue
        const targetTransform = transforms.get(target.id)
        const targetOverride = overrides.get(target.id)
        const targetPosition =
          targetTransform?.position ?? asPoint(targetOverride?.position) ?? target.position
        applyShureMicrophoneConePose(
          group,
          targetId,
          microphonePosition,
          microphoneRotation,
          targetPosition,
        )
      }
    }
  })
  return null
}

export default ShureMicrophoneSystem
