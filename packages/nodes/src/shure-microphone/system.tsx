'use client'

import {
  type AnyNodeId,
  sceneRegistry,
  type ShureMicrophoneNode,
  useLiveNodeOverrides,
  useLiveTransforms,
  useScene,
} from '@pascal-app/core'
import { Html } from '@react-three/drei'
import { createPortal, useFrame } from '@react-three/fiber'
import { useState } from 'react'
import type { Group } from 'three'
import { useShallow } from 'zustand/react/shallow'
import { applyShureMicrophoneConePose } from './geometry'
import { getShureMicrophoneTargetIds } from './target-ids'

function asPoint(value: unknown): [number, number, number] | null {
  return Array.isArray(value) &&
    value.length === 3 &&
    value.every((part) => typeof part === 'number')
    ? (value as [number, number, number])
    : null
}

function registryPosition(nodeId: string): [number, number, number] | null {
  const position = sceneRegistry.nodes.get(nodeId)?.position
  return position ? [position.x, position.y, position.z] : null
}

const ShureMicrophoneSystem = () => {
  const microphoneIds = useScene(
    useShallow((state) =>
      Object.values(state.nodes)
        .filter((node): node is ShureMicrophoneNode => node.type === 'shure-microphone')
        .map((node) => node.id),
    ),
  )

  useFrame(() => {
    const { nodes } = useScene.getState()
    const transforms = useLiveTransforms.getState()
    const overrides = useLiveNodeOverrides.getState()
    for (const node of Object.values(nodes)) {
      if (node.type !== 'shure-microphone') continue
      const microphoneTransform = transforms.get(node.id)
      const microphoneOverride = overrides.get(node.id)
      const microphonePosition =
        microphoneTransform?.position ??
        asPoint(microphoneOverride?.position) ??
        registryPosition(node.id) ??
        node.position
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
          targetTransform?.position ??
          asPoint(targetOverride?.position) ??
          registryPosition(target.id) ??
          target.position
        applyShureMicrophoneConePose(
          group,
          targetId,
          microphonePosition,
          microphoneRotation,
          targetPosition,
          target.beamAngle ?? node.beamAngle ?? 30,
        )
      }
    }
  })
  return (
    <>
      {microphoneIds.map((microphoneId) => (
        <ShureMicrophoneLabel key={microphoneId} microphoneId={microphoneId} />
      ))}
    </>
  )
}

function ShureMicrophoneLabel({ microphoneId }: { microphoneId: AnyNodeId }) {
  const [group, setGroup] = useState<Group | null>(null)

  useFrame(() => {
    if (group) return
    const next = sceneRegistry.nodes.get(microphoneId) as Group | undefined
    if (next) setGroup(next)
  })

  if (!group) return null

  return createPortal(
    <Html
      distanceFactor={8}
      eps={-1}
      position={[0.24, 0.18, 0]}
      style={{ pointerEvents: 'none' }}
      zIndexRange={[20, 0]}
    >
      <div
        style={{
          alignItems: 'center',
          background: 'rgba(15, 23, 42, 0.82)',
          border: '1px solid rgba(155, 255, 51, 0.8)',
          borderRadius: 6,
          boxShadow: '0 2px 8px rgba(0, 0, 0, 0.35)',
          color: '#9bff33',
          display: 'flex',
          fontFamily: 'sans-serif',
          fontSize: 12,
          fontWeight: 600,
          letterSpacing: '0.02em',
          padding: '4px 7px',
          userSelect: 'none',
          whiteSpace: 'nowrap',
        }}
      >
        Shure DCA901
      </div>
    </Html>,
    group,
  )
}

export default ShureMicrophoneSystem
