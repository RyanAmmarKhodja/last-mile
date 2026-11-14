import { useEffect, useRef } from 'react';
import type { MapMouseEvent } from 'maplibre-gl';
import { useMap } from '@/shared/map';

/** Calls `onClick` for clicks on the map that did not hit one of `layers`. */
export function EmptyMapClick({ onClick, layers }: { onClick: () => void; layers: string[] }) {
  const map = useMap();
  const cb = useRef(onClick);
  cb.current = onClick;
  useEffect(() => {
    const handler = (e: MapMouseEvent) => {
      const present = layers.filter((l) => map.getLayer(l));
      if (present.length && map.queryRenderedFeatures(e.point, { layers: present }).length) return;
      cb.current();
    };
    map.on('click', handler);
    return () => void map.off('click', handler);
  }, [map, layers]);
  return null;
}

/** Keeps the camera on a point while `active`; jumps there once when the target changes. */
export function FollowPoint({ point, active, targetKey }: { point: [number, number] | null; active: boolean; targetKey: string | null }) {
  const map = useMap();
  const lastKey = useRef<string | null>(null);
  useEffect(() => {
    if (!point) return;
    if (targetKey !== lastKey.current) {
      lastKey.current = targetKey;
      map.easeTo({ center: point, zoom: Math.max(map.getZoom(), 14), duration: 600 });
    } else if (active) {
      map.easeTo({ center: point, duration: 800 });
    }
  }, [map, point, active, targetKey]);
  return null;
}
