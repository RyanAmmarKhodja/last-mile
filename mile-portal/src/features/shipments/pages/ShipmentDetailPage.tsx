import { Link, useLocation, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { fleetApi, getErrorStatus, trackingApi, shipmentsApi } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { qk } from '@/shared/query/queryClient';
import { Alert, Card, QueryState, ShipmentStatusBadge } from '@/shared/components';
import { formatDateTime, formatWeight, fullName, shortId } from '@/shared/utils/format';
import { ACTIVE_STATUSES, type Shipment } from '@/shared/types/shipments';
import { useDriverDirectory } from '@/features/tracking';
import { LiveDeliveryMap } from '@/features/tracking/components/LiveDeliveryMap';
import { ShipmentTimeline } from '../components/ShipmentTimeline';
import { ShipmentActions } from '../components/ShipmentActions';
import { CopyTrackingLink } from '../components/CopyLink';
import { RouteMap } from '../components/RouteMap';
import { useClientProfile } from '../hooks/useMyClientProfile';
import '../styles/shipments.css';
import '@/features/tracking/styles/tracking.css';

export function ShipmentDetailPage() {
  const { id = '' } = useParams();
  const { user } = useAuth();
  const created = (useLocation().state as { created?: boolean } | null)?.created;
  const isAdmin = user?.role === 'ADMIN';
  const isDriver = user?.role === 'DRIVER';

  const shipment = useQuery({
    queryKey: qk.shipment(id),
    queryFn: () => shipmentsApi.get(id),
    // Keep the timeline fresh while the shipment moves (another actor may change it).
    refetchInterval: (q) => (q.state.data && !['DELIVERED', 'FAILED', 'CANCELLED'].includes(q.state.data.status) ? 15_000 : false),
  });
  const myDriver = useQuery({ queryKey: qk.myDriver, queryFn: fleetApi.me, enabled: isDriver, staleTime: 60_000 });

  const s = shipment.data;
  const status = getErrorStatus(shipment.error);
  if (status === 404 || status === 403)
    return (
      <div className="stack">
        <Alert variant="error">{status === 404 ? 'This shipment does not exist.' : 'You are not a party to this shipment.'}</Alert>
        <Link to="/shipments">Back to shipments</Link>
      </div>
    );

  return (
    <QueryState isLoading={shipment.isLoading} error={shipment.error} errorPrefix="Shipment unavailable">
      {s && <Detail s={s} created={!!created} isAdmin={isAdmin} isAssignedDriver={!!myDriver.data && myDriver.data.id === s.assignedDriverId} userId={user?.id} />}
    </QueryState>
  );
}

function Detail({ s, created, isAdmin, isAssignedDriver, userId }: { s: Shipment; created: boolean; isAdmin: boolean; isAssignedDriver: boolean; userId?: string }) {
  const active = ACTIVE_STATUSES.includes(s.status);
  const sender = useClientProfile(s.senderId, isAdmin);
  const recipient = useClientProfile(s.recipientId, isAdmin);
  const drivers = useDriverDirectory(isAdmin);
  const driver = s.assignedDriverId ? drivers.byId.get(s.assignedDriverId) : undefined;
  const route = useQuery({
    queryKey: qk.shipmentRoute(s.id),
    queryFn: () => trackingApi.shipmentRoute(s.id),
    enabled: isAdmin && s.status !== 'CREATED' && s.status !== 'CANCELLED',
    refetchInterval: active ? 120_000 : false,
  });
  const routePoints = (route.data ?? []).map((p) => [p.longitude, p.latitude] as [number, number]);

  const actorLabel = (actorId: string) => {
    if (actorId === userId) return 'you';
    if (actorId === s.senderUserId) return 'sender';
    if (actorId === s.recipientUserId) return 'recipient';
    if (actorId === 'system') return 'system';
    if (!isAdmin) return null;
    const driverActor = drivers.data?.find((d) => d.userId === actorId);
    return driverActor ? `driver ${fullName(driverActor)}` : `by ${shortId(actorId)}`;
  };
  const youAre = s.senderUserId === userId ? 'the sender' : s.recipientUserId === userId ? 'the recipient' : null;

  return (
    <div className="stack">
      {created && <Alert variant="success">Shipment created. Share the tracking link with the recipient.</Alert>}
      <div className="page-header row-between">
        <div>
          <div className="row">
            <span className="tracking-number">{s.trackingNumber}</span>
            <ShipmentStatusBadge status={s.status} />
          </div>
          <p>
            Created {formatDateTime(s.createdAt)}
            {youAre && ` · you are ${youAre}`}
          </p>
        </div>
        <div className="row">
          <CopyTrackingLink trackingNumber={s.trackingNumber} />
          <ShipmentActions shipment={s} isAssignedDriver={isAssignedDriver} />
        </div>
      </div>

      <div className="shipment-layout">
        <div className="stack">
          {active ? (
            <Card title="Live delivery" subtitle="The vehicle carrying this parcel, in real time.">
              <LiveDeliveryMap trackingNumber={s.trackingNumber} status={s.status} route={isAdmin ? routePoints : undefined} />
            </Card>
          ) : isAdmin && routePoints.length > 1 ? (
            <Card title="Route" subtitle="Where the driver went while carrying this shipment.">
              <RouteMap points={routePoints} fitKey={s.id} />
            </Card>
          ) : s.status === 'CREATED' ? (
            <Alert variant="info">The live map appears here once a driver has been dispatched.</Alert>
          ) : null}

          <Card title="Details">
            <dl className="details">
              <dt>From</dt>
              <dd className="address-block">
                {isAdmin && sender.data && <strong>{fullName(sender.data)}<br /></strong>}
                {s.originAddress}
              </dd>
              <dt>To</dt>
              <dd className="address-block">
                {isAdmin && recipient.data && <strong>{fullName(recipient.data)}<br /></strong>}
                {s.destinationAddress}
              </dd>
              <dt>Weight</dt>
              <dd>{formatWeight(s.weightKg)}</dd>
              {isAdmin && (
                <>
                  <dt>Driver</dt>
                  <dd>
                    {s.assignedDriverId
                      ? driver
                        ? `${fullName(driver)} · ${driver.phoneNumber}`
                        : shortId(s.assignedDriverId)
                      : '—'}
                  </dd>
                  <dt>Vehicle</dt>
                  <dd>{s.assignedVehicleId ? (driver?.vehicle?.id === s.assignedVehicleId ? `${driver.vehicle.licensePlate} · ${driver.vehicle.model}` : shortId(s.assignedVehicleId)) : '—'}</dd>
                  {sender.data && (
                    <>
                      <dt>Sender phone</dt>
                      <dd>{sender.data.phoneNumber}</dd>
                    </>
                  )}
                  {recipient.data && (
                    <>
                      <dt>Recipient phone</dt>
                      <dd>{recipient.data.phoneNumber}</dd>
                    </>
                  )}
                </>
              )}
              <dt>Last update</dt>
              <dd>{formatDateTime(s.updatedAt)}</dd>
            </dl>
          </Card>
        </div>

        <Card title="Timeline">
          <ShipmentTimeline
            current={s.status}
            items={s.history.map((h) => ({ status: h.status, at: h.createdAt, notes: h.notes, actor: actorLabel(h.actorId) }))}
          />
        </Card>
      </div>
    </div>
  );
}
