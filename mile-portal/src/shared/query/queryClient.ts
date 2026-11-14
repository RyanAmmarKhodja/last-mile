import { QueryClient } from '@tanstack/react-query';
import { getErrorStatus } from '@/shared/api';

/** P6: one QueryClient for the app. 4xx answers are final; network / 5xx errors get one retry. */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 15_000,
      refetchOnWindowFocus: false,
      retry: (count, err) => {
        const status = getErrorStatus(err);
        if (status !== undefined && status >= 400 && status < 500) return false;
        return count < 1;
      },
    },
  },
});

/** Query keys, grouped by owning service so invalidation matches the data's owner. */
export const qk = {
  shipments: ['shipments'] as const,
  shipmentList: (params: object) => ['shipments', 'list', params] as const,
  shipmentCount: (params: object) => ['shipments', 'count', params] as const,
  shipment: (id: string) => ['shipments', 'detail', id] as const,
  shipmentRoute: (id: string) => ['tracking', 'route', id] as const,
  publicShipment: (tn: string) => ['shipments', 'public', tn] as const,

  fleet: ['fleet'] as const,
  drivers: (params: object) => ['fleet', 'drivers', params] as const,
  allDrivers: ['fleet', 'drivers', 'all'] as const,
  driver: (id: string) => ['fleet', 'driver', id] as const,
  myDriver: ['fleet', 'drivers', 'me'] as const,
  vehicles: (params: object) => ['fleet', 'vehicles', params] as const,

  profiles: ['profiles'] as const,
  myClientProfile: ['profiles', 'clients', 'me'] as const,
  myAdminProfile: ['profiles', 'admins', 'me'] as const,
  client: (id: string) => ['profiles', 'clients', id] as const,
  clients: (params: object) => ['profiles', 'clients', 'list', params] as const,

  tracking: ['tracking'] as const,
  live: ['tracking', 'live'] as const,
  myLive: ['tracking', 'me', 'live'] as const,
  driverHistory: (id: string, from: string) => ['tracking', 'history', id, from] as const,
};
