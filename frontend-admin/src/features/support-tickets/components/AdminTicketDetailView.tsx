'use client';

import { Button, Card, Checkbox, Input, List, Select, Space, Tag, Typography } from 'antd';

import type { SupportTicketDetailDto } from '@/features/support-tickets/api/supportTickets';
import {
  supportCategoryLabelKey,
  supportPriorityColor,
  supportPriorityLabelKey,
  supportStatusColor,
  supportStatusLabelKey,
} from '@/features/support-tickets/utils/supportTicketDisplay';
import { useI18n } from '@/i18n';
import { formatGermanDate } from '@/lib/dateFormatter';

type AdminTicketDetailViewProps = {
  detail: SupportTicketDetailDto;
  reply: string;
  isInternal: boolean;
  replyPending?: boolean;
  assignPending?: boolean;
  onReplyChange: (value: string) => void;
  onInternalChange: (value: boolean) => void;
  onSendReply: () => void;
  onStatusChange: (status: string) => void;
  onAssign: () => void;
  onBack: () => void;
};

export function AdminTicketDetailView({
  detail,
  reply,
  isInternal,
  replyPending,
  assignPending,
  onReplyChange,
  onInternalChange,
  onSendReply,
  onStatusChange,
  onAssign,
  onBack,
}: AdminTicketDetailViewProps) {
  const { t } = useI18n();

  return (
    <Card
      title={`${detail.ticketNumber} — ${detail.title}`}
      extra={
        <Button type="link" onClick={onBack}>
          {t('support.tickets.backToList')}
        </Button>
      }
    >
      <Space wrap style={{ marginBottom: 16 }}>
        <Tag>{detail.tenantName || '—'}</Tag>
        <Tag>{t(supportCategoryLabelKey(detail.category))}</Tag>
        <Tag color={supportPriorityColor(detail.priority)}>
          {t(supportPriorityLabelKey(detail.priority))}
        </Tag>
        <Tag color={supportStatusColor(detail.status)}>
          {t(supportStatusLabelKey(detail.status))}
        </Tag>
        <Typography.Text type="secondary">
          {t('support.tickets.assignedTo')}:{' '}
          {detail.assignedToDisplayName || t('support.tickets.unassigned')}
        </Typography.Text>
      </Space>
      <Space wrap style={{ marginBottom: 16 }}>
        <Select
          value={detail.status}
          style={{ minWidth: 200 }}
          onChange={onStatusChange}
          options={[
            { value: 'Open', label: t('support.tickets.statusOpen') },
            { value: 'InProgress', label: t('support.tickets.statusInProgress') },
            { value: 'WaitingOnTenant', label: t('support.tickets.statusWaitingOnTenant') },
            { value: 'WaitingOnStaff', label: t('support.tickets.statusWaitingOnStaff') },
            { value: 'Resolved', label: t('support.tickets.statusResolved') },
            { value: 'Closed', label: t('support.tickets.statusClosed') },
          ]}
        />
        <Button loading={assignPending} onClick={onAssign}>
          {t('support.tickets.assignToMe')}
        </Button>
      </Space>
      <List
        dataSource={detail.messages}
        renderItem={(msg) => (
          <List.Item>
            <List.Item.Meta
              title={
                <Space>
                  <span>{msg.authorDisplayName || msg.authorUserId}</span>
                  {msg.isStaffReply ? <Tag color="purple">{t('support.tickets.reply')}</Tag> : null}
                  {msg.isInternal ? (
                    <Tag color="gold">{t('support.tickets.internalNote')}</Tag>
                  ) : null}
                </Space>
              }
              description={
                <>
                  <Typography.Paragraph style={{ marginBottom: 4 }}>{msg.body}</Typography.Paragraph>
                  <Typography.Text type="secondary">
                    {formatGermanDate(msg.createdAtUtc)}
                  </Typography.Text>
                </>
              }
            />
          </List.Item>
        )}
      />
      <Input.TextArea
        rows={3}
        value={reply}
        onChange={(e) => onReplyChange(e.target.value)}
        placeholder={t('support.tickets.message')}
        style={{ marginTop: 16 }}
      />
      <Space style={{ marginTop: 8 }}>
        <Checkbox checked={isInternal} onChange={(e) => onInternalChange(e.target.checked)}>
          {t('support.tickets.internalNote')}
        </Checkbox>
        <Button
          type="primary"
          loading={replyPending}
          disabled={reply.trim().length < 1}
          onClick={onSendReply}
        >
          {t('support.tickets.sendReply')}
        </Button>
      </Space>
    </Card>
  );
}
