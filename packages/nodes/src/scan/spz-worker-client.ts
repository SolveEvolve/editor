import type {
  SerializedSplatGeometry,
  SpzWorkerRequest,
  SpzWorkerResponse,
} from './spz-worker-protocol'

type PendingDecode = {
  resolve: (geometry: SerializedSplatGeometry) => void
  reject: (error: Error) => void
}

export interface SpzWorkerLike {
  onerror: ((event: ErrorEvent) => void) | null
  onmessage: ((event: MessageEvent<SpzWorkerResponse>) => void) | null
  postMessage(message: SpzWorkerRequest, transfer: Transferable[]): void
  terminate(): void
}

export class SpzWorkerClient {
  private nextId = 1
  private pending = new Map<number, PendingDecode>()
  private stopped = false

  constructor(private readonly worker: SpzWorkerLike) {
    worker.onmessage = (event) => this.handleMessage(event.data)
    worker.onerror = (event) => {
      this.stop(new Error(event.message || 'The SPZ decoder worker failed.'))
    }
  }

  get closed() {
    return this.stopped
  }

  decode(buffer: ArrayBuffer): Promise<SerializedSplatGeometry> {
    if (this.stopped) {
      return Promise.reject(new Error('The SPZ decoder worker is not available.'))
    }

    const id = this.nextId++
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject })
      this.worker.postMessage({ type: 'decode', id, buffer }, [buffer])
    })
  }

  stop(error = new Error('The SPZ decoder worker was stopped.')) {
    if (this.stopped) return
    this.stopped = true
    this.worker.terminate()
    for (const request of this.pending.values()) request.reject(error)
    this.pending.clear()
  }

  private handleMessage(message: SpzWorkerResponse) {
    const request = this.pending.get(message.id)
    if (!request) return
    this.pending.delete(message.id)

    if (message.type === 'decoded') {
      request.resolve(message.geometry)
    } else {
      request.reject(new Error(message.message))
    }
  }
}

let sharedClient: SpzWorkerClient | null = null

export function getSharedSpzWorkerClient(): SpzWorkerClient {
  if (sharedClient?.closed !== false) {
    const worker = new Worker(new URL('./spz-worker.js', import.meta.url), { type: 'module' })
    sharedClient = new SpzWorkerClient(worker)
  }
  return sharedClient
}
