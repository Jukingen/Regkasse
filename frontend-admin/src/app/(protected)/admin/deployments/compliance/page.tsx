'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Checkbox, Form, Input, Space, Typography } from 'antd';
import Link from 'next/link';
import { useState } from 'react';

import {
  fetchComplianceGate,
  submitComplianceSignoff,
} from '@/api/manual/deployments';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';

const QUERY_KEY = ['admin', 'deployments', 'compliance'] as const;

export default function DeploymentCompliancePage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { isSuperAdmin, hasPermission } = usePermissions();
  const canApprove =
    isSuperAdmin || hasPermission(PERMISSIONS.DEPLOYMENT_APPROVE) || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const queryClient = useQueryClient();
  const [imageTag, setImageTag] = useState('');
  const [notes, setNotes] = useState('');
  const [checklist, setChecklist] = useState({
    depExportTested: false,
    tseSignatureTested: false,
    finanzOnlineTestSubmission: false,
    ntpTimeSyncChecked: false,
    tenantIsolationVerified: false,
  });

  const breadcrumbs = buildPlatformAdminBreadcrumbs(t, 'deploymentSystem', [
    { title: t('nav.deployments'), href: '/admin/deployments' },
    { title: t('nav.deploymentCompliance') },
  ]);

  const gateQuery = useQuery({
    queryKey: [...QUERY_KEY, imageTag],
    queryFn: ({ signal }) => fetchComplianceGate(imageTag, 'production', signal),
    enabled: canApprove && imageTag.trim().length > 0,
  });

  const signoffMutation = useMutation({
    mutationFn: () =>
      submitComplianceSignoff({
        imageTag: imageTag.trim(),
        stage: 'production',
        notes: notes.trim() || undefined,
        checklist: {
          depExportTested: checklist.depExportTested,
          tseSignatureTested: checklist.tseSignatureTested,
          finanzOnlineTestSubmission: checklist.finanzOnlineTestSubmission,
          ntpTimeSyncChecked: checklist.ntpTimeSyncChecked,
          tenantIsolationVerified: checklist.tenantIsolationVerified,
        },
      }),
    onSuccess: async () => {
      notify.success(t('deployments.compliance.signoffSuccess'));
      await queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'Deployments.complianceSignoff',
        fallbackKey: 'deployments.compliance.signoffFailed',
      });
    },
  });

  if (!canApprove) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('deployments.compliance.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="error" showIcon title={t('deployments.compliance.accessDenied')} />
      </AdminPageShell>
    );
  }

  const allChecked = Object.values(checklist).every(Boolean);

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('deployments.compliance.pageTitle')}
        breadcrumbs={breadcrumbs}
        subtitle={t('deployments.compliance.introBody')}
      />

      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('deployments.compliance.introTitle')}
        description={
          <Space orientation="vertical">
            <Typography.Text>{t('deployments.compliance.pipelineHint')}</Typography.Text>
            <Link href="/admin/deployments">{t('deployments.compliance.linkDashboard')}</Link>
          </Space>
        }
      />

      <Card size="small" style={{ marginBottom: 16 }}>
        <Form layout="vertical">
          <Form.Item label={t('deployments.compliance.imageTag')} required>
            <Input
              value={imageTag}
              placeholder="sha-abcdef1"
              onChange={(e) => setImageTag(e.target.value)}
            />
          </Form.Item>
          <Form.Item label={t('deployments.compliance.checklistTitle')}>
            <Space orientation="vertical">
              {(
                [
                  ['depExportTested', 'depExport'],
                  ['tseSignatureTested', 'tseSignature'],
                  ['finanzOnlineTestSubmission', 'finanzOnline'],
                  ['ntpTimeSyncChecked', 'ntp'],
                  ['tenantIsolationVerified', 'tenantIsolation'],
                ] as const
              ).map(([key, labelKey]) => (
                <Checkbox
                  key={key}
                  checked={checklist[key]}
                  onChange={(e) =>
                    setChecklist((prev) => ({ ...prev, [key]: e.target.checked }))
                  }
                >
                  {t(`deployments.compliance.items.${labelKey}`)}
                </Checkbox>
              ))}
            </Space>
          </Form.Item>
          <Form.Item label={t('deployments.compliance.notes')}>
            <Input.TextArea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </Form.Item>
          <Button
            type="primary"
            disabled={!imageTag.trim() || !allChecked || signoffMutation.isPending}
            loading={signoffMutation.isPending}
            onClick={() => signoffMutation.mutate()}
          >
            {t('deployments.compliance.signoffButton')}
          </Button>
        </Form>
      </Card>

      {imageTag.trim() ? (
        <Card size="small" loading={gateQuery.isLoading} title={t('deployments.compliance.gateStatus')}>
          {gateQuery.data ? (
            <Space orientation="vertical">
              <Typography.Text>
                {t('deployments.compliance.gatePassed')}:{' '}
                {gateQuery.data.gatePassed
                  ? t('deployments.compliance.yes')
                  : t('deployments.compliance.no')}
              </Typography.Text>
              {gateQuery.data.latestSignoff ? (
                <Typography.Text type="secondary">
                  {t('deployments.compliance.signedBy', {
                    who:
                      gateQuery.data.latestSignoff.signedByDisplayName ||
                      gateQuery.data.latestSignoff.signedByUserId,
                    when: new Date(gateQuery.data.latestSignoff.signedAtUtc).toLocaleString(),
                  })}
                </Typography.Text>
              ) : (
                <Typography.Text type="secondary">{t('deployments.compliance.noSignoff')}</Typography.Text>
              )}
            </Space>
          ) : null}
        </Card>
      ) : null}
    </AdminPageShell>
  );
}
