export function formatDateTime(value: string | number | Date): string {
  return new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
}

export function formatTime(value: string | number | Date): string {
  return new Date(value).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

/** "12 s ago", "4 min ago", "3 h ago", then a date. */
export function formatAgo(value: string | number | Date, now = Date.now()): string {
  const s = Math.max(0, Math.round((now - new Date(value).getTime()) / 1000));
  if (s < 5) return 'just now';
  if (s < 60) return `${s} s ago`;
  const m = Math.round(s / 60);
  if (m < 60) return `${m} min ago`;
  const h = Math.round(m / 60);
  if (h < 24) return `${h} h ago`;
  return formatDateTime(value);
}

export function formatWeight(kg: number): string {
  return `${Number(kg).toLocaleString(undefined, { maximumFractionDigits: 2 })} kg`;
}

export function formatSpeed(kmh: number | null | undefined): string {
  return kmh == null ? '—' : `${Math.round(kmh)} km/h`;
}

/** First 8 characters of a uuid, for compact display next to a full-id tooltip. */
export function shortId(id: string | null | undefined): string {
  return id ? id.slice(0, 8) : '—';
}

export function fullName(p: { firstName: string; lastName: string } | null | undefined): string {
  return p ? `${p.firstName} ${p.lastName}`.trim() : '';
}
