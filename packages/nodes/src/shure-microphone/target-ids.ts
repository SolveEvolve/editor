import type { ShureMicrophoneNode } from '@pascal-app/core'

type TargetId = NonNullable<ShureMicrophoneNode['targetId']>

export function getShureMicrophoneTargetIds(node: ShureMicrophoneNode): TargetId[] {
  const targetIds = (node as { targetIds?: TargetId[] }).targetIds ?? []
  return Array.from(
    new Set([node.targetId, ...targetIds].filter((id): id is TargetId => id !== undefined)),
  ).slice(0, 8)
}
