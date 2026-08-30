'use client'

import { type ScanAssetFormat, type ScanNode, useRegistry } from '@pascal-app/core'
import { ErrorBoundary, useAssetUrl, useGLTFKTX2, useViewer } from '@pascal-app/viewer'
import { Html } from '@react-three/drei'
import { useLoader } from '@react-three/fiber'
import { Suspense, useEffect, useLayoutEffect, useMemo, useRef } from 'react'
import type { BufferGeometry, Group, InstancedBufferGeometry, Material, Mesh } from 'three'
import { GaussianSplatPLYLoader } from 'three/addons/loaders/GaussianSplatPLYLoader.js'
import { KSPLATLoader } from 'three/addons/loaders/KSPLATLoader.js'
import { SPLATLoader } from 'three/addons/loaders/SPLATLoader.js'
import { GaussianSplat } from 'three/addons/objects/GaussianSplat.js'
import type NodeMaterial from 'three/src/materials/nodes/NodeMaterial.js'
import { useScanLoadProgress } from './scan-load-progress'
import { applySplatColorManagement, repairSplatBillboardGeometry } from './splat-rendering'
import { WorkerSPZLoader } from './worker-spz-loader'

type SplatAssetFormat = Exclude<ScanAssetFormat, 'mesh'>

const SPLAT_LOADERS = {
  ksplat: KSPLATLoader,
  ply: GaussianSplatPLYLoader,
  splat: SPLATLoader,
  spz: WorkerSPZLoader,
} as const

const sourceGeometryConsumers = new Map<
  BufferGeometry,
  { count: number; disposeTimer: ReturnType<typeof setTimeout> | null }
>()

export const ScanRenderer = ({ node }: { node: ScanNode }) => {
  const showScans = useViewer((s) => s.showScans)
  const ref = useRef<Group>(null!)
  useRegistry(node.id, 'scan', ref)

  const resolvedUrl = useAssetUrl(node.url)
  const resetKey = `${node.assetFormat}:${resolvedUrl ?? ''}`

  return (
    <group
      position={node.position}
      ref={ref}
      rotation={node.rotation}
      scale={[node.scale, node.scale, node.scale]}
      visible={showScans && node.visible}
    >
      {resolvedUrl && (
        <ErrorBoundary
          fallback={<ScanLoadStatus failed url={resolvedUrl} />}
          resetKey={resetKey}
          scope={`scan:${node.id}`}
        >
          <Suspense fallback={<ScanLoadStatus url={resolvedUrl} />}>
            {node.assetFormat === 'mesh' ? (
              <MeshScanModel opacity={node.opacity} url={resolvedUrl} />
            ) : (
              <SplatScanModel format={node.assetFormat} opacity={node.opacity} url={resolvedUrl} />
            )}
          </Suspense>
        </ErrorBoundary>
      )}
    </group>
  )
}

const MeshScanModel = ({ url, opacity }: { url: string; opacity: number }) => {
  const gltf = useGLTFKTX2(url) as { scene: Group }
  const scene = gltf.scene

  useMemo(() => {
    const normalizedOpacity = opacity / 100
    const isTransparent = normalizedOpacity < 1

    const updateMaterial = (material: Material) => {
      if (isTransparent) {
        material.transparent = true
        material.opacity = normalizedOpacity
        material.depthWrite = false
      } else {
        material.transparent = false
        material.opacity = 1
        material.depthWrite = true
      }
      material.needsUpdate = true
    }

    scene.traverse((child) => {
      if ((child as Mesh).isMesh) {
        const mesh = child as Mesh

        mesh.raycast = () => {}
        mesh.userData.excludeFromBvh = true
        mesh.geometry.boundingBox = null
        mesh.geometry.boundingSphere = null
        mesh.frustumCulled = false

        if (Array.isArray(mesh.material)) {
          mesh.material.forEach(updateMaterial)
        } else {
          updateMaterial(mesh.material)
        }
      }
    })
  }, [scene, opacity])

  return <primitive object={scene} />
}

const SplatScanModel = ({
  format,
  url,
  opacity,
}: {
  format: SplatAssetFormat
  url: string
  opacity: number
}) => {
  const Loader = SPLAT_LOADERS[format]
  const sourceGeometry = useLoader(Loader, url) as BufferGeometry
  const splat = useMemo(() => {
    const nextSplat = new GaussianSplat(sourceGeometry)
    repairSplatBillboardGeometry(nextSplat.geometry as InstancedBufferGeometry)
    applySplatColorManagement(nextSplat.material as NodeMaterial)
    return nextSplat
  }, [sourceGeometry])

  useSourceGeometryLifetime(sourceGeometry, Loader, url)

  useLayoutEffect(() => {
    const material = splat.material as Material
    material.transparent = true
    material.opacity = opacity / 100
    material.depthTest = true
    material.depthWrite = false
    material.needsUpdate = true

    splat.raycast = () => {}
    splat.userData.excludeFromBvh = true
    splat.frustumCulled = false
  }, [splat, opacity])

  useEffect(
    () => () => {
      splat.geometry.dispose()
      const materials = Array.isArray(splat.material) ? splat.material : [splat.material]
      materials.forEach((material) => {
        material.dispose()
      })
    },
    [splat],
  )

  return <primitive object={splat} />
}

const ScanLoadStatus = ({ url, failed = false }: { url: string; failed?: boolean }) => {
  const progress = useScanLoadProgress(url)
  const percentage =
    progress.phase === 'download' && progress.total > 0
      ? Math.min(100, Math.round((progress.loaded / progress.total) * 100))
      : null
  const label = failed
    ? 'Scan failed to load'
    : progress.phase === 'decode'
      ? 'Preparing scan…'
      : `Loading scan${percentage === null ? '…' : ` ${percentage}%`}`

  return (
    <Html center style={{ pointerEvents: 'none' }} zIndexRange={[30, 0]}>
      <div
        style={{
          background: failed ? 'rgba(127, 29, 29, 0.88)' : 'rgba(17, 24, 39, 0.82)',
          border: '1px solid rgba(255, 255, 255, 0.18)',
          borderRadius: 8,
          boxShadow: '0 6px 18px rgba(0, 0, 0, 0.24)',
          color: 'white',
          fontFamily: 'sans-serif',
          fontSize: 12,
          padding: '7px 10px',
          userSelect: 'none',
          whiteSpace: 'nowrap',
        }}
      >
        {label}
      </div>
    </Html>
  )
}

function useSourceGeometryLifetime(
  geometry: BufferGeometry,
  Loader: (typeof SPLAT_LOADERS)[SplatAssetFormat],
  url: string,
) {
  useEffect(() => {
    const existing = sourceGeometryConsumers.get(geometry)
    const entry = existing ?? { count: 0, disposeTimer: null }
    entry.count += 1
    if (entry.disposeTimer !== null) {
      clearTimeout(entry.disposeTimer)
      entry.disposeTimer = null
    }
    sourceGeometryConsumers.set(geometry, entry)

    return () => {
      entry.count -= 1
      if (entry.count !== 0) {
        return
      }

      entry.disposeTimer = setTimeout(() => {
        if (entry.count === 0) {
          geometry.dispose()
          useLoader.clear(Loader, url)
          sourceGeometryConsumers.delete(geometry)
        }
      }, 0)
    }
  }, [geometry, Loader, url])
}

export default ScanRenderer
