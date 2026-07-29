export type LicenseActivityFeedType =
  | 'renewal'
  | 'reminder'
  | 'expiry'
  | 'view'
  | 'other';

export type LicenseActivityFeedItem = {
  type: LicenseActivityFeedType;
  /** i18n key under license.activityLog.descriptions.* */
  descriptionKey: string;
  descriptionParams: Record<string, string>;
  timestampUtc: string;
};

export type LicenseActivityFeedInput = {
  action?: string | null;
  sourceCode?: string | null;
  licenseKeyMasked?: string | null;
  timestampUtc: string;
};

function normalizeToken(raw: string | null | undefined): string {
  return (raw ?? '').trim().toLowerCase().replace(/[\s-]+/g, '_');
}

/**
 * Maps dashboard / recent-activity action codes (activate | ACTIVATED | extend | …)
 * into a feed type + i18n description key.
 */
export function mapLicenseActivityFeedItem(
  input: LicenseActivityFeedInput
): LicenseActivityFeedItem {
  const action = normalizeToken(input.action);
  const source = normalizeToken(input.sourceCode);
  const token = action || source;
  const key = input.licenseKeyMasked?.trim() || '—';

  const type = resolveFeedType(token);
  const descriptionKey = resolveDescriptionKey(token, type);

  return {
    type,
    descriptionKey,
    descriptionParams: { key },
    timestampUtc: input.timestampUtc,
  };
}

function resolveFeedType(token: string): LicenseActivityFeedType {
  if (
    token === 'activate' ||
    token === 'activated' ||
    token === 'generate' ||
    token === 'generated' ||
    token === 'extend' ||
    token === 'extended' ||
    token === 'renew' ||
    token === 'renewed' ||
    token === 'force_deactivate' ||
    token === 'force_deactivated'
  ) {
    return 'renewal';
  }

  if (
    token === 'reminder' ||
    token === 'license_reminder' ||
    token === 'license_reminder_sent' ||
    token.includes('reminder')
  ) {
    return 'reminder';
  }

  if (
    token === 'revoke' ||
    token === 'revoked' ||
    token === 'cancel' ||
    token === 'cancelled' ||
    token === 'canceled' ||
    token === 'delete' ||
    token === 'deleted' ||
    token === 'expired' ||
    token === 'expiry' ||
    token === 'unregister' ||
    token === 'unregistered'
  ) {
    return 'expiry';
  }

  if (token === 'details' || token === 'details_viewed' || token === 'view') {
    return 'view';
  }

  return 'other';
}

function resolveDescriptionKey(token: string, type: LicenseActivityFeedType): string {
  switch (token) {
    case 'activate':
    case 'activated':
      return 'license.activityLog.descriptions.activated';
    case 'generate':
    case 'generated':
      return 'license.activityLog.descriptions.generated';
    case 'extend':
    case 'extended':
      return 'license.activityLog.descriptions.extended';
    case 'renew':
    case 'renewed':
      return 'license.activityLog.descriptions.renewed';
    case 'reminder':
    case 'license_reminder':
    case 'license_reminder_sent':
      return 'license.activityLog.descriptions.reminder';
    case 'revoke':
    case 'revoked':
      return 'license.activityLog.descriptions.revoked';
    case 'cancel':
    case 'cancelled':
    case 'canceled':
      return 'license.activityLog.descriptions.cancelled';
    case 'delete':
    case 'deleted':
      return 'license.activityLog.descriptions.deleted';
    case 'unregister':
    case 'unregistered':
      return 'license.activityLog.descriptions.unregistered';
    case 'details':
    case 'details_viewed':
    case 'view':
      return 'license.activityLog.descriptions.viewed';
    case 'force_deactivate':
    case 'force_deactivated':
      return 'license.activityLog.descriptions.forceDeactivated';
    default:
      if (type === 'reminder') return 'license.activityLog.descriptions.reminder';
      if (type === 'expiry') return 'license.activityLog.descriptions.expired';
      if (type === 'renewal') return 'license.activityLog.descriptions.renewed';
      if (type === 'view') return 'license.activityLog.descriptions.viewed';
      return 'license.activityLog.descriptions.other';
  }
}
