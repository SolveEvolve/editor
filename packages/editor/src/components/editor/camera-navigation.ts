export type CameraNavigationKeyState = {
  forward: boolean
  backward: boolean
  left: boolean
  right: boolean
  up: boolean
  down: boolean
}

const KEY_TO_DIRECTION = {
  KeyW: 'forward',
  KeyS: 'backward',
  KeyA: 'left',
  KeyD: 'right',
  KeyQ: 'up',
  KeyZ: 'down',
} as const satisfies Record<string, keyof CameraNavigationKeyState>

export function setCameraNavigationKey(
  state: CameraNavigationKeyState,
  code: string,
  pressed: boolean,
): boolean {
  const direction = KEY_TO_DIRECTION[code as keyof typeof KEY_TO_DIRECTION]
  if (!direction) return false

  const changed = state[direction] !== pressed
  state[direction] = pressed
  return changed
}

export function isCameraNavigationKey(code: string): boolean {
  return code in KEY_TO_DIRECTION
}

export function hasCameraNavigationInput(state: CameraNavigationKeyState): boolean {
  return Object.values(state).some(Boolean)
}
