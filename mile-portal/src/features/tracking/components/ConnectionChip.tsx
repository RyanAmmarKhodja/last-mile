import type { FleetConnection } from '../hooks/useFleetLive';
import type { PublicConnection } from '../hooks/usePublicTracking';

const LABELS: Record<FleetConnection | PublicConnection, { text: string; dot: 'on' | 'warn' | 'off' | '' }> = {
  open: { text: 'Live', dot: 'on' },
  live: { text: 'Live', dot: 'on' },
  connecting: { text: 'Connecting…', dot: 'warn' },
  reconnecting: { text: 'Reconnecting…', dot: 'warn' },
  polling: { text: 'Live (polling every 5 s)', dot: 'warn' },
  forbidden: { text: 'Not allowed', dot: 'off' },
  closed: { text: 'Disconnected', dot: 'off' },
  ended: { text: 'Tracking ended', dot: '' },
  notfound: { text: 'Unknown tracking number', dot: 'off' },
  unavailable: { text: 'Live tracking unavailable', dot: 'off' },
};

export function ConnectionChip({ state, extra }: { state: FleetConnection | PublicConnection; extra?: string }) {
  const l = LABELS[state];
  return (
    <span className="map-chip" role="status">
      <span className={`live-dot ${l.dot}`} aria-hidden="true" />
      {l.text}
      {extra && <span className="muted">· {extra}</span>}
    </span>
  );
}
