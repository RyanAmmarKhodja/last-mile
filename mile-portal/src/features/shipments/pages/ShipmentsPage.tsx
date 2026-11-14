import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { shipmentsApi } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { qk } from '@/shared/query/queryClient';
import { Button, Card, EmptyState, Pagination, QueryState, STATUS_LABEL } from '@/shared/components';
import { fullName, shortId } from '@/shared/utils/format';
import { SHIPMENT_STATUSES, type ListShipmentsParams, type ShipmentParty, type ShipmentStatus } from '@/shared/types/shipments';
import { useDriverDirectory } from '@/features/tracking';
import { ShipmentTable } from '../components/ShipmentTable';
import { useMyClientProfile } from '../hooks/useMyClientProfile';
import '../styles/shipments.css';

const PAGE_SIZE = 15;

/** FR-4: shipments the role can see (ADMIN all · CLIENT own · DRIVER assigned). Filters live in the URL. */
export function ShipmentsPage() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const role = user?.role;
  const status = (params.get('status') as ShipmentStatus | null) ?? undefined;
  const as = (params.get('as') as ShipmentParty | null) ?? undefined;
  const search = params.get('search') ?? '';
  const page = Number(params.get('page') ?? 1) || 1;
  const [searchInput, setSearchInput] = useState(search);

  const query: ListShipmentsParams = { page, pageSize: PAGE_SIZE, status, as, search: search || undefined };
  const list = useQuery({ queryKey: qk.shipmentList(query), queryFn: () => shipmentsApi.list(query), placeholderData: keepPreviousData });
  const drivers = useDriverDirectory(role === 'ADMIN');
  const me = useMyClientProfile();

  function setParam(key: string, value: string | undefined) {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    if (key !== 'page') next.delete('page');
    setParams(next, { replace: true });
  }

  function applySearch(e: FormEvent) {
    e.preventDefault();
    setParam('search', searchInput.trim() || undefined);
  }

  const extra =
    role === 'ADMIN'
      ? {
          header: 'Driver',
          cell: (s: { assignedDriverId: string | null }) =>
            s.assignedDriverId ? fullName(drivers.byId.get(s.assignedDriverId)) || shortId(s.assignedDriverId) : <span className="muted">—</span>,
        }
      : role === 'CLIENT' && me.data
        ? {
            header: 'You are',
            cell: (s: { senderId: string; recipientId: string }) =>
              s.senderId === me.data!.id ? 'Sender' : s.recipientId === me.data!.id ? 'Recipient' : '—',
          }
        : undefined;

  return (
    <div className="stack">
      <div className="page-header row-between">
        <div>
          <h1>{role === 'DRIVER' ? 'My deliveries' : 'Shipments'}</h1>
          <p>
            {role === 'ADMIN'
              ? 'Every shipment in the system.'
              : role === 'CLIENT'
                ? 'Shipments you send or receive.'
                : 'Shipments assigned to you.'}
          </p>
        </div>
        {(role === 'CLIENT' || role === 'ADMIN') && (
          <Link to="/shipments/new" className="btn btn-primary">
            New shipment
          </Link>
        )}
      </div>

      <Card>
        <form className="row filters" onSubmit={applySearch}>
          <input
            className="input"
            type="search"
            placeholder="Tracking number (LM-…)"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            aria-label="Search shipments"
          />
          <select className="input" value={status ?? ''} onChange={(e) => setParam('status', e.target.value || undefined)} aria-label="Status">
            <option value="">All statuses</option>
            {SHIPMENT_STATUSES.map((s) => (
              <option key={s} value={s}>
                {STATUS_LABEL[s]}
              </option>
            ))}
          </select>
          {role === 'CLIENT' && (
            <select className="input" value={as ?? ''} onChange={(e) => setParam('as', e.target.value || undefined)} aria-label="Your role">
              <option value="">Sent and received</option>
              <option value="sender">Sent by me</option>
              <option value="recipient">Sent to me</option>
            </select>
          )}
          <Button type="submit" variant="secondary">
            Search
          </Button>
        </form>

        <QueryState isLoading={list.isLoading} error={list.error} errorPrefix="Shipments are unavailable">
          {list.data && list.data.items.length === 0 ? (
            <EmptyState title="No shipments found.">
              {status || search || as ? 'Try other filters.' : role === 'CLIENT' ? <Link to="/shipments/new">Send your first parcel.</Link> : null}
            </EmptyState>
          ) : (
            list.data && (
              <>
                <ShipmentTable items={list.data.items} extra={extra} />
                <Pagination
                  page={page}
                  pageSize={PAGE_SIZE}
                  totalCount={list.data.totalCount}
                  onPage={(p) => setParam('page', String(p))}
                  disabled={list.isFetching}
                />
              </>
            )
          )}
        </QueryState>
      </Card>
    </div>
  );
}
