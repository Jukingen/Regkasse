'use client';

import {
  Alert,
  Button,
  Col,
  Form,
  Input,
  Modal,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import {
  downloadPermissionAuditExport,
  getPermissionAuditReport,
  getPermissionCompliance,
  schedulePermissionAuditReport,
  type PermissionAccessRow,
  type PermissionAuditExportFormat,
  type PermissionAuditReportParams,
} from '@/features/audit/api/permissionAudit';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

type Props = {
  open: boolean;
  onClose: () => void;
  filters?: PermissionAuditReportParams;
  canExport: boolean;
};

export function PermissionAuditReportModal(props: Props) {
  if (!props.open) return null;
  return <PermissionAuditReportModalContent {...props} />;
}

function PermissionAuditReportModalContent({ open, onClose, filters, canExport }: Props) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const [exporting, setExporting] = useState<PermissionAuditExportFormat | null>(null);
  const [scheduleForm] = Form.useForm();
  const [scheduling, setScheduling] = useState(false);

  const reportQuery = useQuery({
    queryKey: ['permission-audit-report', filters],
    queryFn: () => getPermissionAuditReport(filters ?? {}),
    enabled: open,
  });

  const complianceQuery = useQuery({
    queryKey: ['permission-audit-compliance'],
    queryFn: () => getPermissionCompliance(90),
    enabled: open,
  });

  const report = reportQuery.data;
  const compliance = complianceQuery.data;

  const byDateChart = useMemo(
    () =>
      (report?.byDate ?? []).map((d) => ({
        date: d.date.slice(5),
        count: d.count,
      })),
    [report?.byDate]
  );

  const handleExport = async (format: PermissionAuditExportFormat) => {
    if (!canExport) {
      message.error(t('users.permissionAudit.report.exportDenied'));
      return;
    }
    setExporting(format);
    try {
      await downloadPermissionAuditExport(format, filters ?? {});
      message.success(t('users.permissionAudit.report.exportSuccess'));
    } catch {
      message.error(t('users.permissionAudit.report.exportError'));
    } finally {
      setExporting(null);
    }
  };

  const handleSchedule = async () => {
    try {
      const values = await scheduleForm.validateFields();
      setScheduling(true);
      const recipients = String(values.recipients ?? '')
        .split(/[,;\s]+/)
        .map((s) => s.trim())
        .filter((s) => s.includes('@'));
      await schedulePermissionAuditReport({
        name: values.name as string,
        preset: values.preset as 'weekly' | 'monthly' | 'compliance',
        recipients,
        format: values.format as 'permission-csv' | 'permission-json' | 'permission-pdf',
        roleName: filters?.roleName,
        actorUserId: filters?.actorUserId,
        fromDate: filters?.fromDate,
        toDate: filters?.toDate,
      });
      message.success(t('users.permissionAudit.report.scheduleSuccess'));
      scheduleForm.resetFields();
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(t('users.permissionAudit.report.scheduleError'));
    } finally {
      setScheduling(false);
    }
  };

  const complianceColumns: ColumnsType<PermissionAccessRow> = [
    {
      title: t('users.permissionAudit.report.colSubject'),
      dataIndex: 'subjectName',
      key: 'subjectName',
      render: (v, row) => (
        <span>
          <Tag>{row.subjectType}</Tag> {v}
        </span>
      ),
    },
    {
      title: t('users.permissionAudit.report.colPermission'),
      dataIndex: 'permissionKey',
      key: 'permissionKey',
    },
    {
      title: t('users.permissionAudit.report.colAccess'),
      dataIndex: 'accessState',
      key: 'accessState',
                      render: (v: string) => t(`users.permissionAudit.report.access.${v}`),
    },
    {
      title: t('users.permissionAudit.report.colLastReview'),
      dataIndex: 'lastReviewedAtUtc',
      key: 'lastReviewedAtUtc',
      render: (v?: string | null) => (v ? formatDateTime(v) : '—'),
    },
    {
      title: t('users.permissionAudit.report.colFlags'),
      key: 'flags',
      render: (_, row) => (
        <Space size={4} wrap>
          {row.isExpired ? <Tag color="error">{t('users.permissionAudit.report.expired')}</Tag> : null}
          {row.isStale ? <Tag color="warning">{t('users.permissionAudit.report.stale')}</Tag> : null}
        </Space>
      ),
    },
  ];

  return (
    <Modal
      open={open}
      onCancel={onClose}
      title={t('users.permissionAudit.report.title')}
      width={960}
      footer={null}
      destroyOnHidden
    >
      <Tabs
        items={[
          {
            key: 'summary',
            label: t('users.permissionAudit.report.tabSummary'),
            children: (
              <Space orientation="vertical" style={{ width: '100%' }} size={16}>
                {reportQuery.isError ? (
                  <Alert type="error" title={t('users.permissionAudit.report.loadError')} />
                ) : null}
                <Row gutter={[16, 16]}>
                  <Col xs={12} md={6}>
                    <Statistic
                      title={t('users.permissionAudit.report.totalChanges')}
                      value={report?.totalChanges ?? 0}
                      loading={reportQuery.isLoading}
                    />
                  </Col>
                  <Col xs={12} md={6}>
                    <Statistic
                      title={t('users.permissionAudit.report.criticalChanges')}
                      value={report?.criticalCount ?? 0}
                      loading={reportQuery.isLoading}
                    />
                  </Col>
                  <Col xs={12} md={6}>
                    <Statistic
                      title={t('users.permissionAudit.report.uniqueActors')}
                      value={report?.uniqueActors ?? 0}
                      loading={reportQuery.isLoading}
                    />
                  </Col>
                  <Col xs={12} md={6}>
                    <Statistic
                      title={t('users.permissionAudit.report.uniquePermissions')}
                      value={report?.uniquePermissions ?? 0}
                      loading={reportQuery.isLoading}
                    />
                  </Col>
                </Row>

                <div>
                  <Typography.Title level={5} style={{ marginTop: 0 }}>
                    {t('users.permissionAudit.report.byDate')}
                  </Typography.Title>
                  <div style={{ width: '100%', height: 220 }}>
                    <ResponsiveContainer>
                      <BarChart data={byDateChart}>
                        <CartesianGrid strokeDasharray="3 3" />
                        <XAxis dataKey="date" tick={{ fontSize: 11 }} />
                        <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                        <Tooltip />
                        <Bar dataKey="count" fill="#1677ff" name={t('users.permissionAudit.report.changes')} />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>

                <Row gutter={[16, 16]}>
                  <Col xs={24} md={12}>
                    <Typography.Title level={5}>
                      {t('users.permissionAudit.report.topActors')}
                    </Typography.Title>
                    <Table
                      size="small"
                      pagination={false}
                      rowKey="key"
                      loading={reportQuery.isLoading}
                      dataSource={report?.topActors ?? []}
                      columns={[
                        { title: t('users.permissionAudit.report.colActor'), dataIndex: 'label' },
                        { title: t('users.permissionAudit.report.colCount'), dataIndex: 'count', width: 80 },
                      ]}
                    />
                  </Col>
                  <Col xs={24} md={12}>
                    <Typography.Title level={5}>
                      {t('users.permissionAudit.report.topPermissions')}
                    </Typography.Title>
                    <Table
                      size="small"
                      pagination={false}
                      rowKey="key"
                      loading={reportQuery.isLoading}
                      dataSource={report?.topPermissions ?? []}
                      columns={[
                        {
                          title: t('users.permissionAudit.report.colPermission'),
                          dataIndex: 'label',
                        },
                        { title: t('users.permissionAudit.report.colCount'), dataIndex: 'count', width: 80 },
                      ]}
                    />
                  </Col>
                </Row>
              </Space>
            ),
          },
          {
            key: 'compliance',
            label: t('users.permissionAudit.report.tabCompliance'),
            children: (
              <Space orientation="vertical" style={{ width: '100%' }} size={16}>
                {complianceQuery.isError ? (
                  <Alert type="error" title={t('users.permissionAudit.report.loadError')} />
                ) : null}
                <Row gutter={[16, 16]}>
                  <Col xs={12} md={6}>
                    <Statistic
                      title={t('users.permissionAudit.report.lastReview')}
                      value={
                        compliance?.lastPermissionReviewAtUtc
                          ? formatDateTime(compliance.lastPermissionReviewAtUtc)
                          : '—'
                      }
                      loading={complianceQuery.isLoading}
                    />
                  </Col>
                  <Col xs={12} md={6}>
                    <Statistic
                      title={t('users.permissionAudit.report.rolePermissions')}
                      value={compliance?.rolePermissionCount ?? 0}
                      loading={complianceQuery.isLoading}
                    />
                  </Col>
                  <Col xs={12} md={6}>
                    <Statistic
                      title={t('users.permissionAudit.report.activeOverrides')}
                      value={compliance?.activeOverrideCount ?? 0}
                      loading={complianceQuery.isLoading}
                    />
                  </Col>
                  <Col xs={12} md={6}>
                    <Statistic
                      title={t('users.permissionAudit.report.staleSubjects')}
                      value={compliance?.staleSubjectCount ?? 0}
                      loading={complianceQuery.isLoading}
                    />
                  </Col>
                </Row>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                  {t('users.permissionAudit.report.staleHint', {
                    days: compliance?.staleDaysThreshold ?? 90,
                  })}
                </Typography.Paragraph>
                <Typography.Title level={5} style={{ marginBottom: 8 }}>
                  {t('users.permissionAudit.report.expiredOrStale')}
                </Typography.Title>
                <Table
                  size="small"
                  rowKey={(r) => `${r.subjectType}-${r.subjectId}-${r.permissionKey}-${r.accessState}`}
                  loading={complianceQuery.isLoading}
                  dataSource={compliance?.expiredOrStale ?? []}
                  columns={complianceColumns}
                  pagination={{ pageSize: 8 }}
                  scroll={{ x: true }}
                />
                <Typography.Title level={5} style={{ marginBottom: 8 }}>
                  {t('users.permissionAudit.report.whoHasAccess')}
                </Typography.Title>
                <Table
                  size="small"
                  rowKey={(r) => `m-${r.subjectType}-${r.subjectId}-${r.permissionKey}`}
                  loading={complianceQuery.isLoading}
                  dataSource={compliance?.accessMatrix ?? []}
                  columns={complianceColumns}
                  pagination={{ pageSize: 8 }}
                  scroll={{ x: true }}
                />
              </Space>
            ),
          },
          {
            key: 'export',
            label: t('users.permissionAudit.report.tabExport'),
            children: (
              <Space orientation="vertical" size={12}>
                <Typography.Paragraph>
                  {t('users.permissionAudit.report.exportIntro')}
                </Typography.Paragraph>
                <Space wrap>
                  <Button
                    type="primary"
                    disabled={!canExport}
                    loading={exporting === 'csv'}
                    onClick={() => void handleExport('csv')}
                  >
                    {t('users.permissionAudit.report.exportCsv')}
                  </Button>
                  <Button
                    disabled={!canExport}
                    loading={exporting === 'pdf'}
                    onClick={() => void handleExport('pdf')}
                  >
                    {t('users.permissionAudit.report.exportPdf')}
                  </Button>
                  <Button
                    disabled={!canExport}
                    loading={exporting === 'json'}
                    onClick={() => void handleExport('json')}
                  >
                    {t('users.permissionAudit.report.exportJson')}
                  </Button>
                </Space>
              </Space>
            ),
          },
          {
            key: 'schedule',
            label: t('users.permissionAudit.report.tabSchedule'),
            children: (
              <Form
                form={scheduleForm}
                layout="vertical"
                initialValues={{
                  preset: 'weekly',
                  format: 'permission-pdf',
                  name: t('users.permissionAudit.report.defaultScheduleName'),
                }}
              >
                <Alert
                  type="info"
                  showIcon
                  style={{ marginBottom: 16 }}
                  title={t('users.permissionAudit.report.scheduleIntro')}
                />
                <Form.Item
                  name="name"
                  label={t('users.permissionAudit.report.scheduleName')}
                  rules={[{ required: true }]}
                >
                  <Input />
                </Form.Item>
                <Form.Item
                  name="preset"
                  label={t('users.permissionAudit.report.schedulePreset')}
                  rules={[{ required: true }]}
                >
                  <Select
                    options={[
                      {
                        value: 'weekly',
                        label: t('users.permissionAudit.report.presetWeekly'),
                      },
                      {
                        value: 'monthly',
                        label: t('users.permissionAudit.report.presetMonthly'),
                      },
                      {
                        value: 'compliance',
                        label: t('users.permissionAudit.report.presetCompliance'),
                      },
                    ]}
                  />
                </Form.Item>
                <Form.Item
                  name="recipients"
                  label={t('users.permissionAudit.report.scheduleRecipients')}
                  rules={[{ required: true }]}
                  extra={t('users.permissionAudit.report.scheduleRecipientsHint')}
                >
                  <Input.TextArea rows={2} placeholder="auditor@example.com" />
                </Form.Item>
                <Form.Item name="format" label={t('users.permissionAudit.report.scheduleFormat')}>
                  <Select
                    options={[
                      { value: 'permission-pdf', label: 'PDF' },
                      { value: 'permission-csv', label: 'CSV' },
                      { value: 'permission-json', label: 'JSON' },
                    ]}
                  />
                </Form.Item>
                <Button
                  type="primary"
                  disabled={!canExport}
                  loading={scheduling}
                  onClick={() => void handleSchedule()}
                >
                  {t('users.permissionAudit.report.scheduleSubmit')}
                </Button>
              </Form>
            ),
          },
        ]}
      />
    </Modal>
  );
}
