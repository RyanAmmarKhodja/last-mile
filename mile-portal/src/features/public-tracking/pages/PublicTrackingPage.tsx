import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getErrorStatus, shipmentsApi } from '@/shared/api';
import { qk } from '@/shared/query/queryClient';
import { Alert, Card, QueryState, ShipmentStatusBadge } from '@/shared/components';
import { formatDateTime } from '@/shared/utils/format';
import { ACTIVE_STATUSES, FINAL_STATUSES } from '@/shared/types/shipments';
import { LiveDeliveryMap } from '@/features/tracking/components/LiveDeliveryMap';
import { ShipmentTimeline } from '@/features/shipments';
import { TrackForm } from './TrackForm';
import '@/features/shipments/styles/shipments.css';

/** FR-3: public tracking page. Shows only what ShipmentService S3 and FleetTrackingService T3 make public. */
export function PublicTrackingPage() {
  const { trackingNumber = '' } = useParams();
  const tn = trackingNumber.toUpperCase();
  const tracking = useQuery({
    queryKey: qk.publicShipment(tn),
    queryFn: () => shipmentsApi.track(tn),
    refetchInterval: (q) => (q.state.data && FINAL_STATUSES.includes(q.state.data.status) ? false : 20_000),
  });
  const status = getErrorStatus(tracking.error);

  return (
    <div className="stack">
      <TrackForm initial={tn} />
      {status === 404 ? (
        <Alert variant="error">No shipment has the tracking number {tn}. Check for typos.</Alert>
      ) : status === 429 ? (
        <Alert variant="warning">Too many requests. Wait a few seconds and try again.</Alert>
      ) : (
        <QueryState isLoading={tracking.isLoading} error={tracking.error} errorPrefix="Tracking is unavailable">
          {tracking.data && (
            <>
              <div className="row">
                <span className="tracking-number">{tracking.data.trackingNumber}</span>
                <ShipmentStatusBadge status={tracking.data.status} />
                <span className="muted">Updated {formatDateTime(tracking.data.updatedAt)}</span>
              </div>
              <div className="shipment-layout">
                {ACTIVE_STATUSES.includes(tracking.data.status) || tracking.data.status === 'CREATED' ? (
                  <Card title="Live map">
                    <LiveDeliveryMap className="tall" trackingNumber={tracking.data.trackingNumber} status={tracking.data.status} />
                  </Card>
                ) : (
                  <Card title={tracking.data.status === 'DELIVERED' ? 'Delivered' : 'Delivery finished'}>
                    <p className="muted">The vehicle’s position is only shared while the parcel is on its way.</p>
                  </Card>
                )}
                <Card title="Progress">
                  <ShipmentTimeline current={tracking.data.status} items={tracking.data.timeline.map((t) => ({ status: t.status, at: t.at }))} />
                </Card>
              </div>
            </>
          )}
        </QueryState>
      )}
    </div>
  );
}
