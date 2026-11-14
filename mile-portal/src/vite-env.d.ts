/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** MapLibre style URL (default: OpenFreeMap "liberty"). */
  readonly VITE_MAP_STYLE_URL?: string;
  /** Initial map center as "lon,lat". */
  readonly VITE_MAP_CENTER?: string;
}
