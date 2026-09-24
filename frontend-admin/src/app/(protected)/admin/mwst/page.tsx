'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Descriptions, Typography } from 'antd';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  fetchMwstCanary,
  mwstCanaryQueryKey,
  rollbackMwstCanary,
} from '@/features/mwst-canary/api';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

export default function AdminMwstCanaryPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: mwstCanaryQueryKey,
    queryFn: ({ signal }) => fetchMwstCanary(signal),
  });

  const rollback = useMutation({
    mutationFn: rollbackMwstCanary,
    onSuccess: async (data) => {
      queryClient.setQueryData(mwstCanaryQueryKey, data);
      notify.success(t('nav.mwstRolledBack'));
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'MwstCanary.rollback', fallbackKey: 'common.errorGeneric' });
    },
  });

  const data = query.data;

  return (
    <>
      <AdminPageHeader title={t('nav.mwstCanaryTitle')} subtitle={t('nav.mwstCanaryBody')} />
      <Card>
        {query.isError ? <Alert type="error" showIcon title={t('common.errorGeneric')} /> : null}
        <Descriptions column={1}>
          <Descriptions.Item label="Canary tenant">{data?.canaryTenantId || '—'}</Descriptions.Item>
          <Descriptions.Item label={t('nav.mwstFlag')}>
            {data?.flagEnabled ? 'true' : 'false'}
          </Descriptions.Item>
          <Descriptions.Item label="UseTestEndpoint">
            {data?.useTestEndpoint ? 'true' : 'false'}
          </Descriptions.Item>
          <Descriptions.Item label={t('nav.mwstKsUnused')}>
            {data?.kassenSicherheitProvider || 'not-configured'}
          </Descriptions.Item>
        </Descriptions>
        <Typography.Paragraph type="secondary">{t('nav.mwstCanaryBody')}</Typography.Paragraph>
        <Button
          danger
          loading={rollback.isPending}
          disabled={!data?.canaryTenantId}
          onClick={() => rollback.mutate()}
        >
          {t('nav.mwstRollback')}
        </Button>
      </Card>
    </>
  );
}
