import React, { useEffect, useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { TAB_BAR_HEIGHT } from '../constants/breakpoints';
import { useOfflineOrderManager } from '../hooks/useOfflineOrderManager';
import { eventEmitter } from '../utils/eventEmitter';

type SyncProgress = {
  current: number;
  total: number;
};

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

function SyncProgressBar({ current, total }: SyncProgress) {
  const percent = total > 0 ? Math.round((current / total) * 100) : 0;

  return (
    <View style={styles.progressContainer} accessibilityRole="progressbar">
      <View style={styles.progressTrack}>
        <View style={[styles.progressFill, { width: `${percent}%` }]} />
      </View>
      <Text style={styles.progressLabel}>
        {current} / {total} ({percent}%)
      </Text>
    </View>
  );
}

/**
 * Status-only banner for offline / pending orders.
 * Manual sync lives on the Settings screen (less distraction during checkout).
 */
export function OfflineBanner() {
  const insets = useSafeAreaInsets();
  const { status, getPending } = useOfflineOrderManager();
  const [expiringCount, setExpiringCount] = useState(0);
  const [syncProgress, setSyncProgress] = useState<SyncProgress>({ current: 0, total: 0 });

  useEffect(() => {
    const onProgress = (progress: SyncProgress) => {
      setSyncProgress(progress);
    };

    const onStatusChange = (next: { isSyncing: boolean }) => {
      if (!next.isSyncing) {
        setSyncProgress({ current: 0, total: 0 });
      }
    };

    eventEmitter.on('sync:progress', onProgress);
    eventEmitter.on('sync:status', onStatusChange);

    return () => {
      eventEmitter.off('sync:progress', onProgress);
      eventEmitter.off('sync:status', onStatusChange);
    };
  }, []);

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

  if (!status) {
    return null;
  }

  const isSyncing = status.isSyncing;
  const showSyncProgress = isSyncing && syncProgress.total > 0;
  const showFullBanner = !status.isOnline || status.pendingCount > 0 || isSyncing;

  if (!showFullBanner) {
    return null;
  }

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
          {showSyncProgress ? (
            <SyncProgressBar current={syncProgress.current} total={syncProgress.total} />
          ) : null}
          <Text style={styles.hint}>Synchronisierung unter Einstellungen</Text>
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
          {showSyncProgress ? (
            <SyncProgressBar current={syncProgress.current} total={syncProgress.total} />
          ) : null}
          <Text style={styles.hint}>Synchronisierung unter Einstellungen</Text>
        </View>
      ) : null}

      {status.isOnline && status.pendingCount === 0 && isSyncing ? (
        <View style={[styles.banner, styles.pendingBanner]}>
          {showSyncProgress ? (
            <SyncProgressBar current={syncProgress.current} total={syncProgress.total} />
          ) : (
            <Text style={styles.title}>Synchronisiere…</Text>
          )}
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
  hint: {
    ...SoftTypography.caption,
    color: 'rgba(255,255,255,0.85)',
    textAlign: 'center',
    marginTop: SoftSpacing.xs,
  },
  progressContainer: {
    gap: SoftSpacing.xs,
  },
  progressTrack: {
    height: 6,
    borderRadius: SoftRadius.sm,
    backgroundColor: 'rgba(255,255,255,0.25)',
    overflow: 'hidden',
  },
  progressFill: {
    height: '100%',
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.textInverse,
  },
  progressLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textInverse,
    textAlign: 'center',
    fontWeight: '600',
  },
});
