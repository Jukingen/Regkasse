import React, { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '@/constants/SoftTheme';
import { OFFLINE_CONFIG } from '@/constants/offlineConfig';
import { useOfflineStatus } from '@/hooks/useOfflineStatus';
import { eventEmitter } from '@/utils/eventEmitter';
import { OFFLINE_UI_LEVEL_COLORS, type OfflineUiLevel } from '@/utils/offlineStatusLevel';

type SyncProgress = {
  current: number;
  total: number;
};

const STATUS_LABEL_DE: Record<OfflineUiLevel, string> = {
  online: 'Online',
  warning: 'Warnung',
  critical: 'Kritisch',
  offline: 'Offline',
};

const SYNC_LABEL_DE = {
  syncing: 'Synchronisiere…',
  pending: 'Warteschlange',
  idle: 'Bereit',
  offline: 'Keine Verbindung',
} as const;

function QueueProgressBar({ progress, color }: { progress: number; color: string }) {
  const clamped = Math.max(0, Math.min(1, progress));
  const percent = Math.round(clamped * 100);

  return (
    <View style={styles.progressRow} accessibilityRole="progressbar">
      <View style={styles.progressTrack}>
        <View style={[styles.progressFill, { width: `${percent}%`, backgroundColor: color }]} />
      </View>
      <Text style={styles.progressPercent}>{percent}%</Text>
    </View>
  );
}

/**
 * Prominent POS offline order counter: capacity, remaining time, sync status.
 * Always visible; color-coded per RKSV-style limits (40 warn / 48 critical / 50 max).
 * UI copy is German (de-DE) per POS language rules.
 */
export function OfflineCounter() {
  const {
    pendingCount,
    maxLimit,
    hoursRemaining,
    remainingCapacity,
    isOnline,
    isSyncing,
    statusLevel,
    isExpiringSoon,
  } = useOfflineStatus();

  const [syncProgress, setSyncProgress] = useState<SyncProgress>({
    current: 0,
    total: 0,
  });

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

  const statusColor = OFFLINE_UI_LEVEL_COLORS[statusLevel];
  const statusText = STATUS_LABEL_DE[statusLevel];
  const capacityProgress = maxLimit > 0 ? pendingCount / maxLimit : 0;
  const showSyncQueueProgress = isSyncing && syncProgress.total > 0;
  const showCapacityDetails = !isOnline || pendingCount > 0 || isSyncing;
  const showTimeWarning =
    pendingCount > 0 && (hoursRemaining < OFFLINE_CONFIG.OFFLINE_WARNING_HOURS || !isOnline);
  const showCriticalCapacity =
    pendingCount >= OFFLINE_CONFIG.CRITICAL_PENDING_COUNT || remainingCapacity <= 2;

  const syncLabel = !isOnline
    ? SYNC_LABEL_DE.offline
    : isSyncing
      ? SYNC_LABEL_DE.syncing
      : pendingCount > 0
        ? SYNC_LABEL_DE.pending
        : SYNC_LABEL_DE.idle;

  return (
    <View
      style={[styles.container, { borderColor: statusColor }]}
      accessibilityRole="summary"
      accessibilityLabel={`Offline-Status ${statusText}, ${pendingCount} von ${maxLimit} Bestellungen`}>
      <View style={styles.header}>
        <View style={[styles.statusDot, { backgroundColor: statusColor }]} />
        <Text style={[styles.statusText, { color: statusColor }]}>{statusText}</Text>
        <Text style={styles.syncChip}>{syncLabel}</Text>
        <Text style={styles.badge}>
          {pendingCount} / {maxLimit}
        </Text>
      </View>

      {showCapacityDetails ? (
        <>
          <QueueProgressBar progress={capacityProgress} color={statusColor} />
          <Text style={styles.capacityText}>
            {remainingCapacity === 1
              ? '1 Bestellung Kapazität übrig'
              : `${remainingCapacity} Bestellungen Kapazität übrig`}
          </Text>

          {showSyncQueueProgress ? (
            <View style={styles.syncProgressBlock}>
              <Text style={styles.syncProgressLabel}>
                Sync: {syncProgress.current} / {syncProgress.total}
              </Text>
              <QueueProgressBar
                progress={syncProgress.total > 0 ? syncProgress.current / syncProgress.total : 0}
                color={SoftColors.info}
              />
            </View>
          ) : null}

          {showTimeWarning ? (
            <View style={styles.warningContainer}>
              <Text style={styles.warningText}>
                Noch ca. {hoursRemaining} {hoursRemaining === 1 ? 'Stunde' : 'Stunden'} bis Ablauf
              </Text>
              <Text style={styles.warningText}>
                Nach {OFFLINE_CONFIG.OFFLINE_EXPIRY_HOURS} Stunden werden Offline-Bestellungen
                gelöscht
              </Text>
            </View>
          ) : null}

          {isExpiringSoon && !showTimeWarning ? (
            <View style={styles.warningContainer}>
              <Text style={styles.warningText}>
                Offline-Frist läuft bald ab — bitte synchronisieren
              </Text>
            </View>
          ) : null}

          {pendingCount >= OFFLINE_CONFIG.WARNING_PENDING_COUNT && !showCriticalCapacity ? (
            <View style={styles.warningContainer}>
              <Text style={styles.warningText}>
                Warnung: Warteschlange nähert sich dem Limit ({pendingCount}/{maxLimit})
              </Text>
            </View>
          ) : null}

          {showCriticalCapacity ? (
            <View style={styles.criticalContainer}>
              <Text style={styles.criticalText}>
                KRITISCH! Nur noch {remainingCapacity}{' '}
                {remainingCapacity === 1 ? 'Bestellung' : 'Bestellungen'} möglich
              </Text>
            </View>
          ) : null}
        </>
      ) : (
        <Text style={styles.idleHint}>Offline-Warteschlange leer</Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    borderWidth: 2,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.md,
    marginHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    backgroundColor: SoftColors.bgCard,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: SoftSpacing.xs,
    gap: SoftSpacing.xs,
  },
  statusDot: {
    width: 12,
    height: 12,
    borderRadius: 6,
  },
  statusText: {
    ...SoftTypography.body,
    fontWeight: '700',
    flexShrink: 0,
  },
  syncChip: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    flex: 1,
  },
  badge: {
    backgroundColor: SoftColors.bgSecondary,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.full,
    ...SoftTypography.caption,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    overflow: 'hidden',
  },
  progressRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
  },
  progressTrack: {
    flex: 1,
    height: 8,
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.bgSecondary,
    overflow: 'hidden',
  },
  progressFill: {
    height: '100%',
    borderRadius: SoftRadius.sm,
  },
  progressPercent: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    minWidth: 36,
    textAlign: 'right',
  },
  capacityText: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    marginTop: SoftSpacing.xs,
  },
  idleHint: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
  },
  syncProgressBlock: {
    marginTop: SoftSpacing.sm,
    gap: SoftSpacing.xs,
  },
  syncProgressLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    fontWeight: '600',
  },
  warningContainer: {
    marginTop: SoftSpacing.sm,
    padding: SoftSpacing.sm,
    backgroundColor: SoftColors.warningBg,
    borderRadius: SoftRadius.sm,
    gap: 2,
  },
  warningText: {
    ...SoftTypography.caption,
    color: '#92400e',
  },
  criticalContainer: {
    marginTop: SoftSpacing.sm,
    padding: SoftSpacing.sm,
    backgroundColor: SoftColors.errorBg,
    borderRadius: SoftRadius.sm,
  },
  criticalText: {
    ...SoftTypography.body,
    fontWeight: '700',
    color: SoftColors.error,
    textAlign: 'center',
  },
});
