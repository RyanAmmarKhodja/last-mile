import { MapView, TrailLayer } from '@/shared/map';

/** Route replay of a finished shipment from the sampled history (FR-5, ADMIN). */
export function RouteMap({ points, fitKey }: { points: [number, number][]; fitKey: string }) {
  return (
    <div className="live-delivery">
      <MapView overlay={<span className="map-chip">Route replay · {points.length} sampled points</span>}>
        <TrailLayer points={points} fitKey={fitKey} endpoints />
      </MapView>
      <span className="muted" style={{ fontSize: '0.85rem' }}>
        One point every 2 minutes while the driver carried this shipment (kept 30 days).
      </span>
    </div>
  );
}
