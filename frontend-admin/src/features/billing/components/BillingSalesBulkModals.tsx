'use client';

import { Alert, Form, Input, Modal, Progress, Typography } from 'antd';

import type {
  BulkRunProgress,
  LicenseSalesBulkActionKind,
} from '@/features/billing/utils/billingSalesBulk';
import { useI18n } from '@/i18n';

export type BillingSalesBulkConfirmModalProps = {
  open: boolean;
  action: LicenseSalesBulkActionKind | null;
  selectedCount: number;
  eligibleCount: number;
  loading?: boolean;
  onCancel: () => void;
  onConfirm: (reason?: string) => void;
};

function confirmTitleKey(action: LicenseSalesBulkActionKind): string {
  switch (action) {
    case 'extend30':
      return 'billing.licenseSales.bulk.confirm.extend30Title';
    case 'extend90':
      return 'billing.licenseSales.bulk.confirm.extend90Title';
    case 'extend365':
      return 'billing.licenseSales.bulk.confirm.extend365Title';
    case 'revoke':
      return 'billing.licenseSales.bulk.confirm.revokeTitle';
    default:
      return 'billing.licenseSales.bulk.confirm.title';
  }
}

function confirmMessageKey(action: LicenseSalesBulkActionKind): string {
  switch (action) {
    case 'extend30':
    case 'extend90':
    case 'extend365':
      return 'billing.licenseSales.bulk.confirm.extendMessage';
    case 'revoke':
      return 'billing.licenseSales.bulk.confirm.revokeMessage';
    default:
      return 'billing.licenseSales.bulk.confirm.message';
  }
}

export function BillingSalesBulkConfirmModal({
  open,
  action,
  selectedCount,
  eligibleCount,
  loading,
  onCancel,
  onConfirm,
}: BillingSalesBulkConfirmModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<{ reason?: string }>();
  const isRevoke = action === 'revoke';

  if (!open || !action || action === 'exportCsv') {
    return null;
  }

  const handleOk = async () => {
    if (isRevoke) {
      const values = await form.validateFields();
      onConfirm(values.reason?.trim());
      return;
    }
    onConfirm();
  };

  return (
    <Modal
      open={open}
      title={t(confirmTitleKey(action))}
      onCancel={onCancel}
      onOk={() => void handleOk()}
      confirmLoading={loading}
      okText={t('billing.licenseSales.bulk.confirm.ok')}
      cancelText={t('common.buttons.cancel')}
      okButtonProps={{ danger: isRevoke, disabled: eligibleCount <= 0 }}
      destroyOnHidden
    >
      {eligibleCount <= 0 ? (
        <Alert
          type="warning"
          showIcon
          title={t('billing.licenseSales.bulk.confirm.noneEligible')}
          style={{ marginBottom: 12 }}
        />
      ) : null}
      <Typography.Paragraph>
        {t(confirmMessageKey(action), {
          count: eligibleCount,
          selected: selectedCount,
        })}
      </Typography.Paragraph>
      {isRevoke ? (
        <Form form={form} layout="vertical" preserve={false}>
          <Form.Item
            name="reason"
            label={t('billing.licenseSales.bulk.confirm.revokeReasonLabel')}
            initialValue={t('billing.licenseSales.bulk.confirm.revokeDefaultReason')}
            rules={[
              {
                required: true,
                message: t('billing.licenseSales.bulk.confirm.revokeReasonRequired'),
              },
              {
                min: 10,
                message: t('billing.licenseSales.bulk.confirm.revokeReasonRequired'),
              },
            ]}
          >
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      ) : null}
    </Modal>
  );
}

export type BillingSalesBulkProgressModalProps = {
  open: boolean;
  progress: BulkRunProgress | null;
};

export function BillingSalesBulkProgressModal({
  open,
  progress,
}: BillingSalesBulkProgressModalProps) {
  const { t } = useI18n();
  if (!open || !progress) return null;

  const percent =
    progress.total > 0 ? Math.round((progress.current / progress.total) * 100) : 0;

  return (
    <Modal
      open={open}
      title={t('billing.licenseSales.bulk.progress.title')}
      footer={null}
      closable={false}
      maskClosable={false}
      destroyOnHidden
    >
      <Typography.Paragraph>
        {t('billing.licenseSales.bulk.progress.status', {
          current: progress.current,
          total: progress.total,
          label: progress.label,
        })}
      </Typography.Paragraph>
      <Progress percent={percent} status="active" />
    </Modal>
  );
}
