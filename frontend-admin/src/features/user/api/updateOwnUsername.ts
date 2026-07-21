'use client';

/** PATCH /api/UserManagement/me/username — self-service (same boundary as me/password). */
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { USERNAME_CHANGE_POLICY_QUERY_KEY } from '@/features/user/hooks/useUsernameChangePolicy';
import { customInstance } from '@/lib/axios';

export type UpdateOwnUsernamePayload = {
  newUsername: string;
  reason?: string;
};

export type UpdateOwnUsernameResponse = {
  oldUsername?: string | null;
  newUsername?: string | null;
  message?: string;
};

async function updateOwnUsername(
  payload: UpdateOwnUsernamePayload
): Promise<UpdateOwnUsernameResponse> {
  return customInstance<UpdateOwnUsernameResponse>({
    url: '/api/UserManagement/me/username',
    method: 'PATCH',
    data: {
      newUsername: payload.newUsername,
      reason: payload.reason,
    },
  });
}

export function useUpdateOwnUsernameMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: updateOwnUsername,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: USERNAME_CHANGE_POLICY_QUERY_KEY });
    },
  });
}
