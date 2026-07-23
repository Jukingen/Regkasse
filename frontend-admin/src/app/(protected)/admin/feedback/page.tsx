'use client';

/**
 * Super Admin weekly feedback review inbox.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Form,
  Input,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import {
  type AdminFeedbackCategory,
  type AdminFeedbackDto,
  type AdminFeedbackStatus,
  fetchAllAdminFeedback,
  updateAdminFeedbackStatus,
} from '@/api/manual/adminFeedback';
import { dateColumnRender } from '@/components/DateColumn';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

const CATEGORIES: AdminFeedbackCategory[] = [
  'EaseOfUse',
  'Performance',
  'FeatureRequest',
  'Bug',
];

const STATUSES: AdminFeedbackStatus[] = [
  'UnderReview',
  'InProgress',
  'Implemented',
  'Declined',
  'Duplicate',
];

const STATUS_COLOR: Record<string, string> = {
  UnderReview: 'processing',
  InProgress: 'blue',
  Implemented: 'success',
  Declined: 'default',
  Duplicate: 'warning',
};

export default function AdminFeedbackInboxPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] = useState<string | undefined>('UnderReview');
  const [categoryFilter, setCategoryFilter] = useState<string | undefined>();
  const [editing, setEditing] = useState<AdminFeedbackDto | null>(null);
  const [form] = Form.useForm<{ status: AdminFeedbackStatus; reviewerNote?: string }>();

  const listQuery = useQuery({
    queryKey: ['admin', 'feedback', 'all', statusFilter, categoryFilter],
    queryFn: () =>
      fetchAllAdminFeedback({
        status: statusFilter,
        category: categoryFilter,
        limit: 100,
      }),
  });

  const saveMutation = useMutation({
    mutationFn: ({ id, status, reviewerNote }: { id: string; status: AdminFeedbackStatus; reviewerNote?: string }) =>
      updateAdminFeedbackStatus(id, { status, reviewerNote }),
    onSuccess: async () => {
      notify.success(t('feedback.inbox.saved'));
      setEditing(null);
      await queryClient.invalidateQueries({ queryKey: ['admin', 'feedback'] });
    },
  });

  const columns: ColumnsType<AdminFeedbackDto> = useMemo(
    () => [
      {
        title: t('feedback.inbox.created'),
        dataIndex: 'createdAtUtc',
        width: 160,
        render: dateColumnRender('datetime'),
      },
      {
        title: t('feedback.inbox.tenant'),
        dataIndex: 'tenantName',
        width: 140,
        render: (v: string | null | undefined, row) => v || row.tenantId.slice(0, 8),
      },
      {
        title: t('feedback.form.category'),
        dataIndex: 'category',
        width: 140,
        render: (c: string) => t(`feedback.categories.${c}` as 'feedback.categories.Bug'),
      },
      {
        title: t('feedback.form.title'),
        dataIndex: 'title',
        ellipsis: true,
      },
      {
        title: t('feedback.inbox.filterStatus'),
        dataIndex: 'status',
        width: 140,
        render: (s: string) => (
          <Tag color={STATUS_COLOR[s] ?? 'default'}>
            {t(`feedback.statuses.${s}` as 'feedback.statuses.UnderReview')}
          </Tag>
        ),
      },
      {
        title: t('feedback.inbox.submitter'),
        dataIndex: 'submittedByDisplayName',
        width: 180,
        render: (v: string | null | undefined, row) => (
          <Space direction="vertical" size={0}>
            <span>{v || row.submittedByUserId.slice(0, 8)}</span>
            {row.submittedByUsername ? (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {row.submittedByUsername}
              </Typography.Text>
            ) : null}
          </Space>
        ),
      },
      {
        title: t('feedback.inbox.actions'),
        key: 'actions',
        width: 120,
        render: (_, row) => (
          <Button
            type="link"
            onClick={() => {
              setEditing(row);
              form.setFieldsValue({
                status: row.status as AdminFeedbackStatus,
                reviewerNote: row.reviewerNote ?? undefined,
              });
            }}
          >
            {t('feedback.inbox.updateStatus')}
          </Button>
        ),
      },
    ],
    [form, t],
  );

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {t('feedback.inbox.pageTitle')}
        </Typography.Title>
        <Typography.Paragraph type="secondary">{t('feedback.inbox.pageSubtitle')}</Typography.Paragraph>
      </div>

      <Alert
        type="info"
        showIcon
        message={t('feedback.process.weeklyTitle')}
        description={t('feedback.process.weeklyHint')}
      />

      <Card>
        <Space wrap style={{ marginBottom: 16 }}>
          <Select
            allowClear
            placeholder={t('feedback.inbox.filterStatus')}
            style={{ minWidth: 180 }}
            value={statusFilter}
            onChange={(v) => setStatusFilter(v)}
            options={[
              ...STATUSES.map((s) => ({
                value: s,
                label: t(`feedback.statuses.${s}` as 'feedback.statuses.UnderReview'),
              })),
            ]}
          />
          <Select
            allowClear
            placeholder={t('feedback.inbox.filterCategory')}
            style={{ minWidth: 180 }}
            value={categoryFilter}
            onChange={(v) => setCategoryFilter(v)}
            options={CATEGORIES.map((c) => ({
              value: c,
              label: t(`feedback.categories.${c}` as 'feedback.categories.Bug'),
            }))}
          />
        </Space>

        {listQuery.isError ? (
          <Typography.Text type="danger">{t('feedback.inbox.loadError')}</Typography.Text>
        ) : (
          <Table<AdminFeedbackDto>
            rowKey="id"
            loading={listQuery.isLoading}
            columns={columns}
            dataSource={listQuery.data?.items ?? []}
            pagination={{ pageSize: 20, total: listQuery.data?.total }}
            expandable={{
              expandedRowRender: (row) => (
                <Space direction="vertical" style={{ width: '100%' }}>
                  <Typography.Paragraph style={{ marginBottom: 0 }}>{row.message}</Typography.Paragraph>
                  {row.pagePath ? (
                    <Typography.Text type="secondary">{row.pagePath}</Typography.Text>
                  ) : null}
                  {row.reviewerNote ? (
                    <Typography.Text>
                      <strong>{t('feedback.mine.reviewerNote')}:</strong> {row.reviewerNote}
                    </Typography.Text>
                  ) : null}
                </Space>
              ),
            }}
            locale={{ emptyText: t('feedback.inbox.empty') }}
          />
        )}
      </Card>

      {editing ? (
        <Card title={t('feedback.inbox.updateStatus')}>
          <Typography.Paragraph>
            <strong>{editing.title}</strong>
          </Typography.Paragraph>
          <Form
            form={form}
            layout="vertical"
            onFinish={(values) =>
              saveMutation.mutate({
                id: editing.id,
                status: values.status,
                reviewerNote: values.reviewerNote?.trim() || undefined,
              })
            }
          >
            <Form.Item name="status" label={t('feedback.inbox.filterStatus')} rules={[{ required: true }]}>
              <Select
                options={STATUSES.map((s) => ({
                  value: s,
                  label: t(`feedback.statuses.${s}` as 'feedback.statuses.UnderReview'),
                }))}
              />
            </Form.Item>
            <Form.Item name="reviewerNote" label={t('feedback.inbox.note')}>
              <Input.TextArea rows={3} maxLength={1000} showCount />
            </Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
                {t('feedback.inbox.save')}
              </Button>
              <Button onClick={() => setEditing(null)}>{t('common.buttons.cancel')}</Button>
            </Space>
          </Form>
        </Card>
      ) : null}
    </Space>
  );
}
