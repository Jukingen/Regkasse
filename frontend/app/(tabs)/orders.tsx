import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import React, { useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { SoftColors, SoftSpacing } from '../../constants/SoftTheme';
import { useCart } from '../../contexts/CartContext';
import { usePosPermissions } from '../../hooks/usePosPermissions';
import { pickFirstFreeTableNumber } from '../../utils/posTableOrder';

/**
 * Waiter / cashier order list: open table carts + Neue Bestellung → Kassa.
 */
export default function OrdersScreen() {
  const { t } = useTranslation(['orders', 'checkout', 'common']);
  const { canTakeOrders, canViewOrders } = usePosPermissions();
  const { cartsByTable, switchTable, activeTableId } = useCart();

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

  return (
    <SafeAreaView style={styles.container} edges={['top']}>
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
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
    paddingHorizontal: SoftSpacing.lg,
    paddingTop: SoftSpacing.md,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.md,
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
});
