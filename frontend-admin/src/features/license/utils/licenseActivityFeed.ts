export type LicenseActivityFeedType =
  | 'renewal'
  | 'reminder'
  | 'expiry'
  | 'view'
  | 'other';

export type LicenseActivityFeedItem = {
  type: LicenseActivityFeedType;
  /** i18n key for the primary event label */
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

/** Unified license audit row (GET /api/admin/license/audit). */
export type LicenseAuditFeedInput = {
  action?: string | null;
  tenantName?: string | null;
  performedBy?: string | null;
  createdAtUtc: string;
};

export type LicenseAuditFeedItem = {
  type: LicenseActivityFeedType;
  /** Prefer license.auditLog.actions.* when known */
  actionLabelKey: string;
  /** Raw action code for fallback display */
  actionCode: string;
  tenantName: string | null;
  performedBy: string | null;
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

/**
 * Maps unified license audit rows into dashboard feed display fields.
 */
export function mapLicenseAuditFeedItem(input: LicenseAuditFeedInput): LicenseAuditFeedItem {
  const actionCode = (input.action ?? '').trim().toUpperCase() || 'UNKNOWN';
  const token = normalizeToken(actionCode);
  const type = resolveFeedType(token);
  const knownAuditAction =
    actionCode === 'SALE_CREATED' ||
    actionCode === 'SALE_CANCELLED' ||
    actionCode === 'SALE_REFUNDED' ||
    actionCode === 'LICENSE_ACTIVATED' ||
    actionCode === 'LICENSE_EXTENDED' ||
    actionCode === 'LICENSE_RENEWED' ||
    actionCode === 'LICENSE_UPDATED' ||
    actionCode === 'LICENSE_REMINDER_SENT' ||
    actionCode === 'LICENSE_RENEWAL_PAGE_VIEWED';

  return {
    type,
    actionLabelKey: knownAuditAction
      ? `license.auditLog.actions.${actionCode}`
      : resolveDescriptionKey(token, type),
    actionCode,
    tenantName: input.tenantName?.trim() || null,
    performedBy: input.performedBy?.trim() || null,
    timestampUtc: input.createdAtUtc,
  };
}

function resolveFeedType(token: string): LicenseActivityFeedType {
  if (
    token === 'activate' ||
    token === 'activated' ||
    token === 'license_activated' ||
    token === 'generate' ||
    token === 'generated' ||
    token === 'extend' ||
    token === 'extended' ||
    token === 'license_extended' ||
    token === 'renew' ||
    token === 'renewed' ||
    token === 'license_renewed' ||
    token === 'sale_created' ||
    token === 'license_updated' ||
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
    token === 'sale_cancelled' ||
    token === 'sale_refunded' ||
    token === 'delete' ||
    token === 'deleted' ||
    token === 'expired' ||
    token === 'expiry' ||
    token === 'unregister' ||
    token === 'unregistered'
  ) {
    return 'expiry';
  }

  if (
    token === 'details' ||
    token === 'details_viewed' ||
    token === 'view' ||
    token === 'license_renewal_page_viewed'
  ) {
    return 'view';
  }

  return 'other';
}

function resolveDescriptionKey(token: string, type: LicenseActivityFeedType): string {
  switch (token) {
    case 'activate':
    case 'activated':
    case 'license_activated':
      return 'license.activityLog.descriptions.activated';
    case 'generate':
    case 'generated':
    case 'sale_created':
      return 'license.activityLog.descriptions.generated';
    case 'extend':
    case 'extended':
    case 'license_extended':
      return 'license.activityLog.descriptions.extended';
    case 'renew':
    case 'renewed':
    case 'license_renewed':
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
    case 'sale_cancelled':
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
    case 'license_renewal_page_viewed':
      return 'license.activityLog.descriptions.viewed';
    case 'force_deactivate':
    case 'force_deactivated':
      return 'license.activityLog.descriptions.forceDeactivated';
    case 'sale_refunded':
      return 'license.activityLog.descriptions.cancelled';
    case 'license_updated':
      return 'license.activityLog.descriptions.renewed';
    default:
      if (type === 'reminder') return 'license.activityLog.descriptions.reminder';
      if (type === 'expiry') return 'license.activityLog.descriptions.expired';
      if (type === 'renewal') return 'license.activityLog.descriptions.renewed';
      if (type === 'view') return 'license.activityLog.descriptions.viewed';
      return 'license.activityLog.descriptions.other';
  }
}
