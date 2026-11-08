import type { UserRole } from '@/shared/types/auth';
import { Card, RoleBadge } from '@/shared/components';
import { ROLE_CAPABILITIES } from '../utils/roleCapabilities';

export function RoleCapabilitiesCard({ role }: { role: UserRole }) {
  const info = ROLE_CAPABILITIES[role];
  return (
    <Card title={<>Your role: <RoleBadge role={role} /></>} subtitle={info.summary}>
      <div className="capabilities">
        <div>
          <h3 className="cap-title ok">Allowed</h3>
          <ul className="cap-list">
            {info.can.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
        </div>
        <div>
          <h3 className="cap-title no">Not allowed</h3>
          <ul className="cap-list">
            {info.cannot.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
        </div>
      </div>
    </Card>
  );
}
