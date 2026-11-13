'use client';

import { Button, Card, Input, List, Space, Tag, Typography } from 'antd';

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

type TicketDetailViewProps = {
  detail: SupportTicketDetailDto;
  reply: string;
  replyPending?: boolean;
  statusPending?: boolean;
  onReplyChange: (value: string) => void;
  onSendReply: () => void;
  onClose: () => void;
  onReopen: () => void;
  onBack: () => void;
};

export function TicketDetailView({
  detail,
  reply,
  replyPending,
  statusPending,
  onReplyChange,
  onSendReply,
  onClose,
  onReopen,
  onBack,
}: TicketDetailViewProps) {
  const { t } = useI18n();
  const canClose = detail.status !== 'Closed' && detail.status !== 'Resolved';
  const canReopen = detail.status === 'Closed' || detail.status === 'Resolved';

  return (
    <Card
      title={`${detail.ticketNumber} — ${detail.title}`}
      extra={
        <Space>
          {canClose ? (
            <Button loading={statusPending} onClick={onClose}>
              {t('support.tickets.close')}
            </Button>
          ) : null}
          {canReopen ? (
            <Button loading={statusPending} onClick={onReopen}>
              {t('support.tickets.reopen')}
            </Button>
          ) : null}
          <Button type="link" onClick={onBack}>
            {t('support.tickets.backToList')}
          </Button>
        </Space>
      }
    >
      <Space wrap style={{ marginBottom: 16 }}>
        <Tag>{t(supportCategoryLabelKey(detail.category))}</Tag>
        <Tag color={supportPriorityColor(detail.priority)}>
          {t(supportPriorityLabelKey(detail.priority))}
        </Tag>
        <Tag color={supportStatusColor(detail.status)}>
          {t(supportStatusLabelKey(detail.status))}
        </Tag>
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
      {detail.status !== 'Closed' ? (
        <>
          <Input.TextArea
            rows={3}
            value={reply}
            onChange={(e) => onReplyChange(e.target.value)}
            placeholder={t('support.tickets.message')}
            style={{ marginTop: 16 }}
          />
          <Button
            type="primary"
            style={{ marginTop: 8 }}
            loading={replyPending}
            disabled={reply.trim().length < 1}
            onClick={onSendReply}
          >
            {t('support.tickets.sendReply')}
          </Button>
        </>
      ) : null}
    </Card>
  );
}
