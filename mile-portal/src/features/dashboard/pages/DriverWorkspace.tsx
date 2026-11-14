import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fleetApi, getErrorMessage, getErrorStatus, shipmentsApi, trackingApi } from '@/shared/api';
import { qk } from '@/shared/query/queryClient';
import { DeliveryMarker, MapView, STALE_AFTER_MS, TrailLayer } from '@/shared/map';
import { useNow } from '@/shared/hooks/useNow';
import { Alert, Button, Card, EmptyState, QueryState, ShipmentStatusBadge } from '@/shared/components';
import { formatAgo, formatSpeed, formatWeight } from '@/shared/utils/format';
import { ShipmentActions } from '@/features/shipments';
import { ConnectionChip } from '@/features/tracking';
import '@/features/tracking/styles/tracking.css';
import '../styles/dashboard.css';

/** FR-8: a driver's shift, current job and own position. */
export function DriverWorkspace() {
  const client = useQueryClient();
  const now = useNow(5000);
  const me = useQuery({ queryKey: qk.myDriver, queryFn: fleetApi.me, refetchInterval: 20_000 });

  // At most one active shipment per driver (ux_shipments_driver_active): in transit first, else dispatched.
  const job = useQuery({
    queryKey: qk.shipmentList({ mine: 'active' }),
    queryFn: async () => {
      const [inTransit, dispatched] = await Promise.all([
        shipmentsApi.list({ status: 'IN_TRANSIT', pageSize: 1 }),
        shipmentsApi.list({ status: 'DISPATCHED', pageSize: 1 }),
      ]);
      const summary = inTransit.items[0] ?? dispatched.items[0];
      return summary ? shipmentsApi.get(summary.id) : null;
    },
    refetchInterval: 15_000,
  });

  const live = useQuery({ queryKey: qk.myLive, queryFn: trackingApi.myLive, refetchInterval: 3000, retry: false });
  const historyFrom = useMemo(() => new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(), []);
  const history = useQuery({ queryKey: ['tracking', 'me', 'history', historyFrom], queryFn: () => trackingApi.myHistory({ from: historyFrom }), staleTime: 120_000 });

  const shift = useMutation({
    mutationFn: (value: boolean) => fleetApi.setMyAvailability(value),
    onSuccess: (d) => {
      client.setQueryData(qk.myDriver, d);
      void client.invalidateQueries({ queryKey: qk.tracking });
    },
  });

  if (getErrorStatus(me.error) === 404)
    return (
      <div className="stack">
        <div className="page-header">
          <h1>Driver workspace</h1>
        </div>
        <Alert variant="warning">
          Your driver profile is not ready yet. If you just registered, wait a few seconds and reload; otherwise complete it in{' '}
          <Link to="/account">Account</Link>.
        </Alert>
      </div>
    );

  const d = me.data;
  const s = job.data ?? null;
  const p = live.data;
  const trail = [...(history.data ?? []).map((h) => [h.longitude, h.latitude] as [number, number]), ...(p ? [[p.longitude, p.latitude] as [number, number]] : [])];
  const onDelivery = !!s;
  const state = !d ? '…' : onDelivery ? 'On a delivery' : d.isAvailable ? 'On shift, waiting for a job' : 'Off shift';

  return (
    <div className="stack">
      <div className="page-header">
        <h1>Driver workspace</h1>
        <p>Start your shift to receive deliveries. Your GPS position is only recorded while you are on shift.</p>
      </div>

      <div className="dash-grid">
        <div className="stack">
          <Card title="Your position">
            <div className="live-delivery">
              <MapView overlay={<ConnectionChip
                    state={p && (onDelivery || d?.isAvailable) ? 'live' : live.isLoading ? 'connecting' : 'ended'}
                    extra={p ? `last ping ${formatAgo(p.receivedAt, now)}` : undefined}
                  />}>
                {trail.length > 1 && <TrailLayer points={trail} />}
                {p && <DeliveryMarker latitude={p.latitude} longitude={p.longitude} headingDeg={p.headingDeg} stale={now - Date.parse(p.receivedAt) > STALE_AFTER_MS} />}
              </MapView>
              <span className="muted" style={{ fontSize: '0.85rem' }}>
                {p
                  ? `${formatSpeed(p.speedKmh)} · the line shows the last 2 hours.`
                  : 'No GPS ping in the last 10 minutes. Positions come from the driver app or the telemetry simulator.'}
              </span>
            </div>
          </Card>
        </div>

        <div className="stack">
          <Card title="Shift">
            <QueryState isLoading={me.isLoading} error={me.error} errorPrefix="FleetService is unavailable">
              {d && (
                <div className="stack" style={{ gap: '0.75rem' }}>
                  <div className="shift-state">
                    <span className={`live-dot ${onDelivery || d.isAvailable ? 'on' : ''}`} aria-hidden="true" />
                    {state}
                  </div>
                  <dl className="details">
                    <dt>Vehicle</dt>
                    <dd>{d.vehicle ? `${d.vehicle.licensePlate} · ${d.vehicle.model} (${formatWeight(d.vehicle.capacityKg)})` : 'None assigned. Ask an administrator.'}</dd>
                    <dt>Account</dt>
                    <dd>{d.status === 'ACTIVE' ? 'Active' : d.status}</dd>
                  </dl>
                  {!onDelivery && (
                    <Button
                      variant={d.isAvailable ? 'ghost' : 'primary'}
                      loading={shift.isPending}
                      disabled={!d.isAvailable && (!d.vehicle || d.status !== 'ACTIVE')}
                      onClick={() => shift.mutate(!d.isAvailable)}
                    >
                      {d.isAvailable ? 'End shift' : 'Start shift'}
                    </Button>
                  )}
                  {onDelivery && <span className="muted">You can end your shift once the current delivery is finished.</span>}
                  {shift.error && <Alert variant="error">{getErrorMessage(shift.error)}</Alert>}
                </div>
              )}
            </QueryState>
          </Card>

          <Card title="Current delivery" className="job-card">
            <QueryState isLoading={job.isLoading} error={job.error} errorPrefix="ShipmentService is unavailable">
              {s ? (
                <>
                  <div className="row" style={{ marginBottom: '0.75rem' }}>
                    <Link to={`/shipments/${s.id}`} className="mono">
                      {s.trackingNumber}
                    </Link>
                    <ShipmentStatusBadge status={s.status} />
                  </div>
                  <dl className="details">
                    <dt>{s.status === 'DISPATCHED' ? 'Pick up at' : 'Picked up at'}</dt>
                    <dd>{s.originAddress}</dd>
                    <dt>Deliver to</dt>
                    <dd>{s.destinationAddress}</dd>
                    <dt>Weight</dt>
                    <dd>{formatWeight(s.weightKg)}</dd>
                    {s.history[0]?.notes && (
                      <>
                        <dt>Notes</dt>
                        <dd>{s.history[0].notes}</dd>
                      </>
                    )}
                  </dl>
                  <ShipmentActions shipment={s} isAssignedDriver />
                </>
              ) : (
                <EmptyState title="No delivery assigned.">{d?.isAvailable ? 'An administrator will dispatch one to you.' : 'Start your shift to receive deliveries.'}</EmptyState>
              )}
            </QueryState>
          </Card>
        </div>
      </div>
    </div>
  );
}
