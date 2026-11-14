/** FleetTrackingService DTOs (/api/v1/tracking). */
import type { ShipmentStatus } from './shipments';

export interface LivePosition {
  driverId: string;
  vehicleId: string | null;
  shipmentId: string | null;
  trackingNumber: string | null;
  latitude: number;
  longitude: number;
  recordedAt: string;
  receivedAt: string;
  speedKmh: number | null;
  headingDeg: number | null;
  accuracyM: number | null;
}

export interface PublicPoint {
  latitude: number;
  longitude: number;
  recordedAt: string;
  speedKmh: number | null;
  headingDeg: number | null;
}

/** GET /tracking/track/{tn}: position is null unless DISPATCHED/IN_TRANSIT and the driver is live. */
export interface PublicPosition {
  trackingNumber: string;
  status: ShipmentStatus;
  live: boolean;
  position: PublicPoint | null;
}

export interface HistoryPoint {
  latitude: number;
  longitude: number;
  recordedAt: string;
  speedKmh: number | null;
  headingDeg: number | null;
  vehicleId: string | null;
  shipmentId: string | null;
}

export interface HistoryParams {
  from?: string;
  to?: string;
  limit?: number;
}
