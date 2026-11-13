import { useMutation } from '@tanstack/react-query';

import {
  type TemporaryPasswordResponse,
  resetUserPasswordWithGeneration,
} from '@/features/users/api/usersGateway';

export function useResetUserPassword() {
  return useMutation({
    mutationFn: ({
      tenantId,
      userId,
    }: {
      tenantId: string;
      userId: string;
    }): Promise<TemporaryPasswordResponse> => resetUserPasswordWithGeneration(tenantId, userId),
  });
}
