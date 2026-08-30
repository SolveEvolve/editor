import type {
  FloorplanGeometry,
  GeometryContext,
  ShureMicrophoneNode,
  ShureMicrophoneTargetNode,
} from '@pascal-app/core'
import { getShureMicrophoneTargetIds } from './target-ids'

const CYAN = '#22d3ee'

export function buildShureMicrophoneFloorplan(
  node: ShureMicrophoneNode,
  ctx: GeometryContext,
): FloorplanGeometry {
  const [x, , z] = node.position
  const children: FloorplanGeometry[] = [
    { kind: 'circle', cx: x, cy: z, r: 0.16, fill: '#1f2937', stroke: CYAN, strokeWidth: 0.015 },
  ]
  for (const targetId of getShureMicrophoneTargetIds(node)) {
    const target = ctx.resolve<ShureMicrophoneTargetNode>(targetId)
    if (!target) continue
    children.push({
      kind: 'line',
      x1: x,
      y1: z,
      x2: target.position[0],
      y2: target.position[2],
      stroke: CYAN,
      strokeWidth: 0.025,
      opacity: 0.7,
    })
  }
  return { kind: 'group', children }
}

export function buildShureMicrophoneTargetFloorplan(
  node: ShureMicrophoneTargetNode,
): FloorplanGeometry {
  return {
    kind: 'circle',
    cx: node.position[0],
    cy: node.position[2],
    r: 0.09,
    fill: CYAN,
    stroke: '#ffffff',
    strokeWidth: 0.015,
  }
}
