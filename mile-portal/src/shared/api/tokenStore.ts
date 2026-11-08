import type { TokenResponse } from '@/shared/types/auth';

/**
 * Holds the session tokens.
 * - Access token: memory only (short-lived, never persisted).
 * - Refresh token: localStorage so a page reload can resume the session.
 *   Good enough for this MVP; production should move it to an HttpOnly cookie set by the gateway.
 */
const REFRESH_KEY = 'lastmile.refreshToken';

let accessToken: string | null = null;
const changeListeners = new Set<() => void>();
const expiredListeners = new Set<() => void>();

function safeStorage<T>(fn: () => T, fallback: T): T {
  try {
    return fn();
  } catch {
    return fallback;
  }
}

export const tokenStore = {
  getAccessToken: () => accessToken,

  getRefreshToken: () => safeStorage(() => localStorage.getItem(REFRESH_KEY), null),

  set(tokens: TokenResponse) {
    accessToken = tokens.accessToken;
    safeStorage(() => localStorage.setItem(REFRESH_KEY, tokens.refreshToken), undefined);
    changeListeners.forEach((l) => l());
  },

  clear() {
    accessToken = null;
    safeStorage(() => localStorage.removeItem(REFRESH_KEY), undefined);
    changeListeners.forEach((l) => l());
  },

  /** Fires whenever tokens are set or cleared. */
  subscribe(listener: () => void) {
    changeListeners.add(listener);
    return () => void changeListeners.delete(listener);
  },

  /** Fires when a refresh attempt fails and the session is gone. */
  onSessionExpired(listener: () => void) {
    expiredListeners.add(listener);
    return () => void expiredListeners.delete(listener);
  },

  notifySessionExpired() {
    expiredListeners.forEach((l) => l());
  },
};
