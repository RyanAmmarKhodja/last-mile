import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/shared/components';

/** Tracking numbers are LM- + 12 characters (S3). Spaces and case are forgiven. */
export function TrackForm({ initial = '' }: { initial?: string }) {
  const navigate = useNavigate();
  const [value, setValue] = useState(initial);
  function submit(e: FormEvent) {
    e.preventDefault();
    const tn = value.replace(/\s+/g, '').toUpperCase();
    if (tn) navigate(`/track/${encodeURIComponent(tn)}`);
  }
  return (
    <form className="track-form" onSubmit={submit} role="search">
      <input className="input" value={value} onChange={(e) => setValue(e.target.value)} placeholder="LM-XXXXXXXXXXXX" aria-label="Tracking number" maxLength={40} />
      <Button type="submit">Track</Button>
    </form>
  );
}
