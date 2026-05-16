import React, { useState } from 'react';
import { Modal, Pressable, StyleSheet, Text } from 'react-native';

import { SoftColors, SoftSpacing } from '../constants/SoftTheme';
import type { DevelopmentModeSettings } from '../services/developmentModeClientCache';

type Props = {
  settings: DevelopmentModeSettings | null;
};

/**
 * POS header chip when persisted development-mode is enabled (effective bypasses require Development host).
 */
export function EnvironmentBadge({ settings }: Props) {
  const [open, setOpen] = useState(false);

  if (!settings?.enabled) {
    return null;
  }

  const active: string[] = [];
  if (settings.bypassLicense) active.push('Lizenz');
  if (settings.bypassNtpCheck) active.push('NTP');
  if (settings.bypassTseCheck) active.push('TSE');

  const lines: string[] = [];
  if (settings.bypassLicense) lines.push('✓ Lizenzprüfung umgangen');
  if (settings.bypassNtpCheck) lines.push('✓ NTP-Prüfung umgangen');
  if (settings.bypassTseCheck) lines.push('✓ TSE-Prüfung umgangen');
  if (settings.simulateOffline) lines.push('⚠ Offline-Simulation');
  if (settings.forceOnline) lines.push('✓ Online erzwungen');
  lines.push(`Gültig: ${settings.validDays} Tage`);

  return (
    <>
      <Pressable
        onPress={() => setOpen(true)}
        style={styles.chip}
        accessibilityRole="button"
        accessibilityLabel="Entwicklungsmodus"
      >
        <Text style={styles.chipText}>DEV{active.length > 0 ? ` (${active.join(', ')})` : ''}</Text>
      </Pressable>
      <Modal visible={open} transparent animationType="fade" onRequestClose={() => setOpen(false)}>
        <Pressable style={styles.backdrop} onPress={() => setOpen(false)}>
          <Pressable style={styles.sheet} onPress={(e) => e.stopPropagation()}>
            <Text style={styles.title}>Entwicklungsmodus</Text>
            {lines.map((line) => (
              <Text key={line} style={styles.line}>
                {line}
              </Text>
            ))}
            <Pressable style={styles.closeBtn} onPress={() => setOpen(false)}>
              <Text style={styles.closeText}>Schließen</Text>
            </Pressable>
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  chip: {
    marginLeft: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 4,
    borderRadius: 6,
    backgroundColor: '#fa8c16',
  },
  chipText: {
    color: SoftColors.textInverse,
    fontSize: 11,
    fontWeight: '700',
  },
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    padding: SoftSpacing.lg,
  },
  sheet: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: 12,
    padding: SoftSpacing.lg,
  },
  title: {
    fontSize: 16,
    fontWeight: '700',
    marginBottom: SoftSpacing.sm,
    color: SoftColors.textPrimary,
  },
  line: {
    fontSize: 14,
    marginVertical: 4,
    color: SoftColors.textPrimary,
  },
  closeBtn: {
    marginTop: SoftSpacing.md,
    alignSelf: 'flex-end',
    paddingVertical: 8,
    paddingHorizontal: 12,
  },
  closeText: {
    color: SoftColors.accent,
    fontWeight: '600',
  },
});
