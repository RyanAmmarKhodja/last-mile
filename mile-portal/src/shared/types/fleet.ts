/** FleetService DTOs (/api/v1/fleet). */
export type DriverStatus = 'ACTIVE' | 'INACTIVE' | 'DELETED';

export interface DriverVehicle {
  id: string;
  licensePlate: string;
  model: string;
  capacityKg: number;
}

export interface Driver {
  id: string;
  userId: string;
  firstName: string;
  lastName: string;
  licenseNumber: string;
  phoneNumber: string;
  isAvailable: boolean;
  status: DriverStatus;
  vehicle: DriverVehicle | null;
  createdAt: string;
  updatedAt: string;
}

/** PUT /fleet/drivers/me and /fleet/drivers/{id} (full replacement). */
export interface DriverInput {
  firstName: string;
  lastName: string;
  phoneNumber: string;
  licenseNumber: string;
}

export interface Vehicle {
  id: string;
  licensePlate: string;
  model: string;
  capacityKg: number;
  isActive: boolean;
  assignedDriverId: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface VehicleInput {
  licensePlate: string;
  model: string;
  capacityKg: number;
}

export interface ListDriversParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: DriverStatus;
  available?: boolean;
}

export interface ListVehiclesParams {
  page?: number;
  pageSize?: number;
  search?: string;
  active?: boolean;
  assigned?: boolean;
}
