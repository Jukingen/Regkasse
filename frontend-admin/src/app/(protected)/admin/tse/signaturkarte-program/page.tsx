'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Input,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { DateColumn } from '@/components/DateColumn';
import {
  exportSignaturkarteProgramCsv,
  getSignaturkarteProgramStatus,
  listSignaturkarteProgramDevices,
  markSignaturkarteProgramCompliant,
} from '@/features/signaturkarte-program/api/signaturkarteProgram';
import type { SignaturkarteProgramDevice } from '@/features/signaturkarte-program/types';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { buildAdminBreadcrumbs } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const QUERY_KEY = ['signaturkarte-program'] as const;

function statusColor(status: string): string {
  switch (status) {
    case 'Compliant':
      return 'green';
    case 'Open':
      return 'orange';
    case 'Excluded':
      return 'default';
    case 'Revoked':
      return 'red';
    default:
      return 'default';
  }
}

export default function SignaturkarteProgramPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SETTINGS_VIEW);
  const [statusFilter, setStatusFilter] = useState<string | undefined>('Open');

  const statusQuery = useQuery({
    queryKey: [...QUERY_KEY, 'status'],
    queryFn: ({ signal }) => getSignaturkarteProgramStatus(signal),
    enabled: allowed,
  });

  const devicesQuery = useQuery({
    queryKey: [...QUERY_KEY, 'devices', statusFilter ?? 'all'],
    queryFn: ({ signal }) =>
      listSignaturkarteProgramDevices({
        status: statusFilter,
        signal,
      }),
    enabled: allowed,
  });

  const markMutation = useMutation({
    mutationFn: ({ deviceId, note }: { deviceId: string; note?: string }) =>
      markSignaturkarteProgramCompliant(deviceId, note),
    onSuccess: async (res) => {
      if (!res.success) {
        notify.error(res.message || t('common.errorGeneric'));
        return;
      }
      notify.success(t('signaturkarteProgram.markSuccess'));
      await queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'SignaturkarteProgram.markCompliant',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const columns: ColumnsType<SignaturkarteProgramDevice> = useMemo(
    () => [
      ...(isSuperAdmin
        ? [
            {
              title: t('signaturkarteProgram.colTenant'),
              key: 'tenant',
              width: 160,
              render: (_: unknown, row: SignaturkarteProgramDevice) =>
                row.tenantName || row.tenantSlug || '—',
            },
          ]
        : []),
      {
        title: t('signaturkarteProgram.colSerial'),
        dataIndex: 'serialNumber',
        ellipsis: true,
      },
      {
        title: t('signaturkarteProgram.colProvider'),
        dataIndex: 'provider',
        width: 110,
        render: (v: string | null) => v || '—',
      },
      {
        title: t('signaturkarteProgram.colExpiresAt'),
        dataIndex: 'expiresAt',
        width: 150,
        render: (v: string | null) =>
          v ? <DateColumn date={v} format="datetime" /> : '—',
      },
      {
        title: t('signaturkarteProgram.colProgramCompliantAt'),
        dataIndex: 'programCompliantAtUtc',
        width: 160,
        render: (v: string | null) =>
          v ? <DateColumn date={v} format="datetime" /> : '—',
      },
      {
        title: t('signaturkarteProgram.colStatus'),
        dataIndex: 'status',
        width: 120,
        render: (status: string, row) => (
          <Space size={4} wrap>
            <Tag color={statusColor(status)}>
              {t(`signaturkarteProgram.status.${status}` as 'signaturkarteProgram.status.Open')}
            </Tag>
            {row.certificateExpiresBeforeDeadline ? (
              <Tag color="volcano">{t('signaturkarteProgram.dualRisk')}</Tag>
            ) : null}
          </Space>
        ),
      },
      {
        title: t('signaturkarteProgram.colDays'),
        dataIndex: 'daysToDeadline',
        width: 90,
      },
      {
        title: t('signaturkarteProgram.colActions'),
        key: 'actions',
        width: 140,
        fixed: 'right',
        render: (_, row) =>
          row.status === 'Open' ? (
            <Button
              size="small"
              type="primary"
              loading={markMutation.isPending}
              onClick={() => {
                let note = '';
                modal.confirm({
                  title: t('signaturkarteProgram.markConfirmTitle'),
                  content: (
                    <Space orientation="vertical" style={{ width: '100%' }}>
                      <Typography.Text>
                        {t('signaturkarteProgram.markConfirmBody', {
                          serial: row.serialNumber,
                        })}
                      </Typography.Text>
                      <Alert
                        type="info"
                        showIcon
                        title={t('signaturkarteProgram.markNotExpiry')}
                      />
                      <Input.TextArea
                        rows={2}
                        placeholder={t('signaturkarteProgram.markNotePlaceholder')}
                        onChange={(e) => {
                          note = e.target.value;
                        }}
                      />
                    </Space>
                  ),
                  okText: t('signaturkarteProgram.markAction'),
                  onOk: () =>
                    markMutation.mutateAsync({
                      deviceId: row.deviceId,
                      note: note || undefined,
                    }),
                });
              }}
            >
              {t('signaturkarteProgram.markAction')}
            </Button>
          ) : (
            '—'
          ),
      },
    ],
    [isSuperAdmin, markMutation, modal, t]
  );

  if (!allowed) {
    return <Alert type="error" showIcon title={t('common.forbidden403Title')} />;
  }

  const status = statusQuery.data;
  const totals = status?.totals;

  return (
    <div>
      <AdminPageHeader
        title={t('signaturkarteProgram.title')}
        subtitle={t('signaturkarteProgram.subtitle')}
        breadcrumbs={buildAdminBreadcrumbs(t, [
          { title: t('nav.settings.title'), href: '/settings' },
          { title: t('signaturkarteProgram.title') },
        ])}
      />

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('signaturkarteProgram.separationTitle')}
        description={t('signaturkarteProgram.separationBody')}
        data-certificate-expiry="false"
      />

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('signaturkarteProgram.statOpen')}
              value={totals?.nonCompliant ?? 0}
              valueStyle={{ color: '#fa8c16' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('signaturkarteProgram.statCompliant')}
              value={totals?.compliant ?? 0}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('signaturkarteProgram.statExcluded')} value={totals?.excluded ?? 0} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('signaturkarteProgram.statDays')}
              value={status?.daysRemaining ?? '—'}
            />
          </Card>
        </Col>
      </Row>

      <Card
        title={t('signaturkarteProgram.tableTitle')}
        extra={
          <Space wrap>
            <Select
              allowClear
              placeholder={t('signaturkarteProgram.filterStatus')}
              style={{ minWidth: 140 }}
              value={statusFilter}
              onChange={(v) => setStatusFilter(v)}
              options={[
                { value: 'Open', label: t('signaturkarteProgram.status.Open') },
                { value: 'Compliant', label: t('signaturkarteProgram.status.Compliant') },
                { value: 'Excluded', label: t('signaturkarteProgram.status.Excluded') },
                { value: 'Revoked', label: t('signaturkarteProgram.status.Revoked') },
                { value: 'all', label: t('signaturkarteProgram.filterAll') },
              ]}
            />
            <Button
              onClick={() =>
                exportSignaturkarteProgramCsv({
                  status: statusFilter === 'all' ? undefined : statusFilter,
                }).catch((err) =>
                  notify.apiError(err, {
                    logContext: 'SignaturkarteProgram.export',
                    fallbackKey: 'common.errorGeneric',
                  })
                )
              }
            >
              {t('signaturkarteProgram.exportCsv')}
            </Button>
          </Space>
        }
      >
        <Table<SignaturkarteProgramDevice>
          rowKey="deviceId"
          loading={devicesQuery.isLoading}
          columns={columns}
          dataSource={devicesQuery.data ?? []}
          scroll={{ x: 1100 }}
          pagination={{ pageSize: 25 }}
        />
      </Card>
    </div>
  );
}
