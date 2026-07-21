import React, { useCallback } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { useLicenseStatus } from '../hooks/useLicenseStatus';
import { useMandantLicenseWarning } from '../hooks/useMandantLicenseWarning';
import { formatUserDate } from '../utils/dateFormatter';
import { areLicenseChecksBypassedInDevelopment } from '../utils/licenseCriticalActionGuard';
import { formatLicenseRemainingDe } from '../utils/licenseExpiryRemaining';
import { openLicenseExtension } from '../utils/openAdmin';

/**
 * Mandant (tenant) license warning band with optional renew action (German POS copy).
 * Grace period and pre-expiry windows come from GET /api/license/status?tenantId=….
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

  if (shouldShowGrace) {
    const remaining = state.gracePeriodRemaining;
    const daysExpired = state.daysOverdue;
    const lockLabel =
      (state.lockDate && formatUserDate(state.lockDate)) || formatLockDateFromRemaining(remaining);
    return (
      <View style={[styles.banner, styles.warningBanner]} accessibilityRole="alert">
        <Text style={styles.warningText}>
          Lizenz seit {daysExpired} Tag{daysExpired === 1 ? '' : 'en'} abgelaufen. POS noch{' '}
          {remaining} Tag{remaining === 1 ? '' : 'e'} nutzbar
          {lockLabel ? ` — Sperre am ${lockLabel}` : ''}. Danach nur Super-Administrator.
        </Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Lizenz verlängern"
          onPress={onRenew}
          style={({ pressed }) => [
            styles.renewButton,
            styles.warningRenewButton,
            pressed && styles.pressed,
          ]}>
          <Text style={styles.warningRenewLabel}>Lizenz verlängern</Text>
        </Pressable>
      </View>
    );
  }

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

function formatLockDateFromRemaining(graceRemaining: number): string | null {
  if (!Number.isFinite(graceRemaining) || graceRemaining < 0) return null;
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + Math.max(0, Math.trunc(graceRemaining)));
  return formatUserDate(d.toISOString()) || null;
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
  warningBanner: {
    backgroundColor: '#fff3e0',
    borderBottomColor: '#ffb74d',
  },
  infoBanner: {
    backgroundColor: SoftColors.warningBg,
    borderBottomColor: SoftColors.border,
  },
  warningText: {
    flex: 1,
    color: '#e65100',
    fontWeight: '700',
    fontSize: 14,
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
  warningRenewButton: {
    backgroundColor: '#e65100',
  },
  infoRenewButton: {
    backgroundColor: SoftColors.accent,
  },
  warningRenewLabel: {
    color: SoftColors.textInverse,
    fontWeight: '700',
    fontSize: 13,
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
