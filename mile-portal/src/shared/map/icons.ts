import type { Map as MlMap } from 'maplibre-gl';

export const VEHICLE_ICON = 'lm-vehicle-arrow';

/**
 * Registers the vehicle arrow as an SDF image, so layers can tint it (icon-color) and rotate it (icon-rotate).
 * Drawn on a canvas: no sprite files to host.
 */
export function ensureMapIcons(map: MlMap) {
  if (map.hasImage(VEHICLE_ICON)) return;
  const size = 48;
  const ratio = 2;
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = size * ratio;
  const ctx = canvas.getContext('2d')!;
  ctx.scale(ratio, ratio);
  ctx.fillStyle = '#000';
  ctx.beginPath();
  // Arrow pointing north; MapLibre rotates it clockwise by the heading.
  ctx.moveTo(size / 2, 6);
  ctx.lineTo(size - 12, size - 8);
  ctx.lineTo(size / 2, size - 16);
  ctx.lineTo(12, size - 8);
  ctx.closePath();
  ctx.fill();
  const image = ctx.getImageData(0, 0, canvas.width, canvas.height);
  map.addImage(VEHICLE_ICON, image, { pixelRatio: ratio, sdf: true });
}
