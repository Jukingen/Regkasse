'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button,
  Checkbox,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { DateColumn } from '@/components/DateColumn';
import {
  approveRksvAusfall,
  cancelRksvAusfall,
  listAusfallBegruendungCodes,
  listRksvAusfallEpisodes,
  markManualRksvAusfall,
  triggerRksvAusfall,
} from '@/features/tse-ausfall/api/tseAusfall';
import type { RksvAusfallEpisode } from '@/features/tse-ausfall/types';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const QUERY_KEY = ['tse-ausfall'] as const;

function statusColor(status: string): string {
  switch (status) {
    case 'Verified':
    case 'Closed':
      return 'green';
    case 'Suggested':
    case 'PendingApproval':
      return 'orange';
    case 'Submitted':
      return 'blue';
    case 'Failed':
      return 'red';
    default:
      return 'default';
  }
}

export default function TseAusfallPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const canView = hasPermission(PERMISSIONS.FINANZONLINE_VIEW);
  const canSubmit = hasPermission(PERMISSIONS.FINANZONLINE_SUBMIT);
  const [statusFilter, setStatusFilter] = useState<string | undefined>();
  const [triggerOpen, setTriggerOpen] = useState(false);
  const [form] = Form.useForm();

  const listQuery = useQuery({
    queryKey: [...QUERY_KEY, 'list', statusFilter ?? 'all'],
    queryFn: ({ signal }) => listRksvAusfallEpisodes({ status: statusFilter, signal }),
    enabled: canView,
  });

  const codesQuery = useQuery({
    queryKey: [...QUERY_KEY, 'codes'],
    queryFn: ({ signal }) => listAusfallBegruendungCodes(signal),
    enabled: canView && canSubmit,
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: QUERY_KEY });
  };

  const approveMutation = useMutation({
    mutationFn: (id: string) => approveRksvAusfall(id),
    onSuccess: async (res) => {
      if (!res.success) {
        notify.error(res.message || t('common.errorGeneric'));
        return;
      }
      notify.success(t('tseAusfall.successApprove'));
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'TseAusfall.approve', fallbackKey: 'common.errorGeneric' });
    },
  });

  const manualMutation = useMutation({
    mutationFn: (id: string) => markManualRksvAusfall(id),
    onSuccess: async (res) => {
      if (!res.success) {
        notify.error(res.message || t('common.errorGeneric'));
        return;
      }
      notify.success(t('tseAusfall.successManual'));
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'TseAusfall.markManual', fallbackKey: 'common.errorGeneric' });
    },
  });

  const cancelMutation = useMutation({
    mutationFn: (id: string) => cancelRksvAusfall(id),
    onSuccess: async (res) => {
      if (!res.success) {
        notify.error(res.message || t('common.errorGeneric'));
        return;
      }
      notify.success(t('tseAusfall.successCancel'));
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'TseAusfall.cancel', fallbackKey: 'common.errorGeneric' });
    },
  });

  const triggerMutation = useMutation({
    mutationFn: triggerRksvAusfall,
    onSuccess: async (res) => {
      if (!res.success) {
        notify.error(res.message || t('common.errorGeneric'));
        return;
      }
      notify.success(t('tseAusfall.successTrigger'));
      setTriggerOpen(false);
      form.resetFields();
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, { logContext: 'TseAusfall.trigger', fallbackKey: 'common.errorGeneric' });
    },
  });

  const columns: ColumnsType<RksvAusfallEpisode> = useMemo(
    () => [
      {
        title: t('tseAusfall.colStatus'),
        dataIndex: 'status',
        width: 140,
        render: (v: string) => <Tag color={statusColor(v)}>{v}</Tag>,
      },
      {
        title: t('tseAusfall.colOperation'),
        dataIndex: 'operationKind',
        width: 160,
      },
      {
        title: t('tseAusfall.colType'),
        dataIndex: 'episodeType',
        width: 90,
      },
      {
        title: t('tseAusfall.colDevice'),
        key: 'device',
        ellipsis: true,
        render: (_: unknown, row) => row.deviceSerial || row.certificateSerial || row.kassenId || '—',
      },
      {
        title: t('tseAusfall.colBegruendung'),
        dataIndex: 'begruendung',
        ellipsis: true,
      },
      {
        title: t('tseAusfall.colBeginn'),
        dataIndex: 'beginnUtc',
        width: 170,
        render: (v: string | null) => (v ? <DateColumn date={v} format="datetime" utc /> : '—'),
      },
      {
        title: t('tseAusfall.colEnde'),
        dataIndex: 'endeUtc',
        width: 170,
        render: (v: string | null) => (v ? <DateColumn date={v} format="datetime" utc /> : '—'),
      },
      {
        title: t('tseAusfall.colOutbox'),
        key: 'outbox',
        width: 120,
        render: (_: unknown, row) =>
          row.outboxMessageId ? (
            <Link href={`/rksv/finanz-online-outbox?outboxId=${row.outboxMessageId}`}>
              {t('tseAusfall.openOutbox')}
            </Link>
          ) : (
            '—'
          ),
      },
      {
        title: t('tseAusfall.colActions'),
        key: 'actions',
        width: 280,
        render: (_: unknown, row) => {
          if (!canSubmit) return null;
          const canApprove =
            row.status === 'Suggested' || row.status === 'PendingApproval' || row.status === 'Failed';
          const canCancel = row.status === 'Suggested' || row.status === 'PendingApproval';
          return (
            <Space wrap size="small">
              {canApprove ? (
                <Button
                  size="small"
                  type="primary"
                  loading={approveMutation.isPending}
                  onClick={() => {
                    modal.confirm({
                      title: t('tseAusfall.approveConfirmTitle'),
                      content: t('tseAusfall.approveConfirmContent'),
                      onOk: () => approveMutation.mutateAsync(row.id),
                    });
                  }}
                >
                  {t('tseAusfall.approve')}
                </Button>
              ) : null}
              {row.status !== 'Closed' && row.status !== 'Verified' ? (
                <Button
                  size="small"
                  loading={manualMutation.isPending}
                  onClick={() => {
                    modal.confirm({
                      title: t('tseAusfall.markManualConfirmTitle'),
                      content: t('tseAusfall.markManualConfirmContent'),
                      onOk: () => manualMutation.mutateAsync(row.id),
                    });
                  }}
                >
                  {t('tseAusfall.markManual')}
                </Button>
              ) : null}
              {canCancel ? (
                <Button
                  size="small"
                  danger
                  loading={cancelMutation.isPending}
                  onClick={() => cancelMutation.mutate(row.id)}
                >
                  {t('tseAusfall.cancel')}
                </Button>
              ) : null}
            </Space>
          );
        },
      },
    ],
    [t, canSubmit, approveMutation, manualMutation, cancelMutation, modal]
  );

  if (!canView) {
    return (
      <Typography.Paragraph type="danger">{t('tseAusfall.forbidden')}</Typography.Paragraph>
    );
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseAusfall.title')}
        subtitle={t('tseAusfall.subtitle')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseAusfall.title') }]}
        extra={
          canSubmit ? (
            <Button type="primary" onClick={() => setTriggerOpen(true)}>
              {t('tseAusfall.trigger')}
            </Button>
          ) : null
        }
      />

      <Space style={{ marginBottom: 16 }} wrap>
        <Select
          allowClear
          placeholder={t('tseAusfall.filterStatus')}
          style={{ minWidth: 180 }}
          value={statusFilter}
          onChange={(v) => setStatusFilter(v)}
          options={[
            { value: 'Suggested', label: 'Suggested' },
            { value: 'PendingApproval', label: 'PendingApproval' },
            { value: 'Submitted', label: 'Submitted' },
            { value: 'Verified', label: 'Verified' },
            { value: 'Failed', label: 'Failed' },
            { value: 'Closed', label: 'Closed' },
          ]}
        />
        <Typography.Text type="secondary">{t('tseAusfall.demoNote')}</Typography.Text>
      </Space>

      <Table<RksvAusfallEpisode>
        rowKey="id"
        loading={listQuery.isLoading}
        columns={columns}
        dataSource={listQuery.data ?? []}
        locale={{ emptyText: t('tseAusfall.empty') }}
        pagination={{ pageSize: 20 }}
        scroll={{ x: 1100 }}
      />

      <Modal
        title={t('tseAusfall.triggerTitle')}
        open={triggerOpen}
        onCancel={() => setTriggerOpen(false)}
        confirmLoading={triggerMutation.isPending}
        okText={t('tseAusfall.triggerOk')}
        destroyOnHidden
        onOk={async () => {
          const values = await form.validateFields();
          await triggerMutation.mutateAsync({
            episodeType: values.episodeType,
            operationKind: values.operationKind,
            begruendung: values.begruendung,
            certificateSerial: values.certificateSerial || undefined,
            kassenId: values.kassenId || undefined,
            operatorNote: values.operatorNote || undefined,
            enqueueImmediately: Boolean(values.enqueueImmediately),
          });
        }}
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            episodeType: 'SCU',
            operationKind: 'Ausfall',
            begruendung: 'HARDWARE_DEFEKT',
            enqueueImmediately: false,
          }}
        >
          <Form.Item name="episodeType" label={t('tseAusfall.fieldEpisodeType')} rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'SCU', label: 'SCU' },
                { value: 'Kasse', label: 'Kasse' },
              ]}
            />
          </Form.Item>
          <Form.Item name="operationKind" label={t('tseAusfall.fieldOperation')} rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'Ausfall', label: 'Ausfall' },
                { value: 'Wiederinbetriebnahme', label: 'Wiederinbetriebnahme' },
              ]}
            />
          </Form.Item>
          <Form.Item name="begruendung" label={t('tseAusfall.fieldBegruendung')} rules={[{ required: true }]}>
            <Select
              options={(codesQuery.data ?? ['SONSTIGES']).map((c) => ({ value: c, label: c }))}
            />
          </Form.Item>
          <Form.Item name="certificateSerial" label={t('tseAusfall.fieldCertSerial')}>
            <Input />
          </Form.Item>
          <Form.Item name="kassenId" label={t('tseAusfall.fieldKassenId')}>
            <Input />
          </Form.Item>
          <Form.Item name="operatorNote" label={t('tseAusfall.fieldNote')}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="enqueueImmediately" valuePropName="checked">
            <Checkbox>{t('tseAusfall.fieldEnqueueNow')}</Checkbox>
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
