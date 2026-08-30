declare module 'three/addons/loaders/GaussianSplatPLYLoader.js' {
  import { BufferGeometry, Loader } from 'three'

  export class GaussianSplatPLYLoader extends Loader<BufferGeometry> {}
}

declare module 'three/addons/loaders/KSPLATLoader.js' {
  import { BufferGeometry, Loader } from 'three'

  export class KSPLATLoader extends Loader<BufferGeometry> {}
}

declare module 'three/addons/loaders/SPLATLoader.js' {
  import { BufferGeometry, Loader } from 'three'

  export class SPLATLoader extends Loader<BufferGeometry> {}
}

declare module 'three/addons/loaders/SPZLoader.js' {
  import { BufferGeometry, Loader } from 'three'

  export class SPZLoader extends Loader<BufferGeometry> {
    parse(
      buffer: ArrayBuffer,
      onLoad?: (geometry: BufferGeometry) => void,
      onError?: (error: unknown) => void,
    ): BufferGeometry | Promise<BufferGeometry>
  }
}

declare module 'three/addons/objects/GaussianSplat.js' {
  import { BufferGeometry, Mesh } from 'three'

  export class GaussianSplat extends Mesh {
    readonly isGaussianSplat: true
    constructor(splatGeometry: BufferGeometry)
  }
}
