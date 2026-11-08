import { useState } from 'react';
import { refreshAccessToken } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { Alert, Button, Card } from '@/shared/components';
import { formatDateTime } from '@/shared/utils/format';
import { decodeJwt } from '../utils/jwt';
import { useNow } from '../hooks/useNow';

function formatRemaining(ms: number): string {
  if (ms <= 0) return 'expired';
  const total = Math.floor(ms / 1000);
  return `${Math.floor(total / 60)}m ${String(total % 60).padStart(2, '0')}s`;
}

/** Shows the claims inside the current access token (decoded client-side, not verified). */
export function TokenCard() {
  const { accessToken, user, refreshUser, logout } = useAuth();
  const now = useNow();
  const [busy, setBusy] = useState(false);
  const [copied, setCopied] = useState(false);
  const claims = decodeJwt(accessToken);

  async function handleRefresh() {
    setBusy(true);
    try {
      await refreshAccessToken();
      await refreshUser();
    } catch {
      await logout('Your refresh token is no longer valid (it may have been revoked by an account change). Please sign in again.');
    } finally {
      setBusy(false);
    }
  }

  async function handleCopy() {
    if (!accessToken) return;
    try {
      await navigator.clipboard.writeText(accessToken);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      // Clipboard may be unavailable over plain http on a non-localhost host.
    }
  }

  const roleMismatch = claims?.role && user && claims.role !== user.role;

  return (
    <Card
      title="Access token"
      subtitle="JWT claims (RS256, 15 min lifetime). The backend authorizes using these claims."
      actions={
        <>
          <Button size="sm" variant="secondary" onClick={handleCopy} disabled={!accessToken}>
            {copied ? 'Copied' : 'Copy'}
          </Button>
          <Button size="sm" variant="secondary" onClick={handleRefresh} loading={busy}>
            Refresh now
          </Button>
        </>
      }
    >
      {!claims ? (
        <p className="muted">No access token in memory.</p>
      ) : (
        <>
          {roleMismatch && (
            <Alert variant="warning">
              Your profile role is <strong>{user!.role}</strong> but this token still says <strong>{claims.role}</strong>.
              The backend keeps honouring the old role until the token expires or is replaced. Role changes revoke
              refresh tokens, so “Refresh now” will sign you out.
            </Alert>
          )}
          <dl className="details">
            <dt>sub</dt>
            <dd>
              <code>{claims.sub}</code>
            </dd>
            <dt>role</dt>
            <dd>{claims.role}</dd>
            <dt>email</dt>
            <dd>{claims.email}</dd>
            <dt>jti</dt>
            <dd>
              <code>{claims.jti}</code>
            </dd>
            <dt>iss / aud</dt>
            <dd>
              {claims.iss} / {Array.isArray(claims.aud) ? claims.aud.join(', ') : claims.aud}
            </dd>
            {claims.exp && (
              <>
                <dt>expires</dt>
                <dd>
                  {formatDateTime(claims.exp * 1000)} <span className="muted">({formatRemaining(claims.exp * 1000 - now)})</span>
                </dd>
              </>
            )}
          </dl>
        </>
      )}
    </Card>
  );
}
