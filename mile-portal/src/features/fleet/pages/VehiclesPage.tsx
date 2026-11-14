import { useState, type FormEvent } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fleetApi, getErrorMessage } from '@/shared/api';
import { qk } from '@/shared/query/queryClient';
import { Alert, Button, Card, Dialog, EmptyState, Pagination, QueryState, SelectField, TextField } from '@/shared/components';
import { formatWeight, fullName } from '@/shared/utils/format';
import type { ListVehiclesParams, Vehicle, VehicleInput } from '@/shared/types/fleet';
import { useDriverDirectory } from '@/features/tracking';
import { FleetTabs } from '../components/FleetTabs';

const PAGE_SIZE = 15;
type Mode = { kind: 'create' } | { kind: 'edit'; v: Vehicle } | { kind: 'assign'; v: Vehicle } | null;

/** FR-6: vehicles (FleetService): create, edit, assign / unassign, retire / reactivate. */
export function VehiclesPage() {
  const client = useQueryClient();
  const drivers = useDriverDirectory();
  const [page, setPage] = useState(1);
  const [active, setActive] = useState<'true' | 'false'>('true');
  const [assigned, setAssigned] = useState<'' | 'true' | 'false'>('');
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [mode, setMode] = useState<Mode>(null);
  const [flash, setFlash] = useState<{ variant: 'success' | 'error'; text: string } | null>(null);

  const params: ListVehiclesParams = {
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    active: active === 'true',
    assigned: assigned === '' ? undefined : assigned === 'true',
  };
  const list = useQuery({ queryKey: qk.vehicles(params), queryFn: () => fleetApi.listVehicles(params), placeholderData: keepPreviousData });

  const done = (text: string) => {
    setMode(null);
    setFlash({ variant: 'success', text });
    void client.invalidateQueries({ queryKey: qk.fleet });
  };
  const save = useMutation({
    mutationFn: ({ id, body }: { id?: string; body: VehicleInput }) => (id ? fleetApi.updateVehicle(id, body) : fleetApi.createVehicle(body)),
    onSuccess: (v, vars) => done(`${v.licensePlate} was ${vars.id ? 'updated' : 'added to the fleet'}.`),
  });
  const assign = useMutation({
    mutationFn: ({ v, driverId }: { v: Vehicle; driverId: string | null }) => fleetApi.assignVehicle(v.id, driverId),
    onSuccess: (v) => done(v.assignedDriverId ? `${v.licensePlate} is assigned to ${fullName(drivers.byId.get(v.assignedDriverId)) || 'the driver'}.` : `${v.licensePlate} is unassigned.`),
    onError: (err) => (setMode(null), setFlash({ variant: 'error', text: getErrorMessage(err) })),
  });
  const status = useMutation({
    mutationFn: ({ v, isActive }: { v: Vehicle; isActive: boolean }) => fleetApi.setVehicleActive(v.id, isActive),
    onSuccess: (v) => done(v.isActive ? `${v.licensePlate} is back in service.` : `${v.licensePlate} was retired.`),
    onError: (err) => setFlash({ variant: 'error', text: getErrorMessage(err) }),
  });

  function applySearch(e: FormEvent) {
    e.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  function retire(v: Vehicle) {
    const who = v.assignedDriverId ? ` ${fullName(drivers.byId.get(v.assignedDriverId)) || 'Its driver'} will be unassigned and taken off shift.` : '';
    if (window.confirm(`Retire ${v.licensePlate}?${who}`)) status.mutate({ v, isActive: false });
  }

  return (
    <div className="stack">
      <div className="page-header row-between">
        <div>
          <h1>Fleet</h1>
          <p>A driver needs an active vehicle to go on shift. Vehicles are retired, never deleted.</p>
        </div>
        <Button onClick={() => (save.reset(), setMode({ kind: 'create' }))}>Add vehicle</Button>
      </div>
      <FleetTabs />
      {flash && (
        <Alert variant={flash.variant} onClose={() => setFlash(null)}>
          {flash.text}
        </Alert>
      )}
      <Card>
        <form className="row filters" onSubmit={applySearch}>
          <input className="input" type="search" placeholder="Plate or model" value={searchInput} onChange={(e) => setSearchInput(e.target.value)} aria-label="Search vehicles" />
          <select className="input" value={active} onChange={(e) => (setPage(1), setActive(e.target.value as 'true' | 'false'))} aria-label="In service">
            <option value="true">In service</option>
            <option value="false">Retired</option>
          </select>
          <select className="input" value={assigned} onChange={(e) => (setPage(1), setAssigned(e.target.value as '' | 'true' | 'false'))} aria-label="Assignment">
            <option value="">Assigned or free</option>
            <option value="true">Assigned</option>
            <option value="false">Free</option>
          </select>
          <Button type="submit" variant="secondary">
            Search
          </Button>
        </form>
        <QueryState isLoading={list.isLoading} error={list.error} errorPrefix="FleetService is unavailable">
          {list.data?.items.length === 0 ? (
            <EmptyState title="No vehicles found." />
          ) : (
            list.data && (
              <>
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Plate</th>
                        <th>Model</th>
                        <th>Capacity</th>
                        <th>Driver</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {list.data.items.map((v) => (
                        <tr key={v.id}>
                          <td className="mono">
                            <strong>{v.licensePlate}</strong>
                          </td>
                          <td>{v.model}</td>
                          <td className="nowrap">{formatWeight(v.capacityKg)}</td>
                          <td>{v.assignedDriverId ? fullName(drivers.byId.get(v.assignedDriverId)) || '…' : <span className="muted">Free</span>}</td>
                          <td>
                            <div className="row nowrap">
                              {v.isActive ? (
                                <>
                                  <Button size="sm" variant="ghost" onClick={() => setMode({ kind: 'assign', v })}>
                                    {v.assignedDriverId ? 'Reassign' : 'Assign'}
                                  </Button>
                                  {v.assignedDriverId && (
                                    <Button size="sm" variant="ghost" disabled={assign.isPending} onClick={() => window.confirm(`Unassign ${v.licensePlate}? The driver goes off shift.`) && assign.mutate({ v, driverId: null })}>
                                      Unassign
                                    </Button>
                                  )}
                                  <Button size="sm" variant="ghost" onClick={() => (save.reset(), setMode({ kind: 'edit', v }))}>
                                    Edit
                                  </Button>
                                  <Button size="sm" variant="ghost" disabled={status.isPending} onClick={() => retire(v)}>
                                    Retire
                                  </Button>
                                </>
                              ) : (
                                <Button size="sm" variant="ghost" disabled={status.isPending} onClick={() => status.mutate({ v, isActive: true })}>
                                  Reactivate
                                </Button>
                              )}
                            </div>
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

      <Dialog open={mode?.kind === 'create' || mode?.kind === 'edit'} title={mode?.kind === 'edit' ? `Edit ${mode.v.licensePlate}` : 'Add vehicle'} onClose={() => setMode(null)}>
        {(mode?.kind === 'create' || mode?.kind === 'edit') && (
          <VehicleForm
            initial={mode.kind === 'edit' ? mode.v : undefined}
            pending={save.isPending}
            error={save.error ? getErrorMessage(save.error) : null}
            onCancel={() => setMode(null)}
            onSubmit={(body) => save.mutate({ id: mode.kind === 'edit' ? mode.v.id : undefined, body })}
          />
        )}
      </Dialog>

      <Dialog open={mode?.kind === 'assign'} title={mode?.kind === 'assign' ? `Assign ${mode.v.licensePlate}` : ''} onClose={() => setMode(null)}>
        {mode?.kind === 'assign' && (
          <AssignForm
            vehicle={mode.v}
            candidates={(drivers.data ?? []).filter((d) => d.status === 'ACTIVE' && !d.vehicle)}
            pending={assign.isPending}
            onCancel={() => setMode(null)}
            onSubmit={(driverId) => assign.mutate({ v: mode.v, driverId })}
          />
        )}
      </Dialog>
    </div>
  );
}

function VehicleForm({ initial, pending, error, onSubmit, onCancel }: { initial?: Vehicle; pending: boolean; error: string | null; onSubmit: (b: VehicleInput) => void; onCancel: () => void }) {
  const [plate, setPlate] = useState(initial?.licensePlate ?? '');
  const [model, setModel] = useState(initial?.model ?? '');
  const [capacity, setCapacity] = useState(initial ? String(initial.capacityKg) : '');
  const [localError, setLocalError] = useState<string | null>(null);
  function submit(e: FormEvent) {
    e.preventDefault();
    const capacityKg = Number(capacity.replace(',', '.'));
    if (!Number.isFinite(capacityKg) || capacityKg <= 0 || capacityKg > 100_000) return setLocalError('Capacity must be between 0 and 100 000 kg.');
    setLocalError(null);
    onSubmit({ licensePlate: plate.trim(), model: model.trim(), capacityKg });
  }
  return (
    <form className="form" onSubmit={submit}>
      <TextField label="Licence plate" required value={plate} onChange={(e) => setPlate(e.target.value)} placeholder="12345-120-16" />
      <TextField label="Model" required value={model} onChange={(e) => setModel(e.target.value)} placeholder="Renault Kangoo" />
      <TextField label="Capacity (kg)" required inputMode="decimal" value={capacity} onChange={(e) => setCapacity(e.target.value)} placeholder="650" />
      {(localError || error) && <Alert variant="error">{localError ?? error}</Alert>}
      <div className="dialog-actions">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" loading={pending}>
          {initial ? 'Save' : 'Add vehicle'}
        </Button>
      </div>
    </form>
  );
}

function AssignForm({ vehicle, candidates, pending, onSubmit, onCancel }: { vehicle: Vehicle; candidates: { id: string; firstName: string; lastName: string }[]; pending: boolean; onSubmit: (driverId: string) => void; onCancel: () => void }) {
  const [driverId, setDriverId] = useState(candidates[0]?.id ?? '');
  return (
    <form className="form" onSubmit={(e) => (e.preventDefault(), driverId && onSubmit(driverId))}>
      {candidates.length === 0 ? (
        <Alert variant="info">Every active driver already operates a vehicle. Unassign one first.</Alert>
      ) : (
        <SelectField
          label="Driver without a vehicle"
          value={driverId}
          onChange={(e) => setDriverId(e.target.value)}
          options={candidates.map((d) => ({ value: d.id, label: fullName(d) }))}
          hint={vehicle.assignedDriverId ? 'The current driver loses this vehicle and goes off shift.' : undefined}
        />
      )}
      <div className="dialog-actions">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" loading={pending} disabled={!driverId}>
          Assign
        </Button>
      </div>
    </form>
  );
}
