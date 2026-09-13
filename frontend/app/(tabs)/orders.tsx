import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { SoftColors, SoftSpacing } from '../../constants/SoftTheme';
import { useAuth } from '../../contexts/AuthContext';
import { useCart } from '../../contexts/CartContext';
import { usePosPermissions } from '../../hooks/usePosPermissions';
import {
  listPreorders,
  updatePreorderStatus,
  type PreorderDto,
  type PreorderStatus,
} from '../../services/api/preorderService';
import { formatUserDate } from '../../utils/dateFormatter';
import { formatPrice } from '../../utils/formatPrice';
import { hasPermission } from '../../utils/posPermissions';
import { pickFirstFreeTableNumber } from '../../utils/posTableOrder';

/**
 * Waiter / cashier order list: open table carts + Neue Bestellung → Kassa + Vorbestellung pickup.
 */
export default function OrdersScreen() {
  const { t } = useTranslation(['orders', 'checkout', 'common']);
  const { user } = useAuth();
  const { canTakeOrders, canViewOrders } = usePosPermissions();
  const { cartsByTable, switchTable, activeTableId } = useCart();
  const canUpdatePreorder = hasPermission(user, 'order.update');

  const [preorders, setPreorders] = useState<PreorderDto[]>([]);
  const [preorderFilter, setPreorderFilter] = useState<PreorderStatus | ''>('');
  const [receiptQuery, setReceiptQuery] = useState('');
  const [preorderLoading, setPreorderLoading] = useState(false);

  const openTables = useMemo(() => {
    const rows: { table: number; totalItems: number }[] = [];
    for (const [tableNumStr, cartData] of Object.entries(cartsByTable)) {
      const table = Number(tableNumStr);
      const items = cartData?.items ?? [];
      const totalItems = items.reduce((s: number, i: { qty?: number }) => s + (i.qty ?? 0), 0);
      if (totalItems > 0) rows.push({ table, totalItems });
    }
    return rows.sort((a, b) => a.table - b.table);
  }, [cartsByTable]);

  const loadPreorders = useCallback(async () => {
    if (!canViewOrders) return;
    setPreorderLoading(true);
    try {
      const result = await listPreorders({
        status: preorderFilter,
        receiptNumber: receiptQuery,
        take: 50,
      });
      setPreorders(result.orders);
    } catch {
      Alert.alert(t('orders:error'), t('orders:preorder.loadFailed'));
    } finally {
      setPreorderLoading(false);
    }
  }, [canViewOrders, preorderFilter, receiptQuery, t]);

  useEffect(() => {
    void loadPreorders();
  }, [loadPreorders]);

  const handleNewOrder = useCallback(async () => {
    if (!canTakeOrders) return;
    const counts = new Map<number, number>();
    for (const row of openTables) counts.set(row.table, row.totalItems);
    const table = pickFirstFreeTableNumber(counts);
    if (table !== activeTableId) {
      await switchTable(table);
    }
    router.replace('/(tabs)/cash-register');
  }, [activeTableId, canTakeOrders, openTables, switchTable]);

  const handleOpenTable = useCallback(
    async (table: number) => {
      if (table !== activeTableId) {
        await switchTable(table);
      }
      router.replace('/(tabs)/cash-register');
    },
    [activeTableId, switchTable]
  );

  const statusLabel = (status: string) => {
    switch (status) {
      case 'ready':
        return t('orders:preorder.statusReady');
      case 'collected':
        return t('orders:preorder.statusCollected');
      case 'cancelled':
        return t('orders:preorder.statusCancelled');
      default:
        return t('orders:preorder.statusPending');
    }
  };

  const handlePickup = (row: PreorderDto) => {
    if ((row.remainingAmount ?? 0) > 0.01) {
      Alert.alert(t('orders:error'), t('orders:preorder.pickupBlockedOpen'));
      return;
    }
    Alert.alert(t('orders:preorder.pickup'), t('orders:preorder.pickupConfirm'), [
      { text: t('common:cancel'), style: 'cancel' },
      {
        text: t('orders:preorder.pickup'),
        onPress: () => {
          void (async () => {
            try {
              await updatePreorderStatus(row.id, 'collected');
              await loadPreorders();
              Alert.alert(
                t('orders:preorder.pickup'),
                t('orders:preorder.pickupSuccess', { date: formatUserDate(new Date()) })
              );
            } catch {
              Alert.alert(t('orders:error'), t('orders:preorder.loadFailed'));
            }
          })();
        },
      },
    ]);
  };

  const handleReady = async (row: PreorderDto) => {
    try {
      await updatePreorderStatus(row.id, 'ready');
      await loadPreorders();
    } catch {
      Alert.alert(t('orders:error'), t('orders:preorder.loadFailed'));
    }
  };

  const filters: { id: PreorderStatus | ''; label: string }[] = [
    { id: '', label: t('orders:preorder.filterAll') },
    { id: 'pending', label: t('orders:preorder.statusPending') },
    { id: 'ready', label: t('orders:preorder.statusReady') },
    { id: 'collected', label: t('orders:preorder.statusCollected') },
  ];

  return (
    <SafeAreaView style={styles.container} edges={['top']}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <Text style={styles.title}>{t('orders:title')}</Text>

        {canTakeOrders ? (
          <Pressable
            onPress={() => void handleNewOrder()}
            style={styles.newButton}
            accessibilityRole="button"
            accessibilityLabel={t('orders:new')}>
            <Ionicons name="add-circle-outline" size={22} color={SoftColors.textInverse} />
            <Text style={styles.newButtonText}>{t('orders:new')}</Text>
          </Pressable>
        ) : null}

        {!canViewOrders ? (
          <Text style={styles.empty}>{t('checkout:posFlow.toast.noOrderPermission')}</Text>
        ) : openTables.length === 0 ? (
          <Text style={styles.empty}>{t('orders:no_orders')}</Text>
        ) : (
          <View style={styles.list}>
            {openTables.map((row) => (
              <Pressable
                key={row.table}
                onPress={() => void handleOpenTable(row.table)}
                style={styles.row}
                accessibilityRole="button">
                <Text style={styles.rowTitle}>
                  {t('orders:table')} {row.table}
                </Text>
                <Text style={styles.rowCount}>{row.totalItems}</Text>
              </Pressable>
            ))}
          </View>
        )}

        {canViewOrders ? (
          <View style={styles.preorderBlock}>
            <Text style={styles.sectionTitle}>{t('orders:preorder.sectionTitle')}</Text>
            <View style={styles.searchRow}>
              <TextInput
                style={styles.searchInput}
                value={receiptQuery}
                onChangeText={setReceiptQuery}
                placeholder={t('orders:preorder.searchPlaceholder')}
                autoCapitalize="none"
              />
              <Pressable style={styles.searchButton} onPress={() => void loadPreorders()}>
                <Text style={styles.searchButtonText}>{t('orders:preorder.search')}</Text>
              </Pressable>
            </View>
            <View style={styles.filterRow}>
              {filters.map((f) => (
                <Pressable
                  key={f.id || 'all'}
                  onPress={() => setPreorderFilter(f.id)}
                  style={[styles.filterChip, preorderFilter === f.id && styles.filterChipActive]}>
                  <Text
                    style={[
                      styles.filterChipText,
                      preorderFilter === f.id && styles.filterChipTextActive,
                    ]}>
                    {f.label}
                  </Text>
                </Pressable>
              ))}
            </View>
            {preorderLoading ? (
              <Text style={styles.empty}>{t('common:loading')}</Text>
            ) : preorders.length === 0 ? (
              <Text style={styles.empty}>{t('orders:preorder.empty')}</Text>
            ) : (
              <View style={styles.list}>
                {preorders.map((row) => (
                  <View key={row.id} style={styles.preorderRow}>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.rowTitle}>
                        {row.preorderNumber || row.receiptNumber || row.orderId}
                      </Text>
                      <Text style={styles.preorderMeta}>
                        {statusLabel(row.status)}
                        {row.customerName ? ` · ${row.customerName}` : ''}
                        {(row.remainingAmount ?? 0) > 0.01
                          ? ` · ${t('orders:preorder.openAmount', { amount: formatPrice(row.remainingAmount ?? 0) })}`
                          : ''}
                      </Text>
                    </View>
                    {canUpdatePreorder && row.status === 'pending' ? (
                      <Pressable style={styles.secondaryAction} onPress={() => void handleReady(row)}>
                        <Text style={styles.secondaryActionText}>{t('orders:preorder.markReady')}</Text>
                      </Pressable>
                    ) : null}
                    {canUpdatePreorder && (row.status === 'pending' || row.status === 'ready') ? (
                      <Pressable style={styles.pickupButton} onPress={() => handlePickup(row)}>
                        <Text style={styles.pickupButtonText}>{t('orders:preorder.pickup')}</Text>
                      </Pressable>
                    ) : null}
                  </View>
                ))}
              </View>
            )}
          </View>
        ) : null}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },
  scroll: {
    paddingHorizontal: SoftSpacing.lg,
    paddingTop: SoftSpacing.md,
    paddingBottom: SoftSpacing.xl,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.md,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.sm,
  },
  newButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.sm,
    backgroundColor: SoftColors.accent,
    borderRadius: 8,
    paddingVertical: 14,
    marginBottom: SoftSpacing.lg,
  },
  newButtonText: {
    color: SoftColors.textInverse,
    fontSize: 16,
    fontWeight: '700',
  },
  empty: {
    fontSize: 14,
    color: SoftColors.textMuted,
    lineHeight: 20,
  },
  list: {
    gap: SoftSpacing.sm,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: 8,
    paddingVertical: 14,
    paddingHorizontal: 16,
    backgroundColor: SoftColors.bgCard,
  },
  rowTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  rowCount: {
    fontSize: 14,
    fontWeight: '700',
    color: SoftColors.accentDark,
  },
  preorderBlock: {
    marginTop: SoftSpacing.xl,
  },
  searchRow: {
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
  },
  searchInput: {
    flex: 1,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    backgroundColor: SoftColors.bgCard,
    color: SoftColors.textPrimary,
  },
  searchButton: {
    backgroundColor: SoftColors.accent,
    borderRadius: 8,
    paddingHorizontal: 14,
    justifyContent: 'center',
  },
  searchButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  filterRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: SoftSpacing.md,
  },
  filterChip: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: 16,
    paddingHorizontal: 10,
    paddingVertical: 6,
  },
  filterChipActive: {
    backgroundColor: SoftColors.accent,
    borderColor: SoftColors.accent,
  },
  filterChipText: {
    color: SoftColors.textPrimary,
    fontSize: 12,
  },
  filterChipTextActive: {
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  preorderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: 8,
    paddingVertical: 12,
    paddingHorizontal: 12,
    backgroundColor: SoftColors.bgCard,
  },
  preorderMeta: {
    fontSize: 12,
    color: SoftColors.textMuted,
    marginTop: 2,
  },
  secondaryAction: {
    paddingHorizontal: 8,
    paddingVertical: 6,
  },
  secondaryActionText: {
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
  pickupButton: {
    backgroundColor: SoftColors.accent,
    borderRadius: 6,
    paddingHorizontal: 10,
    paddingVertical: 8,
  },
  pickupButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '700',
    fontSize: 12,
  },
});
