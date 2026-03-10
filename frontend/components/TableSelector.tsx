// Soft minimal table selector component
import React from 'react';
import { View, Text, Pressable, ScrollView, StyleSheet, Vibration, ActivityIndicator } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { SoftColors, SoftShadows, SoftSpacing, SoftRadius, SoftState, SoftTypography } from '../constants/SoftTheme';

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
  const insets = useSafeAreaInsets();
  const tableNumbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

  const handleTablePress = (tableNumber: number) => {
    if (selectedTable === tableNumber) return;
    Vibration.vibrate(30);
    onTableSelect(tableNumber);
  };

  const getTableItemCount = (tableNumber: number): number => {
    const tableCart = tableCarts.get(tableNumber);
    // Cart varsa (boş dahil) cart kaynağı kullan; yoksa recovery (initial load için)
    if (tableCart !== undefined) {
      return tableCart.totalItems ?? 0;
    }
    const recoveryOrder = recoveryData?.tableOrders?.find(
      (order: any) => order.tableNumber === tableNumber
    );
    return recoveryOrder?.itemCount ?? 0;
  };

  return (
    <View style={styles.tableSection} pointerEvents="box-none" accessibilityRole="none" collapsable={false}>
      <View style={styles.sectionTitleRow}>
        <Text style={styles.stepLabel}>1</Text>
        <Text style={styles.sectionTitle} accessibilityRole="header">Tisch wählen</Text>
      </View>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        style={styles.tableScroll}
        contentContainerStyle={[styles.tableScrollContent, { paddingRight: Math.max(SoftSpacing.lg, insets.right) }]}
      >
        {tableNumbers.map((tableNumber) => {
          const itemCount = getTableItemCount(tableNumber);
          const hasItems = itemCount > 0;
          const isSelected = selectedTable === tableNumber;
          const isLoading = tableSelectionLoading === tableNumber;

          return (
            <Pressable
              key={tableNumber}
              style={({ pressed, focused }) => [
                styles.tableTab,
                isSelected && styles.tableTabSelected,
                hasItems && !isSelected && styles.tableTabWithItems,
                isLoading && styles.tableTabLoading,
                pressed && styles.tableTabPressed,
                focused && SoftState.focusVisible,
              ]}
              onPress={() => handleTablePress(tableNumber)}
              disabled={isLoading}
              hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
              accessibilityLabel={isSelected ? `Tisch ${tableNumber} ausgewählt` : `Tisch ${tableNumber} wählen`}
              accessibilityRole="button"
              accessibilityState={{ selected: isSelected, disabled: isLoading }}
            >
              <>
                <Text style={[
                  styles.tableTabText,
                  isSelected && styles.tableTabTextSelected,
                  hasItems && !isSelected && styles.tableTabTextWithItems,
                  isLoading && { opacity: 0.5 } // Lightly dim text when loading
                ]}>
                  {tableNumber}
                </Text>

                {isLoading && (
                  <View style={styles.loadingOverlay} pointerEvents="none">
                    <ActivityIndicator
                      size="small"
                      color={isSelected ? SoftColors.textInverse : SoftColors.accent}
                    />
                  </View>
                )}

                {hasItems && !isLoading && (
                  <View style={styles.itemBadge} pointerEvents="none">
                    <Text style={styles.itemBadgeText}>{itemCount}</Text>
                  </View>
                )}
              </>
            </Pressable>
          );
        })}

        {/* Clear All Button */}
        <Pressable
          style={({ pressed, focused }) => [
            styles.clearAllButton,
            pressed && styles.tableTabPressed,
            focused && SoftState.focusVisible,
          ]}
          onPress={onClearAllTables}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
          accessibilityLabel="Alle Tische leeren"
          accessibilityRole="button"
        >
          <Text style={styles.clearAllEmoji}>🧹</Text>
          <Text style={styles.clearAllText}>Alle leeren</Text>
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
    marginBottom: SoftSpacing.sm,
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
    alignItems: 'center',
    paddingRight: SoftSpacing.lg,
  },
  tableTab: {
    width: 56,
    minHeight: 56,
    borderRadius: SoftRadius.lg,
    backgroundColor: SoftColors.bgSecondary,
    marginRight: SoftSpacing.sm,
    alignItems: 'center',
    justifyContent: 'center',
    ...SoftShadows.sm,
    position: 'relative',
  },
  tableTabSelected: {
    backgroundColor: SoftColors.accent,
    ...SoftShadows.md,
  },
  tableTabWithItems: {
    backgroundColor: SoftColors.bgCard,
    borderWidth: 1,
    borderColor: SoftColors.success,
  },
  tableTabLoading: {},
  tableTabPressed: SoftState.pressedScale,
  tableTabText: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
  },
  tableTabTextSelected: {
    color: SoftColors.textInverse,
  },
  tableTabTextWithItems: {
    color: SoftColors.success,
  },
  itemBadge: {
    position: 'absolute',
    top: -6,
    right: -6,
    backgroundColor: SoftColors.warning,
    borderRadius: SoftRadius.full,
    paddingHorizontal: SoftSpacing.xs,
    paddingVertical: 2,
    minWidth: 20,
    alignItems: 'center',
    justifyContent: 'center',
    ...SoftShadows.sm,
  },
  itemBadgeText: {
    ...SoftTypography.caption,
    fontWeight: '700',
    color: SoftColors.textInverse,
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
    minHeight: 56,
    minWidth: 56,
  },
  clearAllEmoji: {
    fontSize: 18,
    marginBottom: SoftSpacing.xs,
  },
  clearAllText: {
    ...SoftTypography.caption,
    fontWeight: '600',
    color: SoftColors.error,
  },
  loadingOverlay: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    left: 0,
    right: 0,
    justifyContent: 'center',
    alignItems: 'center',
    borderRadius: SoftRadius.lg,
  },
});
