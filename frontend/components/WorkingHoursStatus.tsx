/**
 * Compact restaurant-hours chip for the POS header.
 *
 * DISPLAY ONLY — never blocks orders, payments, or register access (RKSV).
 * Website/App online orders are gated separately via /api/sites/{slug}/status.
 * Cashier can always work regardless of restaurantIsOpen / working hours.
 */
import React from 'react';
import { View, Text, StyleSheet } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useWorkingHours } from '../hooks/useWorkingHours';

function formatMinutes(totalMinutes: number): string {
  const mins = Math.max(0, Math.floor(totalMinutes));
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  if (h <= 0) return `${m} Min`;
  if (m <= 0) return `${h} Std`;
  return `${h} Std ${m} Min`;
}

export function WorkingHoursStatus() {
  const {
    restaurantIsOpen,
    showReminder,
    timeUntilClose,
    timeUntilOpen,
    closeTime,
    openTime,
    message,
    loading,
    posOperationsAllowed,
  } = useWorkingHours();

  if (loading) {
    return null;
  }

  // Informational palette only — closed restaurant is muted, not an error gate.
  const color = showReminder
    ? SoftColors.warning
    : restaurantIsOpen
      ? SoftColors.success
      : SoftColors.textMuted;

  let infoLine: string;
  if (!restaurantIsOpen) {
    infoLine = openTime
      ? `Heute geschlossen (nur Info) · Öffnung ${openTime}`
      : `${message} (nur Info)`;
    if (timeUntilOpen > 0) {
      infoLine = `${infoLine} · in ${formatMinutes(timeUntilOpen)}`;
    }
  } else if (showReminder && timeUntilClose > 0) {
    infoLine = closeTime
      ? `Schließung ${closeTime} · noch ${formatMinutes(timeUntilClose)}`
      : `Schließung in ${formatMinutes(timeUntilClose)}`;
  } else {
    infoLine = closeTime ? `Schließung: ${closeTime}` : message;
  }

  // Harden: never surface a "blocked" state even if a future hook regresses.
  void posOperationsAllowed;

  return (
    <View
      style={[styles.container, { borderColor: color }]}
      accessibilityRole="summary"
      accessibilityLabel={`Öffnungszeiten nur Hinweis. ${infoLine}. POS ist immer bereit.`}
      // Explicit: this chip never disables POS interaction
      pointerEvents="none"
      importantForAccessibility="no-hide-descendants">
      <View style={styles.row}>
        <View style={[styles.dot, { backgroundColor: color }]} />
        <Text style={styles.info} numberOfLines={1}>
          {infoLine}
        </Text>
      </View>
      <Text style={styles.note} numberOfLines={1}>
        POS immer bereit
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: SoftSpacing.xs,
    paddingVertical: 2,
    borderRadius: SoftRadius.sm,
    borderWidth: StyleSheet.hairlineWidth,
    backgroundColor: SoftColors.bgSecondary,
    flexShrink: 1,
    maxWidth: 220,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  dot: {
    width: 6,
    height: 6,
    borderRadius: 3,
  },
  info: {
    ...SoftTypography.caption,
    fontSize: 10,
    fontWeight: '600',
    color: SoftColors.textSecondary,
    flexShrink: 1,
  },
  note: {
    ...SoftTypography.caption,
    fontSize: 9,
    color: SoftColors.textMuted,
    marginTop: 1,
    marginLeft: 10,
  },
});
