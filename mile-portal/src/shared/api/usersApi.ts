import type { ListUsersParams, PagedResult, RegisterRequest, UpdateUserRequest, User } from '@/shared/types/auth';
import { api } from './client';

/** AuthService: /api/v1/users/* */
export const usersApi = {
  me: () => api.get<User>('/users/me').then((r) => r.data),

  /** ADMIN only. */
  list: (params: ListUsersParams = {}) => api.get<PagedResult<User>>('/users', { params }).then((r) => r.data),

  /** ADMIN only; any human role including ADMIN. */
  create: (body: RegisterRequest) => api.post<User>('/users', body).then((r) => r.data),

  /** Owner (email/password, needs currentPassword) or ADMIN (anything). */
  update: (id: string, body: UpdateUserRequest) => api.put<User>(`/users/${id}`, body).then((r) => r.data),

  /** Owner or ADMIN. */
  remove: (id: string) => api.delete(`/users/${id}`).then(() => undefined),
};
