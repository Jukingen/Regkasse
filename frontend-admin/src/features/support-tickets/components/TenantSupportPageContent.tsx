'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Select, Space, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { EmptyState } from '@/components/EmptyState';
import {
  addSupportTicketMessage,
  createSupportTicket,
  fetchMySupportTickets,
  fetchSupportTicket,
  supportTicketQueryKeys,
  updateOwnSupportTicketStatus,
  type SupportTicketListItemDto,
  type SupportTicketListParams,
} from '@/features/support-tickets/api/supportTickets';
import { CreateTicketModal, type CreateTicketFormValues } from '@/features/support-tickets/components/CreateTicketModal';
import { TicketDetailView } from '@/features/support-tickets/components/TicketDetailView';
import {
  supportCategoryLabelKey,
  supportPriorityColor,
  supportPriorityLabelKey,
  supportStatusColor,
  supportStatusLabelKey,
} from '@/features/support-tickets/utils/supportTicketDisplay';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { formatGermanDate } from '@/lib/dateFormatter';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export function TenantSupportPageContent() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [reply, setReply] = useState('');
  const [filters, setFilters] = useState<SupportTicketListParams>({ page: 1, pageSize: 20 });

  const listQuery = useQuery({
    queryKey: supportTicketQueryKeys.mine(filters),
    queryFn: ({ signal }) => fetchMySupportTickets(filters, signal),
  });

  const detailQuery = useQuery({
    queryKey: supportTicketQueryKeys.detail(selectedId ?? ''),
    queryFn: ({ signal }) => fetchSupportTicket(selectedId!, signal),
    enabled: !!selectedId,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: supportTicketQueryKeys.all });
  };

  const createMutation = useMutation({
    mutationFn: (values: CreateTicketFormValues) =>
      createSupportTicket({
        category: values.category,
        priority: values.priority,
        title: values.title.trim(),
        message: values.message.trim(),
      }),
    onSuccess: () => {
      notify.success(t('support.tickets.createSuccess'));
      setCreateOpen(false);
      invalidate();
    },
    onError: () => notify.error(t('support.tickets.error')),
  });

  const replyMutation = useMutation({
    mutationFn: (body: string) => addSupportTicketMessage(selectedId!, body),
    onSuccess: () => {
      notify.success(t('support.tickets.replySuccess'));
      setReply('');
      invalidate();
    },
    onError: () => notify.error(t('support.tickets.error')),
  });

  const statusMutation = useMutation({
    mutationFn: (status: string) => updateOwnSupportTicketStatus(selectedId!, status),
    onSuccess: () => {
      notify.success(t('support.tickets.statusSuccess'));
      invalidate();
    },
    onError: () => notify.error(t('support.tickets.error')),
  });

  const columns: ColumnsType<SupportTicketListItemDto> = useMemo(
    () => [
      {
        title: t('support.tickets.ticketNumber'),
        dataIndex: 'ticketNumber',
        key: 'ticketNumber',
        width: 160,
      },
      { title: t('support.tickets.subject'), dataIndex: 'title', key: 'title' },
      {
        title: t('support.tickets.category'),
        dataIndex: 'category',
        key: 'category',
        render: (value: string) => t(supportCategoryLabelKey(value)),
      },
      {
        title: t('support.tickets.priority'),
        dataIndex: 'priority',
        key: 'priority',
        render: (value: string) => (
          <Tag color={supportPriorityColor(value)}>{t(supportPriorityLabelKey(value))}</Tag>
        ),
      },
      {
        title: t('support.tickets.status'),
        dataIndex: 'status',
        key: 'status',
        render: (value: string) => (
          <Tag color={supportStatusColor(value)}>{t(supportStatusLabelKey(value))}</Tag>
        ),
      },
      {
        title: t('support.tickets.createdAt'),
        dataIndex: 'createdAtUtc',
        key: 'createdAtUtc',
        render: (value: string) => formatGermanDate(value),
      },
      {
        title: t('support.tickets.actions'),
        key: 'actions',
        render: (_, row) => (
          <Button type="link" onClick={() => setSelectedId(row.id)}>
            {t('support.tickets.view')}
          </Button>
        ),
      },
    ],
    [t]
  );

  const pageTitle = t('support.tickets.title');
  const detail = detailQuery.data;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader
        title={pageTitle}
        subtitle={t('support.tickets.subtitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.meinKonto'), href: '/tenant/portal' },
          { title: pageTitle },
        ]}
        extra={
          selectedId ? null : (
            <Button type="primary" onClick={() => setCreateOpen(true)}>
              {t('support.tickets.newTicket')}
            </Button>
          )
        }
      />

      {selectedId && detail ? (
        <TicketDetailView
          detail={detail}
          reply={reply}
          replyPending={replyMutation.isPending}
          statusPending={statusMutation.isPending}
          onReplyChange={setReply}
          onSendReply={() => replyMutation.mutate(reply.trim())}
          onClose={() => statusMutation.mutate('Closed')}
          onReopen={() => statusMutation.mutate('Open')}
          onBack={() => setSelectedId(null)}
        />
      ) : (
        <Card title={t('support.tickets.title')}>
          <Space wrap style={{ marginBottom: 16 }}>
            <Select
              allowClear
              placeholder={t('support.tickets.status')}
              style={{ minWidth: 180 }}
              value={filters.status}
              onChange={(status) => setFilters((prev) => ({ ...prev, page: 1, status }))}
              options={[
                { value: 'Open', label: t('support.tickets.statusOpen') },
                { value: 'InProgress', label: t('support.tickets.statusInProgress') },
                { value: 'Resolved', label: t('support.tickets.statusResolved') },
                { value: 'Closed', label: t('support.tickets.statusClosed') },
              ]}
            />
            <Select
              allowClear
              placeholder={t('support.tickets.category')}
              style={{ minWidth: 180 }}
              value={filters.category}
              onChange={(category) => setFilters((prev) => ({ ...prev, page: 1, category }))}
              options={[
                { value: 'Technical', label: t('support.tickets.categoryTechnical') },
                { value: 'Billing', label: t('support.tickets.categoryBilling') },
                { value: 'License', label: t('support.tickets.categoryLicense') },
                { value: 'FeatureRequest', label: t('support.tickets.categoryFeature') },
                { value: 'General', label: t('support.tickets.categoryGeneral') },
              ]}
            />
          </Space>
          {(listQuery.data?.totalCount ?? 0) === 0 && !listQuery.isLoading ? (
            <EmptyState title={t('support.tickets.noTickets')} />
          ) : (
            <Table<SupportTicketListItemDto>
              rowKey="id"
              loading={listQuery.isLoading}
              columns={columns}
              dataSource={listQuery.data?.items ?? []}
              pagination={{
                current: filters.page ?? 1,
                pageSize: filters.pageSize ?? 20,
                total: listQuery.data?.totalCount ?? 0,
                showSizeChanger: true,
                onChange: (page, pageSize) => setFilters((prev) => ({ ...prev, page, pageSize })),
              }}
            />
          )}
        </Card>
      )}

      <CreateTicketModal
        open={createOpen}
        loading={createMutation.isPending}
        onCancel={() => setCreateOpen(false)}
        onSubmit={(values) => createMutation.mutate(values)}
      />
    </div>
  );
}
