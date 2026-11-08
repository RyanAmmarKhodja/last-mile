import { createContext, useCallback, useContext, useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from 'react';
import { authApi, refreshAccessToken, tokenStore, usersApi } from '@/shared/api';
import type { User, UserRole } from '@/shared/types/auth';

export type AuthStatus = 'loading' | 'authenticated' | 'anonymous';

interface AuthContextValue {
  status: AuthStatus;
  /** Profile from GET /users/me: the source of truth for what the UI shows. */
  user: User | null;
  /** Current access token (memory only), exposed so the UI can show its claims. */
  accessToken: string | null;
  /** One-off message for the login screen (session expired, password changed...). */
  notice: string | null;
  login: (email: string, password: string) => Promise<void>;
  logout: (notice?: string) => Promise<void>;
  refreshUser: () => Promise<User | null>;
  hasRole: (...roles: UserRole[]) => boolean;
  clearNotice: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [user, setUser] = useState<User | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const accessToken = useSyncExternalStore(tokenStore.subscribe, tokenStore.getAccessToken);

  // Resume the session after a reload: refresh token -> new access token -> profile.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (!tokenStore.getRefreshToken()) {
        setStatus('anonymous');
        return;
      }
      try {
        await refreshAccessToken();
        const me = await usersApi.me();
        if (cancelled) return;
        setUser(me);
        setStatus('authenticated');
      } catch {
        tokenStore.clear();
        if (cancelled) return;
        setUser(null);
        setStatus('anonymous');
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // A failed token refresh anywhere in the app ends the session.
  useEffect(
    () =>
      tokenStore.onSessionExpired(() => {
        setUser(null);
        setStatus('anonymous');
        setNotice('Your session has expired. Please sign in again.');
      }),
    [],
  );

  const login = useCallback(async (email: string, password: string) => {
    const tokens = await authApi.login({ email, password });
    tokenStore.set(tokens);
    try {
      const me = await usersApi.me();
      setUser(me);
      setStatus('authenticated');
      setNotice(null);
    } catch (err) {
      tokenStore.clear();
      throw err;
    }
  }, []);

  const logout = useCallback(async (message?: string) => {
    const refreshToken = tokenStore.getRefreshToken();
    try {
      if (tokenStore.getAccessToken()) await authApi.logout(refreshToken);
    } catch {
      // Best effort: the local session is cleared regardless.
    } finally {
      tokenStore.clear();
      setUser(null);
      setStatus('anonymous');
      setNotice(message ?? null);
    }
  }, []);

  const refreshUser = useCallback(async () => {
    const me = await usersApi.me();
    setUser(me);
    return me;
  }, []);

  const hasRole = useCallback((...roles: UserRole[]) => !!user && roles.includes(user.role), [user]);
  const clearNotice = useCallback(() => setNotice(null), []);

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, accessToken, notice, login, logout, refreshUser, hasRole, clearNotice }),
    [status, user, accessToken, notice, login, logout, refreshUser, hasRole, clearNotice],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>.');
  return ctx;
}
