'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Card, Col, DatePicker, Input, Row, Select, Space, Statistic, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  addAdminSupportTicketMessage,
  assignSupportTicket,
  fetchAdminSupportTicket,
  fetchAllSupportTickets,
  fetchSupportInboxSummary,
  supportTicketQueryKeys,
  updateAdminSupportTicketStatus,
  type SupportTicketListItemDto,
  type SupportTicketListParams,
} from '@/features/support-tickets/api/supportTickets';
import { AdminTicketDetailView } from '@/features/support-tickets/components/AdminTicketDetailView';
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

export function AdminSupportInboxPageContent() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [reply, setReply] = useState('');
  const [isInternal, setIsInternal] = useState(false);
  const [filters, setFilters] = useState<SupportTicketListParams>({ page: 1, pageSize: 20 });
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);

  const summaryQuery = useQuery({
    queryKey: supportTicketQueryKeys.summary(),
    queryFn: ({ signal }) => fetchSupportInboxSummary(signal),
  });

  const listQuery = useQuery({
    queryKey: supportTicketQueryKeys.inbox(filters),
    queryFn: ({ signal }) => fetchAllSupportTickets(filters, signal),
  });

  const detailQuery = useQuery({
    queryKey: supportTicketQueryKeys.detail(selectedId ?? ''),
    queryFn: ({ signal }) => fetchAdminSupportTicket(selectedId!, signal),
    enabled: !!selectedId,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: supportTicketQueryKeys.all });
  };

  const replyMutation = useMutation({
    mutationFn: () => addAdminSupportTicketMessage(selectedId!, reply.trim(), isInternal),
    onSuccess: () => {
      notify.success(t('support.tickets.replySuccess'));
      setReply('');
      setIsInternal(false);
      invalidate();
    },
    onError: () => notify.error(t('support.tickets.error')),
  });

  const statusMutation = useMutation({
    mutationFn: (status: string) => updateAdminSupportTicketStatus(selectedId!, status),
    onSuccess: () => {
      notify.success(t('support.tickets.statusSuccess'));
      invalidate();
    },
    onError: () => notify.error(t('support.tickets.error')),
  });

  const assignMutation = useMutation({
    mutationFn: () => assignSupportTicket(selectedId!),
    onSuccess: () => {
      notify.success(t('support.tickets.assignSuccess'));
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
        title: t('support.tickets.tenant'),
        dataIndex: 'tenantName',
        key: 'tenantName',
        render: (value: string | null | undefined) => value || '—',
      },
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
    ],
    [t]
  );

  const summary = summaryQuery.data;
  const detail = detailQuery.data;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader
        title={t('support.tickets.inboxTitle')}
        subtitle={t('support.tickets.inboxSubtitle')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('support.tickets.inboxTitle') }]}
      />

      <Row gutter={[16, 16]}>
        <Col xs={12} md={6}>
          <Card>
            <Statistic title={t('support.tickets.statusOpen')} value={summary?.openCount ?? 0} />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card>
            <Statistic
              title={t('support.tickets.statusInProgress')}
              value={summary?.inProgressCount ?? 0}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card>
            <Statistic
              title={t('support.tickets.resolvedLast30Days')}
              value={summary?.resolvedLast30DaysCount ?? 0}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card>
            <Statistic title={t('support.tickets.totalTickets')} value={summary?.totalCount ?? 0} />
          </Card>
        </Col>
      </Row>

      <Space wrap>
        <Input.Search
          allowClear
          placeholder={t('support.tickets.search')}
          style={{ minWidth: 220 }}
          onSearch={(search) => setFilters((prev) => ({ ...prev, page: 1, search: search || undefined }))}
        />
        <Select
          allowClear
          placeholder={t('support.tickets.status')}
          style={{ minWidth: 180 }}
          value={filters.status}
          onChange={(status) => setFilters((prev) => ({ ...prev, page: 1, status }))}
          options={[
            { value: 'Open', label: t('support.tickets.statusOpen') },
            { value: 'InProgress', label: t('support.tickets.statusInProgress') },
            { value: 'WaitingOnStaff', label: t('support.tickets.statusWaitingOnStaff') },
            { value: 'WaitingOnTenant', label: t('support.tickets.statusWaitingOnTenant') },
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
        <Select
          allowClear
          placeholder={t('support.tickets.priority')}
          style={{ minWidth: 160 }}
          value={filters.priority}
          onChange={(priority) => setFilters((prev) => ({ ...prev, page: 1, priority }))}
          options={[
            { value: 'Urgent', label: t('support.tickets.priorityUrgent') },
            { value: 'High', label: t('support.tickets.priorityHigh') },
            { value: 'Medium', label: t('support.tickets.priorityMedium') },
            { value: 'Low', label: t('support.tickets.priorityLow') },
          ]}
        />
        <DatePicker.RangePicker
          value={range}
          onChange={(next) => {
            setRange(next);
            setFilters((prev) => ({
              ...prev,
              page: 1,
              fromUtc: next?.[0]?.startOf('day').toISOString(),
              toUtc: next?.[1]?.endOf('day').toISOString(),
            }));
          }}
        />
      </Space>

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
        onRow={(row) => ({
          onClick: () => setSelectedId(row.id),
          style: { cursor: 'pointer' },
        })}
      />

      {selectedId && detail ? (
        <AdminTicketDetailView
          detail={detail}
          reply={reply}
          isInternal={isInternal}
          replyPending={replyMutation.isPending}
          assignPending={assignMutation.isPending}
          onReplyChange={setReply}
          onInternalChange={setIsInternal}
          onSendReply={() => replyMutation.mutate()}
          onStatusChange={(status) => statusMutation.mutate(status)}
          onAssign={() => assignMutation.mutate()}
          onBack={() => setSelectedId(null)}
        />
      ) : null}
    </div>
  );
}
