import type { PagedResult } from '@/shared/types/auth';
import type {
  CreateShipmentRequest,
  ListShipmentsParams,
  PublicShipmentTracking,
  Shipment,
  ShipmentSummary,
} from '@/shared/types/shipments';
import { api } from './client';

/** ShipmentService: /api/v1/shipments/* */
export const shipmentsApi = {
  list: (params: ListShipmentsParams = {}) =>
    api.get<PagedResult<ShipmentSummary>>('/shipments', { params }).then((r) => r.data),
  get: (id: string) => api.get<Shipment>(`/shipments/${id}`).then((r) => r.data),
  create: (body: CreateShipmentRequest) => api.post<Shipment>('/shipments', body).then((r) => r.data),

  /** Public, no login (S3). */
  track: (trackingNumber: string) =>
    api.get<PublicShipmentTracking>(`/shipments/track/${encodeURIComponent(trackingNumber)}`).then((r) => r.data),

  /** ADMIN. No driverId = auto-pick (S2). */
  dispatch: (id: string, driverId?: string | null) =>
    api.post<Shipment>(`/shipments/${id}/dispatch`, { driverId: driverId ?? null }).then((r) => r.data),
  pickup: (id: string) => api.post<Shipment>(`/shipments/${id}/pickup`, {}).then((r) => r.data),
  deliver: (id: string, notes?: string) => api.post<Shipment>(`/shipments/${id}/deliver`, { notes: notes || null }).then((r) => r.data),
  fail: (id: string, reason: string) => api.post<Shipment>(`/shipments/${id}/fail`, { reason }).then((r) => r.data),
  cancel: (id: string, notes?: string) => api.post<Shipment>(`/shipments/${id}/cancel`, { notes: notes || null }).then((r) => r.data),
};
