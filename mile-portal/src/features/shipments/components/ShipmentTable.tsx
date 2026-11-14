import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { ShipmentStatusBadge } from '@/shared/components';
import { formatDateTime, formatWeight } from '@/shared/utils/format';
import type { ShipmentSummary } from '@/shared/types/shipments';

interface Props {
  items: ShipmentSummary[];
  /** Extra column (e.g. driver name for admins, "you are the sender" for clients). */
  extra?: { header: string; cell: (s: ShipmentSummary) => ReactNode };
}

export function ShipmentTable({ items, extra }: Props) {
  const navigate = useNavigate();
  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr>
            <th>Tracking number</th>
            <th>Status</th>
            <th>Destination</th>
            <th>Weight</th>
            {extra && <th>{extra.header}</th>}
            <th>Updated</th>
          </tr>
        </thead>
        <tbody>
          {items.map((s) => (
            <tr key={s.id} className="clickable" onClick={() => navigate(`/shipments/${s.id}`)}>
              <td className="mono nowrap">
                <a href={`/shipments/${s.id}`} onClick={(e) => e.preventDefault()}>
                  {s.trackingNumber}
                </a>
              </td>
              <td>
                <ShipmentStatusBadge status={s.status} />
              </td>
              <td className="shipment-dest">{s.destinationAddress}</td>
              <td className="nowrap">{formatWeight(s.weightKg)}</td>
              {extra && <td>{extra.cell(s)}</td>}
              <td className="nowrap muted">{formatDateTime(s.updatedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
