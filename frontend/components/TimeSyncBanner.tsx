import React from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftSpacing } from '../constants/SoftTheme';
import { useTimeSyncStatus } from '../hooks/useTimeSyncStatus';

/**
 * Global POS banner for RKSV clock drift (German copy only).
 */
export function TimeSyncBanner() {
  const { status, loading, absOffsetSeconds, timeSyncCritical, timeSyncWarningBand } = useTimeSyncStatus();

  if (loading && !status) {
    return null;
  }

  if (timeSyncCritical) {
    return (
      <View style={[styles.banner, styles.critical]} accessibilityRole="alert">
        <Text style={styles.criticalText}>
          SYSTEMZEIT FEHLERHAFT – Zahlungen blockiert! Bitte Admin kontaktieren
        </Text>
      </View>
    );
  }

  if (timeSyncWarningBand) {
    const suffix =
      absOffsetSeconds != null
        ? ` (${Math.round(absOffsetSeconds * 10) / 10} Sekunden)`
        : '';
    return (
      <View style={[styles.banner, styles.warning]} accessibilityRole="alert">
        <Text style={styles.warningText}>
          Systemzeit abweichend{suffix} – FinanzOnline könnte Zahlungen ablehnen
        </Text>
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
    borderBottomColor: SoftColors.border,
  },
  critical: {
    backgroundColor: '#b71c1c',
    borderBottomColor: '#7f0000',
  },
  warning: {
    backgroundColor: SoftColors.warningBg,
    borderBottomColor: SoftColors.border,
  },
  criticalText: {
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
