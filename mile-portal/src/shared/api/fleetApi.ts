import type { PagedResult } from '@/shared/types/auth';
import type { Driver, DriverInput, ListDriversParams, ListVehiclesParams, Vehicle, VehicleInput } from '@/shared/types/fleet';
import { api } from './client';

/** FleetService: /api/v1/fleet/* */
export const fleetApi = {
  // Driver (self)
  me: () => api.get<Driver>('/fleet/drivers/me').then((r) => r.data),
  saveMe: (body: DriverInput) => api.put<Driver>('/fleet/drivers/me', body).then((r) => r.data),
  setMyAvailability: (isAvailable: boolean) =>
    api.put<Driver>('/fleet/drivers/me/availability', { isAvailable }).then((r) => r.data),

  // Drivers (ADMIN)
  listDrivers: (params: ListDriversParams = {}) =>
    api.get<PagedResult<Driver>>('/fleet/drivers', { params }).then((r) => r.data),
  /** Every driver with the given filters, page by page (pageSize ≤ 100 on the server). Capped at 50 pages. */
  async allDrivers(params: Omit<ListDriversParams, 'page' | 'pageSize'> = {}): Promise<Driver[]> {
    const all: Driver[] = [];
    for (let page = 1; page <= 50; page++) {
      const res = await fleetApi.listDrivers({ ...params, page, pageSize: 100 });
      all.push(...res.items);
      if (all.length >= res.totalCount || res.items.length === 0) break;
    }
    return all;
  },
  driver: (id: string) => api.get<Driver>(`/fleet/drivers/${id}`).then((r) => r.data),
  updateDriver: (id: string, body: DriverInput) => api.put<Driver>(`/fleet/drivers/${id}`, body).then((r) => r.data),
  setDriverAvailability: (id: string, isAvailable: boolean) =>
    api.put<Driver>(`/fleet/drivers/${id}/availability`, { isAvailable }).then((r) => r.data),

  // Vehicles (ADMIN)
  listVehicles: (params: ListVehiclesParams = {}) =>
    api.get<PagedResult<Vehicle>>('/fleet/vehicles', { params }).then((r) => r.data),
  vehicle: (id: string) => api.get<Vehicle>(`/fleet/vehicles/${id}`).then((r) => r.data),
  createVehicle: (body: VehicleInput) => api.post<Vehicle>('/fleet/vehicles', body).then((r) => r.data),
  updateVehicle: (id: string, body: VehicleInput) => api.put<Vehicle>(`/fleet/vehicles/${id}`, body).then((r) => r.data),
  assignVehicle: (id: string, driverId: string | null) =>
    api.put<Vehicle>(`/fleet/vehicles/${id}/assignment`, { driverId }).then((r) => r.data),
  setVehicleActive: (id: string, isActive: boolean) =>
    api.put<Vehicle>(`/fleet/vehicles/${id}/status`, { isActive }).then((r) => r.data),
};
