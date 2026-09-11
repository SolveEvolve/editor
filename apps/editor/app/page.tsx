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
const ITV_SCENE: SceneGraph = {
  nodes: {
    site_itv: {
      object: 'node',
      id: 'site_itv',
      type: 'site',
      name: 'ITV Scan',
      parentId: null,
      visible: true,
      metadata: {},
      polygon: {
        type: 'polygon',
        points: [
          [-20, -20],
          [20, -20],
          [20, 20],
          [-20, 20],
        ],
      },
      renderGround: false,
      shadowCatcher: false,
      children: ['building_itv'],
    },
    building_itv: {
      object: 'node',
      id: 'building_itv',
      type: 'building',
      name: 'ITV',
      parentId: 'site_itv',
      visible: true,
      metadata: {},
      children: ['level_itv'],
      position: [0, 0, 0],
      rotation: [0, 0, 0],
    },
    level_itv: {
      object: 'node',
      id: 'level_itv',
      type: 'level',
      name: 'Scan Level',
      parentId: 'building_itv',
      visible: true,
      metadata: {},
      children: ['scan_itv'],
      level: 0,
      height: 10,
    },
    scan_itv: {
      object: 'node',
      id: 'scan_itv',
      type: 'scan',
      name: 'ITV Scan',
      parentId: 'level_itv',
      visible: true,
      metadata: {},
      url: '/test-assets/gaussian-splats/itv-scan-converted/itv-scan.spz',
      assetFormat: 'spz',
      position: [-5.5, 0, -3.8],
      rotation: [-Math.PI / 2, 0, 0],
      scale: 1,
      opacity: 100,
    },
  },
  rootNodeIds: ['site_itv'],
  collections: {},
}

async function loadItvScene(): Promise<SceneGraph> {
  return structuredClone(ITV_SCENE)
}

function ItvLaunchPose() {
  const scan = useScene((state) => state.nodes.scan_itv)
  const hasApplied = useRef(false)

  useEffect(() => {
    if (hasApplied.current || scan?.type !== 'scan') return
    useEditor.getState().setMode('select')
    const frame = window.requestAnimationFrame(() => {
      hasApplied.current = true
      emitter.emit('camera-controls:apply-pose', {
        position: [0, 4, 8],
        target: [0, 1, 0],
        projection: 'perspective',
      })
    })
    return () => window.cancelAnimationFrame(frame)
  }, [scan])

  return null
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
        onLoad={loadItvScene}
        projectId={PROJECT_ID}
        sidebarTabs={SIDEBAR_TABS}
        viewerSceneSlot={<ItvLaunchPose />}
        viewerToolbarLeft={<CommunityViewerToolbarLeft />}
        viewerToolbarRight={<CommunityViewerToolbarRight />}
      />
    </div>
  )
}
