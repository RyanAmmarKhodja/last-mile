import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { profilesApi } from '@/shared/api';
import { qk } from '@/shared/query/queryClient';
import { fullName } from '@/shared/utils/format';
import type { ClientProfile } from '@/shared/types/profiles';

interface Props {
  label: string;
  value: ClientProfile | null;
  onChange: (c: ClientProfile | null) => void;
  excludeId?: string;
}

/** ADMIN: search ProfileService client profiles by name, company or phone, and pick one. */
export function ClientPicker({ label, value, onChange, excludeId }: Props) {
  const [term, setTerm] = useState('');
  const [debounced, setDebounced] = useState('');
  useEffect(() => {
    const t = setTimeout(() => setDebounced(term.trim()), 250);
    return () => clearTimeout(t);
  }, [term]);

  const params = { search: debounced || undefined, pageSize: 8 };
  const results = useQuery({ queryKey: qk.clients(params), queryFn: () => profilesApi.listClients(params), enabled: !value });

  if (value)
    return (
      <div className="field">
        <label>{label}</label>
        <div className="picked">
          <div>
            <strong>{fullName(value)}</strong>
            {value.companyName && <span className="muted"> · {value.companyName}</span>}
            <div className="muted">{value.address}</div>
          </div>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => onChange(null)}>
            Change
          </button>
        </div>
      </div>
    );

  const items = (results.data?.items ?? []).filter((c) => c.id !== excludeId);
  return (
    <div className="field">
      <label htmlFor={`picker-${label}`}>{label}</label>
      <input id={`picker-${label}`} className="input" type="search" placeholder="Name, company or phone" value={term} onChange={(e) => setTerm(e.target.value)} />
      <ul className="picker-results" role="listbox" aria-label={label}>
        {results.isLoading && <li className="muted">Searching…</li>}
        {!results.isLoading && items.length === 0 && <li className="muted">No active client matches.</li>}
        {items.map((c) => (
          <li key={c.id}>
            <button type="button" onClick={() => onChange(c)}>
              <strong>{fullName(c)}</strong>
              {c.companyName && <span className="muted"> · {c.companyName}</span>}
              <span className="muted picker-address">{c.address}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
