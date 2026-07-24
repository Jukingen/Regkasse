'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Empty,
  Rate,
  Select,
  Space,
  Tag,
  Typography,
} from 'antd';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  applyTseRecommendation,
  dismissTseRecommendation,
  getTseRecommendations,
  rateTseRecommendation,
} from '@/features/tse-recommendations/api/recommendations';
import type { TseRecommendation } from '@/features/tse-recommendations/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-recommendations'] as const;

function categoryColor(category: string): string {
  switch (category) {
    case 'Cost':
      return 'green';
    case 'Security':
      return 'red';
    case 'Performance':
      return 'purple';
    default:
      return 'blue';
  }
}

function impactColor(impact: string): string {
  switch (impact) {
    case 'High':
      return 'red';
    case 'Medium':
      return 'orange';
    default:
      return 'default';
  }
}

function impactLabel(impact: string, t: (key: string) => string): string {
  switch (impact) {
    case 'High':
      return t('tseRecommendations.impactHigh');
    case 'Medium':
      return t('tseRecommendations.impactMedium');
    case 'Low':
      return t('tseRecommendations.impactLow');
    default:
      return impact;
  }
}

export default function TseRecommendationsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-recommendations'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const listQuery = useQuery({
    queryKey: [...KEY, 'list', tenantId],
    queryFn: ({ signal }) => getTseRecommendations(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: [...KEY, 'list', tenantId] });
  };

  const applyMutation = useMutation({
    mutationFn: (id: string) => applyTseRecommendation(id),
    onSuccess: async (result) => {
      notify.success(result.message || t('tseRecommendations.applySuccess'));
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseRecommendations.apply',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const dismissMutation = useMutation({
    mutationFn: (id: string) => dismissTseRecommendation(id),
    onSuccess: async (result) => {
      notify.success(result.message || t('tseRecommendations.dismissSuccess'));
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseRecommendations.dismiss',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const rateMutation = useMutation({
    mutationFn: ({ id, rating }: { id: string; rating: number }) =>
      rateTseRecommendation(id, rating),
    onSuccess: async () => {
      notify.success(t('tseRecommendations.rateSuccess'));
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseRecommendations.rate',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const recommendations = listQuery.data ?? [];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseRecommendations.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseRecommendations.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseRecommendations.title') }]}
        extra={
          <Select
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 260 }}
            placeholder={t('tseRecommendations.tenantLabel')}
            loading={tenantsQuery.isLoading}
            value={tenantId}
            onChange={setTenantId}
            options={(tenantsQuery.data ?? []).map((tenant) => ({
              value: tenant.id,
              label: `${tenant.name} (${tenant.slug})`,
            }))}
          />
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseRecommendations.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseRecommendations.emptySelect')} />
      ) : listQuery.isError ? (
        <Alert type="error" showIcon message={t('tseRecommendations.loadError')} />
      ) : (
        <Card title={t('tseRecommendations.cardTitle')} loading={listQuery.isLoading}>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('tseRecommendations.diagnosticNote')}
          />

          {recommendations.length === 0 ? (
            <Empty description={t('tseRecommendations.emptyList')} />
          ) : (
            <Space direction="vertical" size={16} style={{ width: '100%' }}>
              {recommendations.map((rec) => (
                <RecommendationCard
                  key={rec.id}
                  rec={rec}
                  t={t}
                  applying={applyMutation.isPending}
                  dismissing={dismissMutation.isPending}
                  onApply={() => applyMutation.mutate(rec.id)}
                  onDismiss={() => dismissMutation.mutate(rec.id)}
                  onRate={(rating) => rateMutation.mutate({ id: rec.id, rating })}
                />
              ))}
            </Space>
          )}
        </Card>
      )}
    </>
  );
}

function RecommendationCard({
  rec,
  t,
  applying,
  dismissing,
  onApply,
  onDismiss,
  onRate,
}: {
  rec: TseRecommendation;
  t: (key: string) => string;
  applying: boolean;
  dismissing: boolean;
  onApply: () => void;
  onDismiss: () => void;
  onRate: (rating: number) => void;
}) {
  return (
    <Card size="small" style={{ borderRadius: 8 }}>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          gap: 16,
          flexWrap: 'wrap',
        }}
      >
        <div style={{ flex: 1, minWidth: 240 }}>
          <Space wrap size={8}>
            <Tag color={categoryColor(rec.category)}>{rec.category}</Tag>
            <Tag color={impactColor(rec.impact)}>{impactLabel(rec.impact, t)}</Tag>
            {rec.isApplied ? <Tag color="success">{t('tseRecommendations.applied')}</Tag> : null}
          </Space>
          <Typography.Title level={5} style={{ marginTop: 8, marginBottom: 4 }}>
            {rec.title}
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
            {rec.description}
          </Typography.Paragraph>
          <Space wrap size="large">
            <Typography.Text type="secondary">
              {t('tseRecommendations.potentialSavings')}: €{rec.estimatedSavings}
              {t('tseRecommendations.perMonth')}
            </Typography.Text>
            <Typography.Text type="secondary">
              {t('tseRecommendations.effort')}: {rec.effortScore}/10
            </Typography.Text>
          </Space>
          <div style={{ marginTop: 12 }}>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
              {t('tseRecommendations.rateLabel')}
            </Typography.Text>
            <Rate
              value={rec.rating || 0}
              onChange={(value) => {
                if (value >= 1) onRate(value);
              }}
            />
          </div>
        </div>
        <Space direction="vertical">
          <Button
            type="primary"
            size="small"
            disabled={rec.isApplied}
            loading={applying}
            onClick={onApply}
          >
            {t('tseRecommendations.apply')}
          </Button>
          <Button size="small" loading={dismissing} onClick={onDismiss}>
            {t('tseRecommendations.dismiss')}
          </Button>
        </Space>
      </div>
    </Card>
  );
}
