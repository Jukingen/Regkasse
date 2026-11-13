'use client';

import { SendOutlined } from '@ant-design/icons';
import { Alert, Button, Descriptions, Space, Tag, Typography } from 'antd';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { postFiskalyTagesabschluss } from '@/features/fiskaly/api/fiskalyReceipts';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

import {
  fiskalyStatusColor,
  readTagesabschlussFiskalyFields,
  type TagesabschlussFiskalyFields,
} from './fiskalyFields';

const { Text } = Typography;

function labelFiskalyStatus(t: (key: string) => string, status: string): string {
  switch (status.toLowerCase()) {
    case 'submitted':
      return t('tagesabschluss.fiskaly.status.Submitted');
    case 'failed':
      return t('tagesabschluss.fiskaly.status.Failed');
    case 'pending':
      return t('tagesabschluss.fiskaly.status.Pending');
    case 'skipped':
      return t('tagesabschluss.fiskaly.status.Skipped');
    default:
      return status;
  }
}

export type TagesabschlussDetailProps = {
  closingId?: string | null;
  dateLabel: string;
  typeLabel: string;
  transactionLabel: string;
  historyRow?: unknown;
};

export function TagesabschlussDetail({
  closingId,
  dateLabel,
  typeLabel,
  transactionLabel,
  historyRow,
}: TagesabschlussDetailProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const canSend = hasPermission(PERMISSIONS.FISKALY_OPERATIONS_TAGESABSCHLUSS);

  const initial = readTagesabschlussFiskalyFields(historyRow);
  const mutation = useMutation({
    mutationFn: (id: string) => postFiskalyTagesabschluss(id),
    onSuccess: async (envelope) => {
      await queryClient.invalidateQueries({ queryKey: ['/api/Tagesabschluss/history'] });
      if (envelope.success) {
        notify.success(t('tagesabschluss.fiskaly.sendSuccess'));
        return;
      }
      notify.error(envelope.error?.message || t('tagesabschluss.fiskaly.sendFailed'));
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'Tagesabschluss.fiskalySubmit',
        fallbackKey: 'tagesabschluss.fiskaly.sendFailed',
      });
    },
  });

  const live: TagesabschlussFiskalyFields = mutation.isSuccess
    ? {
        fiskalyStatus: mutation.data.success ? 'Submitted' : 'Failed',
        fiskalyReceiptId: mutation.data.data?.receiptId ?? initial.fiskalyReceiptId,
        fiskalyError: mutation.data.error?.message ?? initial.fiskalyError,
      }
    : initial;

  const submitted = live.fiskalyStatus?.toLowerCase() === 'submitted';
  const failed = live.fiskalyStatus?.toLowerCase() === 'failed';

  return (
    <Space orientation="vertical" size={12} style={{ width: '100%' }}>
      <Descriptions bordered size="small" column={1}>
        <Descriptions.Item label={t('tagesabschluss.history.colDate')}>{dateLabel}</Descriptions.Item>
        <Descriptions.Item label={t('tagesabschluss.type')}>{typeLabel}</Descriptions.Item>
        <Descriptions.Item label={t('tagesabschluss.history.colTransactions')}>
          {transactionLabel}
        </Descriptions.Item>
        <Descriptions.Item label={t('tagesabschluss.fiskaly.colStatus')}>
          {live.fiskalyStatus ? (
            <Tag color={fiskalyStatusColor(live.fiskalyStatus)} variant="filled">
              {labelFiskalyStatus(t, live.fiskalyStatus)}
            </Tag>
          ) : (
            t('tagesabschluss.fiskaly.notSent')
          )}
        </Descriptions.Item>
        <Descriptions.Item label={t('tagesabschluss.fiskaly.receiptId')}>
          {live.fiskalyReceiptId?.trim() ? (
            <Text copyable>{live.fiskalyReceiptId}</Text>
          ) : (
            '—'
          )}
        </Descriptions.Item>
      </Descriptions>
      {failed && live.fiskalyError ? (
        <Alert type="error" showIcon title={t('tagesabschluss.fiskaly.sendFailed')} description={live.fiskalyError} />
      ) : null}
      <Text type="secondary">{t('tagesabschluss.fiskaly.hint')}</Text>
      {canSend && closingId ? (
        <Button
          type="primary"
          icon={<SendOutlined />}
          loading={mutation.isPending}
          disabled={submitted}
          onClick={() => mutation.mutate(closingId)}
        >
          {t('tagesabschluss.fiskaly.send')}
        </Button>
      ) : null}
    </Space>
  );
}
