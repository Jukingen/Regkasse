'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Form, Switch } from 'antd';

import { getFiskalySettings, updateFiskalySettings } from '@/features/dashboard/api/fiskalyStatus';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';

const SETTINGS_QUERY_KEY = ['admin', 'fiskaly', 'settings'] as const;

/** Enable/disable Fiskaly SIGN AT. Default on until the API returns a stored overlay. */
export function FiskalyEnabledSwitch() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { canManageCashRegisters } = usePermissions();

  const query = useQuery({
    queryKey: SETTINGS_QUERY_KEY,
    queryFn: ({ signal }) => getFiskalySettings(signal),
    enabled: canManageCashRegisters,
    staleTime: 15_000,
  });

  const mutation = useMutation({
    mutationFn: (enabled: boolean) => updateFiskalySettings(enabled),
    onSuccess: async (data) => {
      queryClient.setQueryData(SETTINGS_QUERY_KEY, data);
      await queryClient.invalidateQueries({ queryKey: ['admin', 'fiskaly'] });
      notify.successKey('tseFiskaly.saveSuccess');
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FiskalySettings.update',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  if (!canManageCashRegisters) {
    return null;
  }

  return (
    <Form.Item
      label={t('settings.form.tse.fiskalyEnabled')}
      extra={t('settings.form.tse.fiskalyEnabledHint')}
    >
      <Switch
        checked={query.data?.enabled ?? true}
        loading={mutation.isPending || query.isLoading}
        onChange={(checked) => mutation.mutate(checked)}
      />
    </Form.Item>
  );
}
