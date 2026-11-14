import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

interface Props {
  label: string;
  value: number | string | null | undefined;
  hint?: ReactNode;
  to?: string;
  loading?: boolean;
}

/** A headline number with its label (stat tile). Links to the list behind it when `to` is set. */
export function KpiTile({ label, value, hint, to, loading }: Props) {
  const body = (
    <>
      <span className="kpi-label">{label}</span>
      <span className="kpi-value">{loading ? '…' : (value ?? '—')}</span>
      {hint && <span className="kpi-hint">{hint}</span>}
    </>
  );
  return to ? (
    <Link to={to} className="kpi-tile kpi-link">
      {body}
    </Link>
  ) : (
    <div className="kpi-tile">{body}</div>
  );
}
