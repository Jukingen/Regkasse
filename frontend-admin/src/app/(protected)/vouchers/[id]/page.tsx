'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useState } from 'react';
import { Modal, Alert, Button, Card, Descriptions, Form, Input, Space, Tag, Typography } from 'antd';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { CardSkeleton } from '@/components/Skeleton';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import {
  useAdminVoucherDetail,
  useCancelAdminVoucher,
  useVerifyAdminVoucherCode,
} from '@/api/admin/vouchers';
import { formatCurrency, formatDateTime } from '@/i18n/formatting';
import { VoucherHistory } from '@/features/vouchers/components/VoucherHistory';

function shortId(value: string): string {
  if (!value) return '—';
  return value.length > 14 ? `${value.slice(0, 8)}…` : value;
}

function formatCreatorLabel(input: {
  createdByUserId?: string | null;
  createdByDisplayName?: string | null;
  createdByEmail?: string | null;
  createdByRoles?: string[] | null;
}): string {
  const parts = [input.createdByDisplayName?.trim(), input.createdByEmail?.trim()].filter(
    (x): x is string => !!x
  );
  const roleText = (input.createdByRoles ?? []).filter(Boolean).join(', ');
  if (parts.length > 0 && roleText) return `${parts.join(' · ')} (${roleText})`;
  if (parts.length > 0) return parts.join(' · ');
  if (roleText) {
    const uid = input.createdByUserId?.trim();
    const short = uid && uid.length > 14 ? `${uid.slice(0, 8)}…` : uid || '—';
    return `${short} (${roleText})`;
  }
  const uid = input.createdByUserId?.trim();
  if (!uid) return '—';
  return uid.length > 14 ? `${uid.slice(0, 8)}…` : uid;
}

function statusColor(status: string): string {
  switch (status) {
    case 'Active':
      return 'green';
    case 'PartiallyRedeemed':
      return 'blue';
    case 'Redeemed':
      return 'default';
    case 'Cancelled':
      return 'red';
    case 'Expired':
      return 'orange';
    default:
      return 'default';
  }
}

export default function AdminVoucherDetailPage() {
  const params = useParams();
  const id = typeof params?.id === 'string' ? params.id : '';
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canRead = hasPermission(PERMISSIONS.VOUCHER_READ);

  if (!canRead) {
    return (
      <AdminPageShell>
        <Alert type="error" title={t('vouchers.list.permissionDenied')} showIcon />
      </AdminPageShell>
    );
  }

  if (!id) {
    return (
      <AdminPageShell>
        <Alert type="error" title={t('vouchers.errors.loadFailed')} showIcon />
      </AdminPageShell>
    );
  }

  return <AdminVoucherDetailContent id={id} />;
}

function AdminVoucherDetailContent({ id }: { id: string }) {
  const { message } = useAntdApp();
  const { t, formatLocale } = useI18n();
  const { hasPermission } = usePermissions();
  const canRead = hasPermission(PERMISSIONS.VOUCHER_READ);
  const canAudit = hasPermission(PERMISSIONS.VOUCHER_AUDIT_VIEW);
  const canCancel = hasPermission(PERMISSIONS.VOUCHER_CANCEL);

  const detailQuery = useAdminVoucherDetail(id, { enabled: canRead && !!id });
  const cancelMutation = useCancelAdminVoucher();
  const verifyCodeMutation = useVerifyAdminVoucherCode();
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelForm] = Form.useForm();
  const [verifyCodeInput, setVerifyCodeInput] = useState('');

  const d = detailQuery.data;

  const statusLabel = (s: string) => {
    const key = `vouchers.status.${s}`;
    const lbl = t(key);
    return lbl === key ? s : lbl;
  };

  const showCancel =
    canCancel &&
    d &&
    d.remainingAmount > 0 &&
    d.status !== 'Cancelled' &&
    d.status !== 'Redeemed';

  const submitCancel = async () => {
    const values = await cancelForm.validateFields();
    await cancelMutation.mutateAsync({ id, reason: values.reason as string });
    void message.success(t('vouchers.cancel.success'));
    setCancelOpen(false);
    cancelForm.resetFields();
    await detailQuery.refetch();
  };

  const submitVerifyCode = () => {
    const code = verifyCodeInput.trim();
    if (!code) {
      void message.warning(t('vouchers.detail.verifyCodeEmpty'));
      return;
    }
    verifyCodeMutation
      .mutateAsync({ id, code })
      .then((r) => {
        setVerifyCodeInput('');
        if (r.matches) void message.success(t('vouchers.detail.verifyCodeMatch'));
        else void message.error(t('vouchers.detail.verifyCodeNoMatch'));
      })
      .catch(() => {
        void message.error(t('vouchers.errors.verifyFailed'));
      });
  };

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('vouchers.detail.heading')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('vouchers.title'), href: '/vouchers' },
          { title: d?.maskedCode ?? shortId(id) },
        ]}
        actions={
          <Space>
            <Link href="/vouchers">
              <Button>{t('vouchers.detail.back')}</Button>
            </Link>
            {showCancel ? (
              <Button danger onClick={() => setCancelOpen(true)}>
                {t('vouchers.detail.cancel')}
              </Button>
            ) : null}
          </Space>
        }
      />

      {detailQuery.isLoading ? (
        <CardSkeleton count={2} />
      ) : detailQuery.isError || !d ? (
        <Alert type="error" title={t('vouchers.errors.loadFailed')} showIcon />
      ) : (
        <>
          <Card style={{ marginBottom: 16 }}>
            <Space style={{ marginBottom: 12 }}>
              <Typography.Text type="secondary">{d.maskedCode}</Typography.Text>
              <Tag color={statusColor(d.status)}>{statusLabel(d.status)}</Tag>
            </Space>
            <Descriptions bordered column={{ xs: 1, sm: 2, md: 2 }} size="small">
              <Descriptions.Item label={t('vouchers.list.columns.initialAmount')}>
                {formatCurrency(d.initialAmount, formatLocale, { currency: d.currency || 'EUR' })}
              </Descriptions.Item>
              <Descriptions.Item label={t('vouchers.list.columns.remainingAmount')}>
                {formatCurrency(d.remainingAmount, formatLocale, { currency: d.currency || 'EUR' })}
              </Descriptions.Item>
              <Descriptions.Item label={t('vouchers.list.columns.validFrom')}>
                {formatDateTime(d.validFromUtc, formatLocale)}
              </Descriptions.Item>
              <Descriptions.Item label={t('vouchers.list.columns.expiresAt')}>
                {formatDateTime(d.expiresAtUtc, formatLocale)}
              </Descriptions.Item>
              <Descriptions.Item label={t('vouchers.list.columns.createdBy')}>
                {formatCreatorLabel(d)}
              </Descriptions.Item>
              <Descriptions.Item label={t('vouchers.list.columns.createdAt')}>
                {formatDateTime(d.createdAtUtc, formatLocale)}
              </Descriptions.Item>
              {d.internalNote ? (
                <Descriptions.Item label={t('vouchers.detail.internalNote')} span={2}>
                  {d.internalNote}
                </Descriptions.Item>
              ) : null}
              {d.cancelledAtUtc ? (
                <Descriptions.Item label={t('vouchers.detail.cancelledAt')}>
                  {formatDateTime(d.cancelledAtUtc, formatLocale)}
                </Descriptions.Item>
              ) : null}
              {d.cancellationReason ? (
                <Descriptions.Item label={t('vouchers.detail.cancellationReason')} span={2}>
                  {d.cancellationReason}
                </Descriptions.Item>
              ) : null}
            </Descriptions>
          </Card>

          <Card title={t('vouchers.detail.verifyCodeTitle')} style={{ marginBottom: 16 }}>
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 16 }}
              title={t('vouchers.detail.verifyCodeHint')}
              description={t('vouchers.detail.codePrivacyNotice')}
            />
            <Space orientation="vertical" style={{ width: '100%' }} size="middle">
              <Space.Compact style={{ width: '100%', maxWidth: 520 }}>
                <Input.Password
                  autoComplete="off"
                  placeholder={t('vouchers.detail.verifyCodePlaceholder')}
                  value={verifyCodeInput}
                  onChange={(e) => setVerifyCodeInput(e.target.value)}
                  onPressEnter={() => submitVerifyCode()}
                />
                <Button
                  type="primary"
                  loading={verifyCodeMutation.isPending}
                  onClick={() => submitVerifyCode()}
                >
                  {t('vouchers.detail.verifyCodeSubmit')}
                </Button>
              </Space.Compact>
            </Space>
          </Card>

          <Card title={t('vouchers.ledger.title')}>
            <VoucherHistory
              voucherId={id}
              ledgerEnabled={canRead && canAudit}
              currency={d.currency || 'EUR'}
            />
          </Card>
        </>
      )}

      <Modal
        open={cancelOpen}
        forceRender
        title={t('vouchers.cancel.title')}
        onCancel={() => {
          setCancelOpen(false);
          cancelForm.resetFields();
        }}
        onOk={() =>
          submitCancel().catch(() => {
            void message.error(t('vouchers.errors.cancelFailed'));
            return Promise.reject();
          })
        }
        confirmLoading={cancelMutation.isPending}
        okText={t('vouchers.cancel.confirm')}
        okButtonProps={{ danger: true }}
      >
        <Form form={cancelForm} layout="vertical">
          <Form.Item
            name="reason"
            label={t('vouchers.cancel.reasonLabel')}
            rules={[{ required: true, min: 5, message: t('vouchers.cancel.reasonPlaceholder') }]}
          >
            <Input.TextArea rows={4} maxLength={500} showCount placeholder={t('vouchers.cancel.reasonPlaceholder')} />
          </Form.Item>
        </Form>
      </Modal>
    </AdminPageShell>
  );
}
