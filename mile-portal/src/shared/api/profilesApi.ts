import type { PagedResult } from '@/shared/types/auth';
import type { AdminProfile, AdminProfileInput, ClientProfile, ClientProfileInput, ProfileStatus } from '@/shared/types/profiles';
import { api } from './client';

export interface ListProfilesParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: ProfileStatus;
}

/** ProfileService: /api/v1/profiles/* */
export const profilesApi = {
  myClient: () => api.get<ClientProfile>('/profiles/clients/me').then((r) => r.data),
  saveMyClient: (body: ClientProfileInput) => api.put<ClientProfile>('/profiles/clients/me', body).then((r) => r.data),
  myAdmin: () => api.get<AdminProfile>('/profiles/admins/me').then((r) => r.data),
  saveMyAdmin: (body: AdminProfileInput) => api.put<AdminProfile>('/profiles/admins/me', body).then((r) => r.data),

  /** ADMIN. */
  listClients: (params: ListProfilesParams = {}) =>
    api.get<PagedResult<ClientProfile>>('/profiles/clients', { params }).then((r) => r.data),
  /** ADMIN or owner. */
  client: (id: string) => api.get<ClientProfile>(`/profiles/clients/${id}`).then((r) => r.data),
};
