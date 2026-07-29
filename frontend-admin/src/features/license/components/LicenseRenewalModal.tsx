'use client';

import { LockOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { Flex, Modal, Typography } from 'antd';

import { LicenseRenewalFlow } from '@/features/license/components/LicenseRenewalFlow';
import { useLicenseRenewalFunnelPageView } from '@/features/license/hooks/useLicenseRenewalFunnelPageView';
import {
  getRenewalModalStatusSummary,
  renewalModalIconColor,
} from '@/features/license/utils/renewalModalStatusSummary';
import type { LicenseStatusView } from '@/hooks/useLicenseStatus';
import { tenantLicenseUnifiedQueryKey } from '@/hooks/useTenantLicense';
import { formatDate, useI18n } from '@/i18n';

const { Text, Title, Paragraph } = Typography;

export type LicenseRenewalModalProps = {
  open: boolean;
  tenantId: string;
  status: LicenseStatusView | null;
  onClose: () => void;
  onSuccess?: () => void;
};

/**
 * License renewal modal: state-aware status summary + stepped {@link LicenseRenewalFlow}.
 * Supports proactive renewal while Active as well as Grace / Locked / Archived.
 */
export function LicenseRenewalModal({
  open,
  tenantId: _tenantId,
  status,
  onClose,
  onSuccess,
}: LicenseRenewalModalProps) {
  const { t, formatLocale } = useI18n();
  const queryClient = useQueryClient();
  useLicenseRenewalFunnelPageView(open);

  const summary = status ? getRenewalModalStatusSummary(status) : null;
  const dateLabel = status?.expiredAt ? formatDate(status.expiredAt, formatLocale) : '—';
  const statusTextType =
    summary?.tone === 'danger' ? 'danger' : summary?.tone === 'success' ? 'success' : undefined;

  return (
    <Modal
      title={t('license.renewalModal.title')}
      open={open}
      onCancel={onClose}
      footer={null}
      destroyOnHidden
      width={560}
    >
      <Flex vertical gap={16}>
        <Flex vertical align="center" gap={8} style={{ textAlign: 'center' }}>
          <LockOutlined
            style={{
              fontSize: 40,
              color: summary ? renewalModalIconColor(summary.tone) : '#faad14',
            }}
          />
          <Title level={4} style={{ margin: 0 }}>
            {t(summary?.headingKey ?? 'license.renewalModal.heading')}
          </Title>
          <Paragraph type="secondary" style={{ margin: 0 }}>
            {t(summary?.descriptionKey ?? 'license.renewalModal.description')}
          </Paragraph>
        </Flex>

        {status && summary ? (
          <div
            style={{
              width: '100%',
              textAlign: 'left',
              padding: 12,
              borderRadius: 8,
              background: 'var(--ant-color-fill-quaternary, rgba(0,0,0,0.04))',
            }}
          >
            <Flex justify="space-between">
              <Text type="secondary">{t('license.renewalModal.statusLabel')}</Text>
              <Text strong type={statusTextType}>
                {t(summary.statusValueKey)}
              </Text>
            </Flex>
            <Flex justify="space-between" style={{ marginTop: 8 }}>
              <Text type="secondary">{t(summary.dateLabelKey)}</Text>
              <Text>{dateLabel}</Text>
            </Flex>
            <Flex justify="space-between" style={{ marginTop: 8 }}>
              <Text type="secondary">{t(summary.daysLabelKey)}</Text>
              <Text strong type={summary.daysDanger ? 'danger' : undefined}>
                {summary.daysValue}
              </Text>
            </Flex>
          </div>
        ) : null}

        <LicenseRenewalFlow
          compact
          redirectAfterSuccess
          onSuccess={() => {
            void queryClient.invalidateQueries({ queryKey: tenantLicenseUnifiedQueryKey });
            onSuccess?.();
          }}
          onLeaveAfterSuccess={onClose}
        />
      </Flex>
    </Modal>
  );
}
