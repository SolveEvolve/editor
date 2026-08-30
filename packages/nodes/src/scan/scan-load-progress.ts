import { useSyncExternalStore } from 'react'

export type ScanLoadProgress = {
  phase: 'download' | 'decode'
  loaded: number
  total: number
}

const DEFAULT_PROGRESS: ScanLoadProgress = { phase: 'download', loaded: 0, total: 0 }
const progressByUrl = new Map<string, ScanLoadProgress>()
const listenersByUrl = new Map<string, Set<() => void>>()

export function setScanLoadProgress(url: string, progress: ScanLoadProgress) {
  progressByUrl.set(url, progress)
  listenersByUrl.get(url)?.forEach((listener) => {
    listener()
  })
}

export function clearScanLoadProgress(url: string) {
  progressByUrl.delete(url)
  listenersByUrl.get(url)?.forEach((listener) => {
    listener()
  })
}

export function useScanLoadProgress(url: string): ScanLoadProgress {
  return useSyncExternalStore(
    (listener) => {
      const listeners = listenersByUrl.get(url) ?? new Set()
      listeners.add(listener)
      listenersByUrl.set(url, listeners)
      return () => {
        listeners.delete(listener)
        if (listeners.size === 0) listenersByUrl.delete(url)
      }
    },
    () => progressByUrl.get(url) ?? DEFAULT_PROGRESS,
    () => DEFAULT_PROGRESS,
  )
}
