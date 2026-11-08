export type JwtClaims = Record<string, unknown> & {
  sub?: string;
  jti?: string;
  role?: string;
  email?: string;
  iss?: string;
  aud?: string | string[];
  iat?: number;
  exp?: number;
};

/**
 * Decodes a JWT payload for display only. The signature is NOT verified here; the backend
 * verifies every token it receives.
 */
export function decodeJwt(token: string | null): JwtClaims | null {
  if (!token) return null;
  const payload = token.split('.')[1];
  if (!payload) return null;
  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(payload.length / 4) * 4, '=');
    const json = new TextDecoder().decode(Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)));
    return JSON.parse(json) as JwtClaims;
  } catch {
    return null;
  }
}
