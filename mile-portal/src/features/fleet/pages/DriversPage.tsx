import { useState, type FormEvent } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fleetApi, getErrorMessage, trackingApi } from '@/shared/api';
import { qk } from '@/shared/query/queryClient';
import { Alert, Button, Card, Dialog, EmptyState, Pagination, QueryState } from '@/shared/components';
import { fullName } from '@/shared/utils/format';
import type { Driver, DriverInput, DriverStatus, ListDriversParams } from '@/shared/types/fleet';
import { FleetTabs } from '../components/FleetTabs';
import { DriverForm } from '../components/DriverForm';

const PAGE_SIZE = 15;

/** FR-6: drivers (FleetService), with a live indicator from FleetTrackingService. */
export function DriversPage() {
  const client = useQueryClient();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<DriverStatus>('ACTIVE');
  const [available, setAvailable] = useState<'' | 'true' | 'false'>('');
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Driver | null>(null);
  const [flash, setFlash] = useState<{ variant: 'success' | 'error'; text: string } | null>(null);

  const params: ListDriversParams = { page, pageSize: PAGE_SIZE, status, search: search || undefined, available: available === '' ? undefined : available === 'true' };
  const list = useQuery({ queryKey: qk.drivers(params), queryFn: () => fleetApi.listDrivers(params), placeholderData: keepPreviousData });
  const live = useQuery({ queryKey: qk.live, queryFn: trackingApi.live, refetchInterval: 15_000 });
  const liveIds = new Set((live.data ?? []).map((p) => p.driverId));

  const availability = useMutation({
    mutationFn: ({ d, value }: { d: Driver; value: boolean }) => fleetApi.setDriverAvailability(d.id, value),
    onSuccess: (d) => {
      setFlash({ variant: 'success', text: `${fullName(d)} is now ${d.isAvailable ? 'available' : 'unavailable'}.` });
      void client.invalidateQueries({ queryKey: qk.fleet });
    },
    onError: (err) => setFlash({ variant: 'error', text: getErrorMessage(err) }),
  });
  const update = useMutation({
    mutationFn: ({ id, body }: { id: string; body: DriverInput }) => fleetApi.updateDriver(id, body),
    onSuccess: (d) => {
      setEditing(null);
      setFlash({ variant: 'success', text: `${fullName(d)} was updated.` });
      void client.invalidateQueries({ queryKey: qk.fleet });
    },
  });

  function applySearch(e: FormEvent) {
    e.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  return (
    <div className="stack">
      <div className="page-header">
        <h1>Fleet</h1>
        <p>Drivers are created when a DRIVER account registers. Availability means "on shift and ready to deliver".</p>
      </div>
      <FleetTabs />
      {flash && (
        <Alert variant={flash.variant} onClose={() => setFlash(null)}>
          {flash.text}
        </Alert>
      )}
      <Card>
        <form className="row filters" onSubmit={applySearch}>
          <input className="input" type="search" placeholder="Name, licence or phone" value={searchInput} onChange={(e) => setSearchInput(e.target.value)} aria-label="Search drivers" />
          <select className="input" value={status} onChange={(e) => (setPage(1), setStatus(e.target.value as DriverStatus))} aria-label="Status">
            <option value="ACTIVE">Active accounts</option>
            <option value="INACTIVE">Disabled accounts</option>
            <option value="DELETED">Deleted</option>
          </select>
          <select className="input" value={available} onChange={(e) => (setPage(1), setAvailable(e.target.value as '' | 'true' | 'false'))} aria-label="Availability">
            <option value="">Any availability</option>
            <option value="true">Available</option>
            <option value="false">Unavailable</option>
          </select>
          <Button type="submit" variant="secondary">
            Search
          </Button>
        </form>
        <QueryState isLoading={list.isLoading} error={list.error} errorPrefix="FleetService is unavailable">
          {list.data?.items.length === 0 ? (
            <EmptyState title="No drivers found." />
          ) : (
            list.data && (
              <>
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Driver</th>
                        <th>Phone</th>
                        <th>Licence</th>
                        <th>Vehicle</th>
                        <th>Shift</th>
                        <th>GPS</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {list.data.items.map((d) => (
                        <tr key={d.id}>
                          <td>
                            <strong>{fullName(d)}</strong>
                          </td>
                          <td className="nowrap">{d.phoneNumber}</td>
                          <td className="mono">{d.licenseNumber}</td>
                          <td>{d.vehicle ? `${d.vehicle.licensePlate} · ${d.vehicle.model}` : <span className="muted">None</span>}</td>
                          <td>
                            <span className={`badge ${d.isAvailable ? 'badge-ok' : 'badge-neutral'}`}>{d.isAvailable ? 'Available' : 'Unavailable'}</span>
                          </td>
                          <td>
                            {liveIds.has(d.id) ? (
                              <span className="row nowrap" style={{ gap: '0.35rem' }}>
                                <span className="live-dot on" /> Live
                              </span>
                            ) : (
                              <span className="muted">Offline</span>
                            )}
                          </td>
                          <td>
                            {d.status !== 'DELETED' && (
                              <div className="row nowrap">
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  disabled={availability.isPending || (!d.isAvailable && (!d.vehicle || d.status !== 'ACTIVE'))}
                                  title={!d.isAvailable && !d.vehicle ? 'Assign a vehicle first' : undefined}
                                  onClick={() => availability.mutate({ d, value: !d.isAvailable })}
                                >
                                  {d.isAvailable ? 'End shift' : 'Start shift'}
                                </Button>
                                <Button size="sm" variant="ghost" onClick={() => (update.reset(), setEditing(d))}>
                                  Edit
                                </Button>
                              </div>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <Pagination page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onPage={setPage} disabled={list.isFetching} />
              </>
            )
          )}
        </QueryState>
      </Card>

      <Dialog open={!!editing} title={editing ? `Edit ${fullName(editing)}` : ''} onClose={() => setEditing(null)}>
        {editing && (
          <DriverForm
            initial={{ firstName: editing.firstName, lastName: editing.lastName, phoneNumber: editing.phoneNumber, licenseNumber: editing.licenseNumber }}
            submitLabel="Save"
            pending={update.isPending}
            error={update.error ? getErrorMessage(update.error) : null}
            onSubmit={(body) => update.mutate({ id: editing.id, body })}
            onCancel={() => setEditing(null)}
          />
        )}
      </Dialog>
    </div>
  );
}
