/**
 * Offline queue — operator list for local `offline_orders` + `offline_transactions`.
 * Does not change queue persistence. Fiscal items cannot be deleted.
 */

import { useFocusEffect, useRouter } from 'expo-router';
import React, { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, StyleSheet, Text, TouchableOpacity, View } from 'react-native';

import { OfflineQueuePanel } from '../../components/OfflineQueuePanel';
import { useOfflineQueueIndicator } from '../../hooks/useOfflineQueueIndicator';
import { getOfflineOrderManager } from '../../services/offline/offlineOrderManager';
import type { OfflineQueueListItem } from '../../services/offline/offlineQueueSnapshot';
import {
  removePendingByQueueId,
  retrySinglePending,
  syncPendingPaymentQueue,
} from '../../services/payment/pendingPaymentQueue';

export default function OfflineQueueScreen() {
  const router = useRouter();
  const { t } = useTranslation(['settings', 'common']);
  const { items, lastSuccessfulSyncAt, nextRetryAt, isSyncing, tenantLimit, refresh } =
    useOfflineQueueIndicator();
  const [busy, setBusy] = useState(false);

  useFocusEffect(
    useCallback(() => {
      refresh();
    }, [refresh])
  );

  const handleRetry = useCallback(
    async (item: OfflineQueueListItem) => {
      setBusy(true);
      try {
        if (item.source === 'order') {
          const result = await getOfflineOrderManager().retryOrderById(item.id);
          if (!result.success) {
            Alert.alert(
              t('settings:offlineQueueScreen.retryFailedTitle'),
              result.message || t('settings:offlineQueueScreen.retryFailedMessage')
            );
          }
        } else {
          const { processed, failed } = await retrySinglePending(item.id);
          if (processed === 0 && failed > 0) {
            Alert.alert(
              t('settings:offlineQueueScreen.retryFailedTitle'),
              t('settings:offlineQueueScreen.retryFailedMessage')
            );
          }
        }
      } catch (e) {
        Alert.alert(
          t('settings:offlineQueueScreen.syncErrorTitle'),
          e instanceof Error ? e.message : String(e)
        );
      } finally {
        setBusy(false);
        refresh();
      }
    },
    [refresh, t]
  );

  const handleDelete = useCallback(
    (item: OfflineQueueListItem) => {
      if (!item.canDelete) {
        Alert.alert(
          t('settings:offlineQueueScreen.deleteFiscalBlockedTitle'),
          t('settings:offlineQueueScreen.deleteFiscalBlocked')
        );
        return;
      }
      Alert.alert(
        t('settings:offlineQueueScreen.deleteConfirmTitle'),
        t('settings:offlineQueueScreen.deleteConfirmMessage'),
        [
          { text: t('common:cancel'), style: 'cancel' },
          {
            text: t('settings:offlineQueueScreen.deleteItem'),
            style: 'destructive',
            onPress: () => {
              void (async () => {
                await removePendingByQueueId(item.id);
                refresh();
              })();
            },
          },
        ]
      );
    },
    [refresh, t]
  );

  const handleRetryAll = useCallback(async () => {
    setBusy(true);
    try {
      const orderResult = await getOfflineOrderManager().retryPendingAndFailedOrders();
      const paymentResult = await syncPendingPaymentQueue();
      const failed =
        (orderResult.details?.filter((row) => !row.success).length ?? 0) + paymentResult.failed;
      const processed =
        (orderResult.details?.filter((row) => row.success).length ?? 0) + paymentResult.processed;
      Alert.alert(
        t('settings:offlineQueueScreen.syncTitle'),
        t('settings:offlineQueueScreen.syncResult', {
          processed,
          failedSuffix:
            failed > 0 ? t('settings:offlineQueueScreen.syncFailedSuffix', { failed }) : '',
        })
      );
    } catch (e) {
      Alert.alert(
        t('settings:offlineQueueScreen.syncErrorTitle'),
        e instanceof Error ? e.message : String(e)
      );
    } finally {
      setBusy(false);
      refresh();
    }
  }, [refresh, t]);

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity
          onPress={() => {
            router.back();
          }}
          style={styles.backBtn}
        >
          <Text style={styles.backText}>{t('settings:offlineQueueScreen.back')}</Text>
        </TouchableOpacity>
        <Text style={styles.title}>{t('settings:offlineQueueScreen.title')}</Text>
      </View>
      <View style={styles.toolbar}>
        <View style={styles.supportBanner}>
          <Text style={styles.supportBannerTitle}>
            {t('settings:offlineQueueScreen.supportTitle')}
          </Text>
          <Text style={styles.supportBannerText}>
            {t('settings:offlineQueueScreen.supportText')}
          </Text>
        </View>
      </View>
      <View style={styles.panel}>
        <OfflineQueuePanel
          items={items}
          lastSuccessfulSyncAt={lastSuccessfulSyncAt}
          nextRetryAt={nextRetryAt}
          offlineUsed={tenantLimit.current}
          offlineLimit={tenantLimit.limit}
          syncing={busy || isSyncing}
          onRetry={(item) => {
            void handleRetry(item);
          }}
          onDelete={handleDelete}
          onRetryAll={() => {
            void handleRetryAll();
          }}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 12,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e5e7eb',
  },
  backBtn: {
    marginRight: 12,
  },
  backText: {
    fontSize: 16,
    color: '#007AFF',
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    color: '#111',
  },
  toolbar: {
    padding: 12,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e5e7eb',
  },
  supportBanner: {
    backgroundColor: '#eff6ff',
    borderRadius: 8,
    padding: 10,
    borderWidth: 1,
    borderColor: '#bfdbfe',
  },
  supportBannerTitle: {
    fontSize: 13,
    fontWeight: '700',
    color: '#1e3a8a',
    marginBottom: 4,
  },
  supportBannerText: {
    fontSize: 12,
    color: '#1e40af',
    lineHeight: 17,
  },
  panel: {
    flex: 1,
    padding: 12,
  },
});
