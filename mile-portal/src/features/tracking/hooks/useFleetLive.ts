import { useEffect, useMemo, useRef, useState } from 'react';
import { openAuthorizedStream, trackingApi, type SseState } from '@/shared/api';
import type { LivePosition } from '@/shared/types/tracking';

export type FleetConnection = SseState | 'polling' | 'unavailable';

export interface FleetLive {
  positions: LivePosition[];
  byDriver: ReadonlyMap<string, LivePosition>;
  connection: FleetConnection;
  /** Date.now() of the last snapshot or batch. */
  lastUpdateAt: number | null;
}

const POLL_MS = 5000;
const POLL_AFTER_MS = 3000;

/**
 * P3: the admin fleet map's live state. The SSE stream sends a snapshot on connect, then one batch per second.
 * While the stream is down for more than 3 s, GET /tracking/live is polled every 5 s instead.
 */
export function useFleetLive(enabled = true): FleetLive {
  const [byDriver, setByDriver] = useState<ReadonlyMap<string, LivePosition>>(() => new Map());
  const [connection, setConnection] = useState<FleetConnection>('connecting');
  const [lastUpdateAt, setLastUpdateAt] = useState<number | null>(null);
  const snapshotPending = useRef(true);

  useEffect(() => {
    if (!enabled) return;
    let pollTimer: ReturnType<typeof setInterval> | undefined;
    let pollDelay: ReturnType<typeof setTimeout> | undefined;
    let streamOpen = false;
    let disposed = false;

    const replace = (list: LivePosition[]) => {
      setByDriver(new Map(list.map((p) => [p.driverId, p])));
      setLastUpdateAt(Date.now());
    };

    const poll = async () => {
      try {
        const list = await trackingApi.live();
        if (disposed || streamOpen) return;
        replace(list);
        setConnection('polling');
      } catch {
        // Tracking down: keep the last known positions (they fade as they age) and say so.
        if (!disposed && !streamOpen) setConnection('unavailable');
      }
    };

    const stopPolling = () => {
      clearTimeout(pollDelay);
      clearInterval(pollTimer);
      pollDelay = undefined;
      pollTimer = undefined;
    };
    const startPollingSoon = () => {
      if (pollTimer || pollDelay) return;
      pollDelay = setTimeout(() => {
        pollDelay = undefined;
        if (disposed || streamOpen) return;
        void poll();
        pollTimer = setInterval(poll, POLL_MS);
      }, POLL_AFTER_MS);
    };

    const close = openAuthorizedStream({
      url: trackingApi.liveStreamUrl,
      onStateChange: (state) => {
        streamOpen = state === 'open';
        if (state === 'open') {
          snapshotPending.current = true; // the first message after (re)connect is a full snapshot
          stopPolling();
          setConnection('open');
        } else if (state === 'forbidden' || state === 'closed') {
          stopPolling();
          setConnection(state);
        } else {
          setConnection((c) => (c === 'polling' || c === 'unavailable' ? c : state));
          startPollingSoon();
        }
      },
      onMessage: ({ event, data }) => {
        if (event === 'positions') {
          const list = JSON.parse(data) as LivePosition[];
          if (snapshotPending.current) {
            snapshotPending.current = false;
            replace(list);
          } else if (list.length) {
            setByDriver((prev) => {
              const next = new Map(prev);
              list.forEach((p) => next.set(p.driverId, p));
              return next;
            });
            setLastUpdateAt(Date.now());
          }
        } else if (event === 'offline') {
          const { driverIds } = JSON.parse(data) as { driverIds: string[] };
          setByDriver((prev) => {
            const next = new Map(prev);
            driverIds.forEach((id) => next.delete(id));
            return next;
          });
        }
      },
    });

    return () => {
      disposed = true;
      stopPolling();
      close();
    };
  }, [enabled]);

  const positions = useMemo(() => [...byDriver.values()], [byDriver]);
  return { positions, byDriver, connection, lastUpdateAt };
}
