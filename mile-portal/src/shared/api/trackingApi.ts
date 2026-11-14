import type { HistoryParams, HistoryPoint, LivePosition, PublicPosition } from '@/shared/types/tracking';
import { api } from './client';

/** FleetTrackingService: /api/v1/tracking/* */
export const trackingApi = {
  /** Public (T3). */
  publicPosition: (trackingNumber: string) =>
    api.get<PublicPosition>(`/tracking/track/${encodeURIComponent(trackingNumber)}`).then((r) => r.data),
  publicStreamUrl: (trackingNumber: string) => `/api/v1/tracking/track/${encodeURIComponent(trackingNumber)}/stream`,

  /** ADMIN. */
  live: () => api.get<LivePosition[]>('/tracking/live').then((r) => r.data),
  liveStreamUrl: '/api/v1/tracking/live/stream',
  /** ADMIN or the driver themselves. */
  driverLive: (driverId: string) => api.get<LivePosition>(`/tracking/drivers/${driverId}/live`).then((r) => r.data),
  driverHistory: (driverId: string, params: HistoryParams = {}) =>
    api.get<HistoryPoint[]>(`/tracking/drivers/${driverId}/history`, { params }).then((r) => r.data),
  shipmentRoute: (shipmentId: string) => api.get<HistoryPoint[]>(`/tracking/shipments/${shipmentId}/route`).then((r) => r.data),

  /** DRIVER. */
  myLive: () => api.get<LivePosition>('/tracking/drivers/me/live').then((r) => r.data),
  myHistory: (params: HistoryParams = {}) => api.get<HistoryPoint[]>('/tracking/drivers/me/history', { params }).then((r) => r.data),
};
