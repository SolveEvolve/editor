'use client'

import { emitter, useScene } from '@pascal-app/core'
import { Editor, ItemsPanel, type SceneGraph, useEditor } from '@pascal-app/editor'
import { Hammer, Layers, Package, Settings } from 'lucide-react'
import Image from 'next/image'
import Link from 'next/link'
import { useEffect, useRef } from 'react'
import { BuildTab } from '@/components/build-tab'
import {
  CommunityViewerToolbarLeft,
  CommunityViewerToolbarRight,
} from '@/components/viewer-toolbar'

// The open-source editor only ships the built-in catalog (no uploaded items),
// so the Library/Community/Mine source chips and tag filters add nothing —
// drop them and keep the panel to plain categories.
function EditorItemsPanel() {
  return <ItemsPanel showSourceFilter={false} showTagFilters={false} />
}

const SIDEBAR_TABS = [
  {
    id: 'site',
    label: 'Scene',
    component: () => null,
    mobileDefaultSnap: 0.5,
    mobileIcon: <Layers className="h-5 w-5" />,
    icon: (
      <Image
        alt=""
        className="h-8 w-8 object-contain"
        height={32}
        src="/icons/scene.webp"
        width={32}
      />
    ),
  },
  {
    id: 'build',
    label: 'Build',
    component: BuildTab,
    mobileDefaultSnap: 0.5,
    mobileIcon: <Hammer className="h-5 w-5" />,
    icon: (
      <Image
        alt=""
        className="h-8 w-8 object-contain"
        height={32}
        src="/icons/build.webp"
        width={32}
      />
    ),
  },
  {
    id: 'items',
    label: 'Items',
    component: EditorItemsPanel,
    mobileDefaultSnap: 0.5,
    mobileIcon: <Package className="h-5 w-5" />,
    icon: (
      <Image
        alt=""
        className="h-8 w-8 object-contain"
        height={32}
        src="/icons/couch.webp"
        width={32}
      />
    ),
  },
  {
    id: 'settings',
    label: 'Settings',
    component: () => null,
    mobileDefaultSnap: 0.5,
    mobileIcon: <Settings className="h-5 w-5" />,
    icon: (
      <Image
        alt=""
        className="h-8 w-8 object-contain"
        height={32}
        src="/icons/settings.webp"
        width={32}
      />
    ),
  },
]

const PROJECT_ID = 'local-editor'
const DEMO_SPAWN_ID = 'spawn_9dxykm8b3plfgbam'
const DEMO_SPAWN_EYE_HEIGHT = 1.65

function DemoLaunchPose() {
  const spawn = useScene((state) => state.nodes[DEMO_SPAWN_ID])
  const hasApplied = useRef(false)

  useEffect(() => {
    if (hasApplied.current || spawn?.type !== 'spawn') return
    hasApplied.current = true
    useEditor.getState().setMode('select')

    const [x, y, z] = spawn.position
    const forwardX = -Math.sin(spawn.rotation)
    const forwardZ = -Math.cos(spawn.rotation)
    const eyeY = y + DEMO_SPAWN_EYE_HEIGHT
    const frame = window.requestAnimationFrame(() => {
      emitter.emit('camera-controls:apply-pose', {
        position: [x, eyeY, z],
        target: [x + forwardX * 8, eyeY - 0.9, z + forwardZ * 8],
        projection: 'perspective',
      })
    })

    return () => window.cancelAnimationFrame(frame)
  }, [spawn])

  return null
}

const PROTO_DEMO_SCENE: SceneGraph = {
  nodes: {
    site_splat_demo: {
      object: 'node',
      id: 'site_splat_demo',
      type: 'site',
      name: 'Proto August Site',
      parentId: null,
      visible: true,
      metadata: {},
      polygon: {
        type: 'polygon',
        points: [
          [-15, -15],
          [15, -15],
          [15, 15],
          [-15, 15],
        ],
      },
      renderGround: false,
      shadowCatcher: true,
      children: ['building_splat_demo'],
    },
    building_splat_demo: {
      object: 'node',
      id: 'building_splat_demo',
      type: 'building',
      name: 'Proto August Building',
      parentId: 'site_splat_demo',
      visible: true,
      metadata: {},
      children: ['level_splat_demo'],
      position: [0, 0, 0],
      rotation: [0, 0, 0],
    },
    level_splat_demo: {
      object: 'node',
      id: 'level_splat_demo',
      type: 'level',
      name: 'Scan Level',
      parentId: 'building_splat_demo',
      visible: true,
      metadata: {},
      children: [
        'scan_proto_august',
        'shure-microphone_em4p38utk859cif1',
        'shure-microphone-target_26sygq8c4lslvus6',
        'shure-microphone-target_ba20kgmkh3muro3c',
        'shure-microphone-target_jf84lh9jikci175g',
        'spawn_9dxykm8b3plfgbam',
        'item_swbh30xhl0yqyxhp',
        'item_0pncv22y76f0fyd1',
        'item_dz7wyxl3vyn71w2j',
        'item_th1so8limf3a3m1d',
      ],
      level: 0,
      height: 3,
    },
    scan_proto_august: {
      object: 'node',
      id: 'scan_proto_august',
      type: 'scan',
      name: 'Proto August Scan',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      url: '/test-assets/gaussian-splats/proto-august-scan-converted/proto-august-scan-464k.spz',
      assetFormat: 'spz',
      position: [-4.9, 0.8, 3.3],
      rotation: [-Math.PI / 2, 0, -0.12217304763960307],
      scale: 1,
      opacity: 100,
    },
    'shure-microphone_em4p38utk859cif1': {
      object: 'node',
      id: 'shure-microphone_em4p38utk859cif1',
      type: 'shure-microphone',
      name: 'Shure Microphone',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      position: [3.587648111203707, 5.330595709669998, -0.21191005168788016],
      rotation: [0, 0, 0],
      targetId: 'shure-microphone-target_26sygq8c4lslvus6',
      targetIds: [
        'shure-microphone-target_ba20kgmkh3muro3c',
        'shure-microphone-target_jf84lh9jikci175g',
      ],
      beamAngle: 30,
    },
    'shure-microphone-target_26sygq8c4lslvus6': {
      object: 'node',
      id: 'shure-microphone-target_26sygq8c4lslvus6',
      type: 'shure-microphone-target',
      name: 'Microphone target',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      position: [5.5, 0.1281505072178809, 0],
      microphoneId: 'shure-microphone_em4p38utk859cif1',
      beamAngle: 30,
    },
    'shure-microphone-target_ba20kgmkh3muro3c': {
      object: 'node',
      id: 'shure-microphone-target_ba20kgmkh3muro3c',
      type: 'shure-microphone-target',
      name: 'Microphone target 2',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      position: [12.749349466770456, 0.12298399194282583, -5.515604665541342],
      microphoneId: 'shure-microphone_em4p38utk859cif1',
      beamAngle: 10,
    },
    'shure-microphone-target_jf84lh9jikci175g': {
      object: 'node',
      id: 'shure-microphone-target_jf84lh9jikci175g',
      type: 'shure-microphone-target',
      name: 'Microphone target 3',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      position: [1.4948079185090464, 0.09767871949152462, -1.0621751565787108],
      microphoneId: 'shure-microphone_em4p38utk859cif1',
      beamAngle: 12,
    },
    spawn_9dxykm8b3plfgbam: {
      object: 'node',
      id: 'spawn_9dxykm8b3plfgbam',
      type: 'spawn',
      name: 'Spawn Point',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      position: [-4.5, 0, 0],
      rotation: -Math.PI / 2,
    },
    item_swbh30xhl0yqyxhp: {
      object: 'node',
      id: 'item_swbh30xhl0yqyxhp',
      type: 'item',
      name: 'Tesla Model Y',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      position: [5.5, 0, 0],
      rotation: [0, -Math.PI / 4, 0],
      scale: [1, 1, 1],
      children: [],
      asset: {
        id: 'tesla',
        category: 'outdoor',
        name: 'Tesla Model Y',
        thumbnail: '/items/tesla/thumbnail.png',
        floorPlanUrl: '/items/tesla/floor-plan.png',
        source: 'library',
        src: '/items/tesla/model.glb',
        dimensions: [1.98, 1.62, 4.76],
        tags: [
          'tesla',
          'car',
          'vehicle',
          'automobile',
          'electric',
          'modern',
          'minimalist',
          'sleek',
          'metal',
          'glass',
          'luxury',
          'transportation',
          'commuting',
        ],
        offset: [0.0039, -0.0102, -0.0148],
        rotation: [0, 0, 0],
        scale: [1, 1, 1],
      },
    },
    item_0pncv22y76f0fyd1: {
      object: 'node',
      id: 'item_0pncv22y76f0fyd1',
      type: 'item',
      name: 'Hard Light',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      position: [1.5, 0, 2],
      rotation: [0, (3 * Math.PI) / 4, 0],
      scale: [1.2, 1.2, 1.2],
      children: [],
      asset: {
        id: 'hard-light',
        category: 'production',
        name: 'Hard Light',
        thumbnail: '/icons/hard-light.webp',
        source: 'library',
        src: '/assets/production/hard-light.glb',
        dimensions: [0.7234, 1.9029, 0.8077],
        tags: ['light', 'lighting', 'production', 'c-stand', 'floor'],
        offset: [0, 0.9522, 0],
        rotation: [0, 0, 0],
        scale: [1, 1, 1],
      },
    },
    item_dz7wyxl3vyn71w2j: {
      object: 'node',
      id: 'item_dz7wyxl3vyn71w2j',
      type: 'item',
      name: 'Hard Light',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      position: [4, 0, -3],
      rotation: [0, 0, 0],
      scale: [1.2, 1.2, 1.2],
      children: [],
      asset: {
        id: 'hard-light',
        category: 'production',
        name: 'Hard Light',
        thumbnail: '/icons/hard-light.webp',
        source: 'library',
        src: '/assets/production/hard-light.glb',
        dimensions: [0.7234, 1.9029, 0.8077],
        tags: ['light', 'lighting', 'production', 'c-stand', 'floor'],
        offset: [0, 0.9522, 0],
        rotation: [0, 0, 0],
        scale: [1, 1, 1],
      },
    },
    item_th1so8limf3a3m1d: {
      object: 'node',
      id: 'item_th1so8limf3a3m1d',
      type: 'item',
      name: 'Cinema Camera',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      position: [-0.25, 0, 0.25],
      rotation: [0, 3.403392041388942, 0],
      scale: [0.9, 0.9, 0.9],
      children: [],
      asset: {
        id: 'cinema-camera',
        category: 'production',
        name: 'Cinema Camera',
        thumbnail: '/icons/cinema-camera.webp',
        source: 'library',
        src: '/assets/production/cinema-camera.glb',
        dimensions: [1.4112, 1.9027, 1.2331],
        tags: ['camera', 'cinema', 'production', 'tripod', 'floor'],
        offset: [0, 0.9524, 0],
        rotation: [0, 0, 0],
        scale: [1, 1, 1],
      },
    },
  },
  rootNodeIds: ['site_splat_demo'],
  collections: {},
}

async function loadProtoDemoScene(): Promise<SceneGraph> {
  return structuredClone(PROTO_DEMO_SCENE)
}

export default function Home() {
  return (
    <div className="relative h-screen w-screen">
      {PROJECT_ID === 'local-editor' && (
        <div className="pointer-events-none absolute top-3 left-1/2 z-40 -translate-x-1/2">
          <div className="pointer-events-auto flex items-center gap-3 rounded-full border border-border/60 bg-background/90 px-4 py-1.5 text-xs shadow-sm backdrop-blur">
            <span className="text-muted-foreground">Local editor — scenes are not saved.</span>
            <Link className="font-medium text-foreground hover:underline" href="/scenes">
              Open recent scenes
            </Link>
            <span aria-hidden className="text-muted-foreground">
              ·
            </span>
            <Link className="font-medium text-foreground hover:underline" href="/scenes">
              Create new
            </Link>
          </div>
        </div>
      )}
      <Editor
        layoutVersion="v2"
        onLoad={loadProtoDemoScene}
        projectId={PROJECT_ID}
        sidebarTabs={SIDEBAR_TABS}
        viewerSceneSlot={<DemoLaunchPose />}
        viewerToolbarLeft={<CommunityViewerToolbarLeft />}
        viewerToolbarRight={<CommunityViewerToolbarRight />}
      />
    </div>
  )
}
