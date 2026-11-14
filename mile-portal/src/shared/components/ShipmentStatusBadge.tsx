import type { ShipmentStatus } from '@/shared/types/shipments';

export const STATUS_LABEL: Record<ShipmentStatus, string> = {
  CREATED: 'Created',
  DISPATCHED: 'Dispatched',
  IN_TRANSIT: 'In transit',
  DELIVERED: 'Delivered',
  FAILED: 'Failed',
  CANCELLED: 'Cancelled',
};

/** Status is always shown as text; the color only reinforces it. */
export function ShipmentStatusBadge({ status }: { status: ShipmentStatus }) {
  return <span className={`badge badge-status badge-status-${status.toLowerCase()}`}>{STATUS_LABEL[status]}</span>;
}
