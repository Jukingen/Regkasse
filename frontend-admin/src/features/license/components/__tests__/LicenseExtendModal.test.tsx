import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { LicenseExtendModal } from '@/features/license/components/LicenseExtendModal';

import {
  EXPIRED_UNTIL,
  EXPIRED_UNTIL_DISPLAY,
  EXTENDED_UNTIL,
  EXTENDED_UNTIL_DISPLAY,
  interpolateT,
  resolvedLicense,
} from './licenseUiTestFixtures';

const previewMutateAsync = vi.hoisted(() => vi.fn());
const extendMutateAsync = vi.hoisted(() => vi.fn());

vi.mock('@/features/license/hooks/useLicensePreview', () => ({
  useLicensePreview: () => ({
    mutateAsync: previewMutateAsync,
    isPending: false,
  }),
}));

vi.mock('@/features/license/hooks/useExtendTenantLicense', () => ({
  useExtendTenantLicense: () => ({
    mutateAsync: extendMutateAsync,
    isPending: false,
  }),
}));

vi.mock('@/components/Skeleton', () => ({
  FormSkeleton: () => null,
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    formatLocale: 'de-DE',
    t: interpolateT({
      'license.extendModal.title': 'Mandantenlizenz verlängern',
      'license.extendModal.currentStatus': 'Aktueller Status',
      'license.extendModal.statusLabel': 'Status',
      'license.extendModal.validUntilLabel': 'Gültig bis',
      'license.extendModal.licenseKeyLabel': 'Neuer Lizenzschlüssel',
      'license.extendModal.licenseKeyPlaceholder': 'REGK-yyyyMMdd-slug-XXXXXXXX',
      'license.extendModal.infoText':
        'Einheitliches Format REGK-yyyyMMdd-slug-XXXXXXXX. Die Gültigkeitsdauer steht im Schlüssel.',
      'license.extendModal.confirmButton': 'Lizenz verlängern',
      'license.extendModal.previewButton': 'Vorschau',
      'license.extendModal.previewTitle': 'Lizenzinformationen',
      'license.extendModal.previewValidFrom': 'Gültig ab',
      'license.extendModal.previewValidUntil': 'Gültig bis',
      'license.extendModal.previewDuration': 'Laufzeit',
      'license.extendModal.previewPlan': 'Tarif',
      'license.extendModal.previewStatus': 'Status',
      'license.extendModal.previewStatusValid': 'Gültig',
      'license.extendModal.previewStatusInvalid': 'Ungültig',
      'license.extendModal.previewStatusExpired': 'Abgelaufen',
      'license.extendModal.previewConfirmMessage': 'Bitte bestätigen Sie die Verlängerung.',
      'license.extendModal.previewDurationCombined': '{{days}} Tage ({{period}})',
      'license.extendModal.previewDurationAnnual': '1 Jahr',
      'license.extendModal.previewPlanAnnual': 'Jahreslizenz',
      'license.extendModal.noLicenseKey': 'Bitte geben Sie einen Lizenzschlüssel ein.',
      'license.extendModal.success': 'Lizenz wurde erfolgreich verlängert',
      'license.extendModal.successDetails': 'Neue Lizenz gültig bis {{date}}',
      'license.phase.labels.lockdown': 'Lockdown',
      'license.phase.labels.active': 'Aktiv',
      'license.mandant.licenseKey': 'Lizenzschlüssel',
      'common.buttons.cancel': 'Abbrechen',
      'common.buttons.close': 'Schließen',
    }),
  }),
}));

const NEW_KEY = 'REGK-20271231-dev-ABCD1234';

describe('LicenseExtendModal', () => {
  beforeEach(() => {
    previewMutateAsync.mockReset();
    extendMutateAsync.mockReset();
  });

  it('shows success with the new valid-until date and notifies the parent', async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();

    previewMutateAsync.mockResolvedValue({
      valid: true,
      status: 'valid',
      licenseKey: NEW_KEY,
      validFromUtc: '2026-08-14T00:00:00.000Z',
      validUntilUtc: EXTENDED_UNTIL,
      durationDays: 365,
    });
    extendMutateAsync.mockResolvedValue({
      success: true,
      licenseKey: NEW_KEY,
      validUntilUtc: EXTENDED_UNTIL,
      status: 'active',
      message: 'ok',
    });

    render(
      <LicenseExtendModal
        open
        tenantId="tenant-1"
        status={{
          kind: 'lockdown',
          features: [],
          validUntilUtc: EXPIRED_UNTIL,
        }}
        resolvedStatus={resolvedLicense('lockdown')}
        onClose={() => undefined}
        onSuccess={onSuccess}
      />
    );

    expect(screen.getByText('Lockdown')).toBeInTheDocument();
    expect(screen.getByText(EXPIRED_UNTIL_DISPLAY)).toBeInTheDocument();

    await user.type(
      screen.getByPlaceholderText('REGK-yyyyMMdd-slug-XXXXXXXX'),
      NEW_KEY
    );
    await user.click(screen.getByRole('button', { name: 'Vorschau' }));

    await waitFor(() => {
      expect(previewMutateAsync).toHaveBeenCalled();
    });
    expect(await screen.findByText('Bitte bestätigen Sie die Verlängerung.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Lizenz verlängern' }));

    expect(await screen.findByText('Lizenz wurde erfolgreich verlängert')).toBeInTheDocument();
    expect(screen.getByText(`Neue Lizenz gültig bis ${EXTENDED_UNTIL_DISPLAY}`)).toBeInTheDocument();
    expect(screen.getAllByText(EXTENDED_UNTIL_DISPLAY).length).toBeGreaterThan(0);
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(extendMutateAsync).toHaveBeenCalledWith({
      licenseKey: NEW_KEY,
      expectedValidUntilUtc: EXTENDED_UNTIL,
    });
  });
});
