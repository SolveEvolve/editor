import { describe, expect, mock, test } from 'bun:test'
import { decodeSpzWithFallback } from './worker-spz-loader'

describe('worker-backed SPZ decoding', () => {
  test('uses the worker result without starting the fallback', async () => {
    const fallback = mock(async () => 'fallback')
    const result = await decodeSpzWithFallback(
      new ArrayBuffer(4),
      async () => 'worker',
      fallback,
      () => {},
      20,
    )

    expect(result).toBe('worker')
    expect(fallback).not.toHaveBeenCalled()
  })

  test('falls back after a worker error', async () => {
    const result = await decodeSpzWithFallback(
      new ArrayBuffer(4),
      async () => {
        throw new Error('worker failed')
      },
      async (buffer) => `fallback:${buffer.byteLength}`,
      () => {},
      20,
    )

    expect(result).toBe('fallback:4')
  })

  test('recycles a timed-out worker and completes through the fallback', async () => {
    const timedOut = mock(() => {})
    const result = await decodeSpzWithFallback(
      new ArrayBuffer(4),
      () => new Promise(() => {}),
      async () => 'fallback',
      timedOut,
      1,
    )

    expect(result).toBe('fallback')
    expect(timedOut).toHaveBeenCalledTimes(1)
  })
})
