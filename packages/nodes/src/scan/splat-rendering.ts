import { BufferAttribute, type InstancedBufferGeometry, SRGBColorSpace } from 'three'
import type NodeMaterial from 'three/src/materials/nodes/NodeMaterial.js'
import { colorSpaceToWorking } from 'three/tsl'

const COLOR_MANAGED_FLAG = 'pascalSplatColorManaged'

export function applySplatColorManagement(material: NodeMaterial): boolean {
  if (material.userData[COLOR_MANAGED_FLAG] || !material.colorNode) return false
  material.colorNode = colorSpaceToWorking(
    material.colorNode,
    SRGBColorSpace,
  ) as unknown as NodeMaterial['colorNode']
  material.userData[COLOR_MANAGED_FLAG] = true
  material.needsUpdate = true
  return true
}

export function repairSplatBillboardGeometry(geometry: InstancedBufferGeometry): boolean {
  const position = geometry.getAttribute('position')
  const index = geometry.getIndex()
  if (!index || position.count !== 4 || index.count !== 6) return false

  const expanded = new Float32Array(index.count * position.itemSize)
  for (let vertex = 0; vertex < index.count; vertex += 1) {
    const sourceVertex = index.getX(vertex)
    for (let component = 0; component < position.itemSize; component += 1) {
      expanded[vertex * position.itemSize + component] =
        position.array[sourceVertex * position.itemSize + component] ?? 0
    }
  }

  geometry.setAttribute('position', new BufferAttribute(expanded, position.itemSize))
  geometry.setIndex(null)
  return true
}
