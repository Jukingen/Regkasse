import React, { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

import { OfflineQueueLimitBar } from './OfflineQueueLimitBar';
import type { OfflineQueueListItem } from '../services/offline/offlineQueueSnapshot';
import { formatUserDateTime } from '../utils/dateFormatter';
import {
  formatRetryCountdownLabel,
  formatRetryCountdownMs,
} from '../utils/offlineQueueIndicator';

export type OfflineQueuePanelProps = {
  items: OfflineQueueListItem[];
  lastSuccessfulSyncAt: Date | null;
  nextRetryAt: Date | null;
  now?: Date;
  syncing?: boolean;
  offlineUsed?: number;
  offlineLimit?: number;
  onRetry: (item: OfflineQueueListItem) => void;
  onDelete: (item: OfflineQueueListItem) => void;
  onRetryAll: () => void;
};

function statusKey(status: OfflineQueueListItem['status']): string {
  switch (status) {
    case 'synced':
      return 'settings:offlineQueueScreen.statusSynced';
    case 'failed':
      return 'settings:offlineQueueScreen.statusFailed';
    case 'unknown':
      return 'settings:offlineQueueScreen.statusUnknown';
    default:
      return 'settings:offlineQueueScreen.statusPending';
  }
}

export function OfflineQueuePanel({
  items,
  lastSuccessfulSyncAt,
  nextRetryAt,
  now,
  syncing = false,
  offlineUsed = 0,
  offlineLimit = 50,
  onRetry,
  onDelete,
  onRetryAll,
}: OfflineQueuePanelProps) {
  const { t } = useTranslation(['settings', 'offline']);
  const clock = now ?? new Date();
  const countdown = useMemo(
    () => formatRetryCountdownLabel(formatRetryCountdownMs(nextRetryAt, clock)),
    [clock, nextRetryAt]
  );
  const lastSyncLabel = lastSuccessfulSyncAt
    ? formatUserDateTime(lastSuccessfulSyncAt, { includeSeconds: true })
    : t('settings:offlineQueueScreen.lastSuccessfulSyncNever');
  const retryableCount = items.filter((row) => row.canRetry).length;

  return (
    <View style={styles.wrap} testID="offline-queue-panel">
      <View style={styles.syncBox}>
        <OfflineQueueLimitBar
          current={offlineUsed}
          limit={offlineLimit}
          label={t('offline:limit.used', {
            current: offlineUsed,
            limit: offlineLimit,
          })}
        />
        <Text style={styles.syncLine}>
          {t('settings:offlineQueueScreen.lastSuccessfulSync', { time: lastSyncLabel })}
        </Text>
        <Text style={styles.syncLine}>
          {countdown
            ? t('settings:offlineQueueScreen.nextRetry', { time: countdown })
            : t('settings:offlineQueueScreen.nextRetryNone')}
        </Text>
      </View>
      <Pressable
        style={[styles.retryAll, (syncing || retryableCount === 0) && styles.retryAllDisabled]}
        onPress={onRetryAll}
        disabled={syncing || retryableCount === 0}
        accessibilityRole="button"
        accessibilityLabel={t('settings:offlineQueueScreen.retryAll')}
      >
        <Text style={styles.retryAllText}>{t('settings:offlineQueueScreen.retryAll')}</Text>
      </Pressable>
      {items.length === 0 ? (
        <Text style={styles.empty}>{t('settings:offlineQueueScreen.emptyAll')}</Text>
      ) : (
        <ScrollView style={styles.list}>
          {items.map((item) => (
            <View
              key={`${item.source}-${item.id}`}
              style={styles.card}
              accessibilityLabel={`${item.id} ${formatUserDateTime(item.timestamp, { includeSeconds: true })}`}
            >
              <View style={styles.row}>
                <Text style={styles.id} numberOfLines={1}>
                  {item.id}
                </Text>
                <Text style={styles.status}>{t(statusKey(item.status))}</Text>
              </View>
              <View style={styles.row}>
                <Text style={styles.meta}>
                  {formatUserDateTime(item.timestamp, { includeSeconds: true })}
                </Text>
                <Text style={styles.amount}>€ {item.amount.toFixed(2)}</Text>
              </View>
              <Text style={styles.source}>
                {item.source === 'order'
                  ? t('settings:offlineQueueScreen.sourceOrder')
                  : t('settings:offlineQueueScreen.sourceTransaction')}
              </Text>
              {item.lastError ? <Text style={styles.error}>{item.lastError}</Text> : null}
              <View style={styles.actions}>
                {item.canRetry ? (
                  <Pressable
                    style={styles.retryBtn}
                    onPress={() => onRetry(item)}
                    accessibilityRole="button"
                    accessibilityLabel={t('settings:offlineQueueScreen.retrySend')}
                  >
                    <Text style={styles.retryBtnText}>
                      {t('settings:offlineQueueScreen.retrySend')}
                    </Text>
                  </Pressable>
                ) : null}
                {item.canDelete ? (
                  <Pressable
                    style={styles.deleteBtn}
                    onPress={() => onDelete(item)}
                    accessibilityRole="button"
                    accessibilityLabel={t('settings:offlineQueueScreen.deleteItem')}
                  >
                    <Text style={styles.deleteBtnText}>
                      {t('settings:offlineQueueScreen.deleteItem')}
                    </Text>
                  </Pressable>
                ) : null}
              </View>
            </View>
          ))}
        </ScrollView>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flex: 1,
  },
  syncBox: {
    backgroundColor: '#eff6ff',
    borderRadius: 8,
    padding: 10,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: '#bfdbfe',
  },
  syncLine: {
    fontSize: 12,
    color: '#1e40af',
    marginBottom: 2,
  },
  retryAll: {
    backgroundColor: '#007AFF',
    paddingVertical: 10,
    borderRadius: 8,
    alignItems: 'center',
    marginBottom: 10,
  },
  retryAllDisabled: {
    opacity: 0.6,
  },
  retryAllText: {
    color: '#fff',
    fontSize: 15,
    fontWeight: '600',
  },
  empty: {
    fontSize: 14,
    color: '#6b7280',
    textAlign: 'center',
    padding: 24,
  },
  list: {
    flex: 1,
  },
  card: {
    marginBottom: 12,
    padding: 14,
    backgroundColor: '#fff',
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#e5e7eb',
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 4,
    gap: 8,
  },
  id: {
    flex: 1,
    fontSize: 12,
    color: '#374151',
    fontFamily: 'monospace',
  },
  status: {
    fontSize: 12,
    fontWeight: '600',
    color: '#111',
  },
  meta: {
    fontSize: 12,
    color: '#6b7280',
  },
  amount: {
    fontSize: 16,
    fontWeight: '700',
    color: '#111',
  },
  source: {
    fontSize: 11,
    color: '#9ca3af',
    marginBottom: 8,
  },
  error: {
    fontSize: 12,
    color: '#dc2626',
    marginBottom: 8,
  },
  actions: {
    flexDirection: 'row',
    gap: 8,
  },
  retryBtn: {
    backgroundColor: '#22c55e',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 6,
  },
  retryBtnText: {
    color: '#fff',
    fontSize: 13,
    fontWeight: '600',
  },
  deleteBtn: {
    backgroundColor: '#6b7280',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 6,
  },
  deleteBtnText: {
    color: '#fff',
    fontSize: 13,
    fontWeight: '600',
  },
});
