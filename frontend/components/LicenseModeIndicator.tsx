import { Ionicons } from '@expo/vector-icons';
import React, { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useAuth } from '../contexts/AuthContext';
import { useLicenseStatus } from '../hooks/useLicenseStatus';
import { TENANT_WARNING_DAYS_BEFORE_EXPIRY } from '../constants/licenseGracePeriod';

/** Kept in sync with `LicenseExpiryBanner` warning window to avoid duplicate copy. */
const LICENSE_EXPIRY_WARNING_DAYS = TENANT_WARNING_DAYS_BEFORE_EXPIRY;

/**
 * High-visibility strip for POS mode: demo login vs long-horizon trial license.
 * Expired and short trial windows use `LicenseExpiryBanner` instead.
 */
export function LicenseModeIndicator() {
  const { t } = useTranslation('license');
  const { user } = useAuth();
  const { status, loading } = useLicenseStatus();

  const isDemoAccount = user?.isDemo === true;

  const showLongTrial = useMemo(() => {
    if (!status || loading) return false;
    if (!status.isTrial || status.isExpired) return false;
    return status.daysRemaining > LICENSE_EXPIRY_WARNING_DAYS;
  }, [status, loading]);

  if (isDemoAccount) {
    return (
      <View style={[styles.container, styles.demo]} accessibilityRole="text">
        <Ionicons name="construct" size={16} color={SoftColors.textInverse} accessibilityElementsHidden />
        <Text style={styles.text} numberOfLines={2}>
          {t('license:modeBanner.demoAccount')}
        </Text>
      </View>
    );
  }

  if (!showLongTrial || !status) {
    return null;
  }

  return (
    <View style={[styles.container, styles.trial]} accessibilityRole="text">
      <Ionicons name="information-circle" size={16} color={SoftColors.textInverse} accessibilityElementsHidden />
      <Text style={styles.text} numberOfLines={2}>
        {t('license:modeBanner.trialDays', { count: status.daysRemaining })}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    marginHorizontal: SoftSpacing.md,
    marginTop: SoftSpacing.sm,
    marginBottom: SoftSpacing.xs,
    borderRadius: SoftRadius.md,
    gap: SoftSpacing.sm,
  },
  trial: {
    backgroundColor: '#1890ff',
  },
  demo: {
    backgroundColor: '#722ed1',
  },
  text: {
    ...SoftTypography.label,
    color: SoftColors.textInverse,
    fontSize: 12,
    fontWeight: '600',
    flexShrink: 1,
    textAlign: 'center',
  },
});
