import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { IfVerticalFeature } from '../../components/IfVerticalFeature';
import { SoftColors, SoftRadius, SoftSpacing } from '../../constants/SoftTheme';
import { useAuth } from '../../contexts/AuthContext';
import { useKitchenHub } from '../../hooks/useKitchenHub';
import {
  listKitchenOrders,
  updateKitchenOrderItemStatus,
  updateKitchenOrderStatus,
  type KitchenOrder,
  type KitchenOrderStatus,
} from '../../services/api/kitchenOrderService';
import {
  applyKitchenOrderEvent,
  formatKitchenOrderAge,
  isKdsActiveStatus,
  nextKitchenItemStatus,
  ordersInColumn,
} from '../../services/kitchenDisplayModel';
import { syncKitchenPendingFromOrders } from '../../services/kitchenPendingStore';
import { hasPermission } from '../../utils/posPermissions';

const COLUMNS: KitchenOrderStatus[] = ['Pending', 'InPreparation', 'Ready'];

export default function KitchenDisplayScreen() {
  const { t } = useTranslation('verticalProfiles');
  const { user } = useAuth();
  const canUpdate = hasPermission(user, 'kitchen.update');
  const [orders, setOrders] = useState<KitchenOrder[]>([]);
  const [nowMs, setNowMs] = useState(() => Date.now());
  const [busyId, setBusyId] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const applyOrder = useCallback((order: KitchenOrder) => {
    setOrders((current) => applyKitchenOrderEvent(current, order));
  }, []);

  const connectionState = useKitchenHub({
    onCreated: applyOrder,
    onUpdated: applyOrder,
  });

  const reload = useCallback(async () => {
    const rows = await listKitchenOrders();
    const active = rows.filter((row) => isKdsActiveStatus(row.status));
    setOrders(active);
    syncKitchenPendingFromOrders(active);
  }, []);

  useEffect(() => {
    void reload().catch(() => {
      setOrders([]);
      syncKitchenPendingFromOrders([]);
    });
  }, [reload]);

  useEffect(() => {
    const timer = setInterval(() => setNowMs(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);

  const selected = useMemo(
    () => orders.find((row) => row.id === selectedId) ?? null,
    [orders, selectedId]
  );

  const setOrderStatus = useCallback(
    async (id: string, status: KitchenOrderStatus) => {
      setBusyId(id);
      try {
        const updated = await updateKitchenOrderStatus(id, status);
        applyOrder(updated);
      } finally {
        setBusyId(null);
      }
    },
    [applyOrder]
  );

  const toggleItem = useCallback(
    async (order: KitchenOrder, itemId: string, currentStatus: KitchenOrder['items'][number]['status']) => {
      const next = nextKitchenItemStatus(currentStatus);
      if (!next || !canUpdate) return;
      setBusyId(`${order.id}:${itemId}`);
      try {
        const updated = await updateKitchenOrderItemStatus(order.id, itemId, next);
        applyOrder(updated);
      } finally {
        setBusyId(null);
      }
    },
    [applyOrder, canUpdate]
  );

  const markColumnReady = useCallback(
    async (status: KitchenOrderStatus) => {
      const rows = ordersInColumn(orders, status).filter((row) => row.status !== 'Ready');
      for (const row of rows) {
        await setOrderStatus(row.id, 'Ready');
      }
    },
    [orders, setOrderStatus]
  );

  const connectionColor =
    connectionState === 'connected'
      ? SoftColors.success
      : connectionState === 'reconnecting'
        ? SoftColors.warning
        : SoftColors.error;
  const connectionLabel =
    connectionState === 'connected'
      ? t('screens.kitchen.connectionConnected')
      : connectionState === 'reconnecting'
        ? t('screens.kitchen.connectionReconnecting')
        : t('screens.kitchen.connectionDisconnected');

  return (
    <IfVerticalFeature
      feature="kitchenDisplay"
      fallback={
        <SafeAreaView style={styles.container}>
          <Text style={styles.empty}>{t('screens.unavailable')}</Text>
        </SafeAreaView>
      }
    >
      <SafeAreaView style={styles.container} testID="kds-screen">
        <View style={styles.header}>
          <View style={styles.headerText}>
            <Text style={styles.title}>{t('screens.kitchen.title')}</Text>
            <Text style={styles.subtitle}>{t('screens.kitchen.subtitle')}</Text>
          </View>
          <View
            testID="kds-connection"
            accessibilityRole="text"
            accessibilityLabel={connectionLabel}
            style={[styles.connectionBadge, { backgroundColor: connectionColor }]}
          />
        </View>

        <View style={styles.columns} testID="kds-columns">
          {COLUMNS.map((status) => {
            const rows = ordersInColumn(orders, status);
            return (
              <View
                key={status}
                style={styles.column}
                testID={`kds-column-${status}`}
                accessibilityLabel={columnTitle(t, status)}>
                <Text style={styles.columnTitle}>{columnTitle(t, status)}</Text>
                <Text style={styles.columnCount}>{rows.length}</Text>
                {canUpdate && status !== 'Ready' ? (
                  <Pressable
                    style={styles.bulkButton}
                    disabled={rows.length === 0 || busyId !== null}
                    onPress={() => void markColumnReady(status)}
                    accessibilityRole="button"
                    accessibilityLabel={t('screens.kitchen.markAllReady')}>
                    <Text style={styles.bulkButtonText}>{t('screens.kitchen.markAllReady')}</Text>
                  </Pressable>
                ) : null}
                <ScrollView contentContainerStyle={styles.columnBody}>
                  {rows.length === 0 ? (
                    <Text style={styles.columnEmpty}>{t('screens.kitchen.empty')}</Text>
                  ) : (
                    rows.map((order) => (
                      <KitchenOrderCard
                        key={order.id}
                        order={order}
                        nowMs={nowMs}
                        canUpdate={canUpdate}
                        busy={busyId !== null}
                        onOpen={() => setSelectedId(order.id)}
                        onToggleItem={(itemId, itemStatus) =>
                          void toggleItem(order, itemId, itemStatus)
                        }
                        t={t}
                      />
                    ))
                  )}
                </ScrollView>
              </View>
            );
          })}
        </View>

        <Modal
          visible={selected !== null}
          transparent
          animationType="fade"
          onRequestClose={() => setSelectedId(null)}>
          <Pressable style={styles.modalBackdrop} onPress={() => setSelectedId(null)}>
            <Pressable style={styles.modalCard} onPress={() => undefined}>
              {selected ? (
                <>
                  <Text style={styles.modalTitle}>{tableLabel(t, selected)}</Text>
                  <OrderAge createdAtUtc={selected.createdAtUtc} nowMs={nowMs} t={t} />
                  {selected.notes ? (
                    <Text style={styles.notes}>{selected.notes}</Text>
                  ) : null}
                  {selected.items.map((item) => (
                    <Pressable
                      key={item.id}
                      testID={`kds-item-${item.id}`}
                      style={styles.itemRow}
                      disabled={!canUpdate || !nextKitchenItemStatus(item.status)}
                      onPress={() => void toggleItem(selected, item.id, item.status)}
                      accessibilityRole="button"
                      accessibilityLabel={`${item.quantity} × ${item.productName}`}>
                      <Text style={styles.item}>
                        {item.quantity} × {item.productName}
                      </Text>
                      <Text style={styles.itemStatus}>{itemStatusLabel(t, item.status)}</Text>
                      {item.notes ? <Text style={styles.itemNotes}>{item.notes}</Text> : null}
                    </Pressable>
                  ))}
                  {canUpdate ? (
                    <View style={styles.actions}>
                      {selected.status === 'Pending' ? (
                        <StatusButton
                          label={t('screens.kitchen.markPreparing')}
                          disabled={busyId === selected.id}
                          onPress={() => void setOrderStatus(selected.id, 'InPreparation')}
                        />
                      ) : null}
                      {selected.status !== 'Ready' ? (
                        <StatusButton
                          label={t('screens.kitchen.markReady')}
                          disabled={busyId === selected.id}
                          onPress={() => void setOrderStatus(selected.id, 'Ready')}
                        />
                      ) : (
                        <StatusButton
                          label={t('screens.kitchen.markServed')}
                          disabled={busyId === selected.id}
                          onPress={() => void setOrderStatus(selected.id, 'Served')}
                        />
                      )}
                    </View>
                  ) : null}
                  <Pressable
                    style={styles.closeButton}
                    onPress={() => setSelectedId(null)}
                    accessibilityRole="button"
                    accessibilityLabel={t('screens.kitchen.close')}>
                    <Text style={styles.closeButtonText}>{t('screens.kitchen.close')}</Text>
                  </Pressable>
                </>
              ) : null}
            </Pressable>
          </Pressable>
        </Modal>
      </SafeAreaView>
    </IfVerticalFeature>
  );
}

function KitchenOrderCard({
  order,
  nowMs,
  canUpdate,
  busy,
  onOpen,
  onToggleItem,
  t,
}: {
  order: KitchenOrder;
  nowMs: number;
  canUpdate: boolean;
  busy: boolean;
  onOpen: () => void;
  onToggleItem: (itemId: string, status: KitchenOrder['items'][number]['status']) => void;
  t: (key: string, options?: Record<string, string>) => string;
}) {
  return (
    <Pressable
      testID={`kds-card-${order.id}`}
      style={styles.card}
      onPress={onOpen}
      accessibilityRole="button"
      accessibilityLabel={tableLabel(t, order)}>
      <Text style={styles.cardTitle}>{tableLabel(t, order)}</Text>
      <OrderAge createdAtUtc={order.createdAtUtc} nowMs={nowMs} t={t} />
      {order.notes ? <Text style={styles.notes}>{order.notes}</Text> : null}
      {order.items.map((item) => (
        <Pressable
          key={item.id}
          testID={`kds-item-${item.id}`}
          style={styles.itemRow}
          disabled={!canUpdate || busy || !nextKitchenItemStatus(item.status)}
          onPress={() => onToggleItem(item.id, item.status)}
          accessibilityRole="button"
          accessibilityLabel={`${item.quantity} × ${item.productName}`}>
          <Text style={styles.item}>
            {item.quantity} × {item.productName}
          </Text>
          <Text style={styles.itemStatus}>{itemStatusLabel(t, item.status)}</Text>
          {item.notes ? <Text style={styles.itemNotes}>{item.notes}</Text> : null}
        </Pressable>
      ))}
    </Pressable>
  );
}

function OrderAge({
  createdAtUtc,
  nowMs,
  t,
}: {
  createdAtUtc: string;
  nowMs: number;
  t: (key: string) => string;
}) {
  const age = formatKitchenOrderAge(createdAtUtc, nowMs);
  return (
    <Text
      testID="kds-age"
      style={[styles.age, age.overdue && styles.ageOverdue]}
      accessibilityLabel={age.overdue ? t('screens.kitchen.ageOverdue') : age.text}>
      {age.text}
    </Text>
  );
}

function StatusButton({
  label,
  disabled,
  onPress,
}: {
  label: string;
  disabled: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      style={styles.action}
      disabled={disabled}
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={label}>
      <Text style={styles.actionText}>{label}</Text>
    </Pressable>
  );
}

function tableLabel(t: (key: string, options?: Record<string, string>) => string, order: KitchenOrder): string {
  const table = order.tableNumber?.trim();
  return table ? t('screens.kitchen.table', { table }) : t('screens.kitchen.takeAway');
}

function columnTitle(t: (key: string) => string, status: KitchenOrderStatus): string {
  if (status === 'InPreparation') return t('screens.kitchen.columnPreparing');
  if (status === 'Ready') return t('screens.kitchen.columnReady');
  return t('screens.kitchen.columnPending');
}

function itemStatusLabel(t: (key: string) => string, status: string): string {
  if (status === 'Preparing') return t('screens.kitchen.itemPreparing');
  if (status === 'Ready') return t('screens.kitchen.itemReady');
  if (status === 'Served') return t('screens.kitchen.itemServed');
  return t('screens.kitchen.itemPending');
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: SoftSpacing.lg,
    paddingTop: SoftSpacing.md,
    paddingBottom: SoftSpacing.sm,
  },
  headerText: {
    flex: 1,
    gap: 4,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  subtitle: {
    color: SoftColors.textSecondary,
    lineHeight: 20,
  },
  connectionBadge: {
    width: 14,
    height: 14,
    borderRadius: 7,
    marginLeft: SoftSpacing.md,
  },
  columns: {
    flex: 1,
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    paddingBottom: SoftSpacing.md,
  },
  column: {
    flex: 1,
    backgroundColor: SoftColors.bgSecondary,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.sm,
    minWidth: 0,
  },
  columnTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  columnCount: {
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.sm,
  },
  columnBody: {
    gap: SoftSpacing.sm,
    paddingBottom: SoftSpacing.lg,
  },
  columnEmpty: {
    color: SoftColors.textMuted,
    textAlign: 'center',
    marginTop: SoftSpacing.md,
  },
  bulkButton: {
    backgroundColor: SoftColors.accent,
    borderRadius: SoftRadius.sm,
    paddingVertical: 6,
    paddingHorizontal: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
    alignItems: 'center',
  },
  bulkButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
    fontSize: 13,
  },
  card: {
    padding: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgCard,
    borderWidth: 1,
    borderColor: SoftColors.border,
    gap: 4,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  age: {
    color: SoftColors.textSecondary,
    fontWeight: '600',
    fontVariant: ['tabular-nums'],
  },
  ageOverdue: {
    color: SoftColors.error,
    fontWeight: '800',
  },
  notes: {
    backgroundColor: SoftColors.warningBg,
    color: SoftColors.textPrimary,
    padding: SoftSpacing.xs,
    borderRadius: SoftRadius.sm,
    fontWeight: '600',
  },
  itemRow: {
    gap: 2,
    paddingVertical: 2,
  },
  item: {
    color: SoftColors.textSecondary,
    fontSize: 14,
  },
  itemStatus: {
    color: SoftColors.accentDark,
    fontSize: 12,
    fontWeight: '600',
  },
  itemNotes: {
    backgroundColor: SoftColors.warningBg,
    color: SoftColors.textPrimary,
    paddingHorizontal: SoftSpacing.xs,
    borderRadius: SoftRadius.sm,
    fontSize: 12,
  },
  empty: {
    color: SoftColors.textMuted,
    textAlign: 'center',
    marginTop: SoftSpacing.xl,
  },
  modalBackdrop: {
    flex: 1,
    backgroundColor: SoftColors.overlay,
    justifyContent: 'center',
    padding: SoftSpacing.lg,
  },
  modalCard: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    gap: SoftSpacing.sm,
  },
  modalTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  actions: {
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    marginTop: SoftSpacing.sm,
  },
  action: {
    backgroundColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
  },
  actionText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
  closeButton: {
    alignSelf: 'flex-start',
    paddingVertical: SoftSpacing.sm,
  },
  closeButtonText: {
    color: SoftColors.textSecondary,
    fontWeight: '600',
  },
});
