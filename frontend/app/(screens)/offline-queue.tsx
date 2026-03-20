/**
 * Offline payment queue management — list, filter, retry single, sync all, copy queue ID.
 * Operator visibility and recovery UX; does not change fiscal/offline intent model.
 */

import React, { useCallback, useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  RefreshControl,
  Share,
} from 'react-native';
import * as Clipboard from 'expo-clipboard';
import { useRouter } from 'expo-router';
import {
  getAllQueueEntries,
  syncPendingPaymentQueue,
  retrySinglePending,
  type PendingPaymentEntry,
  type OfflineTransactionStatus,
} from '../../services/payment/pendingPaymentQueue';

const FILTER_ALL = 'All';
const FILTER_PENDING = 'Pending';
const FILTER_FAILED = 'Failed';
const RKSV_HANDOFF_PREFIX = 'RKSV_HANDOFF_V1';

function formatDate(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleString('de-DE', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

/** User-friendly status and error text (no raw NON_FISCAL_PENDING / codes only). */
function getStatusLabel(status: OfflineTransactionStatus): string {
  switch (status) {
    case 'Pending':
      return 'In Warteschlange';
    case 'Synced':
      return 'Synchronisiert';
    case 'Failed':
      return 'Fehlgeschlagen';
    default:
      return status ?? '—';
  }
}

function getStatusColor(status: OfflineTransactionStatus): string {
  switch (status) {
    case 'Synced':
      return '#22c55e';
    case 'Pending':
      return '#eab308';
    case 'Failed':
      return '#ef4444';
    default:
      return '#6b7280';
  }
}

/** Map raw lastError to short user-friendly message. */
function getErrorSummary(lastError: string | undefined): string {
  if (!lastError) return '—';
  if (lastError.includes('sync_transport_failed') || lastError.includes('Network')) {
    return 'Verbindung fehlgeschlagen';
  }
  if (lastError.toLowerCase().includes('duplicate') || lastError.toLowerCase().includes('already')) {
    return 'Bereits übertragen / Duplikat';
  }
  if (lastError.includes('NON_FISCAL_PENDING') || lastError.includes('offline')) {
    return 'Noch nicht an Server gesendet';
  }
  if (lastError.length > 60) return lastError.slice(0, 57) + '…';
  return lastError;
}

type MobileIncidentHandoffPayload = {
  source: 'mobile-offline-queue';
  correlationId: string;
  generatedAt: string;
  adminPath: string;
  queueHint?: string;
};

function buildIncidentHandoffPayload(correlationId: string, queueId?: string): string {
  const payload: MobileIncidentHandoffPayload = {
    source: 'mobile-offline-queue',
    correlationId,
    generatedAt: new Date().toISOString(),
    adminPath: `/rksv/incident?correlationId=${encodeURIComponent(correlationId)}`,
    queueHint: queueId,
  };
  return `${RKSV_HANDOFF_PREFIX}:${JSON.stringify(payload)}`;
}

export default function OfflineQueueScreen() {
  const router = useRouter();
  const [entries, setEntries] = useState<PendingPaymentEntry[]>([]);
  const [filter, setFilter] = useState<string>(FILTER_ALL);
  const [loading, setLoading] = useState(true);
  const [syncing, setSyncing] = useState(false);
  const [retryingId, setRetryingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const all = await getAllQueueEntries();
      setEntries(all);
    } catch (e) {
      console.warn('[OfflineQueue] Load failed:', e);
      setEntries([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const filtered =
    filter === FILTER_ALL
      ? entries
      : entries.filter((e) => e.status === filter);

  const handleSyncAll = useCallback(async () => {
    setSyncing(true);
    try {
      const { processed, failed } = await syncPendingPaymentQueue();
      await load();
      if (processed > 0 || failed > 0) {
        Alert.alert(
          'Synchronisation',
          `${processed} Zahlung(en) synchronisiert.${failed > 0 ? ` ${failed} fehlgeschlagen.` : ''}`
        );
      } else {
        Alert.alert('Hinweis', 'Keine ausstehenden Zahlungen in der Warteschlange.');
      }
    } catch (e) {
      Alert.alert('Fehler', e instanceof Error ? e.message : 'Sync fehlgeschlagen');
    } finally {
      setSyncing(false);
    }
  }, [load]);

  const handleRetrySingle = useCallback(
    async (queueId: string) => {
      setRetryingId(queueId);
      try {
        const { processed, failed } = await retrySinglePending(queueId);
        await load();
        if (processed > 0) {
          Alert.alert('Erfolg', 'Zahlung wurde synchronisiert.');
        } else if (failed > 0) {
          Alert.alert('Hinweis', 'Synchronisation fehlgeschlagen. Bitte Fehlermeldung prüfen.');
        }
      } catch (e) {
        Alert.alert('Fehler', e instanceof Error ? e.message : 'Retry fehlgeschlagen');
      } finally {
        setRetryingId(null);
      }
    },
    [load]
  );

  const copyToClipboard = useCallback(async (label: string, value: string) => {
    const v = value.trim();
    if (!v) return;
    try {
      await Clipboard.setStringAsync(v);
      Alert.alert('Kopiert', `${label} wurde in die Zwischenablage kopiert.`);
    } catch {
      try {
        await Share.share({ message: v, title: label });
      } catch {
        Alert.alert(label, v);
      }
    }
  }, []);

  const handleCopyId = useCallback(
    async (queueId: string) => {
      await copyToClipboard('Offline-Queue-ID', queueId);
    },
    [copyToClipboard]
  );

  const handleShareReplayBatchId = useCallback(
    async (batchId: string, queueId?: string) => {
      const v = batchId.trim();
      if (!v) return;
      const handoffPayload = buildIncidentHandoffPayload(v, queueId);
      try {
        await Share.share({
          message:
            `Replay-Batch-Correlation-ID (Support): ${v}\n` +
            `Admin Incident Path: /rksv/incident?correlationId=${encodeURIComponent(v)}\n` +
            `${handoffPayload}`,
          title: 'Support: Replay-Batch',
        });
      } catch {
        await copyToClipboard('Support-Handoff', handoffPayload);
      }
    },
    [copyToClipboard]
  );

  const pendingCount = entries.filter((e) => e.status === 'Pending').length;
  const failedCount = entries.filter((e) => e.status === 'Failed').length;

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backBtn}>
          <Text style={styles.backText}>← Zurück</Text>
        </TouchableOpacity>
        <Text style={styles.title}>Offline-Warteschlange</Text>
      </View>

      <View style={styles.toolbar}>
        <View style={styles.supportBanner}>
          <Text style={styles.supportBannerTitle}>Support-Hinweis</Text>
          <Text style={styles.supportBannerText}>
            Die Replay-Batch-Correlation-ID verknüpft einen Server-Replay mit Zahlungen und Logs. Bitte diese ID
            (und nach erfolgreicher Sync die Payment-ID) an den Support melden — schneller als nur die lokale
            Queue-ID.
          </Text>
        </View>
        <View style={styles.filterRow}>
          {[FILTER_ALL, FILTER_PENDING, FILTER_FAILED].map((f) => (
            <TouchableOpacity
              key={f}
              style={[styles.filterChip, filter === f && styles.filterChipActive]}
              onPress={() => setFilter(f)}
            >
              <Text style={[styles.filterChipText, filter === f && styles.filterChipTextActive]}>
                {f === FILTER_ALL ? 'Alle' : f === FILTER_PENDING ? `Ausstehend (${pendingCount})` : `Fehlgeschlagen (${failedCount})`}
              </Text>
            </TouchableOpacity>
          ))}
        </View>
        <TouchableOpacity
          style={[styles.syncAllBtn, syncing && styles.syncAllBtnDisabled]}
          onPress={handleSyncAll}
          disabled={syncing || pendingCount === 0}
        >
          {syncing ? (
            <ActivityIndicator size="small" color="#fff" />
          ) : (
            <Text style={styles.syncAllText}>Alle synchronisieren</Text>
          )}
        </TouchableOpacity>
      </View>

      {loading ? (
        <View style={styles.centered}>
          <ActivityIndicator size="large" color="#007AFF" />
          <Text style={styles.loadingText}>Lade Warteschlange…</Text>
        </View>
      ) : filtered.length === 0 ? (
        <View style={styles.centered}>
          <Text style={styles.emptyText}>
            {filter === FILTER_ALL
              ? 'Keine Einträge in der Offline-Warteschlange.'
              : `Keine Einträge mit Status „${filter === FILTER_PENDING ? 'Ausstehend' : 'Fehlgeschlagen'}“.`}
          </Text>
        </View>
      ) : (
        <ScrollView
          style={styles.list}
          refreshControl={
            <RefreshControl refreshing={false} onRefresh={load} />
          }
        >
          {filtered.map((entry) => (
            <View key={entry.queueId} style={styles.card}>
              <View style={styles.cardRow}>
                <Text style={styles.cardDate}>{formatDate(entry.createdAt)}</Text>
                <View style={[styles.badge, { backgroundColor: getStatusColor(entry.status) }]}>
                  <Text style={styles.badgeText}>{getStatusLabel(entry.status)}</Text>
                </View>
              </View>
              <View style={styles.cardRow}>
                <Text style={styles.cardAmount}>
                  € {Number(entry.paymentRequest?.totalAmount ?? 0).toFixed(2)}
                </Text>
                {entry.syncedPaymentId && (
                  <Text style={styles.syncedId} numberOfLines={1}>
                    ID: {entry.syncedPaymentId.slice(0, 8)}…
                  </Text>
                )}
              </View>
              {entry.lastAttemptAt && (
                <Text style={styles.meta}>Letzter Versuch: {formatDate(entry.lastAttemptAt)}</Text>
              )}
              {entry.lastError && (
                <Text style={styles.errorText}>{getErrorSummary(entry.lastError)}</Text>
              )}
              {entry.replayBatchCorrelationId ? (
                <View style={styles.metaRow}>
                  <Text style={styles.metaLabel}>Replay-Batch-Correlation-ID (Support, beste Korrelation):</Text>
                  <Text style={styles.metaId} selectable>
                    {entry.replayBatchCorrelationId}
                  </Text>
                  <View style={styles.batchActions}>
                    <TouchableOpacity
                      style={styles.copySmallBtn}
                      onPress={() => copyToClipboard('Replay-Batch-ID', entry.replayBatchCorrelationId!)}
                    >
                      <Text style={styles.copySmallBtnText}>Kopieren</Text>
                    </TouchableOpacity>
                    <TouchableOpacity
                      style={styles.copySmallBtn}
                      onPress={() => handleShareReplayBatchId(entry.replayBatchCorrelationId!, entry.queueId)}
                    >
                      <Text style={styles.copySmallBtnText}>Teilen</Text>
                    </TouchableOpacity>
                  </View>
                </View>
              ) : (
                <Text style={styles.metaMuted}>
                  Replay-Batch-ID erscheint nach einem Replay-Versuch (Sync), sobald der Server eine Batch-ID
                  liefert.
                </Text>
              )}
              <View style={styles.actions}>
                {(entry.status === 'Pending' || entry.status === 'Failed') && (
                  <TouchableOpacity
                    style={styles.retryBtn}
                    onPress={() => handleRetrySingle(entry.queueId)}
                    disabled={retryingId === entry.queueId}
                  >
                    {retryingId === entry.queueId ? (
                      <ActivityIndicator size="small" color="#fff" />
                    ) : (
                      <Text style={styles.retryBtnText}>Erneut senden</Text>
                    )}
                  </TouchableOpacity>
                )}
                <TouchableOpacity
                  style={styles.copyBtn}
                  onPress={() => handleCopyId(entry.queueId)}
                >
                  <Text style={styles.copyBtnText}>Queue-ID</Text>
                </TouchableOpacity>
              </View>
            </View>
          ))}
        </ScrollView>
      )}
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
    marginBottom: 10,
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
  filterRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 10,
  },
  filterChip: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
    backgroundColor: '#e5e7eb',
  },
  filterChipActive: {
    backgroundColor: '#007AFF',
  },
  filterChipText: {
    fontSize: 13,
    color: '#374151',
  },
  filterChipTextActive: {
    color: '#fff',
  },
  syncAllBtn: {
    backgroundColor: '#007AFF',
    paddingVertical: 10,
    borderRadius: 8,
    alignItems: 'center',
  },
  syncAllBtnDisabled: {
    opacity: 0.7,
  },
  syncAllText: {
    color: '#fff',
    fontSize: 15,
    fontWeight: '600',
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  loadingText: {
    marginTop: 8,
    fontSize: 14,
    color: '#6b7280',
  },
  emptyText: {
    fontSize: 14,
    color: '#6b7280',
    textAlign: 'center',
  },
  list: {
    flex: 1,
  },
  card: {
    margin: 12,
    marginBottom: 0,
    padding: 14,
    backgroundColor: '#fff',
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#e5e7eb',
  },
  cardRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 4,
  },
  cardDate: {
    fontSize: 13,
    color: '#6b7280',
  },
  badge: {
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 6,
  },
  badgeText: {
    fontSize: 12,
    color: '#fff',
    fontWeight: '600',
  },
  cardAmount: {
    fontSize: 17,
    fontWeight: '700',
    color: '#111',
  },
  syncedId: {
    fontSize: 11,
    color: '#6b7280',
    flex: 1,
    marginLeft: 8,
  },
  meta: {
    fontSize: 11,
    color: '#9ca3af',
    marginTop: 4,
  },
  metaRow: {
    marginTop: 6,
    paddingTop: 6,
    borderTopWidth: 1,
    borderTopColor: '#f3f4f6',
  },
  metaLabel: {
    fontSize: 11,
    color: '#6b7280',
    marginBottom: 2,
  },
  metaId: {
    fontSize: 11,
    color: '#374151',
    fontFamily: 'monospace',
  },
  metaMuted: {
    fontSize: 11,
    color: '#9ca3af',
    marginTop: 6,
    fontStyle: 'italic',
  },
  batchActions: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 6,
  },
  copySmallBtn: {
    backgroundColor: '#3b82f6',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 6,
  },
  copySmallBtnText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: '600',
  },
  errorText: {
    fontSize: 12,
    color: '#dc2626',
    marginTop: 4,
  },
  actions: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 10,
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
  copyBtn: {
    backgroundColor: '#6b7280',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 6,
  },
  copyBtnText: {
    color: '#fff',
    fontSize: 13,
  },
});
