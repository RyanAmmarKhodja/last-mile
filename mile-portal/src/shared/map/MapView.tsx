import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import maplibregl, { type Map as MlMap } from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import { DEFAULT_CENTER, DEFAULT_ZOOM, MAP_STYLE_URL } from './mapConfig';
import { ensureMapIcons } from './icons';
import './map.css';

const MapContext = createContext<MlMap | null>(null);

/** The map instance, available to layer components once the style has loaded. */
export function useMap(): MlMap {
  const map = useContext(MapContext);
  if (!map) throw new Error('useMap must be used inside a loaded <MapView>.');
  return map;
}

interface Props {
  /** Layers, controls and overlays. Rendered only after the style is loaded. */
  children?: ReactNode;
  className?: string;
  center?: [number, number];
  zoom?: number;
  /** Overlay shown on top of the map (status chips, empty states). */
  overlay?: ReactNode;
}

/** MapLibre wrapper: one map per instance, resized with its container, layers as children (§4.2). */
export function MapView({ children, className, center = DEFAULT_CENTER, zoom = DEFAULT_ZOOM, overlay }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [map, setMap] = useState<MlMap | null>(null);
  const [styleError, setStyleError] = useState(false);
  // The initial view is applied once; later changes are the layers' job (fitBounds, follow).
  const initialView = useRef({ center, zoom });

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    const instance = new maplibregl.Map({
      container,
      style: MAP_STYLE_URL,
      center: initialView.current.center,
      zoom: initialView.current.zoom,
      attributionControl: { compact: true },
    });
    instance.addControl(new maplibregl.NavigationControl({ visualizePitch: false }), 'top-right');
    instance.addControl(new maplibregl.ScaleControl({ unit: 'metric' }), 'bottom-left');

    let loaded = false;
    instance.on('load', () => {
      loaded = true;
      ensureMapIcons(instance);
      setMap(instance);
    });
    instance.on('error', (e) => {
      // A style that never loads (offline, blocked) leaves an empty canvas: say so.
      if (!loaded && !instance.isStyleLoaded()) setStyleError(true);
      else if (import.meta.env.DEV) console.warn('[map]', e.error?.message ?? e);
    });

    const observer = new ResizeObserver(() => instance.resize());
    observer.observe(container);
    return () => {
      observer.disconnect();
      setMap(null);
      instance.remove();
    };
  }, []);

  return (
    <div className={['map-view', className].filter(Boolean).join(' ')}>
      <div ref={containerRef} className="map-canvas" />
      {styleError && (
        <div className="map-message">
          The base map could not be loaded. Check your internet connection; live positions still work.
        </div>
      )}
      {map && <MapContext.Provider value={map}>{children}</MapContext.Provider>}
      {overlay && <div className="map-overlay">{overlay}</div>}
    </div>
  );
}
