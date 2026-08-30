import { type BufferGeometry, FileLoader, Loader } from 'three'
import { SPZLoader } from 'three/addons/loaders/SPZLoader.js'
import { clearScanLoadProgress, setScanLoadProgress } from './scan-load-progress'
import { getSharedSpzWorkerClient } from './spz-worker-client'
import { deserializeSplatGeometry } from './spz-worker-protocol'

export class WorkerSPZLoader extends Loader<BufferGeometry> {
  load(
    url: string,
    onLoad: (geometry: BufferGeometry) => void,
    onProgress?: (event: ProgressEvent) => void,
    onError?: (error: unknown) => void,
  ) {
    const loader = new FileLoader(this.manager)
    loader.setPath(this.path)
    loader.setResponseType('arraybuffer')
    loader.setRequestHeader(this.requestHeader)
    loader.setWithCredentials(this.withCredentials)
    setScanLoadProgress(url, { phase: 'download', loaded: 0, total: 0 })

    loader.load(
      url,
      (result) => {
        const buffer = result as ArrayBuffer
        setScanLoadProgress(url, {
          phase: 'decode',
          loaded: buffer.byteLength,
          total: buffer.byteLength,
        })

        this.decode(buffer)
          .then((geometry) => {
            clearScanLoadProgress(url)
            onLoad(geometry)
          })
          .catch((error) => {
            this.manager.itemError(url)
            onError?.(error)
          })
      },
      (event) => {
        setScanLoadProgress(url, {
          phase: 'download',
          loaded: event.loaded,
          total: event.lengthComputable ? event.total : 0,
        })
        onProgress?.(event)
      },
      onError,
    )
  }

  private async decode(buffer: ArrayBuffer): Promise<BufferGeometry> {
    if (typeof Worker === 'undefined') {
      return await new SPZLoader(this.manager).parse(buffer)
    }

    const client = getSharedSpzWorkerClient()
    return decodeSpzWithFallback(
      buffer,
      async (workerBuffer) => deserializeSplatGeometry(await client.decode(workerBuffer)),
      async (fallbackBuffer) => await new SPZLoader(this.manager).parse(fallbackBuffer),
      () => client.stop(new Error('The SPZ decoder worker timed out.')),
    )
  }
}

export function decodeSpzWithFallback<T>(
  buffer: ArrayBuffer,
  workerDecode: (buffer: ArrayBuffer) => Promise<T>,
  fallbackDecode: (buffer: ArrayBuffer) => Promise<T>,
  onWorkerTimeout: () => void,
  timeoutMs = 1500,
): Promise<T> {
  const fallbackBuffer = buffer.slice(0)

  return new Promise((resolve, reject) => {
    let fallbackStarted = false
    let settled = false

    const startFallback = () => {
      if (fallbackStarted || settled) return
      fallbackStarted = true
      fallbackDecode(fallbackBuffer).then(resolve, reject)
    }

    const timer = setTimeout(() => {
      onWorkerTimeout()
      startFallback()
    }, timeoutMs)

    workerDecode(buffer).then(
      (result) => {
        if (fallbackStarted || settled) return
        settled = true
        clearTimeout(timer)
        resolve(result)
      },
      () => {
        clearTimeout(timer)
        startFallback()
      },
    )
  })
}
