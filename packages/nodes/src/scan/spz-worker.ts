import { SPZLoader } from 'three/addons/loaders/SPZLoader.js'
import {
  type SpzWorkerRequest,
  type SpzWorkerResponse,
  serializeSplatGeometry,
} from './spz-worker-protocol'

type WorkerScope = {
  onmessage: ((event: MessageEvent<SpzWorkerRequest>) => void) | null
  postMessage(message: SpzWorkerResponse, transfer?: Transferable[]): void
}

const workerScope = self as unknown as WorkerScope
const loader = new SPZLoader()

workerScope.onmessage = async ({ data }) => {
  if (data.type !== 'decode') return

  try {
    const geometry = await loader.parse(data.buffer)
    const serialized = serializeSplatGeometry(geometry)
    workerScope.postMessage(
      { type: 'decoded', id: data.id, geometry: serialized.geometry },
      serialized.transfer,
    )
    geometry.dispose()
  } catch (error) {
    workerScope.postMessage({
      type: 'error',
      id: data.id,
      message: error instanceof Error ? error.message : String(error),
    })
  }
}
