/** ShipmentService DTOs (/api/v1/shipments). */
export const SHIPMENT_STATUSES = ['CREATED', 'DISPATCHED', 'IN_TRANSIT', 'DELIVERED', 'FAILED', 'CANCELLED'] as const;
export type ShipmentStatus = (typeof SHIPMENT_STATUSES)[number];

export const ACTIVE_STATUSES: readonly ShipmentStatus[] = ['DISPATCHED', 'IN_TRANSIT'];
export const FINAL_STATUSES: readonly ShipmentStatus[] = ['DELIVERED', 'FAILED', 'CANCELLED'];

export interface StatusEntry {
  status: ShipmentStatus;
  notes: string | null;
  actorId: string;
  createdAt: string;
}

export interface Shipment {
  id: string;
  trackingNumber: string;
  status: ShipmentStatus;
  senderId: string;
  senderUserId: string;
  recipientId: string;
  recipientUserId: string;
  originAddress: string;
  destinationAddress: string;
  weightKg: number;
  assignedDriverId: string | null;
  assignedVehicleId: string | null;
  history: StatusEntry[];
  createdAt: string;
  updatedAt: string;
}

export interface ShipmentSummary {
  id: string;
  trackingNumber: string;
  status: ShipmentStatus;
  senderId: string;
  recipientId: string;
  destinationAddress: string;
  weightKg: number;
  assignedDriverId: string | null;
  createdAt: string;
  updatedAt: string;
}

/** Public tracking (S3): no personal data. */
export interface PublicShipmentTracking {
  trackingNumber: string;
  status: ShipmentStatus;
  assignedDriverId: string | null;
  assignedVehicleId: string | null;
  timeline: { status: ShipmentStatus; at: string }[];
  updatedAt: string;
}

export interface CreateShipmentRequest {
  senderClientId?: string;
  recipientClientId?: string;
  recipientEmail?: string;
  weightKg: number;
  originAddress?: string | null;
  destinationAddress?: string | null;
  notes?: string | null;
}

export type ShipmentParty = 'sender' | 'recipient';

export interface ListShipmentsParams {
  page?: number;
  pageSize?: number;
  status?: ShipmentStatus;
  as?: ShipmentParty;
  driverId?: string;
  search?: string;
}
