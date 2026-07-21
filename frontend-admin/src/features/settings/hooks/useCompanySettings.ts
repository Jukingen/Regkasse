import { useQueryClient } from '@tanstack/react-query';

import {
  getGetApiCompanySettingsQueryKey,
  useGetApiCompanySettings,
  usePutApiCompanySettings,
} from '@/api/generated/company-settings/company-settings';
import type { UpdateCompanySettingsRequest } from '@/api/generated/model';

export function useCompanySettings() {
  return useGetApiCompanySettings();
}

export function useUpdateCompanySettings() {
  const queryClient = useQueryClient();
  const mutation = usePutApiCompanySettings({
    mutation: {
      onSuccess: async () => {
        await queryClient.invalidateQueries({ queryKey: getGetApiCompanySettingsQueryKey() });
      },
    },
  });

  return {
    ...mutation,
    updateSettings: (data: UpdateCompanySettingsRequest) => mutation.mutateAsync({ data }),
    isLoading: mutation.isPending,
  };
}
