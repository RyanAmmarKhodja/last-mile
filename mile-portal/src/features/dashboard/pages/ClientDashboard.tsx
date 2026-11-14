import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQueries, useQuery } from '@tanstack/react-query';
import { shipmentsApi } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { qk } from '@/shared/query/queryClient';
import { Button, Card, EmptyState, KpiTile, QueryState, ShipmentStatusBadge } from '@/shared/components';
import type { ShipmentSummary } from '@/shared/types/shipments';
import { LiveDeliveryMap } from '@/features/tracking/components/LiveDeliveryMap';
import { ShipmentTable, useMyClientProfile } from '@/features/shipments';
import '@/features/shipments/styles/shipments.css';
import '../styles/dashboard.css';

/** FR-1: the signed-in customer's current deliveries, live, plus their recent shipments. */
export function ClientDashboard() {
  const { user } = useAuth();
  const me = useMyClientProfile();
  const [selected, setSelected] = useState<string | null>(null);

  const [inTransit, dispatched, created, delivered] = useQueries({
    queries: (['IN_TRANSIT', 'DISPATCHED', 'CREATED', 'DELIVERED'] as const).map((status) => ({
      queryKey: qk.shipmentList({ status, pageSize: 20, page: 1 }),
      queryFn: () => shipmentsApi.list({ status, pageSize: 20, page: 1 }),
      refetchInterval: 20_000,
    })),
  });
  const recent = useQuery({ queryKey: qk.shipmentList({ page: 1, pageSize: 5 }), queryFn: () => shipmentsApi.list({ page: 1, pageSize: 5 }) });

  const active: ShipmentSummary[] = [...(inTransit.data?.items ?? []), ...(dispatched.data?.items ?? [])];
  const current = active.find((s) => s.id === selected) ?? active[0] ?? null;
  const roleIn = (s: ShipmentSummary) => (me.data ? (s.senderId === me.data.id ? 'You send' : 'For you') : null);

  return (
    <div className="stack">
      <div className="page-header row-between">
        <div>
          <h1>Hello{me.data ? `, ${me.data.firstName}` : ` ${user?.email.split('@')[0]}`}</h1>
          <p>Follow your deliveries live, or send a new parcel.</p>
        </div>
        <Link to="/shipments/new" className="btn btn-primary">
          Send a parcel
        </Link>
      </div>

      <div className="kpi-row">
        <KpiTile label="On the way" value={active.length} hint="dispatched or in transit" loading={inTransit.isLoading || dispatched.isLoading} />
        <KpiTile label="Waiting for a driver" value={created.data?.totalCount} to="/shipments?status=CREATED" loading={created.isLoading} />
        <KpiTile label="Delivered" value={delivered.data?.totalCount} to="/shipments?status=DELIVERED" loading={delivered.isLoading} />
      </div>

      <Card
        title="Live delivery"
        subtitle={current ? `${roleIn(current) ?? ''}${roleIn(current) ? ' · ' : ''}${current.destinationAddress}` : 'Your parcels appear here while a driver carries them.'}
        actions={
          current && (
            <Link to={`/shipments/${current.id}`} className="btn btn-secondary btn-sm">
              Details
            </Link>
          )
        }
      >
        <QueryState isLoading={inTransit.isLoading || dispatched.isLoading} error={inTransit.error ?? dispatched.error} errorPrefix="Shipments are unavailable">
          {current ? (
            <>
              {active.length > 1 && (
                <div className="delivery-switch" role="group" aria-label="Active deliveries">
                  {active.map((s) => (
                    <Button key={s.id} size="sm" variant={s.id === current.id ? 'primary' : 'ghost'} onClick={() => setSelected(s.id)}>
                      <span className="mono">{s.trackingNumber}</span>
                    </Button>
                  ))}
                </div>
              )}
              <div className="row" style={{ marginBottom: '0.75rem' }}>
                <span className="mono">{current.trackingNumber}</span>
                <ShipmentStatusBadge status={current.status} />
              </div>
              <LiveDeliveryMap key={current.trackingNumber} trackingNumber={current.trackingNumber} status={current.status} />
            </>
          ) : (
            <EmptyState title="No delivery on the way right now.">
              {created.data?.totalCount ? `${created.data.totalCount} shipment(s) waiting for a driver.` : <Link to="/shipments/new">Send a parcel</Link>}
            </EmptyState>
          )}
        </QueryState>
      </Card>

      <Card title="Recent shipments" actions={<Link to="/shipments">See all</Link>}>
        <QueryState isLoading={recent.isLoading} error={recent.error}>
          {recent.data?.items.length ? (
            <ShipmentTable items={recent.data.items} extra={me.data ? { header: 'You are', cell: (s) => (s.senderId === me.data!.id ? 'Sender' : 'Recipient') } : undefined} />
          ) : (
            <EmptyState title="No shipments yet." />
          )}
        </QueryState>
      </Card>
    </div>
  );
}
