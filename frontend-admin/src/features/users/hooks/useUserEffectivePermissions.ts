import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type UpsertUserPermissionOverrideRequest,
  deleteUserPermissionOverride,
  getUserEffectivePermissions,
  upsertUserPermissionOverride,
  userEffectivePermissionsQueryKey,
} from '../api/userPermissionOverridesApi';

export function useUserEffectivePermissions(userId: string | null | undefined, enabled: boolean) {
  return useQuery({
    queryKey: userId
      ? userEffectivePermissionsQueryKey(userId)
      : ['user-effective-permissions', 'none'],
    queryFn: () => getUserEffectivePermissions(userId!),
    enabled: enabled && Boolean(userId),
  });
}

export function useUserPermissionOverrideMutations(userId: string | null | undefined) {
  const queryClient = useQueryClient();

  const invalidate = () => {
    if (userId) {
      void queryClient.invalidateQueries({ queryKey: userEffectivePermissionsQueryKey(userId) });
    }
  };

  const upsertMutation = useMutation({
    mutationFn: (body: UpsertUserPermissionOverrideRequest) =>
      upsertUserPermissionOverride(userId!, body),
    onSuccess: invalidate,
  });

  const deleteMutation = useMutation({
    mutationFn: (overrideId: string) => deleteUserPermissionOverride(userId!, overrideId),
    onSuccess: invalidate,
  });

  return { upsertMutation, deleteMutation };
}
