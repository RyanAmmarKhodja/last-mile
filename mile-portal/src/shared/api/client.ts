import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import type { TokenResponse } from '@/shared/types/auth';
import { tokenStore } from './tokenStore';

/**
 * All calls go to the same origin under /api/v1. The NGINX gateway routes them to the right
 * microservice (in dev, Vite proxies /api to the gateway).
 */
export const API_BASE_URL = '/api/v1';

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

/** Interceptor-free instance for the refresh call itself (avoids recursion). */
const bare = axios.create({ baseURL: API_BASE_URL });

let refreshInFlight: Promise<string> | null = null;

/**
 * Exchanges the stored refresh token for a new token pair. Single-flight: concurrent callers share
 * one request, which matters because AuthService rotates the refresh token on every use.
 */
export function refreshAccessToken(): Promise<string> {
  if (!refreshInFlight) {
    const refreshToken = tokenStore.getRefreshToken();
    const request = refreshToken
      ? bare.post<TokenResponse>('/auth/refresh', { refreshToken }).then((res) => {
          tokenStore.set(res.data);
          return res.data.accessToken;
        })
      : Promise.reject(new Error('No refresh token stored.'));
    refreshInFlight = request.finally(() => {
      refreshInFlight = null;
    });
  }
  return refreshInFlight;
}

api.interceptors.request.use((config) => {
  const token = tokenStore.getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean };

/** Endpoints where a 401 means "bad credentials", not "expired access token". */
const NO_REFRESH_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetriableConfig | undefined;
    const shouldRefresh =
      error.response?.status === 401 &&
      config !== undefined &&
      !config._retried &&
      !NO_REFRESH_PATHS.some((p) => config.url?.startsWith(p)) &&
      tokenStore.getRefreshToken() !== null;

    if (!shouldRefresh) throw error;

    config._retried = true;
    try {
      const token = await refreshAccessToken();
      config.headers.Authorization = `Bearer ${token}`;
      return api(config);
    } catch {
      tokenStore.clear();
      tokenStore.notifySessionExpired();
      throw error;
    }
  },
);
