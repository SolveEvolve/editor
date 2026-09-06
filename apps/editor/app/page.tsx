'use client'

import { Editor, ItemsPanel, type SceneGraph } from '@pascal-app/editor'
import { Hammer, Layers, Package, Settings } from 'lucide-react'
import Image from 'next/image'
import Link from 'next/link'
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
      children: ['scan_proto_august'],
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
      position: [0, 0.9, -0.3],
      rotation: [-Math.PI / 2, 0, 0],
      scale: 1,
      opacity: 100,
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
        viewerToolbarLeft={<CommunityViewerToolbarLeft />}
        viewerToolbarRight={<CommunityViewerToolbarRight />}
      />
    </div>
  )
}
