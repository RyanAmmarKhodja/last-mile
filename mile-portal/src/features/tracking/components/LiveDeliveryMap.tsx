import { useState } from 'react';
import { DeliveryMarker, MapView, STALE_AFTER_MS, TrailLayer, MAP_COLORS } from '@/shared/map';
import { useNow } from '@/shared/hooks/useNow';
import { formatAgo, formatSpeed } from '@/shared/utils/format';
import { ACTIVE_STATUSES, type ShipmentStatus } from '@/shared/types/shipments';
import { usePublicTracking } from '../hooks/usePublicTracking';
import { ConnectionChip } from './ConnectionChip';
import '../styles/tracking.css';

interface Props {
  trackingNumber: string;
  /** Status known from ShipmentService; the stream's own status wins once it arrives. */
  status?: ShipmentStatus;
  /** Optional sampled route ([lon, lat]) drawn under the live tail (ADMIN route replay). */
  route?: [number, number][];
  className?: string;
}

/**
 * One delivery on a map, from the public tracking stream (P2): the same view for the signed-in client,
 * the public link and the shipment page. No driver identity is ever shown here (T3).
 */
export function LiveDeliveryMap({ trackingNumber, status: knownStatus, route, className }: Props) {
  const live = usePublicTracking(trackingNumber);
  const now = useNow(5000);
  const [follow, setFollow] = useState(true);
  const status = live.status ?? knownStatus ?? null;
  const active = status !== null && ACTIVE_STATUSES.includes(status);
  const p = live.position;
  const stale = !!p && now - Date.parse(p.recordedAt) > STALE_AFTER_MS;

  let message: string | null = null;
  if (live.connection === 'notfound') message = 'This tracking number is unknown to live tracking.';
  else if (status === 'CREATED') message = 'The live map starts when a driver is dispatched.';
  else if (status && !active) message = 'This delivery is finished, so its live position is no longer shared.';
  else if (active && !p) message = 'Waiting for the driver’s next GPS position…';

  return (
    <div className={['live-delivery', className].filter(Boolean).join(' ')}>
      <MapView
        overlay={
          <>
            <ConnectionChip state={active || live.connection !== 'live' ? live.connection : 'ended'} />
            {p && (
              <span className="map-chip">
                {formatSpeed(p.speedKmh)} · {formatAgo(p.recordedAt, now)}
              </span>
            )}
          </>
        }
      >
        {route && route.length > 1 && <TrailLayer points={route} color={MAP_COLORS.idle} dashed fitKey={p ? undefined : trackingNumber} endpoints />}
        {live.trail.length > 1 && <TrailLayer points={live.trail} />}
        {p && <DeliveryMarker latitude={p.latitude} longitude={p.longitude} headingDeg={p.headingDeg} stale={stale} follow={follow} />}
      </MapView>
      <div className="live-delivery-footer">
        <span className="muted">{message ?? 'Positions are shown as they arrive. The line is the path since you opened this page.'}</span>
        {p && (
          <label className="row nowrap">
            <input type="checkbox" checked={follow} onChange={(e) => setFollow(e.target.checked)} /> Follow
          </label>
        )}
      </div>
    </div>
  );
}
