import type { LoginRequest, RegisterRequest, TokenResponse, User } from '@/shared/types/auth';
import { api } from './client';

/** AuthService: /api/v1/auth/* */
export const authApi = {
  register: (body: RegisterRequest) => api.post<User>('/auth/register', body).then((r) => r.data),

  login: (body: LoginRequest) => api.post<TokenResponse>('/auth/login', body).then((r) => r.data),

  /** Blacklists the current access token and revokes the given refresh token. */
  logout: (refreshToken: string | null) =>
    api.post<void>('/auth/logout', { refreshToken }).then(() => undefined),
};
