import { useEffect, useRef } from 'react';
import maplibregl from 'maplibre-gl';
import { useMap } from './MapView';

interface Props {
  latitude: number;
  longitude: number;
  headingDeg?: number | null;
  stale?: boolean;
  /** Keep the camera on the marker as it moves. */
  follow?: boolean;
  /** Animation length between two positions (ms). */
  durationMs?: number;
}

const ARROW_SVG =
  '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 2 20 21 12 16.5 4 21Z"/></svg>';

/** The single delivery vehicle on the client / public map, animated between pings (P4). */
export function DeliveryMarker({ latitude, longitude, headingDeg, stale = false, follow = false, durationMs = 900 }: Props) {
  const map = useMap();
  const markerRef = useRef<maplibregl.Marker | null>(null);
  const bodyRef = useRef<HTMLDivElement | null>(null);
  const current = useRef<[number, number] | null>(null);
  const anim = useRef<number | null>(null);
  const followRef = useRef(follow);
  followRef.current = follow;

  useEffect(() => {
    const el = document.createElement('div');
    el.className = 'delivery-marker';
    el.setAttribute('aria-label', 'Delivery vehicle');
    const body = document.createElement('div');
    body.className = 'delivery-marker-body';
    body.innerHTML = ARROW_SVG;
    el.appendChild(body);
    bodyRef.current = body;
    markerRef.current = new maplibregl.Marker({ element: el }).setLngLat([longitude, latitude]).addTo(map);
    current.current = [longitude, latitude];
    // jumpTo, not easeTo: follow mode re-centres every frame and would cancel an animated zoom.
    map.jumpTo({ center: [longitude, latitude], zoom: Math.max(map.getZoom(), 15) });
    return () => {
      if (anim.current) cancelAnimationFrame(anim.current);
      markerRef.current?.remove();
      markerRef.current = null;
    };
    // Created once; position changes are animated below.
  }, [map]);

  useEffect(() => {
    const marker = markerRef.current;
    const from = current.current;
    if (!marker || !from) return;
    const to: [number, number] = [longitude, latitude];
    if (anim.current) cancelAnimationFrame(anim.current);
    const start = performance.now();
    const step = (t: number) => {
      const k = Math.min(1, (t - start) / durationMs);
      const e = k * (2 - k); // ease-out
      const pos: [number, number] = [from[0] + (to[0] - from[0]) * e, from[1] + (to[1] - from[1]) * e];
      current.current = pos;
      marker.setLngLat(pos);
      if (followRef.current) map.setCenter(pos);
      anim.current = k < 1 ? requestAnimationFrame(step) : null;
    };
    anim.current = requestAnimationFrame(step);
  }, [map, latitude, longitude, durationMs]);

  useEffect(() => {
    if (bodyRef.current) bodyRef.current.style.transform = `rotate(${headingDeg ?? 0}deg)`;
  }, [headingDeg]);

  useEffect(() => {
    markerRef.current?.getElement().classList.toggle('stale', stale);
  }, [stale]);

  return null;
}
