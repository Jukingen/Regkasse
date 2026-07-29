'use client';

import { Alert, Button, Flex, Input, Steps, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import { useMemo, useState } from 'react';

import { LicenseRenewalSuccessModal } from '@/features/license/components/LicenseRenewalSuccessModal';
import {
  formatLicensePreviewDurationCombined,
  formatLicensePreviewPlanName,
} from '@/features/license/utils/licensePreviewDisplay';
import { useLicenseRenewal } from '@/hooks/useLicenseRenewal';
import { formatDate, useI18n } from '@/i18n';

const { Text, Paragraph } = Typography;

export type LicenseRenewalFlowProps = {
  /** Called after successful activation (before optional dashboard navigation). */
  onSuccess?: () => void;
  /** Called when the user leaves via the success modal dashboard CTA. */
  onLeaveAfterSuccess?: () => void;
  /** When true (default), success modal navigates to dashboard. */
  redirectAfterSuccess?: boolean;
  /** Compact layout inside a modal. */
  compact?: boolean;
};

/**
 * Stepped mandant license renewal: key → confirm (preview) → activate.
 * Optional payment redirect via `NEXT_PUBLIC_LICENSE_PAYMENT_URL` / billing hub / support mail.
 */
export function LicenseRenewalFlow({
  onSuccess,
  onLeaveAfterSuccess,
  redirectAfterSuccess = true,
  compact = false,
}: LicenseRenewalFlowProps) {
  const { t, formatLocale } = useI18n();
  const router = useRouter();
  const [currentStep, setCurrentStep] = useState(0);
  const [licenseKey, setLicenseKey] = useState('');
  const [successModalOpen, setSuccessModalOpen] = useState(false);
  const [successExpiryLabel, setSuccessExpiryLabel] = useState('—');
  const [successLicenseKey, setSuccessLicenseKey] = useState('');
  const {
    preview,
    renew,
    goToPayment,
    paymentTarget,
    error,
    clearError,
    isPreviewing,
    isRenewing,
    lastPreview,
  } = useLicenseRenewal();

  const stepItems = useMemo(
    () => [
      { title: t('license.renewalFlow.steps.key') },
      { title: t('license.renewalFlow.steps.confirm') },
      { title: t('license.renewalFlow.steps.done') },
    ],
    [t]
  );

  const handleNextFromKey = async () => {
    const key = licenseKey.trim();
    if (!key) return;
    const result = await preview(key);
    if (result.success) {
      setCurrentStep(1);
    }
  };

  const handleActivate = async () => {
    const result = await renew(licenseKey);
    if (!result.success) return;

    const expiryIso = result.validUntilUtc ?? lastPreview?.validUntilUtc ?? null;
    const expiryLabel = expiryIso
      ? formatDate(expiryIso, formatLocale)
      : t('license.renewalFlow.validUntilFallback');

    setSuccessExpiryLabel(expiryLabel);
    setSuccessLicenseKey(result.licenseKey?.trim() || licenseKey.trim());
    setCurrentStep(2);
    setSuccessModalOpen(true);
    onSuccess?.();
  };

  const openDashboard = () => {
    setSuccessModalOpen(false);
    onLeaveAfterSuccess?.();
    if (redirectAfterSuccess) {
      router.replace('/dashboard');
      router.refresh();
      return;
    }
    if (typeof window !== 'undefined') {
      window.location.reload();
    }
  };

  const handlePay = () => {
    goToPayment((href) => {
      router.push(href);
    });
  };

  const validUntilLabel = lastPreview?.validUntilUtc
    ? formatDate(lastPreview.validUntilUtc, formatLocale)
    : t('license.renewalFlow.validUntilFallback');

  const durationLabel = lastPreview
    ? formatLicensePreviewDurationCombined(lastPreview.durationDays, t)
    : '—';

  const planLabel = lastPreview
    ? formatLicensePreviewPlanName(lastPreview.durationDays, t)
    : '—';

  return (
    <>
      <Flex vertical gap={compact ? 12 : 16}>
        <Steps
          current={currentStep}
          size={compact ? 'small' : 'default'}
          items={stepItems}
        />

        {error ? (
          <Alert
            type="error"
            showIcon
            title={error.message}
            description={error.details}
            closable
            onClose={clearError}
          />
        ) : null}

        {currentStep === 0 ? (
          <Flex vertical gap={12}>
            <Paragraph style={{ margin: 0 }}>
              {t('license.renewalFlow.keyIntro')}
            </Paragraph>
            <Input
              size="large"
              value={licenseKey}
              onChange={(e) => {
                clearError();
                setLicenseKey(e.target.value);
              }}
              placeholder={t('license.renewalFlow.keyPlaceholder')}
              autoComplete="off"
              onPressEnter={(e) => {
                e.preventDefault();
                void handleNextFromKey();
              }}
            />
            <Text type="secondary" style={{ fontSize: 12 }}>
              {t('license.renewalFlow.keyFormatHint')}
            </Text>
            <Flex justify="space-between" wrap="wrap" gap={8}>
              <Button onClick={handlePay}>
                {paymentTarget.kind === 'external'
                  ? t('license.renewalFlow.payExternal')
                  : paymentTarget.kind === 'mailto'
                    ? t('license.renewalFlow.contactSupport')
                    : t('license.renewalFlow.openBilling')}
              </Button>
              <Button
                type="primary"
                loading={isPreviewing}
                disabled={!licenseKey.trim()}
                onClick={() => void handleNextFromKey()}
              >
                {t('license.renewalFlow.next')}
              </Button>
            </Flex>
          </Flex>
        ) : null}

        {currentStep === 1 ? (
          <Flex vertical gap={12}>
            <Alert
              type="info"
              showIcon
              title={t('license.renewalFlow.confirmTitle')}
              description={t('license.renewalFlow.confirmDescription')}
            />
            <div
              style={{
                padding: 16,
                borderRadius: 8,
                background: 'var(--ant-color-fill-quaternary, rgba(0,0,0,0.04))',
              }}
            >
              <Flex justify="space-between" gap={8} wrap="wrap">
                <Text type="secondary">{t('license.renewalFlow.licenseKeyLabel')}</Text>
                <Text code style={{ wordBreak: 'break-all' }}>
                  {licenseKey.trim()}
                </Text>
              </Flex>
              <Flex justify="space-between" style={{ marginTop: 8 }} wrap="wrap" gap={8}>
                <Text type="secondary">{t('license.renewalFlow.planLabel')}</Text>
                <Text strong>{planLabel}</Text>
              </Flex>
              <Flex justify="space-between" style={{ marginTop: 8 }} wrap="wrap" gap={8}>
                <Text type="secondary">{t('license.renewalFlow.durationLabel')}</Text>
                <Text strong>{durationLabel}</Text>
              </Flex>
              <Flex justify="space-between" style={{ marginTop: 8 }} wrap="wrap" gap={8}>
                <Text type="secondary">{t('license.renewalFlow.validUntilLabel')}</Text>
                <Text strong style={{ color: 'var(--ant-color-success, #52c41a)' }}>
                  {validUntilLabel}
                </Text>
              </Flex>
            </div>
            <Flex justify="space-between" wrap="wrap" gap={8}>
              <Button
                disabled={isRenewing}
                onClick={() => {
                  clearError();
                  setCurrentStep(0);
                }}
              >
                {t('license.renewalFlow.back')}
              </Button>
              <Button type="primary" loading={isRenewing} onClick={() => void handleActivate()}>
                {t('license.renewalFlow.activate')}
              </Button>
            </Flex>
          </Flex>
        ) : null}

        {currentStep === 2 ? (
          <Alert
            type="success"
            showIcon
            title={t('license.renewalFlow.successTitle')}
            description={
              redirectAfterSuccess
                ? t('license.renewalFlow.successRedirect')
                : t('license.renewalFlow.successStay')
            }
          />
        ) : null}

        {currentStep < 2 ? (
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('license.renewalModal.supportContact')}
          </Text>
        ) : null}
      </Flex>

      <LicenseRenewalSuccessModal
        open={successModalOpen}
        newExpiryDateLabel={successExpiryLabel}
        licenseKey={successLicenseKey}
        onOpenDashboard={openDashboard}
      />
    </>
  );
}
