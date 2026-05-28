/**
 * Development-only tenant slug switcher (API list + local storage + X-Tenant-Id on API requests).
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Modal,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';

import { canonicalDevTenantSlug, getDevTenantPresetName, isSameDevTenantPreset } from '../../../constants/devTenantCatalog';
import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../../../constants/SoftTheme';
import { useAuth } from '../../../contexts/AuthContext';
import {
  getDevTenantSlugOverride,
  setDevTenantAndPersist,
} from '../../../services/tenant/devTenant';
import { useTenants } from '../../../hooks/useTenants';
import { sessionManager } from '../../../services/session/sessionManager';
import { reloadApp } from './reloadApp';

const isDev = __DEV__;

export function DevTenantSwitcher() {
  const { isAuthenticated } = useAuth();
  const [currentTenant, setCurrentTenant] = useState<string>('dev');
  const [open, setOpen] = useState(false);

  const { tenants, isLoading, isError, isFromCache, refreshTenants } = useTenants({
    enabled: isAuthenticated,
  });

  useEffect(() => {
    if (!isDev) return;
    void (async () => {
      const stored = await getDevTenantSlugOverride();
      setCurrentTenant(stored ?? 'dev');
    })();
  }, []);

  useEffect(() => {
    if (!open || !isDev || !isAuthenticated) return;
    void (async () => {
      await refreshTenants();
      const stored = await getDevTenantSlugOverride();
      setCurrentTenant(stored ?? 'dev');
    })();
  }, [open, isAuthenticated, refreshTenants]);

  const currentLabel = useMemo(() => {
    const match = tenants.find((row) => isSameDevTenantPreset(row.slug, currentTenant));
    if (match) return match.name;
    return getDevTenantPresetName(currentTenant) ?? currentTenant;
  }, [tenants, currentTenant]);

  const onSelect = useCallback(async (slug: string) => {
    setOpen(false);
    await setDevTenantAndPersist(slug);
    await sessionManager.clearSession();
    setCurrentTenant(slug);
    reloadApp();
  }, []);

  if (!isDev) {
    return null;
  }

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

            {isAuthenticated ? (
              <Pressable
                style={styles.refreshBtn}
                onPress={() => void refreshTenants()}
                accessibilityRole="button"
                accessibilityLabel="Mandantenliste aktualisieren"
              >
                <Text style={styles.refreshText}>
                  {isLoading ? 'Wird aktualisiert…' : 'Aktualisieren'}
                </Text>
              </Pressable>
            ) : null}

            {isFromCache ? (
              <Text style={styles.cacheHint}>Offline-Zwischenspeicher (API nicht erreichbar)</Text>
            ) : null}

            {!isAuthenticated ? (
              <Text style={styles.emptyText}>Bitte zuerst anmelden.</Text>
            ) : isLoading ? (
              <ActivityIndicator color={SoftColors.accent} style={styles.loader} />
            ) : isError ? (
              <Text style={styles.emptyText}>
                Mandantenliste konnte nicht geladen werden.
              </Text>
            ) : tenants.length === 0 ? (
              <Text style={styles.emptyText}>Keine Mandanten verfügbar.</Text>
            ) : (
              tenants.map((tenant) => (
                <Pressable
                  key={tenant.id}
                  style={[
                    styles.option,
                    isSameDevTenantPreset(tenant.slug, currentTenant) && styles.optionSelected,
                  ]}
                  onPress={() => void onSelect(tenant.slug)}
                >
                  <Text
                    style={[
                      styles.optionText,
                      isSameDevTenantPreset(tenant.slug, currentTenant) && styles.optionTextSelected,
                    ]}
                  >
                    {tenant.name}
                  </Text>
                  <Text style={styles.optionSlug}>{canonicalDevTenantSlug(tenant.slug)}</Text>
                </Pressable>
              ))
            )}

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
  refreshBtn: {
    alignSelf: 'flex-start',
    paddingVertical: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.sm,
    borderRadius: SoftRadius.sm,
    borderWidth: 1,
    borderColor: SoftColors.accent,
  },
  refreshText: {
    ...SoftTypography.caption,
    color: SoftColors.accent,
    fontWeight: '600',
  },
  cacheHint: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    fontStyle: 'italic',
  },
  loader: {
    marginVertical: SoftSpacing.md,
  },
  emptyText: {
    ...SoftTypography.body,
    color: SoftColors.textMuted,
    paddingVertical: SoftSpacing.sm,
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
