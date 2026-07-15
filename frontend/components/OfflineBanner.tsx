import React, { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
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

type ManualSyncButtonProps = {
  isSyncing: boolean;
  onPress: () => void;
  variant?: 'default' | 'pending' | 'compact';
};

function ManualSyncButton({
  isSyncing,
  onPress,
  variant = 'default',
}: ManualSyncButtonProps) {
  const buttonStyle =
    variant === 'pending'
      ? styles.pendingButton
      : variant === 'compact'
        ? styles.compactButton
        : styles.button;

  return (
    <Pressable
      style={[buttonStyle, isSyncing && styles.buttonDisabled]}
      onPress={onPress}
      disabled={isSyncing}
      accessibilityRole="button"
      accessibilityLabel={isSyncing ? 'Synchronisierung läuft' : 'Jetzt synchronisieren'}
    >
      {isSyncing ? (
        <ActivityIndicator color={SoftColors.textInverse} size="small" />
      ) : (
        <Ionicons name="refresh" size={18} color={SoftColors.textInverse} />
      )}
      <Text style={styles.buttonText}>
        {isSyncing ? 'Synchronisiere...' : 'Jetzt synchronisieren'}
      </Text>
    </Pressable>
  );
}

export function OfflineBanner() {
  const insets = useSafeAreaInsets();
  const { status, syncNow, getPending } = useOfflineOrderManager();
  const [expiringCount, setExpiringCount] = useState(0);
  const [syncing, setSyncing] = useState(false);
  const [syncProgress, setSyncProgress] = useState<SyncProgress>({ current: 0, total: 0 });

  useEffect(() => {
    const onProgress = (progress: SyncProgress) => {
      setSyncProgress(progress);
    };

    const onStatusChange = (status: { isSyncing: boolean }) => {
      if (!status.isSyncing) {
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

  const handleManualSync = useCallback(async () => {
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

  const isSyncing = syncing || status.isSyncing;
  const showSyncProgress = isSyncing && syncProgress.total > 0;
  const showFullBanner = !status.isOnline || status.pendingCount > 0 || isSyncing;

  if (!showFullBanner) {
    return (
      <View
        style={[
          styles.container,
          { bottom: TAB_BAR_HEIGHT + insets.bottom + SoftSpacing.sm },
        ]}
        pointerEvents="box-none"
      >
        <View style={[styles.banner, styles.compactBanner]}>
          <ManualSyncButton
            isSyncing={isSyncing}
            onPress={() => void handleManualSync()}
            variant="compact"
          />
        </View>
      </View>
    );
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
          <ManualSyncButton
            isSyncing={isSyncing}
            onPress={() => void handleManualSync()}
          />
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
          <ManualSyncButton
            isSyncing={isSyncing}
            onPress={() => void handleManualSync()}
            variant="pending"
          />
        </View>
      ) : null}

      {status.isOnline && status.pendingCount === 0 && isSyncing ? (
        <View style={[styles.banner, styles.pendingBanner]}>
          {showSyncProgress ? (
            <SyncProgressBar current={syncProgress.current} total={syncProgress.total} />
          ) : null}
          <ManualSyncButton
            isSyncing={isSyncing}
            onPress={() => void handleManualSync()}
            variant="pending"
          />
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
  compactBanner: {
    backgroundColor: '#2a4a6b',
    paddingVertical: SoftSpacing.xs,
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
  compactButton: {
    minHeight: 40,
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.accent,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.md,
  },
  pendingButton: {
    marginTop: SoftSpacing.xs,
    minHeight: 40,
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.accent,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.md,
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
