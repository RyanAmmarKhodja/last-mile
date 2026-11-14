import { useEffect, useRef } from 'react';
import maplibregl, { type GeoJSONSource, type MapLayerMouseEvent } from 'maplibre-gl';
import type { LivePosition } from '@/shared/types/tracking';
import { useMap } from './MapView';
import { VEHICLE_ICON } from './icons';
import { MAP_COLORS, STALE_AFTER_MS } from './mapConfig';

const SOURCE = 'lm-vehicles';
const HALO = 'lm-vehicles-halo';
const ARROW = 'lm-vehicles-arrow';
const LABEL = 'lm-vehicles-label';

interface Props {
  positions: LivePosition[];
  selectedId?: string | null;
  onSelect?: (driverId: string | null) => void;
  /** Label under each vehicle (plate or driver name), shown from zoom 12. */
  labelFor?: (p: LivePosition) => string;
  /** Fit the camera to the vehicles the first time there are any. */
  fitOnFirstData?: boolean;
  /** Re-evaluated every few seconds so vehicles fade when their last ping gets old. */
  now: number;
}

/**
 * Every live vehicle as one GeoJSON source (P4): a white halo plus a heading arrow tinted by state.
 * Data updates are coalesced to one setData per animation frame, so 1 000 vehicles at 1 Hz stay smooth.
 */
export function VehicleLayer({ positions, selectedId, onSelect, labelFor, fitOnFirstData = true, now }: Props) {
  const map = useMap();
  const frame = useRef<number | null>(null);
  const fitted = useRef(false);
  const onSelectRef = useRef(onSelect);
  onSelectRef.current = onSelect;

  // Sources and layers: once per map.
  useEffect(() => {
    map.addSource(SOURCE, { type: 'geojson', data: { type: 'FeatureCollection', features: [] } });
    map.addLayer({
      id: HALO,
      type: 'circle',
      source: SOURCE,
      paint: {
        'circle-radius': ['case', ['get', 'selected'], 15, 12],
        'circle-color': MAP_COLORS.halo,
        'circle-stroke-width': ['case', ['get', 'selected'], 3, 1.5],
        'circle-stroke-color': [
          'case',
          ['get', 'selected'],
          MAP_COLORS.selected,
          ['get', 'delivering'],
          MAP_COLORS.delivering,
          MAP_COLORS.idle,
        ],
        'circle-opacity': ['case', ['get', 'stale'], 0.55, 0.95],
        'circle-stroke-opacity': ['case', ['get', 'stale'], 0.5, 1],
      },
    });
    map.addLayer({
      id: ARROW,
      type: 'symbol',
      source: SOURCE,
      layout: {
        'icon-image': VEHICLE_ICON,
        'icon-size': 0.42,
        'icon-rotate': ['get', 'heading'],
        'icon-rotation-alignment': 'map',
        'icon-allow-overlap': true,
        'icon-ignore-placement': true,
      },
      paint: {
        'icon-color': [
          'case',
          ['get', 'selected'],
          MAP_COLORS.selected,
          ['get', 'delivering'],
          MAP_COLORS.delivering,
          MAP_COLORS.idle,
        ],
        'icon-opacity': ['case', ['get', 'stale'], 0.5, 1],
      },
    });
    map.addLayer({
      id: LABEL,
      type: 'symbol',
      source: SOURCE,
      minzoom: 12,
      layout: {
        'text-field': ['get', 'label'],
        'text-font': ['Noto Sans Bold'],
        'text-size': 11,
        'text-offset': [0, 1.6],
        'text-anchor': 'top',
        'text-optional': true,
      },
      paint: { 'text-color': '#1d1a2b', 'text-halo-color': '#ffffff', 'text-halo-width': 1.5 },
    });

    const click = (e: MapLayerMouseEvent) => {
      const id = e.features?.[0]?.properties?.driverId as string | undefined;
      if (id) {
        e.originalEvent.stopPropagation();
        onSelectRef.current?.(id);
      }
    };
    const enter = () => (map.getCanvas().style.cursor = 'pointer');
    const leave = () => (map.getCanvas().style.cursor = '');
    map.on('click', HALO, click);
    map.on('mouseenter', HALO, enter);
    map.on('mouseleave', HALO, leave);

    return () => {
      map.off('click', HALO, click);
      map.off('mouseenter', HALO, enter);
      map.off('mouseleave', HALO, leave);
      if (frame.current) cancelAnimationFrame(frame.current);
      // The map may already be gone when the page unmounts.
      if (map.getStyle()) {
        [LABEL, ARROW, HALO].forEach((id) => map.getLayer(id) && map.removeLayer(id));
        if (map.getSource(SOURCE)) map.removeSource(SOURCE);
      }
    };
  }, [map]);

  // Data: coalesced to one setData per frame.
  useEffect(() => {
    if (frame.current) cancelAnimationFrame(frame.current);
    frame.current = requestAnimationFrame(() => {
      frame.current = null;
      const source = map.getSource<GeoJSONSource>(SOURCE);
      if (!source) return;
      source.setData({
        type: 'FeatureCollection',
        features: positions.map((p) => ({
          type: 'Feature',
          id: p.driverId,
          geometry: { type: 'Point', coordinates: [p.longitude, p.latitude] },
          properties: {
            driverId: p.driverId,
            heading: p.headingDeg ?? 0,
            delivering: !!p.shipmentId,
            stale: now - Date.parse(p.receivedAt) > STALE_AFTER_MS,
            selected: p.driverId === selectedId,
            label: labelFor?.(p) ?? '',
          },
        })),
      });

      if (fitOnFirstData && !fitted.current && positions.length > 0) {
        fitted.current = true;
        fitToPoints(map, positions.map((p) => [p.longitude, p.latitude]));
      }
    });
  }, [map, positions, selectedId, labelFor, fitOnFirstData, now]);

  return null;
}

/** Fits the camera to the points (one point → fly there at street level). */
export function fitToPoints(map: maplibregl.Map, points: [number, number][], maxZoom = 15) {
  if (points.length === 0) return;
  if (points.length === 1) return void map.easeTo({ center: points[0], zoom: Math.max(map.getZoom(), 14) });
  const bounds = points.reduce((b, p) => b.extend(p), new maplibregl.LngLatBounds(points[0], points[0]));
  map.fitBounds(bounds, { padding: 60, maxZoom, duration: 600 });
}
