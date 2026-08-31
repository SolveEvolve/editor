import { z } from 'zod'
import { BaseNode, nodeType, objectId } from '../base'

export const ShureMicrophoneNode = BaseNode.extend({
  id: objectId('shure-microphone'),
  type: nodeType('shure-microphone'),
  position: z.tuple([z.number(), z.number(), z.number()]).default([0, 0, 0]),
  rotation: z.tuple([z.number(), z.number(), z.number()]).default([0, 0, 0]),
  targetId: objectId('shure-microphone-target').optional(),
  targetIds: z.array(objectId('shure-microphone-target')).max(7).default([]),
  beamAngle: z.number().min(5).max(120).default(30),
})

export type ShureMicrophoneNode = z.infer<typeof ShureMicrophoneNode>

export const ShureMicrophoneTargetNode = BaseNode.extend({
  id: objectId('shure-microphone-target'),
  type: nodeType('shure-microphone-target'),
  position: z.tuple([z.number(), z.number(), z.number()]).default([0, 0, 2]),
  microphoneId: objectId('shure-microphone').optional(),
  beamAngle: z.number().min(5).max(120).default(30),
})

export type ShureMicrophoneTargetNode = z.infer<typeof ShureMicrophoneTargetNode>
