'use client';

import { CustomerServiceOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button,
  Card,
  Col,
  Form,
  Input,
  List,
  Modal,
  Progress,
  Row,
  Space,
  Statistic,
  Typography,
} from 'antd';
import React, { useState } from 'react';

import { createAdminFeedback } from '@/api/manual/adminFeedback';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { ActivitySummary } from '@/features/dashboard/components/ActivitySummary';
import { ManagerLicenseStatusCard } from '@/features/dashboard/components/ManagerLicenseStatusCard';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { formatGermanDate } from '@/lib/dateFormatter';

type OnboardingOverview = {
  tenantId: string;
  completedCount: number;
  totalCount: number;
  isFullyComplete: boolean;
  steps: { step: string; isCompleted: boolean; completedAtUtc?: string | null }[];
};

async function fetchOnboarding(tenantId: string): Promise<OnboardingOverview> {
  const { data } = await AXIOS_INSTANCE.get<OnboardingOverview>(
    `/api/admin/onboarding/${tenantId}`
  );
  return data;
}

async function completeOnboardingStep(tenantId: string, step: string): Promise<OnboardingOverview> {
  const { data } = await AXIOS_INSTANCE.post<OnboardingOverview>(
    `/api/admin/onboarding/${tenantId}/steps/${encodeURIComponent(step)}/complete`
  );
  return data;
}

async function fetchRegisterCount(): Promise<number> {
  try {
    const { data } = await AXIOS_INSTANCE.get<{ totalCount?: number; items?: unknown[] }>(
      '/api/admin/cash-registers',
      { params: { page: 1, pageSize: 1 } }
    );
    if (typeof data.totalCount === 'number') return data.totalCount;
    return Array.isArray(data.items) ? data.items.length : 0;
  } catch {
    return 0;
  }
}

export default function TenantSelfServiceDashboardPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const tenant = useCurrentTenant();
  const { status } = useLicenseStatus();
  const queryClient = useQueryClient();
  const [supportOpen, setSupportOpen] = useState(false);
  const [supportForm] = Form.useForm<{ title: string; message: string }>();

  const tenantId = tenant?.id;

  const registersQuery = useQuery({
    queryKey: ['tenant-portal', 'registers'],
    queryFn: fetchRegisterCount,
  });

  const onboardingQuery = useQuery({
    queryKey: ['tenant-portal', 'onboarding', tenantId],
    queryFn: () => fetchOnboarding(tenantId!),
    enabled: !!tenantId,
  });

  const completeMutation = useMutation({
    mutationFn: (step: string) => completeOnboardingStep(tenantId!, step),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['tenant-portal', 'onboarding', tenantId] });
      notify.success(t('tenantPortal.onboarding.complete'));
    },
    onError: () => notify.error(t('feedback.form.error')),
  });

  const supportMutation = useMutation({
    mutationFn: (values: { title: string; message: string }) =>
      createAdminFeedback({
        category: 'FeatureRequest',
        title: values.title,
        message: values.message,
        pagePath: '/tenant/dashboard',
      }),
    onSuccess: () => {
      notify.success(t('feedback.form.success'));
      setSupportOpen(false);
      supportForm.resetFields();
    },
    onError: () => notify.error(t('feedback.form.error')),
  });

  const expiryLabel = status?.expiredAt
    ? formatGermanDate(status.expiredAt)
    : t('tenantPortal.license.unknown');

  return (
    <div style={{ padding: 24 }}>
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap' }}>
          <div>
            <Typography.Title level={2} style={{ margin: 0 }}>
              {t('tenantPortal.page.title')}
            </Typography.Title>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
              {t('tenantPortal.page.subtitle')}
            </Typography.Paragraph>
          </div>
          <Button
            type="primary"
            icon={<CustomerServiceOutlined />}
            onClick={() => setSupportOpen(true)}
          >
            {t('tenantPortal.page.requestSupport')}
          </Button>
        </div>

        <Row gutter={[16, 16]}>
          <Col xs={24} lg={14}>
            <ManagerLicenseStatusCard />
          </Col>
          <Col xs={24} lg={10}>
            <Card title={t('tenantPortal.license.title')}>
              <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                <Statistic
                  title={t('tenantPortal.license.status')}
                  value={status?.state ?? t('tenantPortal.license.unknown')}
                />
                <Statistic title={t('tenantPortal.license.expiry')} value={expiryLabel} />
                <Statistic
                  title={t('tenantPortal.license.package')}
                  value={status?.licensePlan ?? t('tenantPortal.license.unknown')}
                />
                <Statistic
                  title={t('tenantPortal.license.registers')}
                  value={registersQuery.data ?? 0}
                  loading={registersQuery.isLoading}
                />
              </Space>
            </Card>
          </Col>
        </Row>

        {onboardingQuery.data ? (
          <Card title={t('tenantPortal.onboarding.title')}>
            <Progress
              percent={Math.round(
                (onboardingQuery.data.completedCount / Math.max(1, onboardingQuery.data.totalCount)) *
                  100
              )}
              style={{ marginBottom: 16 }}
            />
            <List
              dataSource={onboardingQuery.data.steps}
              renderItem={(item) => (
                <List.Item
                  actions={
                    item.isCompleted
                      ? undefined
                      : [
                          <Button
                            key="done"
                            size="small"
                            loading={completeMutation.isPending}
                            onClick={() => completeMutation.mutate(item.step)}
                          >
                            {t('tenantPortal.onboarding.complete')}
                          </Button>,
                        ]
                  }
                >
                  <List.Item.Meta
                    title={t(
                      `tenantPortal.onboarding.steps.${item.step}` as 'tenantPortal.onboarding.steps.AccountCreated'
                    )}
                    description={item.isCompleted ? '✓' : null}
                  />
                </List.Item>
              )}
            />
          </Card>
        ) : null}

        <Card title={t('tenantPortal.activity.title')}>
          <ActivitySummary limit={8} />
        </Card>
      </Space>

      <Modal
        title={t('tenantPortal.page.requestSupport')}
        open={supportOpen}
        onCancel={() => setSupportOpen(false)}
        confirmLoading={supportMutation.isPending}
        onOk={() => supportForm.submit()}
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary">
          {t('tenantPortal.page.requestSupportHint')}
        </Typography.Paragraph>
        <Form
          form={supportForm}
          layout="vertical"
          onFinish={(values) => supportMutation.mutate(values)}
        >
          <Form.Item
            name="title"
            label={t('feedback.form.title')}
            rules={[{ required: true, min: 3 }]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="message"
            label={t('feedback.form.message')}
            rules={[{ required: true, min: 10 }]}
          >
            <Input.TextArea rows={4} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
