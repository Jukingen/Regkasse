/**
 * Development-only tenant slug switcher (local storage + X-Tenant-Id on API requests).
 */
import React, { useCallback, useEffect, useState } from 'react';
import { Modal, Platform, Pressable, StyleSheet, Text, View } from 'react-native';

import { DEV_TENANT_PRESETS } from '../../../constants/devTenantPresets';
import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../../../constants/SoftTheme';
import {
  getDevTenantSlugOverride,
  setDevTenantAndPersist,
} from '../../../services/tenant/devTenant';
import { reloadApp } from './reloadApp';

const isDev = __DEV__;

export function DevTenantSwitcher() {
  const [currentTenant, setCurrentTenant] = useState<string>('dev');
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!isDev) return;
    void (async () => {
      const stored = await getDevTenantSlugOverride();
      setCurrentTenant(stored ?? 'dev');
    })();
  }, []);

  const onSelect = useCallback(async (value: string) => {
    setOpen(false);
    await setDevTenantAndPersist(value);
    setCurrentTenant(value);
    reloadApp();
  }, []);

  if (!isDev) {
    return null;
  }

  const currentLabel =
    DEV_TENANT_PRESETS.find((p) => p.value === currentTenant)?.label ?? currentTenant;

  return (
    <>
      <Pressable
        onPress={() => setOpen(true)}
        style={styles.chip}
        accessibilityRole="button"
        accessibilityLabel="Mandant (Entwicklung)"
      >
        <Text style={styles.chipText} numberOfLines={1}>
          Mandant: {currentLabel}
        </Text>
      </Pressable>

      <Modal visible={open} transparent animationType="fade" onRequestClose={() => setOpen(false)}>
        <Pressable style={styles.backdrop} onPress={() => setOpen(false)}>
          <Pressable style={styles.sheet} onPress={(e) => e.stopPropagation()}>
            <Text style={styles.title}>Mandant (Entwicklung)</Text>
            <Text style={styles.hint}>
              {Platform.OS === 'web'
                ? 'Seite wird nach der Auswahl neu geladen.'
                : 'App wird nach der Auswahl neu geladen.'}
            </Text>
            {DEV_TENANT_PRESETS.map((preset) => (
              <Pressable
                key={preset.value}
                style={[
                  styles.option,
                  preset.value === currentTenant && styles.optionSelected,
                ]}
                onPress={() => void onSelect(preset.value)}
              >
                <Text
                  style={[
                    styles.optionText,
                    preset.value === currentTenant && styles.optionTextSelected,
                  ]}
                >
                  {preset.label}
                </Text>
                <Text style={styles.optionSlug}>{preset.value}</Text>
              </Pressable>
            ))}
            <Pressable style={styles.cancelBtn} onPress={() => setOpen(false)}>
              <Text style={styles.cancelText}>Abbrechen</Text>
            </Pressable>
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  chip: {
    backgroundColor: SoftColors.bgSecondary,
    borderRadius: SoftRadius.sm,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 4,
    maxWidth: 200,
    borderWidth: 1,
    borderColor: SoftColors.border,
  },
  chipText: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    fontSize: 11,
  },
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    padding: SoftSpacing.lg,
  },
  sheet: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.md,
    gap: SoftSpacing.sm,
  },
  title: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
  },
  hint: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.xs,
  },
  option: {
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.sm,
    borderRadius: SoftRadius.sm,
    borderWidth: 1,
    borderColor: SoftColors.border,
  },
  optionSelected: {
    borderColor: SoftColors.accent,
    backgroundColor: SoftColors.accentLight,
  },
  optionText: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
  },
  optionTextSelected: {
    fontWeight: '600',
    color: SoftColors.accent,
  },
  optionSlug: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: 2,
  },
  cancelBtn: {
    marginTop: SoftSpacing.sm,
    alignItems: 'center',
    paddingVertical: SoftSpacing.sm,
  },
  cancelText: {
    ...SoftTypography.body,
    color: SoftColors.accent,
  },
});
