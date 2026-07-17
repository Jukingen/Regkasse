import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { useTagesabschlussStatus } from '../hooks/useTagesabschlussStatus';

/**
 * POS banner when Tagesabschluss is still due for the effective register.
 * Navigates to Einstellungen (ShiftManager) — never auto-closes (RKSV).
 */
export function TagesabschlussReminder() {
  const router = useRouter();
  const { isClosingRequired, hoursRemaining, loading } = useTagesabschlussStatus();

  if (loading || !isClosingRequired) {
    return null;
  }

  return (
    <View style={styles.warningBanner} accessibilityRole="alert">
      <Text style={styles.warningTitle}>Tagesabschluss steht aus</Text>
      <Text style={styles.warningText}>
        Bitte führen Sie den Tagesabschluss durch. Noch {hoursRemaining} Stunde
        {hoursRemaining === 1 ? '' : 'n'} verbleibend (bis 00:00 Europe/Vienna).
      </Text>
      <Pressable
        style={({ pressed }) => [styles.closeButton, pressed && styles.pressed]}
        onPress={() => router.push('/(tabs)/settings' as const)}
        accessibilityRole="button"
        accessibilityLabel="Tagesabschluss jetzt durchführen"
      >
        <Text style={styles.closeButtonText}>Jetzt durchführen</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  warningBanner: {
    backgroundColor: '#fffbeb',
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#eab308',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
  },
  warningTitle: {
    fontWeight: '700',
    color: SoftColors.textPrimary,
    fontSize: 15,
    marginBottom: 4,
  },
  warningText: {
    color: SoftColors.textSecondary,
    fontSize: 13,
    marginBottom: SoftSpacing.sm,
  },
  closeButton: {
    alignSelf: 'flex-start',
    backgroundColor: SoftColors.accent,
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: SoftRadius.md,
  },
  closeButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
    fontSize: 13,
  },
  pressed: {
    opacity: 0.85,
  },
});
