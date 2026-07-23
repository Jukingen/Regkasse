'use client';

/**
 * Super Admin risk scoring / anomaly inbox.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Col, Form, Input, Modal, Progress, Row, Space, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import { getRiskScores, resolveRisk } from '@/features/risk/api/riskScores';
import type { RiskScoreDto } from '@/features/risk/types';
import { deactivateUser } from '@/features/users/api/usersApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

const LEVEL_COLOR: Record<string, string> = {
  Critical: 'red',
  High: 'orange',
  Medium: 'gold',
  Low: 'green',
};

export default function RiskDashboardPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [resolveTarget, setResolveTarget] = useState<RiskScoreDto | null>(null);
  const [form] = Form.useForm<{ resolution: string }>();

  const listQuery = useQuery({
    queryKey: ['admin', 'risk-scores'],
    queryFn: ({ signal }) => getRiskScores({ unresolvedOnly: true, limit: 100 }, signal),
    refetchInterval: 30_000,
  });

  const resolveMutation = useMutation({
    mutationFn: ({ id, resolution }: { id: string; resolution: string }) => resolveRisk(id, resolution),
    onSuccess: async () => {
      notify.success(t('riskDashboard.resolveSuccess'));
      setResolveTarget(null);
      form.resetFields();
      await queryClient.invalidateQueries({ queryKey: ['admin', 'risk-scores'] });
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'RiskDashboard.resolve', fallbackKey: 'common.errorGeneric' });
    },
  });

  const blockMutation = useMutation({
    mutationFn: (userId: string) =>
      deactivateUser(userId, { reason: t('riskDashboard.blockUserReason') }),
    onSuccess: () => {
      notify.success(t('riskDashboard.blockUserSuccess'));
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'RiskDashboard.blockUser', fallbackKey: 'common.errorGeneric' });
    },
  });

  const summary = listQuery.data?.summary;
  const items = listQuery.data?.items ?? [];

  const columns: ColumnsType<RiskScoreDto> = useMemo(
    () => [
      {
        title: t('riskDashboard.colTenant'),
        dataIndex: 'tenantName',
        width: 140,
        render: (v: string | null | undefined, row) => v || row.tenantId.slice(0, 8),
      },
      {
        title: t('riskDashboard.colUser'),
        key: 'user',
        width: 180,
        render: (_, row) => row.userEmail || row.userName || row.userId.slice(0, 8),
      },
      {
        title: t('riskDashboard.colAction'),
        dataIndex: 'actionType',
        width: 160,
        render: (action: string) => <Tag>{action}</Tag>,
      },
      {
        title: t('riskDashboard.colRiskLevel'),
        dataIndex: 'riskLevel',
        width: 120,
        render: (level: string) => (
          <Tag color={LEVEL_COLOR[level] ?? 'default'}>
            {t(`riskDashboard.levels.${level}` as 'riskDashboard.levels.Low')}
          </Tag>
        ),
      },
      {
        title: t('riskDashboard.colScore'),
        dataIndex: 'score',
        width: 140,
        render: (score: number) => (
          <Progress
            percent={score}
            size="small"
            status={score >= 70 ? 'exception' : score >= 50 ? 'active' : 'normal'}
          />
        ),
      },
      {
        title: t('riskDashboard.colReason'),
        dataIndex: 'reason',
        ellipsis: true,
      },
      {
        title: t('riskDashboard.colCreated'),
        dataIndex: 'createdAt',
        width: 160,
        render: dateColumnRender('datetime'),
      },
      {
        title: t('riskDashboard.colActions'),
        key: 'actions',
        width: 220,
        render: (_, record) => (
          <Space>
            <Button
              size="small"
              onClick={() => {
                setResolveTarget(record);
                form.setFieldsValue({ resolution: '' });
              }}
            >
              {t('riskDashboard.resolve')}
            </Button>
            <Button
              size="small"
              danger
              loading={blockMutation.isPending}
              onClick={() => {
                modal.confirm({
                  title: t('riskDashboard.blockUserTitle'),
                  content: t('riskDashboard.blockUserContent'),
                  okType: 'danger',
                  onOk: () => blockMutation.mutateAsync(record.userId),
                });
              }}
            >
              {t('riskDashboard.blockUser')}
            </Button>
          </Space>
        ),
      },
    ],
    [t, form, modal, blockMutation]
  );

  return (
    <div style={{ padding: 24 }}>
      <Typography.Title level={3} style={{ marginTop: 0 }}>
        {t('riskDashboard.pageTitle')}
      </Typography.Title>
      <Typography.Paragraph type="secondary">{t('riskDashboard.pageSubtitle')}</Typography.Paragraph>

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={12} md={6}>
          <Card>
            <Statistic
              title={t('riskDashboard.statCritical')}
              value={summary?.critical ?? 0}
              valueStyle={{ color: '#cf1322' }}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card>
            <Statistic
              title={t('riskDashboard.statHigh')}
              value={summary?.high ?? 0}
              valueStyle={{ color: '#d46b08' }}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card>
            <Statistic
              title={t('riskDashboard.statMedium')}
              value={summary?.medium ?? 0}
              valueStyle={{ color: '#d4b106' }}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card>
            <Statistic
              title={t('riskDashboard.statLow')}
              value={summary?.low ?? 0}
              valueStyle={{ color: '#389e0d' }}
            />
          </Card>
        </Col>
      </Row>

      <Card>
        <Table
          rowKey="id"
          loading={listQuery.isLoading}
          dataSource={items}
          columns={columns}
          pagination={{ pageSize: 20 }}
          locale={{ emptyText: t('riskDashboard.empty') }}
        />
      </Card>

      <Modal
        title={t('riskDashboard.resolveTitle')}
        open={!!resolveTarget}
        onCancel={() => {
          setResolveTarget(null);
          form.resetFields();
        }}
        confirmLoading={resolveMutation.isPending}
        onOk={async () => {
          const values = await form.validateFields();
          if (!resolveTarget) return;
          await resolveMutation.mutateAsync({
            id: resolveTarget.id,
            resolution: values.resolution,
          });
        }}
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary">{t('riskDashboard.resolvePrompt')}</Typography.Paragraph>
        <Form form={form} layout="vertical">
          <Form.Item
            name="resolution"
            rules={[{ required: true, min: 3 }]}
          >
            <Input.TextArea rows={3} placeholder={t('riskDashboard.resolvePlaceholder')} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
