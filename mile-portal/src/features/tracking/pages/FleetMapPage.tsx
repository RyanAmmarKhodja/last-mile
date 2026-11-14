import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { trackingApi } from '@/shared/api';
import { MapView, STALE_AFTER_MS, TrailLayer, VehicleLayer, MAP_COLORS } from '@/shared/map';
import { useNow } from '@/shared/hooks/useNow';
import { qk } from '@/shared/query/queryClient';
import { Button, EmptyState } from '@/shared/components';
import { formatAgo, formatSpeed, formatTime, fullName, shortId } from '@/shared/utils/format';
import type { LivePosition } from '@/shared/types/tracking';
import { useFleetLive } from '../hooks/useFleetLive';
import { useDriverDirectory } from '../hooks/useDriverDirectory';
import { ConnectionChip } from '../components/ConnectionChip';
import { EmptyMapClick, FollowPoint } from '../components/MapHelpers';
import '../styles/tracking.css';

type Filter = 'all' | 'delivering' | 'idle';
const VEHICLE_LAYERS = ['lm-vehicles-halo'];
const TRAIL_WINDOW_MS = 2 * 60 * 60 * 1000;
const MAX_TAIL = 600;

/** FR-2: every vehicle on shift, live (ADMIN). */
export function FleetMapPage() {
  const fleet = useFleetLive();
  const drivers = useDriverDirectory();
  const now = useNow(5000);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [filter, setFilter] = useState<Filter>('all');
  const [search, setSearch] = useState('');
  const [follow, setFollow] = useState(false);

  const nameOf = useCallback((driverId: string) => fullName(drivers.byId.get(driverId)) || `Driver ${shortId(driverId)}`, [drivers.byId]);
  const plateOf = useCallback((driverId: string) => drivers.byId.get(driverId)?.vehicle?.licensePlate ?? null, [drivers.byId]);
  const labelFor = useCallback((p: LivePosition) => plateOf(p.driverId) ?? nameOf(p.driverId), [plateOf, nameOf]);

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    return fleet.positions
      .filter((p) => (filter === 'all' ? true : filter === 'delivering' ? !!p.shipmentId : !p.shipmentId))
      .filter((p) => {
        if (!q) return true;
        const hay = [nameOf(p.driverId), plateOf(p.driverId) ?? '', p.trackingNumber ?? ''].join(' ').toLowerCase();
        return hay.includes(q);
      })
      .sort((a, b) => Number(!!b.shipmentId) - Number(!!a.shipmentId) || nameOf(a.driverId).localeCompare(nameOf(b.driverId)));
  }, [fleet.positions, filter, search, nameOf, plateOf]);

  const delivering = fleet.positions.filter((p) => p.shipmentId).length;
  const selected = selectedId ? (fleet.byDriver.get(selectedId) ?? null) : null;

  // Trail for the selected vehicle: sampled history (last 2 h) + the live tail received since selecting it.
  const historyFrom = useMemo(() => (selectedId ? new Date(Date.now() - TRAIL_WINDOW_MS).toISOString() : ''), [selectedId]);
  const history = useQuery({
    queryKey: qk.driverHistory(selectedId ?? '', historyFrom),
    queryFn: () => trackingApi.driverHistory(selectedId!, { from: historyFrom, limit: 2000 }),
    enabled: !!selectedId,
    staleTime: 120_000,
  });
  const [tail, setTail] = useState<[number, number][]>([]);
  useEffect(() => setTail([]), [selectedId]);
  useEffect(() => {
    if (!selected) return;
    const pt: [number, number] = [selected.longitude, selected.latitude];
    setTail((t) => {
      const last = t[t.length - 1];
      return last && last[0] === pt[0] && last[1] === pt[1] ? t : [...t, pt].slice(-MAX_TAIL);
    });
  }, [selected]);
  const trail = useMemo<[number, number][]>(
    () => [...(history.data ?? []).map((h) => [h.longitude, h.latitude] as [number, number]), ...tail],
    [history.data, tail],
  );

  const forbidden = fleet.connection === 'forbidden';

  return (
    <div className="fleet-map-page">
      <MapView
        overlay={
          <>
            <ConnectionChip state={fleet.connection} extra={fleet.lastUpdateAt ? `updated ${formatAgo(fleet.lastUpdateAt, now)}` : undefined} />
            {fleet.positions.length === 0 && fleet.connection === 'open' && <span className="map-chip">No vehicle is on shift right now</span>}
          </>
        }
      >
        {selectedId && trail.length > 1 && <TrailLayer points={trail} color={MAP_COLORS.selected} />}
        <VehicleLayer positions={visible} selectedId={selectedId} onSelect={setSelectedId} labelFor={labelFor} now={now} />
        <EmptyMapClick layers={VEHICLE_LAYERS} onClick={() => setSelectedId(null)} />
        <FollowPoint point={selected ? [selected.longitude, selected.latitude] : null} active={follow} targetKey={selectedId} />
      </MapView>

      <aside className="fleet-panel" aria-label="Vehicles">
        <div className="fleet-panel-head">
          <h1>Live fleet</h1>
          <div className="fleet-counts">
            <span>
              <strong>{fleet.positions.length}</strong> on the map
            </span>
            <span style={{ color: MAP_COLORS.delivering }}>
              <span className="legend-swatch" />
              <strong>{delivering}</strong> delivering
            </span>
            <span style={{ color: MAP_COLORS.idle }}>
              <span className="legend-swatch" />
              <strong>{fleet.positions.length - delivering}</strong> available
            </span>
          </div>
          <input
            className="input"
            type="search"
            placeholder="Driver, plate or tracking number"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Search vehicles"
          />
          <div className="row" role="group" aria-label="Filter">
            {(['all', 'delivering', 'idle'] as Filter[]).map((f) => (
              <Button key={f} size="sm" variant={filter === f ? 'primary' : 'ghost'} onClick={() => setFilter(f)}>
                {f === 'all' ? 'All' : f === 'delivering' ? 'Delivering' : 'Available'}
              </Button>
            ))}
          </div>
        </div>

        {forbidden ? (
          <EmptyState title="The fleet stream is for administrators.">Your token was refused by FleetTrackingService.</EmptyState>
        ) : visible.length === 0 ? (
          <EmptyState title={fleet.positions.length ? 'No vehicle matches.' : 'No live vehicles.'}>
            {fleet.positions.length ? 'Change the search or filter.' : 'Vehicles appear here as soon as a driver on shift sends a GPS ping.'}
          </EmptyState>
        ) : (
          <ul className="fleet-list">
            {visible.map((p) => {
              const stale = now - Date.parse(p.receivedAt) > STALE_AFTER_MS;
              return (
                <li key={p.driverId}>
                  <button
                    className={['fleet-item', p.driverId === selectedId && 'selected', stale && 'stale'].filter(Boolean).join(' ')}
                    onClick={() => setSelectedId(p.driverId === selectedId ? null : p.driverId)}
                    aria-pressed={p.driverId === selectedId}
                  >
                    <span className="fleet-item-top">
                      <span>{nameOf(p.driverId)}</span>
                      <span className="muted">{formatSpeed(p.speedKmh)}</span>
                    </span>
                    <span className="fleet-item-meta">
                      {plateOf(p.driverId) ?? 'No plate'} · {p.trackingNumber ? `Delivering ${p.trackingNumber}` : 'Available'} ·{' '}
                      {formatAgo(p.receivedAt, now)}
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        )}

        {selected && (
          <div className="fleet-detail">
            <div className="row-between">
              <strong>{nameOf(selected.driverId)}</strong>
              <Button size="sm" variant="ghost" onClick={() => setSelectedId(null)}>
                Close
              </Button>
            </div>
            <dl className="details">
              <dt>Vehicle</dt>
              <dd>
                {plateOf(selected.driverId) ?? '—'} {drivers.byId.get(selected.driverId)?.vehicle?.model && <span className="muted">({drivers.byId.get(selected.driverId)!.vehicle!.model})</span>}
              </dd>
              <dt>Phone</dt>
              <dd>{drivers.byId.get(selected.driverId)?.phoneNumber ?? '—'}</dd>
              <dt>Shipment</dt>
              <dd>
                {selected.shipmentId ? <Link to={`/shipments/${selected.shipmentId}`}>{selected.trackingNumber}</Link> : 'None (available)'}
              </dd>
              <dt>Speed</dt>
              <dd>
                {formatSpeed(selected.speedKmh)}
                {selected.headingDeg != null && ` · heading ${Math.round(selected.headingDeg)}°`}
              </dd>
              <dt>Last ping</dt>
              <dd>
                {formatTime(selected.recordedAt)} <span className="muted">({formatAgo(selected.receivedAt, now)})</span>
              </dd>
              <dt>Position</dt>
              <dd className="mono">
                {selected.latitude.toFixed(5)}, {selected.longitude.toFixed(5)}
                {selected.accuracyM != null && <span className="muted"> ±{Math.round(selected.accuracyM)} m</span>}
              </dd>
            </dl>
            <label className="row">
              <input type="checkbox" checked={follow} onChange={(e) => setFollow(e.target.checked)} /> Follow this vehicle
            </label>
            <span className="muted">
              Trail: last 2 h of sampled history{history.data ? ` (${history.data.length} points)` : ''} plus live positions.
            </span>
          </div>
        )}
      </aside>
    </div>
  );
}
