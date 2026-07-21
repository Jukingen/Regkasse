import React from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftSpacing } from '../constants/SoftTheme';
import { TENANT_WARNING_DAYS_BEFORE_EXPIRY } from '../constants/licenseGracePeriod';
import { useLicenseStatus } from '../hooks/useLicenseStatus';
import { formatUserDateTime } from '../utils/dateFormatter';
import { areLicenseChecksBypassedInDevelopment } from '../utils/licenseCriticalActionGuard';
import { formatLicenseRemainingDe } from '../utils/licenseExpiryRemaining';

const WARNING_THRESHOLD_DAYS = TENANT_WARNING_DAYS_BEFORE_EXPIRY;

/**
 * POS lisans uyarı bandı (Almanca):
 *   - daysRemaining ∈ (0, 15]      → sarı uyarı
 *   - isExpired === true           → kırmızı blok-uyarı
 *   - aksi halde                   → render edilmez (sessiz)
 *
 * UX kuralı: bu band hiçbir aksiyonu engellemez. Ödeme bloklaması arka uçta
 * `PaymentService.EnsureLicenseNotExpired()` tarafından yapılır.
 */
export function LicenseExpiryBanner() {
  const { status } = useLicenseStatus();

  if (areLicenseChecksBypassedInDevelopment()) return null;

  if (!status) return null;

  const { isExpired, daysRemaining, expiryDate } = status;

  if (isExpired) {
    const dateLabel = formatExpiryDateTimeDe(expiryDate);
    return (
      <View style={[styles.banner, styles.expired]} accessibilityRole="alert">
        <Text style={styles.expiredText} numberOfLines={2}>
          {dateLabel
            ? `LIZENZ ABGELAUFEN am ${dateLabel} – Zahlungserstellung gesperrt. Bitte Support kontaktieren.`
            : 'LIZENZ ABGELAUFEN – Zahlungserstellung gesperrt. Bitte Support kontaktieren.'}
        </Text>
      </View>
    );
  }

  if (daysRemaining > 0 && daysRemaining <= WARNING_THRESHOLD_DAYS) {
    const dateLabel = formatExpiryDateTimeDe(expiryDate);
    const remainingLabel =
      formatLicenseRemainingDe(daysRemaining, expiryDate) ?? `${daysRemaining} Tag(e)`;
    return (
      <View style={[styles.banner, styles.warning]} accessibilityRole="alert">
        <Text style={styles.warningText} numberOfLines={2}>
          {dateLabel
            ? `Lizenz läuft in ${remainingLabel} ab (${dateLabel}) – bitte rechtzeitig verlängern`
            : `Lizenz läuft in ${remainingLabel} ab – bitte rechtzeitig verlängern`}
        </Text>
      </View>
    );
  }

  return null;
}

function formatExpiryDateTimeDe(iso: string | null): string | null {
  if (!iso) return null;
  const formatted = formatUserDateTime(iso);
  return formatted || null;
}

const styles = StyleSheet.create({
  banner: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: SoftColors.border,
  },
  expired: {
    backgroundColor: '#b71c1c',
    borderBottomColor: '#7f0000',
  },
  warning: {
    backgroundColor: SoftColors.warningBg,
    borderBottomColor: SoftColors.border,
  },
  expiredText: {
    color: SoftColors.textInverse,
    fontWeight: '700',
    fontSize: 14,
    textAlign: 'center',
  },
  warningText: {
    color: SoftColors.textPrimary,
    fontWeight: '600',
    fontSize: 14,
    textAlign: 'center',
  },
});
