import { BufferAttribute, BufferGeometry } from 'three'

type TransferableTypedArray =
  | Float32Array
  | Float64Array
  | Int8Array
  | Int16Array
  | Int32Array
  | Uint8Array
  | Uint8ClampedArray
  | Uint16Array
  | Uint32Array

type TypedArrayName =
  | 'Float32Array'
  | 'Float64Array'
  | 'Int8Array'
  | 'Int16Array'
  | 'Int32Array'
  | 'Uint8Array'
  | 'Uint8ClampedArray'
  | 'Uint16Array'
  | 'Uint32Array'

export interface SerializedBufferAttribute {
  buffer: ArrayBuffer
  byteOffset: number
  length: number
  itemSize: number
  normalized: boolean
  name: string
  arrayType: TypedArrayName
}

export interface SerializedSplatGeometry {
  attributes: Record<string, SerializedBufferAttribute>
}

export type SpzWorkerRequest = {
  type: 'decode'
  id: number
  buffer: ArrayBuffer
}

export type SpzWorkerResponse =
  | { type: 'decoded'; id: number; geometry: SerializedSplatGeometry }
  | { type: 'error'; id: number; message: string }

const ARRAY_CONSTRUCTORS = {
  Float32Array,
  Float64Array,
  Int8Array,
  Int16Array,
  Int32Array,
  Uint8Array,
  Uint8ClampedArray,
  Uint16Array,
  Uint32Array,
} as const

export function serializeSplatGeometry(geometry: BufferGeometry): {
  geometry: SerializedSplatGeometry
  transfer: ArrayBuffer[]
} {
  const attributes: Record<string, SerializedBufferAttribute> = {}
  const transfer = new Set<ArrayBuffer>()

  for (const [attributeName, attribute] of Object.entries(geometry.attributes)) {
    if (
      'isInterleavedBufferAttribute' in attribute &&
      attribute.isInterleavedBufferAttribute === true
    ) {
      throw new Error(`Cannot transfer interleaved splat attribute "${attributeName}".`)
    }

    const array = attribute.array as TransferableTypedArray
    if (!(array.buffer instanceof ArrayBuffer)) {
      throw new Error(`Cannot transfer shared splat attribute "${attributeName}".`)
    }

    const arrayType = array.constructor.name as TypedArrayName
    if (!(arrayType in ARRAY_CONSTRUCTORS)) {
      throw new Error(`Unsupported splat attribute array type "${arrayType}".`)
    }

    attributes[attributeName] = {
      arrayType,
      buffer: array.buffer,
      byteOffset: array.byteOffset,
      itemSize: attribute.itemSize,
      length: array.length,
      name: attribute.name,
      normalized: attribute.normalized,
    }
    transfer.add(array.buffer)
  }

  return { geometry: { attributes }, transfer: [...transfer] }
}

export function deserializeSplatGeometry(serialized: SerializedSplatGeometry): BufferGeometry {
  const geometry = new BufferGeometry()

  for (const [attributeName, attribute] of Object.entries(serialized.attributes)) {
    const ArrayConstructor = ARRAY_CONSTRUCTORS[attribute.arrayType]
    const array = new ArrayConstructor(
      attribute.buffer,
      attribute.byteOffset,
      attribute.length,
    ) as TransferableTypedArray
    const bufferAttribute = new BufferAttribute(array, attribute.itemSize, attribute.normalized)
    bufferAttribute.name = attribute.name
    geometry.setAttribute(attributeName, bufferAttribute)
  }

  return geometry
}
