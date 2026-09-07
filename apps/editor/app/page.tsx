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
      position: [-4.9, 0.9, 3.3],
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
      position: [3, 0.1281505072178809, 2],
      microphoneId: 'shure-microphone_em4p38utk859cif1',
      beamAngle: 10,
    },
    'shure-microphone-target_ba20kgmkh3muro3c': {
      object: 'node',
      id: 'shure-microphone-target_ba20kgmkh3muro3c',
      type: 'shure-microphone-target',
      name: 'Microphone target 2',
      parentId: 'level_splat_demo',
      visible: true,
      metadata: {},
      position: [5.749349466770456, 0.12298399194282583, -2.0156046655413418],
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
      position: [1.9948079185090464, 0.09767871949152462, -3.5621751565787108],
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
