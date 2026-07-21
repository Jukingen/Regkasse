// Soft minimal table selector component
import React from 'react';
import { useTranslation } from 'react-i18next';
import { View, Text, Pressable, ScrollView, StyleSheet, Vibration } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { TableSelectorTile, webTablePressableOutlineOff } from './TableSelectorTile';
import {
  SoftColors,
  SoftShadows,
  SoftSpacing,
  SoftRadius,
  SoftState,
  SoftTypography,
} from '../constants/SoftTheme';

interface TableSelectorProps {
  selectedTable: number;
  onTableSelect: (tableNumber: number) => void;
  tableCarts: Map<number, any>;
  recoveryData: any;
  tableSelectionLoading: number | null;
  onClearAllTables: () => void;
}

export const TableSelector: React.FC<TableSelectorProps> = ({
  selectedTable,
  onTableSelect,
  tableCarts,
  recoveryData,
  tableSelectionLoading,
  onClearAllTables,
}) => {
  const { t } = useTranslation(['checkout']);
  const insets = useSafeAreaInsets();
  const tableNumbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

  const handleTablePress = (tableNumber: number) => {
    if (selectedTable === tableNumber) return;
    Vibration.vibrate(30);
    onTableSelect(tableNumber);
  };

  const getTableItemCount = (tableNumber: number): number => {
    const tableCart = tableCarts.get(tableNumber);
    if (tableCart !== undefined) {
      return tableCart.totalItems ?? 0;
    }
    const recoveryOrder = recoveryData?.tableOrders?.find(
      (order: any) => order.tableNumber === tableNumber
    );
    return recoveryOrder?.itemCount ?? 0;
  };

  return (
    <View
      style={styles.tableSection}
      pointerEvents="box-none"
      accessibilityRole="none"
      collapsable={false}>
      <View style={styles.sectionTitleRow}>
        <Text style={styles.stepLabel}>1</Text>
        <Text style={styles.sectionTitle} accessibilityRole="header">
          {t('checkout:posFlow.section.table')}
        </Text>
      </View>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        style={styles.tableScroll}
        contentContainerStyle={[
          styles.tableScrollContent,
          { paddingRight: Math.max(SoftSpacing.lg, insets.right) },
        ]}>
        {tableNumbers.map((tableNumber) => {
          const itemCount = getTableItemCount(tableNumber);
          const isSelected = selectedTable === tableNumber;
          const isLoading = tableSelectionLoading === tableNumber;

          return (
            <TableSelectorTile
              key={tableNumber}
              tableNumber={tableNumber}
              itemCount={itemCount}
              isSelected={isSelected}
              isLoading={isLoading}
              onPress={() => {
                handleTablePress(tableNumber);
              }}
            />
          );
        })}

        <Pressable
          style={({ pressed }) => [
            styles.clearAllButton,
            webTablePressableOutlineOff,
            pressed && styles.clearAllButtonPressed,
          ]}
          onPress={onClearAllTables}
          hitSlop={{ top: 12, bottom: 12, left: 12, right: 12 }}
          accessibilityLabel={t('checkout:posFlow.tableSelector.a11yClearAll')}
          accessibilityRole="button">
          <Text style={styles.clearAllEmoji}>🧹</Text>
          <Text style={styles.clearAllText}>{t('checkout:posFlow.tableSelector.clearAll')}</Text>
        </Pressable>
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  tableSection: {
    backgroundColor: SoftColors.bgCard,
    paddingVertical: SoftSpacing.md,
    paddingHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
    zIndex: 1,
    elevation: 1,
  },
  sectionTitleRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: SoftSpacing.xs,
    marginBottom: SoftSpacing.md,
  },
  stepLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    width: 14,
  },
  sectionTitle: {
    ...SoftTypography.h2,
    color: SoftColors.textPrimary,
    flex: 1,
  },
  tableScroll: {
    flexDirection: 'row',
  },
  tableScrollContent: {
    alignItems: 'flex-end',
    paddingBottom: 2,
    paddingRight: SoftSpacing.lg,
  },
  clearAllButton: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    marginLeft: SoftSpacing.md,
    borderRadius: SoftRadius.lg,
    backgroundColor: SoftColors.errorBg,
    borderWidth: 1,
    borderColor: SoftColors.error,
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 92,
    minWidth: 72,
    ...SoftShadows.sm,
  },
  clearAllButtonPressed: SoftState.pressed,
  clearAllEmoji: {
    fontSize: 20,
    marginBottom: SoftSpacing.xs,
  },
  clearAllText: {
    ...SoftTypography.caption,
    fontWeight: '600',
    color: SoftColors.error,
    textAlign: 'center',
  },
});
