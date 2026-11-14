import { STATUS_LABEL } from '@/shared/components';
import { formatDateTime } from '@/shared/utils/format';
import type { ShipmentStatus } from '@/shared/types/shipments';

export interface TimelineItem {
  status: ShipmentStatus;
  at: string;
  notes?: string | null;
  actor?: string | null;
}

const HAPPY_PATH: ShipmentStatus[] = ['CREATED', 'DISPATCHED', 'IN_TRANSIT', 'DELIVERED'];

/** Status history, oldest first, with the remaining happy-path steps shown as pending. */
export function ShipmentTimeline({ items, current }: { items: TimelineItem[]; current: ShipmentStatus }) {
  const sorted = [...items].sort((a, b) => Date.parse(a.at) - Date.parse(b.at));
  const done = new Set(sorted.map((i) => i.status));
  const finalReached = current === 'DELIVERED' || current === 'FAILED' || current === 'CANCELLED';
  const pending = finalReached ? [] : HAPPY_PATH.filter((s) => !done.has(s));
  return (
    <ol className="timeline">
      {sorted.map((i, idx) => (
        <li key={`${i.status}-${idx}`}>
          <div className="timeline-title">{STATUS_LABEL[i.status]}</div>
          <div className="timeline-meta">
            {formatDateTime(i.at)}
            {i.actor && ` · ${i.actor}`}
          </div>
          {i.notes && <div className="timeline-note">“{i.notes}”</div>}
        </li>
      ))}
      {pending.map((s) => (
        <li key={s} className="pending">
          <div className="timeline-title muted">{STATUS_LABEL[s]}</div>
        </li>
      ))}
    </ol>
  );
}
