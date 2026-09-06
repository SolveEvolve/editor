import type {
  GeometryContext,
  ShureMicrophoneNode,
  ShureMicrophoneTargetNode,
} from '@pascal-app/core'
import {
  ConeGeometry,
  EdgesGeometry,
  Euler,
  Group,
  LineBasicMaterial,
  LineSegments,
  Mesh,
  MeshBasicMaterial,
  Quaternion,
  SphereGeometry,
  Vector3,
} from 'three'
import { getShureMicrophoneTargetIds } from './target-ids'

const CYAN = '#22d3ee'
const DOWN = new Vector3(0, -1, 0)

type Point = readonly [number, number, number]
type ConeGuide = LineSegments & {
  userData: {
    shureConeLength?: number
    shureConeRadius?: number
  }
}

const beamRadius = (length: number, beamAngle: number) =>
  Math.tan((beamAngle * Math.PI) / 360) * length

const source = new Vector3()
const target = new Vector3()
const direction = new Vector3()
const sourceRotationEuler = new Euler()
const inverseRotation = new Quaternion()

export function applyShureMicrophoneConePose(
  group: Group,
  targetId: string,
  sourcePosition: Point,
  sourceRotation: Point,
  targetPosition: Point,
  beamAngle: number,
) {
  const cone = group.getObjectByName(`direction-cone:${targetId}`) as ConeGuide | undefined
  if (!cone) return

  source.fromArray(sourcePosition)
  target.fromArray(targetPosition)
  direction.subVectors(target, source)
  const length = direction.length()
  if (length < 0.001) {
    cone.visible = false
    return
  }

  cone.visible = true
  direction
    .normalize()
    .applyQuaternion(
      inverseRotation
        .setFromEuler(
          sourceRotationEuler.set(sourceRotation[0], sourceRotation[1], sourceRotation[2]),
        )
        .invert(),
    )
  const baseLength = cone.userData.shureConeLength ?? length
  const baseRadius = cone.userData.shureConeRadius ?? Math.max(0.12, baseLength * 0.16)
  const radius = beamRadius(length, beamAngle)
  cone.position.copy(direction).multiplyScalar(length / 2)
  cone.quaternion.setFromUnitVectors(DOWN, direction)
  cone.scale.set(radius / baseRadius, length / baseLength, radius / baseRadius)
}

export function buildShureMicrophoneGeometry(
  node: ShureMicrophoneNode,
  ctx: GeometryContext,
): Group {
  const beamAngle = node.beamAngle ?? 30
  const group = new Group()
  const disk = new Mesh(
    new ConeGeometry(0.16, 0.035, 32),
    new MeshBasicMaterial({ color: '#1f2937' }),
  )
  disk.name = 'microphone-disk'
  group.add(disk)

  for (const targetId of getShureMicrophoneTargetIds(node)) {
    const target = ctx.resolve<ShureMicrophoneTargetNode>(targetId)
    if (!target) continue
    const targetDirection = new Vector3(
      target.position[0] - node.position[0],
      target.position[1] - node.position[1],
      target.position[2] - node.position[2],
    )
    const length = targetDirection.length()
    if (length < 0.001) continue
    const targetBeamAngle = target.beamAngle ?? beamAngle
    const radius = beamRadius(length, targetBeamAngle)
    const coneGeometry = new ConeGeometry(radius, length, 16, 1, true)
    const cone = new LineSegments(
      new EdgesGeometry(coneGeometry),
      new LineBasicMaterial({
        color: CYAN,
        transparent: true,
        opacity: 0.8,
        depthTest: true,
        depthWrite: false,
      }),
    )
    coneGeometry.dispose()
    cone.name = `direction-cone:${targetId}`
    cone.raycast = () => {}
    cone.userData.shureConeLength = length
    cone.userData.shureConeRadius = radius
    group.add(cone)
    applyShureMicrophoneConePose(
      group,
      targetId,
      node.position,
      node.rotation,
      target.position,
      targetBeamAngle,
    )
  }
  return group
}

export function buildShureMicrophoneTargetGeometry(_node: ShureMicrophoneTargetNode): Group {
  const group = new Group()
  const marker = new Mesh(
    new SphereGeometry(0.08, 16, 12),
    new MeshBasicMaterial({ color: CYAN, transparent: true, opacity: 0.9 }),
  )
  marker.name = 'microphone-target'
  group.add(marker)
  return group
}
