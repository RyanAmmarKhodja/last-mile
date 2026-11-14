import { useMutation, useQueryClient } from '@tanstack/react-query';
import { qk } from '@/shared/query/queryClient';
import type { Shipment } from '@/shared/types/shipments';

/**
 * A shipment transition. On success the detail cache gets the returned shipment, and lists, counts and
 * fleet data (D3: availability follows dispatch and completion) are refetched.
 */
export function useShipmentMutation<TVars>(fn: (vars: TVars) => Promise<Shipment>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: (shipment) => {
      client.setQueryData(qk.shipment(shipment.id), shipment);
      void client.invalidateQueries({ queryKey: qk.shipments });
      void client.invalidateQueries({ queryKey: qk.fleet });
    },
  });
}
