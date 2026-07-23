'use client';

import { Alert, Button, Card, Checkbox, Col, Row, Select, Space, Typography } from 'antd';
import { useParams } from 'next/navigation';
import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import {
  getTenantIndustryTemplate,
  listIndustryTemplates,
  setTenantIndustryTemplate,
} from '@/features/users/api/industryTemplatesApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function TenantIndustryPage() {
  const params = useParams();
  const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';
  const { t } = useI18n();
  const { message } = useAntdApp();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const queryClient = useQueryClient();
  const canEdit = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const canView =
    canEdit || hasPermission(PERMISSIONS.ROLE_VIEW) || hasPermission(PERMISSIONS.ROLE_MANAGE);

  const [templateId, setTemplateId] = useState<string>('none');
  const [seedMissing, setSeedMissing] = useState(true);

  const templatesQuery = useQuery({
    queryKey: ['industry-templates'],
    queryFn: listIndustryTemplates,
    enabled: canView,
  });
  const tenantQuery = useQuery({
    queryKey: ['industry-templates', 'tenant', tenantId],
    queryFn: () => getTenantIndustryTemplate(tenantId),
    enabled: canView && Boolean(tenantId),
  });

  useEffect(() => {
    if (!tenantQuery.data) return;
    setTemplateId(tenantQuery.data.industryTemplateId || 'none');
  }, [tenantQuery.data]);

  const saveMutation = useMutation({
    mutationFn: () =>
      setTenantIndustryTemplate(tenantId, {
        industryTemplateId: templateId === 'none' ? null : templateId,
        seedMissingStarters: seedMissing,
      }),
    onSuccess: (result) => {
      message.success(
        t('tenants.industry.saveSuccess', { count: result.startersCreated })
      );
      void queryClient.invalidateQueries({
        queryKey: ['industry-templates', 'tenant', tenantId],
      });
    },
    onError: () => message.error(t('tenants.industry.saveError')),
  });

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('tenants.list.pageTitle'), href: '/admin/tenants' },
    { title: t('tenants.industry.pageTitle') },
  ];

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('tenants.industry.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="warning" showIcon title={t('access.hub.accessDeniedTitle')} />
      </AdminPageShell>
    );
  }

  const options = [
    { value: 'none', label: t('tenants.create.industry.none') },
    ...(templatesQuery.data ?? []).map((tpl) => ({
      value: tpl.id,
      label: tpl.name,
    })),
  ];

  const selected = templatesQuery.data?.find((t) => t.id === templateId);

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('tenants.industry.pageTitle')} breadcrumbs={breadcrumbs} />
      <Typography.Paragraph type="secondary">{t('tenants.industry.intro')}</Typography.Paragraph>

      <Card loading={tenantQuery.isLoading}>
        <Space orientation="vertical" style={{ width: '100%' }} size={16}>
          <div>
            <Typography.Text strong>{t('tenants.industry.templateLabel')}</Typography.Text>
            <Select
              style={{ width: '100%', marginTop: 8 }}
              value={templateId}
              onChange={setTemplateId}
              options={options}
              disabled={!canEdit}
            />
          </div>

          {selected ? (
            <Alert
              type="info"
              showIcon
              title={selected.name}
              description={selected.description}
            />
          ) : null}

          {selected?.slots?.length ? (
            <Row gutter={[12, 12]}>
              {selected.slots.map((slot) => (
                <Col xs={24} md={12} key={slot.key}>
                  <Card size="small" title={slot.displayName}>
                    <Typography.Text type="secondary">
                      {slot.systemRole} · {slot.recommendedPackageSlugs.join(', ') || '—'}
                    </Typography.Text>
                  </Card>
                </Col>
              ))}
            </Row>
          ) : null}

          <Checkbox
            checked={seedMissing}
            disabled={!canEdit}
            onChange={(e) => setSeedMissing(e.target.checked)}
          >
            {t('tenants.industry.seedMissing')}
          </Checkbox>

          {canEdit ? (
            <Button
              type="primary"
              loading={saveMutation.isPending}
              onClick={() => saveMutation.mutate()}
            >
              {t('common.buttons.save')}
            </Button>
          ) : null}
        </Space>
      </Card>
    </AdminPageShell>
  );
}
