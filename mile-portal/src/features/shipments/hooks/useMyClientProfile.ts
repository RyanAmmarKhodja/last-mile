import { useQuery } from '@tanstack/react-query';
import { profilesApi } from '@/shared/api';
import { useAuth } from '@/shared/context/AuthContext';
import { qk } from '@/shared/query/queryClient';

/** CLIENT: own profile (its id tells sender from recipient in shipment lists). */
export function useMyClientProfile() {
  const { user } = useAuth();
  return useQuery({ queryKey: qk.myClientProfile, queryFn: profilesApi.myClient, enabled: user?.role === 'CLIENT', staleTime: 60_000 });
}

/** ADMIN: a client profile by id (sender / recipient names), cached for the session. */
export function useClientProfile(id: string | null | undefined, enabled = true) {
  return useQuery({ queryKey: qk.client(id ?? ''), queryFn: () => profilesApi.client(id!), enabled: !!id && enabled, staleTime: 5 * 60_000 });
}
