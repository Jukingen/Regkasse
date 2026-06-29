import React, { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { TAB_BAR_HEIGHT } from '../constants/breakpoints';
import { useOfflineOrderManager } from '../hooks/useOfflineOrderManager';

function countExpiringPending(
  pending: Awaited<ReturnType<ReturnType<typeof useOfflineOrderManager>['getPending']>>
): number {
  const now = Date.now();
  return pending.filter((order) => {
    if (order.status !== 'pending') return false;
    const hoursLeft = (new Date(order.expiresAt).getTime() - now) / (1000 * 60 * 60);
    return hoursLeft > 0 && hoursLeft < 24;
  }).length;
}

export function OfflineBanner() {
  const insets = useSafeAreaInsets();
  const { status, syncNow, getPending } = useOfflineOrderManager();
  const [expiringCount, setExpiringCount] = useState(0);
  const [syncing, setSyncing] = useState(false);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (!status) {
        if (!cancelled) setExpiringCount(0);
        return;
      }

      try {
        const pending = await getPending();
        if (!cancelled) setExpiringCount(countExpiringPending(pending));
      } catch {
        if (!cancelled) setExpiringCount(0);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [status, getPending]);

  const handleSync = useCallback(async () => {
    if (syncing || status?.isSyncing) return;
    setSyncing(true);
    try {
      await syncNow();
    } finally {
      setSyncing(false);
    }
  }, [syncNow, status?.isSyncing, syncing]);

  if (!status) {
    return null;
  }

  if (status.isOnline && status.pendingCount === 0) {
    return null;
  }

  const isSyncing = syncing || status.isSyncing;

  return (
    <View
      style={[
        styles.container,
        { bottom: TAB_BAR_HEIGHT + insets.bottom + SoftSpacing.sm },
      ]}
      accessibilityRole="summary"
      pointerEvents="box-none"
    >
      {!status.isOnline ? (
        <View style={[styles.banner, styles.offlineBanner]} accessibilityRole="alert">
          <Text style={styles.icon} accessibilityElementsHidden>
            🔴
          </Text>
          <Text style={styles.title}>OFFLINE-MODUS</Text>
          <Text style={styles.subtitle}>Keine Internetverbindung</Text>
          <Text style={styles.detail}>
            {status.pendingCount}{' '}
            {status.pendingCount === 1 ? 'Bestellung wartet' : 'Bestellungen warten'}
          </Text>
          {expiringCount > 0 ? (
            <Text style={styles.warning}>
              ⚠️ {expiringCount}{' '}
              {expiringCount === 1
                ? 'Bestellung läuft innerhalb von 24 Stunden ab'
                : 'Bestellungen laufen innerhalb von 24 Stunden ab'}
            </Text>
          ) : null}
          <Pressable
            style={[styles.button, isSyncing && styles.buttonDisabled]}
            onPress={() => void handleSync()}
            disabled={isSyncing}
            accessibilityRole="button"
            accessibilityLabel={
              isSyncing ? 'Synchronisierung läuft' : 'Jetzt synchronisieren'
            }
          >
            {isSyncing ? (
              <ActivityIndicator color={SoftColors.textInverse} size="small" />
            ) : null}
            <Text style={styles.buttonText}>
              {isSyncing ? 'Synchronisierung läuft…' : 'Jetzt synchronisieren'}
            </Text>
          </Pressable>
        </View>
      ) : null}

      {status.isOnline && status.pendingCount > 0 ? (
        <View style={[styles.banner, styles.pendingBanner]}>
          <Text style={styles.icon} accessibilityElementsHidden>
            📋
          </Text>
          <Text style={styles.title}>
            {status.pendingCount}{' '}
            {status.pendingCount === 1
              ? 'Offline-Bestellung wartet'
              : 'Offline-Bestellungen warten'}
          </Text>
          {expiringCount > 0 ? (
            <Text style={styles.warning}>
              ⚠️ {expiringCount}{' '}
              {expiringCount === 1
                ? 'Bestellung läuft innerhalb von 24 Stunden ab'
                : 'Bestellungen laufen innerhalb von 24 Stunden ab'}
            </Text>
          ) : null}
          <Pressable
            style={[styles.button, styles.pendingButton, isSyncing && styles.buttonDisabled]}
            onPress={() => void handleSync()}
            disabled={isSyncing}
            accessibilityRole="button"
            accessibilityLabel="Synchronisieren"
          >
            {isSyncing ? (
              <ActivityIndicator color={SoftColors.textInverse} size="small" />
            ) : null}
            <Text style={styles.buttonText}>
              {isSyncing ? 'Synchronisierung läuft…' : 'Synchronisieren'}
            </Text>
          </Pressable>
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    left: SoftSpacing.md,
    right: SoftSpacing.md,
    zIndex: 50,
    elevation: 8,
  },
  banner: {
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    gap: SoftSpacing.xs,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.2,
    shadowRadius: 4,
  },
  offlineBanner: {
    backgroundColor: '#5c1010',
  },
  pendingBanner: {
    backgroundColor: '#1e3a5f',
  },
  icon: {
    fontSize: 16,
    textAlign: 'center',
  },
  title: {
    ...SoftTypography.body,
    color: SoftColors.textInverse,
    fontWeight: '700',
    textAlign: 'center',
  },
  subtitle: {
    ...SoftTypography.caption,
    color: '#ffcdd2',
    textAlign: 'center',
  },
  detail: {
    ...SoftTypography.caption,
    color: SoftColors.textInverse,
    textAlign: 'center',
  },
  warning: {
    ...SoftTypography.caption,
    color: '#ffe082',
    fontWeight: '600',
    textAlign: 'center',
  },
  button: {
    marginTop: SoftSpacing.xs,
    minHeight: 40,
    borderRadius: SoftRadius.sm,
    backgroundColor: 'rgba(255,255,255,0.2)',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.md,
  },
  pendingButton: {
    backgroundColor: SoftColors.accent,
  },
  buttonDisabled: {
    opacity: 0.7,
  },
  buttonText: {
    ...SoftTypography.caption,
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
});
