import { useEffect, useId, useRef } from 'react';
import type { GeoJSONSource } from 'maplibre-gl';
import type { Feature } from 'geojson';
import { useMap } from './MapView';
import { fitToPoints } from './VehicleLayer';
import { MAP_COLORS } from './mapConfig';

interface Props {
  /** [lon, lat] in time order. */
  points: [number, number][];
  color?: string;
  dashed?: boolean;
  /** Fit the camera to the trail whenever `fitKey` changes (e.g. a new shipment / driver selected). */
  fitKey?: string | null;
  /** Draw start and end dots. */
  endpoints?: boolean;
}

/** A route line: sampled history plus the live tail (§4.2). Placed under the vehicle layers. */
export function TrailLayer({ points, color = MAP_COLORS.trail, dashed = false, fitKey, endpoints = false }: Props) {
  const map = useMap();
  const id = `lm-trail-${useId().replace(/:/g, '')}`;
  const lastFit = useRef<string | null | undefined>(undefined);

  useEffect(() => {
    map.addSource(id, { type: 'geojson', data: { type: 'FeatureCollection', features: [] } });
    // Insert below the vehicles if they are already on the map.
    const before = map.getLayer('lm-vehicles-halo') ? 'lm-vehicles-halo' : undefined;
    map.addLayer(
      {
        id: `${id}-casing`,
        type: 'line',
        source: id,
        filter: ['==', ['geometry-type'], 'LineString'],
        layout: { 'line-join': 'round', 'line-cap': 'round' },
        paint: { 'line-color': '#ffffff', 'line-width': 7, 'line-opacity': 0.8 },
      },
      before,
    );
    map.addLayer(
      {
        id: `${id}-line`,
        type: 'line',
        source: id,
        filter: ['==', ['geometry-type'], 'LineString'],
        layout: { 'line-join': 'round', 'line-cap': 'round' },
        paint: { 'line-color': color, 'line-width': 4, ...(dashed ? { 'line-dasharray': [1.5, 1.5] } : {}) },
      },
      before,
    );
    map.addLayer(
      {
        id: `${id}-ends`,
        type: 'circle',
        source: id,
        filter: ['==', ['geometry-type'], 'Point'],
        paint: {
          'circle-radius': 5,
          'circle-color': ['case', ['==', ['get', 'end'], 'start'], '#ffffff', color],
          'circle-stroke-color': color,
          'circle-stroke-width': 2,
        },
      },
      before,
    );
    return () => {
      if (!map.getStyle()) return;
      [`${id}-ends`, `${id}-line`, `${id}-casing`].forEach((l) => map.getLayer(l) && map.removeLayer(l));
      if (map.getSource(id)) map.removeSource(id);
    };
  }, [map, id, color, dashed]);

  useEffect(() => {
    const source = map.getSource<GeoJSONSource>(id);
    if (!source) return;
    const features: Feature[] = [];
    if (points.length >= 2)
      features.push({ type: 'Feature', geometry: { type: 'LineString', coordinates: points }, properties: {} });
    if (endpoints && points.length >= 1)
      features.push({ type: 'Feature', geometry: { type: 'Point', coordinates: points[0] }, properties: { end: 'start' } });
    source.setData({ type: 'FeatureCollection', features });

    if (fitKey !== undefined && fitKey !== lastFit.current && points.length > 0) {
      lastFit.current = fitKey;
      fitToPoints(map, points);
    }
  }, [map, id, points, fitKey, endpoints, color, dashed]);

  return null;
}
