import React from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftSpacing } from '../constants/SoftTheme';
import { useLicenseStatus } from '../hooks/useLicenseStatus';
import { areLicenseChecksBypassedInDevelopment } from '../utils/licenseCriticalActionGuard';
import { TENANT_WARNING_DAYS_BEFORE_EXPIRY } from '../constants/licenseGracePeriod';
import { formatUserDate } from '../utils/dateFormatter';

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
    const dateLabel = formatExpiryDateDe(expiryDate);
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
    const dateLabel = formatExpiryDateDe(expiryDate);
    return (
      <View style={[styles.banner, styles.warning]} accessibilityRole="alert">
        <Text style={styles.warningText} numberOfLines={2}>
          {dateLabel
            ? `Lizenz läuft in ${daysRemaining} Tag(en) ab (${dateLabel}) – bitte rechtzeitig verlängern`
            : `Lizenz läuft in ${daysRemaining} Tag(en) ab – bitte rechtzeitig verlängern`}
        </Text>
      </View>
    );
  }

  return null;
}

function formatExpiryDateDe(iso: string | null): string | null {
  if (!iso) return null;
  const formatted = formatUserDate(iso);
  return formatted || iso.slice(0, 10);
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
