'use client';

/**
 * DEP export period-compliance dashboard (yearly legal deadline + recommended periods).
 * Distinct from diagnostic RKSV compliance report at /rksv/compliance.
 */
import {
  CheckCircleOutlined,
  HistoryOutlined,
  ReloadOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Progress,
  Select,
  Space,
  Table,
  Tag,
  Timeline,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import Link from 'next/link';
import React, { useMemo, useState } from 'react';

import { getAdminCashRegisters } from '@/api/admin-rksv/client';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { DateColumn } from '@/components/DateColumn';
import { DepExportArchiveCard } from '@/features/rksv/dep-export-compliance/DepExportArchiveCard';
import { DepExportAuditTrailCard } from '@/features/rksv/dep-export-compliance/DepExportAuditTrailCard';
import { DepExportComplianceReportCard } from '@/features/rksv/dep-export-compliance/DepExportComplianceReportCard';
import { DepExportComplianceScoreCard } from '@/features/rksv/dep-export-compliance/DepExportComplianceScoreCard';
import { DepExportPushSettingsCard } from '@/features/rksv/dep-export-compliance/DepExportPushSettingsCard';
import { DepExportStatisticsCard } from '@/features/rksv/dep-export-compliance/DepExportStatisticsCard';
import { DepExportValidationCard } from '@/features/rksv/dep-export-compliance/DepExportValidationCard';
import {
  type DepExportRequirementDto,
  useDepExportComplianceStatus,
  useDepExportRequirements,
  useGenerateDepExportForCompliance,
} from '@/features/rksv/hooks/useDepExportCompliance';
import { buildDepExportFileName } from '@/features/rksv/utils/depExportFileName';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { createJsonExportBlob } from '@/lib/download/exportDownload';
import { ADMIN_NAV_GROUP_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

const DEP_EXPORT_PAGE = '/admin/rksv/dep-export';

function categoryColor(category: string): string {
  switch (category) {
    case 'Urgent':
      return 'red';
    case 'Yearly':
      return 'blue';
    case 'Quarterly':
      return 'orange';
    case 'Monthly':
      return 'default';
    default:
      return 'default';
  }
}

function daysUntil(dueDate: string | null | undefined): number | null {
  if (!dueDate) return null;
  return Math.ceil(dayjs(dueDate).diff(dayjs(), 'day', true));
}

export default function DepExportComplianceDashboard() {
  const { t } = useI18n();
  const notify = useNotify();
  const { tenant } = useTenant();
  const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();

  const {
    data: complianceStatus,
    isLoading: statusLoading,
    isFetching: statusFetching,
    error: statusError,
    refetch: refetchStatus,
  } = useDepExportComplianceStatus();

  const {
    data: requirements,
    isLoading: requirementsLoading,
    isFetching: requirementsFetching,
    error: requirementsError,
    refetch: refetchRequirements,
  } = useDepExportRequirements();

  const { data: cashRegisters, isLoading: registersLoading } = useQuery({
    queryKey: rksvAdminQueryKeys.cashRegisters,
    queryFn: getAdminCashRegisters,
    staleTime: 60_000,
  });

  const exportMutation = useGenerateDepExportForCompliance();

  const loading = statusLoading || requirementsLoading;
  const fetching = statusFetching || requirementsFetching;

  const registerOptions = useMemo(
    () =>
      (cashRegisters ?? [])
        .filter((r): r is { id: string; registerNumber?: string } => Boolean(r.id))
        .map((r) => ({
          value: r.id,
          label: r.registerNumber || r.id,
        })),
    [cashRegisters]
  );

  const selectedRegister = useMemo(
    () => (cashRegisters ?? []).find((r) => r.id === cashRegisterId),
    [cashRegisters, cashRegisterId]
  );

  const timelineItems = useMemo(() => {
    const rows = [...(requirements ?? [])].sort((a, b) => {
      const da = a.dueDate ? dayjs(a.dueDate).valueOf() : Number.MAX_SAFE_INTEGER;
      const db = b.dueDate ? dayjs(b.dueDate).valueOf() : Number.MAX_SAFE_INTEGER;
      return da - db;
    });

    return rows.map((item) => ({
      key: item.id,
      color: item.isCompleted ? 'green' : item.category === 'Urgent' ? 'red' : 'gray',
      children: (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
            <Typography.Text strong>{item.title}</Typography.Text>
            {item.dueDate ? (
              <Typography.Text type="secondary">
                <DateColumn date={item.dueDate} />
              </Typography.Text>
            ) : null}
          </div>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 4 }}>
            {item.description}
          </Typography.Paragraph>
        </div>
      ),
    }));
  }, [requirements]);

  const runExport = async (requirement: DepExportRequirementDto) => {
    if (!cashRegisterId) {
      notify.warning('rksvHub.depExportCompliancePage.selectRegisterWarning');
      return;
    }
    if (!requirement.periodStart || !requirement.periodEnd) {
      notify.warning('rksvHub.depExportCompliancePage.missingPeriodWarning');
      return;
    }

    try {
      const exportRoot = await exportMutation.mutateAsync({
        cashRegisterId,
        fromUtc: dayjs(requirement.periodStart).startOf('day').toISOString(),
        toUtc: dayjs(requirement.periodEnd).endOf('day').toISOString(),
        includeSpecialReceipts: true,
        includeDailyClosings: true,
      });

      const fileName = buildDepExportFileName(tenant?.slug, selectedRegister?.registerNumber);
      const blob = createJsonExportBlob(exportRoot);
      const url = globalThis.URL.createObjectURL(blob);
      const a = globalThis.document.createElement('a');
      a.href = url;
      a.download = fileName;
      a.click();
      globalThis.URL.revokeObjectURL(url);

      notify.successKey('rksvHub.depExportCompliancePage.exportSuccess');
      await Promise.all([refetchStatus(), refetchRequirements()]);
    } catch (err) {
      notify.apiError(err, {
        logContext: 'DepExportCompliance.export',
        fallbackKey: 'rksvHub.depExportCompliancePage.exportFailed',
      });
    }
  };

  const runNextExport = async () => {
    const next =
      complianceStatus?.nextRequirement ??
      requirements?.find((r) => !r.isCompleted && r.periodStart && r.periodEnd);
    if (!next) {
      notify.info('rksvHub.depExportCompliancePage.noPendingRequirement');
      return;
    }
    await runExport(next);
  };

  const columns: ColumnsType<DepExportRequirementDto> = [
    {
      title: t('rksvHub.depExportCompliancePage.colCategory'),
      dataIndex: 'category',
      key: 'category',
      width: 120,
      render: (category: string) => <Tag color={categoryColor(category)}>{category}</Tag>,
    },
    {
      title: t('rksvHub.depExportCompliancePage.colTitle'),
      dataIndex: 'title',
      key: 'title',
    },
    {
      title: t('rksvHub.depExportCompliancePage.colDescription'),
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
    },
    {
      title: t('rksvHub.depExportCompliancePage.colDue'),
      dataIndex: 'dueDate',
      key: 'dueDate',
      width: 200,
      render: (date: string | null | undefined, record) => {
        if (!date) return '—';
        if (record.isCompleted) {
          return (
            <Space>
              <DateColumn date={date} />
              <Tag color="green">{t('rksvHub.depExportCompliancePage.done')}</Tag>
            </Space>
          );
        }
        const left = daysUntil(date);
        const color =
          left === null ? 'default' : left > 30 ? 'green' : left > 7 ? 'orange' : 'red';
        const label =
          left === null
            ? null
            : left > 0
              ? t('rksvHub.depExportCompliancePage.daysLeft', { days: left })
              : t('rksvHub.depExportCompliancePage.overdue');
        return (
          <Space wrap>
            <DateColumn date={date} />
            {label ? <Tag color={color}>{label}</Tag> : null}
          </Space>
        );
      },
    },
    {
      title: t('rksvHub.depExportCompliancePage.colPriority'),
      dataIndex: 'priority',
      key: 'priority',
      width: 140,
      render: (priority: number) => (
        <Progress
          percent={Math.min(100, Math.max(0, priority * 20))}
          size="small"
          strokeColor={priority >= 4 ? '#cf1322' : priority >= 3 ? '#faad14' : '#52c41a'}
        />
      ),
    },
    {
      title: t('rksvHub.depExportCompliancePage.colStatus'),
      dataIndex: 'isCompleted',
      key: 'isCompleted',
      width: 130,
      render: (isCompleted: boolean) =>
        isCompleted ? (
          <Tag icon={<CheckCircleOutlined />} color="success">
            {t('rksvHub.depExportCompliancePage.statusDone')}
          </Tag>
        ) : (
          <Tag icon={<WarningOutlined />} color="warning">
            {t('rksvHub.depExportCompliancePage.statusPending')}
          </Tag>
        ),
    },
    {
      title: t('rksvHub.depExportCompliancePage.colActions'),
      key: 'actions',
      width: 220,
      render: (_, record) => (
        <Space wrap>
          {!record.isCompleted ? (
            <Button
              size="small"
              type="primary"
              loading={exportMutation.isPending}
              onClick={() => void runExport(record)}
            >
              {t('rksvHub.depExportCompliancePage.exportRow')}
            </Button>
          ) : null}
          <Link href={DEP_EXPORT_PAGE}>
            <Button size="small" icon={<HistoryOutlined />}>
              {t('rksvHub.depExportCompliancePage.history')}
            </Button>
          </Link>
        </Space>
      ),
    },
  ];

  const loadError = statusError ?? requirementsError;

  return (
    <div>
      <AdminPageHeader
        title={t('rksvHub.depExportCompliancePage.title')}
        subtitle={t('rksvHub.depExportCompliancePage.subtitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_GROUP_LABEL_KEYS.rksv), href: '/rksv' },
          { title: t('rksvHub.depExportCompliancePage.breadcrumb') },
        ]}
        extra={
          <Space wrap>
            <Button
              icon={<ReloadOutlined />}
              loading={fetching}
              onClick={() => {
                void refetchStatus();
                void refetchRequirements();
              }}
            >
              {t('rksvHub.depExportCompliancePage.refresh')}
            </Button>
            <Button type="primary" loading={exportMutation.isPending} onClick={() => void runNextExport()}>
              {t('rksvHub.depExportCompliancePage.createExport')}
            </Button>
            <Link href={DEP_EXPORT_PAGE}>
              <Button>{t('rksvHub.depExportCompliancePage.openDepExport')}</Button>
            </Link>
          </Space>
        }
      />

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('rksvHub.depExportCompliancePage.disclaimerTitle')}
        description={
          complianceStatus?.disclaimer ?? t('rksvHub.depExportCompliancePage.disclaimerFallback')
        }
      />

      {loadError ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('rksvHub.depExportCompliancePage.loadFailed')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={loadError}
              logContext="DepExportCompliance.load"
              fallbackKey="rksvHub.depExportCompliancePage.loadFailed"
            />
          }
        />
      ) : null}

      <DepExportComplianceScoreCard style={{ marginBottom: 16 }} />

      <DepExportComplianceReportCard
        status={complianceStatus}
        requirements={requirements}
        loading={loading}
        tenantSlug={tenant?.slug}
        tenantName={tenant?.name}
      />

      <DepExportValidationCard style={{ marginBottom: 16 }} />

      <DepExportArchiveCard style={{ marginBottom: 16 }} />

      <DepExportStatisticsCard style={{ marginBottom: 16 }} />

      <DepExportPushSettingsCard style={{ marginBottom: 16 }} />

      <DepExportAuditTrailCard style={{ marginBottom: 16 }} />

      <Card style={{ marginBottom: 16 }}>
        <Space wrap style={{ marginBottom: 16 }}>
          <Typography.Text>{t('rksvHub.depExportCompliancePage.cashRegisterLabel')}</Typography.Text>
          <Select
            style={{ minWidth: 220 }}
            placeholder={t('rksvHub.depExportCompliancePage.cashRegisterPlaceholder')}
            loading={registersLoading}
            options={registerOptions}
            value={cashRegisterId}
            onChange={setCashRegisterId}
            allowClear
          />
        </Space>

        {complianceStatus ? (
          <Space wrap style={{ marginBottom: 8 }}>
            <Tag color={complianceStatus.isCompliant ? 'success' : 'error'}>
              {complianceStatus.isCompliant
                ? t('rksvHub.depExportCompliancePage.legalOk')
                : t('rksvHub.depExportCompliancePage.legalIncomplete', {
                    count: complianceStatus.legalIncompleteCount,
                  })}
            </Tag>
            {complianceStatus.currentPeriod ? (
              <Tag>
                {t('rksvHub.depExportCompliancePage.currentPeriod', {
                  type: complianceStatus.currentPeriod.periodType,
                  status: complianceStatus.currentPeriod.status,
                })}
              </Tag>
            ) : null}
          </Space>
        ) : null}
      </Card>

      <Card title={t('rksvHub.depExportCompliancePage.requirementsTitle')} style={{ marginBottom: 16 }}>
        <Table<DepExportRequirementDto>
          rowKey="id"
          loading={loading || fetching}
          dataSource={requirements ?? []}
          columns={columns}
          pagination={false}
          scroll={{ x: 1100 }}
        />
      </Card>

      <Card title={t('rksvHub.depExportCompliancePage.timelineTitle')}>
        {timelineItems.length === 0 ? (
          <Typography.Text type="secondary">
            {t('rksvHub.depExportCompliancePage.timelineEmpty')}
          </Typography.Text>
        ) : (
          <Timeline items={timelineItems} />
        )}
      </Card>
    </div>
  );
}
