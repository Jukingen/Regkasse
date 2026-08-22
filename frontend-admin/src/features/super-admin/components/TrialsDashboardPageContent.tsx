'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Col, Form, InputNumber, Modal, Row, Space, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import React, { useMemo, useState } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import {
  ConvertTrialToPaidModal,
  type ConvertTrialFormValues,
} from '@/features/super-admin/components/ConvertTrialToPaidModal';
import {
  convertTrialToPaid,
  extendTrial,
  fetchTrialAnalytics,
  fetchTrialDashboard,
  softDeleteTrial,
  type TrialConversionResult,
  type TrialTenantSummary,
} from '@/features/super-admin/api/adminTrials';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

function daysColor(days: number | null | undefined): string {
  if (days == null) return 'default';
  if (days > 7) return 'success';
  if (days >= 3) return 'warning';
  return 'error';
}

export function TrialsDashboardPageContent() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [extendTarget, setExtendTarget] = useState<TrialTenantSummary | null>(null);
  const [convertTarget, setConvertTarget] = useState<TrialTenantSummary | null>(null);
  const [conversionSuccess, setConversionSuccess] = useState<TrialConversionResult | null>(null);
  const [extendForm] = Form.useForm<{ additionalDays: number }>();

  const query = useQuery({
    queryKey: ['admin', 'trials', 'dashboard'],
    queryFn: fetchTrialDashboard,
  });

  const analyticsQuery = useQuery({
    queryKey: ['admin', 'trials', 'analytics'],
    queryFn: fetchTrialAnalytics,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['admin', 'trials'] });
  };

  const extendMutation = useMutation({
    mutationFn: ({ tenantId, days }: { tenantId: string; days: number }) =>
      extendTrial(tenantId, days),
    onSuccess: () => {
      notify.success(t('trials.actions.extend'));
      setExtendTarget(null);
      invalidate();
    },
    onError: (err) => notify.apiError(err, { logContext: 'Trials.extend' }),
  });

  const convertMutation = useMutation({
    mutationFn: ({
      tenantId,
      values,
    }: {
      tenantId: string;
      values: ConvertTrialFormValues;
    }) =>
      convertTrialToPaid(tenantId, values.licenseSaleId.trim(), {
        addRemainingTrialDays: values.addRemainingTrialDays,
        notes: values.notes,
      }),
    onSuccess: (result) => {
      setConversionSuccess(result);
      invalidate();
    },
    onError: (err) => notify.apiError(err, { logContext: 'Trials.convert' }),
  });

  const deleteMutation = useMutation({
    mutationFn: (tenantId: string) => softDeleteTrial(tenantId),
    onSuccess: () => {
      notify.success(t('trials.actions.delete'));
      invalidate();
    },
    onError: (err) => notify.apiError(err, { logContext: 'Trials.delete' }),
  });

  const rows = useMemo(() => {
    const data = query.data;
    if (!data) return [];
    const map = new Map<string, TrialTenantSummary>();
    for (const row of [...data.activeTrials, ...data.expiredTrials]) {
      map.set(row.tenantId, row);
    }
    return Array.from(map.values());
  }, [query.data]);

  const columns: ColumnsType<TrialTenantSummary> = [
    {
      title: t('trials.columns.tenant'),
      key: 'tenant',
      render: (_, r) => (
        <Space orientation="vertical" size={0}>
          <Link href={`/admin/tenants/${r.tenantId}`}>{r.name}</Link>
          <Typography.Text type="secondary">{r.slug}</Typography.Text>
        </Space>
      ),
    },
    {
      title: t('trials.columns.status'),
      dataIndex: 'trialStatus',
      key: 'status',
      render: (status: string | null | undefined) => {
        const key =
          status && ['active', 'expired', 'converted', 'deleted'].includes(status)
            ? `trials.status.${status}`
            : null;
        return <Tag>{key ? t(key) : (status ?? '—')}</Tag>;
      },
    },
    {
      title: t('trials.columns.daysLeft'),
      dataIndex: 'daysRemaining',
      key: 'days',
      render: (days: number | null | undefined) => (
        <Tag color={daysColor(days)}>{days ?? '—'}</Tag>
      ),
    },
    {
      title: t('trials.columns.endsAt'),
      dataIndex: 'trialEndsAtUtc',
      key: 'ends',
      render: (v: string | null | undefined) =>
        v ? new Date(v).toLocaleDateString() : '—',
    },
    {
      title: t('trials.columns.reminders'),
      key: 'reminders',
      render: (_, r) => (
        <Typography.Text type="secondary">
          {[r.reminder7dSent && '7d', r.reminder3dSent && '3d', r.reminder1dSent && '1d']
            .filter(Boolean)
            .join(' · ') || '—'}
        </Typography.Text>
      ),
    },
    {
      title: t('tenants.columns.actions'),
      key: 'actions',
      render: (_, r) => (
        <Space wrap>
          <Button
            size="small"
            onClick={() => {
              extendForm.setFieldsValue({ additionalDays: 14 });
              setExtendTarget(r);
            }}
          >
            {t('trials.actions.extend')}
          </Button>
          <Button
            size="small"
            type="primary"
            onClick={() => {
              setConversionSuccess(null);
              setConvertTarget(r);
            }}
          >
            {t('trials.actions.convert')}
          </Button>
          <Button
            size="small"
            danger
            onClick={() => {
              modal.confirm({
                title: t('trials.actions.delete'),
                content: t('trials.deleteConfirm'),
                onOk: () => deleteMutation.mutateAsync(r.tenantId),
              });
            }}
          >
            {t('trials.actions.delete')}
          </Button>
        </Space>
      ),
    },
  ];

  const analytics = analyticsQuery.data;

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('trials.title')} subtitle={t('trials.subtitle')} />
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} md={6}>
          <Card>
            <Statistic
              title={t('trials.stats.created30d')}
              value={analytics?.trialsCreatedLast30Days ?? 0}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card>
            <Statistic title={t('trials.stats.active')} value={analytics?.activeTrials ?? query.data?.activeCount ?? 0} />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card>
            <Statistic
              title={t('trials.stats.converted')}
              value={analytics?.convertedTrials ?? query.data?.convertedCount ?? 0}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card>
            <Statistic
              title={t('trials.stats.conversionRate')}
              value={analytics?.conversionRatePercent ?? query.data?.conversionRatePercent ?? 0}
              suffix="%"
            />
          </Card>
        </Col>
      </Row>

      {analytics?.averageDaysToConvert != null || analytics?.mostCommonLicensePlan ? (
        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          <Col xs={24} sm={12}>
            <Card size="small">
              <Statistic
                title={t('trials.stats.avgDaysToConvert')}
                value={analytics?.averageDaysToConvert ?? '—'}
              />
            </Card>
          </Col>
          <Col xs={24} sm={12}>
            <Card size="small">
              <Statistic
                title={t('trials.stats.topPlan')}
                value={analytics?.mostCommonLicensePlan ?? '—'}
              />
            </Card>
          </Col>
        </Row>
      ) : null}

      {analytics?.monthlyTrend && analytics.monthlyTrend.length > 0 ? (
        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          <Col xs={24} lg={14}>
            <Card title={t('trials.analytics.monthlyTrend')} size="small">
              <div style={{ width: '100%', height: 240 }}>
                <ResponsiveContainer>
                  <BarChart data={analytics.monthlyTrend}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="yearMonth" />
                    <YAxis allowDecimals={false} />
                    <Tooltip />
                    <Legend />
                    <Bar dataKey="trialsStarted" name={t('trials.analytics.started')} fill="#1677ff" />
                    <Bar dataKey="converted" name={t('trials.analytics.converted')} fill="#52c41a" />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>
          <Col xs={24} lg={10}>
            <Card title={t('trials.analytics.byDuration')} size="small">
              <div style={{ width: '100%', height: 240 }}>
                <ResponsiveContainer>
                  <PieChart>
                    <Pie
                      data={analytics.conversionByTrialDuration.map((b) => ({
                        name: `${b.trialDurationDays}d`,
                        value: b.convertedCount,
                      }))}
                      dataKey="value"
                      nameKey="name"
                      outerRadius={80}
                      label
                    >
                      {analytics.conversionByTrialDuration.map((_, idx) => (
                        <Cell
                          key={`cell-${idx}`}
                          fill={['#1677ff', '#52c41a', '#faad14', '#722ed1'][idx % 4]}
                        />
                      ))}
                    </Pie>
                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>
        </Row>
      ) : null}

      {analytics?.conversionByPlan && analytics.conversionByPlan.length > 0 ? (
        <Card title={t('trials.analytics.byPlan')} size="small" style={{ marginBottom: 16 }}>
          <Table
            size="small"
            pagination={false}
            rowKey="licensePlan"
            dataSource={analytics.conversionByPlan}
            columns={[
              { title: t('trials.stats.topPlan'), dataIndex: 'licensePlan' },
              { title: t('trials.analytics.converted'), dataIndex: 'convertedCount' },
            ]}
          />
        </Card>
      ) : null}

      <Card>
        <Table
          rowKey="tenantId"
          loading={query.isLoading}
          columns={columns}
          dataSource={rows}
          locale={{ emptyText: t('trials.empty') }}
          pagination={{ pageSize: 20 }}
        />
      </Card>

      <Modal
        title={t('trials.extend.title')}
        open={!!extendTarget}
        onCancel={() => setExtendTarget(null)}
        confirmLoading={extendMutation.isPending}
        onOk={() => extendForm.submit()}
        okText={t('trials.extend.ok')}
        destroyOnHidden
      >
        <Form
          form={extendForm}
          layout="vertical"
          onFinish={(values) => {
            if (!extendTarget) return;
            extendMutation.mutate({ tenantId: extendTarget.tenantId, days: values.additionalDays });
          }}
        >
          <Form.Item
            name="additionalDays"
            label={t('trials.extend.daysLabel')}
            rules={[{ required: true }]}
            initialValue={14}
          >
            <InputNumber min={1} max={365} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>

      <ConvertTrialToPaidModal
        open={!!convertTarget || !!conversionSuccess}
        tenant={convertTarget}
        loading={convertMutation.isPending}
        success={conversionSuccess}
        onCancel={() => {
          setConvertTarget(null);
          setConversionSuccess(null);
        }}
        onSuccessClose={() => {
          setConvertTarget(null);
          setConversionSuccess(null);
        }}
        onSubmit={(values) => {
          if (!convertTarget) return;
          convertMutation.mutate({ tenantId: convertTarget.tenantId, values });
        }}
      />
    </AdminPageShell>
  );
}
