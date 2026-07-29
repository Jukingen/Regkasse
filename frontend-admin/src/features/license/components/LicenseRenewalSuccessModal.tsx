'use client';

import { CheckCircleOutlined } from '@ant-design/icons';
import { Button, Flex, Modal, Typography } from 'antd';

import { useI18n } from '@/i18n';

const { Text, Title, Paragraph } = Typography;

export type LicenseRenewalSuccessModalProps = {
  open: boolean;
  newExpiryDateLabel: string;
  licenseKey: string;
  onOpenDashboard: () => void;
};

/**
 * Post-renewal confirmation modal (blocking). Email is sent server-side after extend.
 */
export function LicenseRenewalSuccessModal({
  open,
  newExpiryDateLabel,
  licenseKey,
  onOpenDashboard,
}: LicenseRenewalSuccessModalProps) {
  const { t } = useI18n();

  return (
    <Modal
      title={t('license.renewalSuccessModal.title')}
      open={open}
      closable={false}
      mask={{ closable: false }}
      keyboard={false}
      footer={null}
      destroyOnHidden
      centered
      width={480}
    >
      <Flex vertical align="center" gap={16} style={{ textAlign: 'center', paddingBlock: 8 }}>
        <CheckCircleOutlined style={{ fontSize: 56, color: '#52c41a' }} aria-hidden />
        <Title level={4} style={{ margin: 0 }}>
          {t('license.renewalSuccessModal.heading')}
        </Title>
        <Paragraph type="secondary" style={{ margin: 0 }}>
          {t('license.renewalSuccessModal.description', { date: newExpiryDateLabel })}
        </Paragraph>

        <div
          style={{
            width: '100%',
            padding: 16,
            borderRadius: 8,
            background: 'var(--ant-color-fill-quaternary, rgba(0,0,0,0.04))',
            textAlign: 'left',
          }}
        >
          <Flex justify="space-between" gap={12} wrap="wrap">
            <Text type="secondary">{t('license.renewalSuccessModal.validUntilLabel')}</Text>
            <Text strong>{newExpiryDateLabel}</Text>
          </Flex>
          <Flex justify="space-between" gap={12} wrap="wrap" style={{ marginTop: 8 }}>
            <Text type="secondary">{t('license.renewalSuccessModal.licenseKeyLabel')}</Text>
            <Text code style={{ wordBreak: 'break-all' }}>
              {licenseKey}
            </Text>
          </Flex>
        </div>

        <Button type="primary" size="large" block onClick={onOpenDashboard}>
          {t('license.renewalSuccessModal.openDashboard')}
        </Button>
      </Flex>
    </Modal>
  );
}
