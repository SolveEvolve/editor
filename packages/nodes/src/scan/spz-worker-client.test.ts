import { describe, expect, test } from 'bun:test'
import { SpzWorkerClient, type SpzWorkerLike } from './spz-worker-client'
import type { SpzWorkerResponse } from './spz-worker-protocol'

class FakeWorker implements SpzWorkerLike {
  onerror: ((event: ErrorEvent) => void) | null = null
  onmessage: ((event: MessageEvent<SpzWorkerResponse>) => void) | null = null
  messages: Array<{ id: number; buffer: ArrayBuffer }> = []
  terminated = false

  postMessage(message: { id: number; buffer: ArrayBuffer }) {
    this.messages.push(message)
  }

  terminate() {
    this.terminated = true
  }

  respond(message: SpzWorkerResponse) {
    this.onmessage?.({ data: message } as MessageEvent<SpzWorkerResponse>)
  }
}

describe('SPZ worker client', () => {
  test('routes concurrent responses by request id', async () => {
    const worker = new FakeWorker()
    const client = new SpzWorkerClient(worker)
    const first = client.decode(new ArrayBuffer(2))
    const second = client.decode(new ArrayBuffer(3))

    worker.respond({ type: 'decoded', id: worker.messages[1]!.id, geometry: { attributes: {} } })
    worker.respond({ type: 'decoded', id: worker.messages[0]!.id, geometry: { attributes: {} } })

    expect(await first).toEqual({ attributes: {} })
    expect(await second).toEqual({ attributes: {} })
  })

  test('isolates decode errors and rejects pending work when stopped', async () => {
    const worker = new FakeWorker()
    const client = new SpzWorkerClient(worker)
    const malformed = client.decode(new ArrayBuffer(1))
    worker.respond({ type: 'error', id: worker.messages[0]!.id, message: 'Invalid SPZ header.' })
    expect(malformed).rejects.toThrow('Invalid SPZ header')

    const pending = client.decode(new ArrayBuffer(1))
    client.stop()
    expect(pending).rejects.toThrow('stopped')
    expect(worker.terminated).toBe(true)
  })
})
