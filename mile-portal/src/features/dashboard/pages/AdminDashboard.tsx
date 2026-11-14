import { Link } from 'react-router-dom';
import { useQueries, useQuery } from '@tanstack/react-query';
import { fleetApi, getErrorMessage, shipmentsApi } from '@/shared/api';
import { qk } from '@/shared/query/queryClient';
import { MapView, VehicleLayer } from '@/shared/map';
import { useNow } from '@/shared/hooks/useNow';
import { Alert, Button, Card, EmptyState, KpiTile, ShipmentStatusBadge } from '@/shared/components';
import { formatAgo, formatWeight } from '@/shared/utils/format';
import { SHIPMENT_STATUSES, type ShipmentStatus, type ShipmentSummary } from '@/shared/types/shipments';
import { ConnectionChip, useDriverDirectory, useFleetLive } from '@/features/tracking';
import { useShipmentMutation } from '@/features/shipments/hooks/useShipmentMutation';
import '@/features/tracking/styles/tracking.css';
import '@/features/shipments/styles/shipments.css';
import '../styles/dashboard.css';

/** FR-7: operations overview for ADMIN. */
export function AdminDashboard() {
  const fleet = useFleetLive();
  const drivers = useDriverDirectory();
  const now = useNow(5000);

  // Counts per status: one pageSize=1 request each, reading totalCount (no stats endpoint exists).
  const counts = useQueries({
    queries: SHIPMENT_STATUSES.map((status) => ({
      queryKey: qk.shipmentCount({ status }),
      queryFn: () => shipmentsApi.list({ status, pageSize: 1 }).then((r) => r.totalCount),
      refetchInterval: 30_000,
    })),
  });
  const count = (s: ShipmentStatus) => counts[SHIPMENT_STATUSES.indexOf(s)]?.data;
  const countsLoading = counts.some((c) => c.isLoading);
  const countsError = counts.find((c) => c.error)?.error;

  const available = useQuery({
    queryKey: qk.drivers({ available: true, pageSize: 1 }),
    queryFn: () => fleetApi.listDrivers({ available: true, pageSize: 1 }).then((r) => r.totalCount),
    refetchInterval: 30_000,
  });
  const waiting = useQuery({
    queryKey: qk.shipmentList({ status: 'CREATED', pageSize: 6, page: 1 }),
    queryFn: () => shipmentsApi.list({ status: 'CREATED', pageSize: 6, page: 1 }),
    refetchInterval: 30_000,
  });

  const delivering = fleet.positions.filter((p) => p.shipmentId).length;
  const outForDelivery = (count('DISPATCHED') ?? 0) + (count('IN_TRANSIT') ?? 0);

  return (
    <div className="stack">
      <div className="page-header">
        <h1>Operations</h1>
        <p>Live fleet and shipment flow across the platform.</p>
      </div>

      <div className="kpi-row">
        <KpiTile label="Vehicles on the map" value={fleet.positions.length} hint={`${delivering} delivering`} to="/map" loading={fleet.connection === 'connecting'} />
        <KpiTile label="Drivers available" value={available.data} hint="on shift, no shipment" to="/fleet/drivers" loading={available.isLoading} />
        <KpiTile label="Waiting for dispatch" value={count('CREATED')} to="/shipments?status=CREATED" loading={countsLoading} />
        <KpiTile label="Out for delivery" value={countsLoading ? undefined : outForDelivery} hint="dispatched or in transit" to="/shipments?status=IN_TRANSIT" loading={countsLoading} />
      </div>

      <div className="dash-grid">
        <div className="stack">
          <Card
            title="Live fleet"
            subtitle={fleet.lastUpdateAt ? `Updated ${formatAgo(fleet.lastUpdateAt, now)}` : undefined}
            actions={
              <Link to="/map" className="btn btn-secondary btn-sm">
                Open full map
              </Link>
            }
          >
            <div className="mini-map">
              <MapView overlay={<ConnectionChip state={fleet.connection} />}>
                <VehicleLayer positions={fleet.positions} now={now} labelFor={(p) => drivers.byId.get(p.driverId)?.vehicle?.licensePlate ?? ''} />
              </MapView>
            </div>
          </Card>

          <Card title="Waiting for dispatch" actions={<Link to="/shipments?status=CREATED">See all</Link>}>
            {waiting.error ? (
              <Alert variant="error">{getErrorMessage(waiting.error)}</Alert>
            ) : waiting.data?.items.length === 0 ? (
              <EmptyState title="Nothing to dispatch.">New shipments appear here.</EmptyState>
            ) : (
              <div className="table-wrap">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Tracking number</th>
                      <th>Destination</th>
                      <th>Weight</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {(waiting.data?.items ?? []).map((s) => (
                      <WaitingRow key={s.id} s={s} />
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </div>

        <Card title="Shipments by status">
          {countsError ? (
            <Alert variant="error">ShipmentService is unavailable: {getErrorMessage(countsError)}</Alert>
          ) : (
            <ul className="status-list">
              {SHIPMENT_STATUSES.map((s) => (
                <li key={s}>
                  <Link to={`/shipments?status=${s}`}>
                    <ShipmentStatusBadge status={s} />
                    <span className="status-count">{count(s) ?? '…'}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </div>
  );
}

function WaitingRow({ s }: { s: ShipmentSummary }) {
  const dispatch = useShipmentMutation(() => shipmentsApi.dispatch(s.id, null));
  return (
    <tr>
      <td className="mono nowrap">
        <Link to={`/shipments/${s.id}`}>{s.trackingNumber}</Link>
      </td>
      <td className="shipment-dest">{s.destinationAddress}</td>
      <td className="nowrap">{formatWeight(s.weightKg)}</td>
      <td className="nowrap">
        {dispatch.error ? (
          <span className="field-error" title={getErrorMessage(dispatch.error)}>
            {getErrorMessage(dispatch.error)}
          </span>
        ) : (
          <Button size="sm" loading={dispatch.isPending} onClick={() => dispatch.mutate(undefined)} title="Auto-pick the smallest available vehicle that fits">
            Auto-dispatch
          </Button>
        )}
      </td>
    </tr>
  );
}
