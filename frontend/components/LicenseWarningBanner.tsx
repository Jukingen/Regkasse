import React, { useCallback } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { useLicenseStatus } from '../hooks/useLicenseStatus';
import { useMandantLicenseWarning } from '../hooks/useMandantLicenseWarning';
import { areLicenseChecksBypassedInDevelopment } from '../utils/licenseCriticalActionGuard';
import { formatLicenseRemainingDe } from '../utils/licenseExpiryRemaining';
import { openLicenseExtension } from '../utils/openAdmin';

/**
 * Mandant (tenant) license warning band with optional renew action (German POS copy).
 * Pre-expiry window only — active grace UX is {@link GracePeriodWarning}.
 * Status from GET /api/license/status?tenantId=….
 */
export function LicenseWarningBanner() {
  const { state, shouldShowGrace, shouldShowPreExpiry } = useMandantLicenseWarning();
  const { status: deploymentStatus } = useLicenseStatus();

  const onRenew = useCallback(() => {
    const machineHash = deploymentStatus?.machineHash ?? '';
    void openLicenseExtension(machineHash);
  }, [deploymentStatus?.machineHash]);

  if (areLicenseChecksBypassedInDevelopment()) return null;
  if (!state || state.canAccess === false) return null;

  // Grace is owned by GracePeriodWarning (banner + modal).
  if (shouldShowGrace) return null;

  if (shouldShowPreExpiry) {
    const remainingLabel =
      formatLicenseRemainingDe(state.daysRemaining, state.validUntil) ??
      `${state.daysRemaining} Tag${state.daysRemaining === 1 ? '' : 'e'}`;
    return (
      <View style={[styles.banner, styles.infoBanner]} accessibilityRole="alert">
        <Text style={styles.infoText}>Lizenz läuft in {remainingLabel} ab.</Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Jetzt verlängern"
          onPress={onRenew}
          style={({ pressed }) => [
            styles.renewButton,
            styles.infoRenewButton,
            pressed && styles.pressed,
          ]}>
          <Text style={styles.infoRenewLabel}>Jetzt verlängern</Text>
        </Pressable>
      </View>
    );
  }

  return null;
}

const styles = StyleSheet.create({
  banner: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: SoftSpacing.sm,
    flexWrap: 'wrap',
  },
  infoBanner: {
    backgroundColor: SoftColors.warningBg,
    borderBottomColor: SoftColors.border,
  },
  infoText: {
    flex: 1,
    color: SoftColors.textPrimary,
    fontWeight: '600',
    fontSize: 14,
  },
  renewButton: {
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.sm,
  },
  infoRenewButton: {
    backgroundColor: SoftColors.accent,
  },
  infoRenewLabel: {
    color: SoftColors.textInverse,
    fontWeight: '600',
    fontSize: 13,
  },
  pressed: {
    opacity: 0.85,
  },
});
