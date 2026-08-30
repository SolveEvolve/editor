import { describe, expect, test } from 'bun:test'
import {
  type CameraNavigationKeyState,
  hasCameraNavigationInput,
  isCameraNavigationKey,
  setCameraNavigationKey,
} from './camera-navigation'

const createState = (): CameraNavigationKeyState => ({
  forward: false,
  backward: false,
  left: false,
  right: false,
  up: false,
  down: false,
})

describe('camera navigation keys', () => {
  test('maps Q upward and Z downward without claiming Page Up or Page Down', () => {
    const state = createState()

    expect(setCameraNavigationKey(state, 'KeyQ', true)).toBe(true)
    expect(state.up).toBe(true)
    expect(setCameraNavigationKey(state, 'KeyZ', true)).toBe(true)
    expect(state.down).toBe(true)
    expect(isCameraNavigationKey('PageUp')).toBe(false)
    expect(isCameraNavigationKey('PageDown')).toBe(false)
  })

  test('supports combined movement and reports state changes once', () => {
    const state = createState()

    expect(setCameraNavigationKey(state, 'KeyW', true)).toBe(true)
    expect(setCameraNavigationKey(state, 'KeyQ', true)).toBe(true)
    expect(setCameraNavigationKey(state, 'KeyQ', true)).toBe(false)
    expect(hasCameraNavigationInput(state)).toBe(true)

    setCameraNavigationKey(state, 'KeyW', false)
    setCameraNavigationKey(state, 'KeyQ', false)
    expect(hasCameraNavigationInput(state)).toBe(false)
  })
})
