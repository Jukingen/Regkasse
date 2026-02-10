// Soft minimal table selector component
import React from 'react';
import { View, Text, Pressable, ScrollView, StyleSheet, Vibration, ActivityIndicator } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography, SoftShadows } from '../constants/SoftTheme';

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
  const tableNumbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

  const handleTablePress = (tableNumber: number) => {
    if (tableSelectionLoading !== null) return;
    if (selectedTable === tableNumber) return;
    Vibration.vibrate(30);
    onTableSelect(tableNumber);
  };

  const getTableItemCount = (tableNumber: number): number => {
    const tableCart = tableCarts.get(tableNumber);
    const hasItems = tableCart && tableCart.items && tableCart.items.length > 0;
    const recoveryOrder = recoveryData?.tableOrders?.find(
      (order: any) => order.tableNumber === tableNumber
    );
    const recoveryItemCount = recoveryOrder?.itemCount || 0;
    return hasItems ? (tableCart?.totalItems || tableCart?.items?.length || 0) : recoveryItemCount;
  };

  return (
    <View style={styles.tableSection}>
      <Text style={styles.sectionTitle}>Select Table</Text>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        style={styles.tableScroll}
        contentContainerStyle={styles.tableScrollContent}
      >
        {tableNumbers.map((tableNumber) => {
          const itemCount = getTableItemCount(tableNumber);
          const hasItems = itemCount > 0;
          const isSelected = selectedTable === tableNumber;
          const isLoading = tableSelectionLoading === tableNumber;

          return (
            <Pressable
              key={tableNumber}
              style={({ pressed }) => [
                styles.tableTab,
                isSelected && styles.tableTabSelected,
                hasItems && !isSelected && styles.tableTabWithItems,
                isLoading && styles.tableTabLoading,
                pressed && styles.tableTabPressed,
              ]}
              onPress={() => handleTablePress(tableNumber)}
              disabled={isLoading}
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
                  <ActivityIndicator
                    size="small"
                    color={isSelected ? SoftColors.textInverse : SoftColors.accent}
                    style={styles.loadingOverlay}
                  />
                )}

                {hasItems && !isLoading && (
                  <View style={styles.itemBadge}>
                    <Text style={styles.itemBadgeText}>{itemCount}</Text>
                  </View>
                )}
              </>
            </Pressable>
          );
        })}

        {/* Clear All Button */}
        <Pressable
          style={({ pressed }) => [
            styles.clearAllButton,
            pressed && styles.tableTabPressed,
          ]}
          onPress={onClearAllTables}
        >
          <Text style={styles.clearAllEmoji}>🧹</Text>
          <Text style={styles.clearAllText}>Clear All</Text>
        </Pressable>
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  tableSection: {
    backgroundColor: SoftColors.bgCard,
    paddingVertical: SoftSpacing.lg,
    paddingHorizontal: SoftSpacing.lg,
    marginBottom: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  sectionTitle: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.md,
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
    height: 56,
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
    borderWidth: 2,
    borderColor: SoftColors.success,
  },
  tableTabLoading: {
    // opacity: 0.7, // ❌ Removed to keep text visible
  },
  tableTabPressed: {
    opacity: 0.85,
    transform: [{ scale: 0.95 }],
  },
  tableTabText: {
    ...SoftTypography.h3,
    color: SoftColors.textSecondary,
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
    // backgroundColor: 'rgba(255,255,255,0.2)', // Optional: subtle overlay back
    borderRadius: SoftRadius.lg,
  },
});
