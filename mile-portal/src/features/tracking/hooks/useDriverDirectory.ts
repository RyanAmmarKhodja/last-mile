import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { fleetApi } from '@/shared/api';
import { qk } from '@/shared/query/queryClient';
import type { Driver } from '@/shared/types/fleet';

/** ADMIN: every driver (all statuses), keyed by driver id, to put names and plates on live positions. */
export function useDriverDirectory(enabled = true) {
  const query = useQuery({
    queryKey: qk.allDrivers,
    queryFn: async () => {
      // Default listing is ACTIVE only; live drivers are ACTIVE, but shipments may reference older drivers.
      const [active, inactive] = await Promise.all([
        fleetApi.allDrivers({ status: 'ACTIVE' }),
        fleetApi.allDrivers({ status: 'INACTIVE' }),
      ]);
      return [...active, ...inactive];
    },
    staleTime: 60_000,
    enabled,
  });
  const byId = useMemo(() => new Map<string, Driver>((query.data ?? []).map((d) => [d.id, d])), [query.data]);
  return { ...query, byId };
}
