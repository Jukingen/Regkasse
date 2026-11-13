'use client';

import { Alert, Button, Form, Input, Modal, Space } from 'antd';
import { useMemo, useState } from 'react';

import { useGetApiAdminTenantsTenantIdChQrGapAcceptance } from '@/api/generated/admin/admin';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCompanySettings } from '@/features/settings/hooks/useCompanySettings';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

import {
  confirmChQrBankUpload,
  downloadChQrInvoicePdf,
} from '../api/chQrRechnungOperatorApi';

export type ChQrRechnungOperatorActionsProps = {
  invoiceId: string;
};

export function ChQrRechnungOperatorActions({ invoiceId }: ChQrRechnungOperatorActionsProps) {
  const { t } = useI18n();
  const notify = useNotify();
  const { user } = useAuth();
  const { tenant } = useTenant();
  const settings = useCompanySettings();
  const country = settings.data?.country;
  const tenantId = tenant?.id ?? '';
  const allowed =
    country === 'CH' &&
    isSuperAdmin(user?.role) &&
    hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL) &&
    tenantId.length > 0;
  const gapsQuery = useGetApiAdminTenantsTenantIdChQrGapAcceptance(tenantId, {
    query: { enabled: allowed, retry: false },
  });
  const [downloaded, setDownloaded] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [bankReference, setBankReference] = useState('');
  const [saving, setSaving] = useState(false);

  const outstanding = useMemo(() => {
    const known = gapsQuery.data?.knownGaps ?? [];
    const accepted = new Set(gapsQuery.data?.acceptance?.acceptedGaps ?? []);
    return known
      .filter((gap) => gap.present !== true && gap.id && !accepted.has(gap.id))
      .map((gap) => gap.id as string);
  }, [gapsQuery.data]);

  if (!allowed) {
    return null;
  }

  const gapsAccepted = gapsQuery.isSuccess && outstanding.length === 0;

  const download = async () => {
    try {
      const blob = await downloadChQrInvoicePdf(tenantId, invoiceId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `qr-rechnung-${invoiceId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
      setDownloaded(true);
      notify.success(t('invoices.chQr.downloaded'));
    } catch {
      notify.error(t('invoices.chQr.downloadFailed'));
    }
  };

  const confirm = async () => {
    const reference = bankReference.trim();
    if (!reference) {
      notify.error(t('invoices.chQr.bankReferenceRequired'));
      return;
    }
    setSaving(true);
    try {
      await confirmChQrBankUpload(tenantId, invoiceId, {
        uploadedBy: user?.userName || user?.email || 'super-admin',
        uploadedAtUtc: new Date().toISOString(),
        bankReference: reference,
      });
      setModalOpen(false);
      setBankReference('');
      notify.success(t('invoices.chQr.uploaded'));
    } catch {
      notify.error(t('invoices.chQr.uploadFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Space orientation="vertical" size="small" style={{ width: '100%', marginBottom: 12 }}>
      {outstanding.length > 0 ? (
        <Alert
          type="warning"
          showIcon
          data-testid="ch-qr-operator-outstanding"
          title={t('invoices.chQr.outstandingWarning')}
          description={outstanding.join(', ')}
        />
      ) : null}
      {gapsAccepted ? (
        <Space wrap>
          <Button data-testid="ch-qr-operator-download" onClick={() => void download()}>
            {t('invoices.chQr.download')}
          </Button>
          <Button
            data-testid="ch-qr-operator-upload"
            disabled={!downloaded}
            onClick={() => setModalOpen(true)}
          >
            {t('invoices.chQr.markUploaded')}
          </Button>
        </Space>
      ) : null}
      <Modal
        open={modalOpen}
        title={t('invoices.chQr.modalTitle')}
        okText={t('invoices.chQr.confirm')}
        cancelText={t('invoices.chQr.cancel')}
        confirmLoading={saving}
        onCancel={() => setModalOpen(false)}
        onOk={() => void confirm()}
      >
        <Form layout="vertical">
          <Form.Item label={t('invoices.chQr.bankReference')} required>
            <Input
              data-testid="ch-qr-operator-bank-reference"
              value={bankReference}
              onChange={(event) => setBankReference(event.target.value)}
            />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
