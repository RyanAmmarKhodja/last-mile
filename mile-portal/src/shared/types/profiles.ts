/** ProfileService DTOs (/api/v1/profiles). */
export type ProfileStatus = 'ACTIVE' | 'INACTIVE' | 'DELETED';

export interface ClientProfile {
  id: string;
  userId: string;
  firstName: string;
  lastName: string;
  companyName: string | null;
  phoneNumber: string;
  address: string;
  status: ProfileStatus;
  createdAt: string;
  updatedAt: string;
}

/** PUT /profiles/clients/me and /profiles/clients/{id} (full replacement). */
export interface ClientProfileInput {
  firstName: string;
  lastName: string;
  companyName?: string | null;
  phoneNumber: string;
  address: string;
}

export interface AdminProfile {
  id: string;
  userId: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  status: ProfileStatus;
  createdAt: string;
  updatedAt: string;
}

export interface AdminProfileInput {
  firstName: string;
  lastName: string;
  phoneNumber?: string | null;
}
