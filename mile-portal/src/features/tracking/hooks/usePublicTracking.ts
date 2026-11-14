import { useEffect, useSyncExternalStore } from 'react';
import { getErrorStatus, trackingApi } from '@/shared/api';
import { ACTIVE_STATUSES, type ShipmentStatus } from '@/shared/types/shipments';
import type { PublicPoint } from '@/shared/types/tracking';

export type PublicConnection = 'connecting' | 'live' | 'reconnecting' | 'ended' | 'notfound' | 'unavailable';

export interface PublicTrackingState {
  status: ShipmentStatus | null;
  position: PublicPoint | null;
  /** [lon, lat] received since this page started watching (the public API has no history, T3). */
  trail: [number, number][];
  connection: PublicConnection;
  /** Date.now() of the last position or status message. */
  lastMessageAt: number | null;
}

const INITIAL: PublicTrackingState = { status: null, position: null, trail: [], connection: 'connecting', lastMessageAt: null };
const MAX_TRAIL = 2000;
const CLOSE_GRACE_MS = 2000;
const RETRY_MS = 5000;

interface Entry {
  state: PublicTrackingState;
  listeners: Set<() => void>;
  refs: number;
  source: EventSource | null;
  closeTimer?: ReturnType<typeof setTimeout>;
  retryTimer?: ReturnType<typeof setTimeout>;
  stopped: boolean;
}

/** P8: one EventSource per tracking number, shared by every component that shows it. */
const registry = new Map<string, Entry>();

function update(entry: Entry, patch: Partial<PublicTrackingState>) {
  entry.state = { ...entry.state, ...patch };
  entry.listeners.forEach((l) => l());
}

function applyPosition(entry: Entry, position: PublicPoint) {
  const point: [number, number] = [position.longitude, position.latitude];
  const last = entry.state.trail[entry.state.trail.length - 1];
  const trail = last && last[0] === point[0] && last[1] === point[1] ? entry.state.trail : [...entry.state.trail, point].slice(-MAX_TRAIL);
  update(entry, { position, trail, lastMessageAt: Date.now() });
}

function open(tn: string, entry: Entry) {
  if (entry.stopped || entry.source) return;
  const source = new EventSource(trackingApi.publicStreamUrl(tn));
  entry.source = source;

  source.onopen = () => update(entry, { connection: 'live' });
  source.addEventListener('status', (e) => {
    const { status } = JSON.parse((e as MessageEvent).data) as { status: ShipmentStatus };
    const active = ACTIVE_STATUSES.includes(status);
    update(entry, { status, connection: 'live', lastMessageAt: Date.now(), ...(active ? {} : { position: null }) });
  });
  source.addEventListener('position', (e) => applyPosition(entry, JSON.parse((e as MessageEvent).data) as PublicPoint));
  source.addEventListener('ended', () => {
    entry.stopped = true;
    source.close();
    entry.source = null;
    update(entry, { connection: 'ended', position: null });
  });
  source.onerror = () => {
    if (entry.stopped) return;
    if (source.readyState === EventSource.CLOSED) {
      // HTTP error (404 / 429 / 5xx): EventSource gives up, so we retry ourselves.
      entry.source = null;
      update(entry, { connection: 'reconnecting' });
      entry.retryTimer = setTimeout(() => open(tn, entry), RETRY_MS);
    } else {
      update(entry, { connection: 'reconnecting' });
    }
  };
}

function start(tn: string, entry: Entry) {
  // Seed from the plain GET so the map has a position before the stream's first event.
  trackingApi
    .publicPosition(tn)
    .then((p) => {
      if (entry.refs === 0) return;
      update(entry, { status: p.status });
      if (p.position) applyPosition(entry, p.position);
    })
    .catch((err) => {
      if (getErrorStatus(err) === 404) {
        entry.stopped = true;
        entry.source?.close();
        entry.source = null;
        update(entry, { connection: 'notfound' });
      } else if (!entry.source || entry.source.readyState !== EventSource.OPEN) {
        update(entry, { connection: 'unavailable' });
      }
    });
  open(tn, entry);
}

function acquire(tn: string): Entry {
  let entry = registry.get(tn);
  if (!entry) {
    entry = { state: INITIAL, listeners: new Set(), refs: 0, source: null, stopped: false };
    registry.set(tn, entry);
  }
  clearTimeout(entry.closeTimer);
  if (entry.refs++ === 0 && !entry.source && !entry.stopped) start(tn, entry);
  return entry;
}

function release(tn: string) {
  const entry = registry.get(tn);
  if (!entry) return;
  if (--entry.refs > 0) return;
  entry.closeTimer = setTimeout(() => {
    if (entry.refs > 0) return;
    entry.source?.close();
    clearTimeout(entry.retryTimer);
    registry.delete(tn);
  }, CLOSE_GRACE_MS);
}

const EMPTY_SUBSCRIBE = () => () => {};

/** Live position of one shipment from the public tracking stream (P2). `null` = not watching. */
export function usePublicTracking(trackingNumber: string | null | undefined): PublicTrackingState {
  const tn = trackingNumber?.trim().toUpperCase() || null;

  useEffect(() => {
    if (!tn) return;
    acquire(tn);
    return () => release(tn);
  }, [tn]);

  return useSyncExternalStore(
    tn
      ? (listener) => {
          // The entry exists once the effect above ran; subscribe lazily to whatever is registered.
          const entry = registry.get(tn) ?? acquireForSubscribe(tn);
          entry.listeners.add(listener);
          return () => entry.listeners.delete(listener);
        }
      : EMPTY_SUBSCRIBE,
    () => (tn ? (registry.get(tn)?.state ?? INITIAL) : INITIAL),
  );
}

/** useSyncExternalStore can subscribe before the effect runs: create a passive entry without a ref. */
function acquireForSubscribe(tn: string): Entry {
  const entry: Entry = { state: INITIAL, listeners: new Set(), refs: 0, source: null, stopped: false };
  registry.set(tn, entry);
  return entry;
}
