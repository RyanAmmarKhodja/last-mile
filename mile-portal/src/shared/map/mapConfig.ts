/** P1: MapLibre style (OpenFreeMap by default) and the initial view. Both are build-time settings. */
export const MAP_STYLE_URL: string = import.meta.env.VITE_MAP_STYLE_URL ?? 'https://tiles.openfreemap.org/styles/liberty';

function parseCenter(raw: string | undefined): [number, number] {
  const parts = (raw ?? '').split(',').map(Number);
  return parts.length === 2 && parts.every(Number.isFinite) ? [parts[0], parts[1]] : [3.0588, 36.7538]; // Algiers
}

/** [lon, lat] */
export const DEFAULT_CENTER = parseCenter(import.meta.env.VITE_MAP_CENTER);
export const DEFAULT_ZOOM = 11;

/** A live position older than this is drawn faded. */
export const STALE_AFTER_MS = 60_000;

export const MAP_COLORS = {
  delivering: '#512bd4',
  idle: '#6a6680',
  selected: '#0b5cad',
  trail: '#2470d8',
  halo: '#ffffff',
};
