import type { LicenseLifecycleUiState, LicenseStatusView } from '@/hooks/useLicenseStatus';

export type RenewalModalStatusTone = 'success' | 'warning' | 'danger';

export type RenewalModalStatusSummary = {
  state: LicenseLifecycleUiState;
  tone: RenewalModalStatusTone;
  /** i18n key for the main heading */
  headingKey: string;
  /** i18n key for the supporting description */
  descriptionKey: string;
  /** i18n key for the status value (Active / Grace / Locked / Archived) */
  statusValueKey: string;
  /** i18n key for the date row label (valid until vs expired since) */
  dateLabelKey: string;
  /** i18n key for the days row label */
  daysLabelKey: string;
  /** Value shown in the days row */
  daysValue: number;
  /** When true, days value uses danger styling */
  daysDanger: boolean;
};

/**
 * Builds copy/metrics for the license renewal modal status panel.
 * Proactive renewal (Active) must not be presented as locked/expired.
 */
export function getRenewalModalStatusSummary(
  status: LicenseStatusView
): RenewalModalStatusSummary {
  switch (status.state) {
    case 'Active':
      return {
        state: 'Active',
        tone: 'success',
        headingKey: 'license.renewalModal.headingActive',
        descriptionKey: 'license.renewalModal.descriptionActive',
        statusValueKey: 'license.renewalModal.statusActive',
        dateLabelKey: 'license.renewalModal.validUntilLabel',
        daysLabelKey: 'license.renewalModal.daysRemainingLabel',
        daysValue: status.daysUntilExpiry,
        daysDanger: false,
      };
    case 'Grace':
      return {
        state: 'Grace',
        tone: 'warning',
        headingKey: 'license.renewalModal.headingGrace',
        descriptionKey: 'license.renewalModal.descriptionGrace',
        statusValueKey: 'license.renewalModal.statusGrace',
        dateLabelKey: 'license.renewalModal.expiredAtLabel',
        daysLabelKey: 'license.renewalModal.graceDaysRemainingLabel',
        daysValue: status.graceDaysRemaining,
        daysDanger: false,
      };
    case 'Archived':
      return {
        state: 'Archived',
        tone: 'danger',
        headingKey: 'license.renewalModal.heading',
        descriptionKey: 'license.renewalModal.description',
        statusValueKey: 'license.renewalModal.statusArchived',
        dateLabelKey: 'license.renewalModal.expiredAtLabel',
        daysLabelKey: 'license.renewalModal.daysOverdueLabel',
        daysValue: status.daysOverdue,
        daysDanger: true,
      };
    case 'Locked':
    default:
      return {
        state: 'Locked',
        tone: 'danger',
        headingKey: 'license.renewalModal.heading',
        descriptionKey: 'license.renewalModal.description',
        statusValueKey: 'license.renewalModal.statusLocked',
        dateLabelKey: 'license.renewalModal.expiredAtLabel',
        daysLabelKey: 'license.renewalModal.daysOverdueLabel',
        daysValue: status.daysOverdue,
        daysDanger: true,
      };
  }
}

export function renewalModalIconColor(tone: RenewalModalStatusTone): string {
  switch (tone) {
    case 'success':
      return '#52c41a';
    case 'warning':
      return '#faad14';
    case 'danger':
    default:
      return '#ff4d4f';
  }
}
