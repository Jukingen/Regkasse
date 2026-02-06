// Compact POS cart display - optimized for speed and minimal space
import React from 'react';
import { View, Text, Pressable, ScrollView, StyleSheet } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftShadows } from '../constants/SoftTheme';

interface CartItem {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

interface CartDisplayProps {
  cart: any;
  selectedTable: number;
  loading: boolean;
  error: string | null;
  onQuantityUpdate: (itemId: string, newQuantity: number) => void;
  onItemRemove: (itemId: string) => void;
  onClearCart: () => void;
}

export const CartDisplay: React.FC<CartDisplayProps> = ({
  cart,
  selectedTable,
  loading,
  error,
  onQuantityUpdate,
  onItemRemove,
  onClearCart,
}) => {
  const itemCount = cart?.items?.length || 0;
  const totalAmount = cart?.grandTotal || 0;

  // Loading state
  if (loading) {
    return (
      <View style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.headerTitle}>Table {selectedTable}</Text>
        </View>
        <Text style={styles.loadingText}>Loading...</Text>
      </View>
    );
  }

  // Error state
  if (error) {
    return (
      <View style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.headerTitle}>Table {selectedTable}</Text>
        </View>
        <Text style={styles.errorText}>{error}</Text>
      </View>
    );
  }

  // Empty state
  if (!cart || !cart.items || cart.items.length === 0) {
    return (
      <View style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.headerTitle}>Table {selectedTable}</Text>
          <Text style={styles.emptyBadge}>Empty</Text>
        </View>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Compact header with summary */}
      <View style={styles.header}>
        <Text style={styles.headerTitle}>Table {selectedTable}</Text>
        <View style={styles.headerRight}>
          <Text style={styles.itemCount}>{itemCount} items</Text>
          <Pressable onPress={onClearCart} style={styles.clearBtn}>
            <Text style={styles.clearBtnText}>×</Text>
          </Pressable>
        </View>
      </View>

      {/* Scrollable compact item list */}
      <ScrollView
        style={styles.itemList}
        showsVerticalScrollIndicator={true}
        nestedScrollEnabled={true}
      >
        {cart.items.map((item: CartItem) => (
          <View key={item.id} style={styles.itemRow}>
            {/* Quantity controls - left */}
            <View style={styles.qtyGroup}>
              <Pressable
                style={styles.qtyBtn}
                onPress={() => onQuantityUpdate(item.id, item.quantity - 1)}
              >
                <Text style={styles.qtyBtnText}>−</Text>
              </Pressable>
              <Text style={styles.qtyValue}>{item.quantity}</Text>
              <Pressable
                style={styles.qtyBtn}
                onPress={() => onQuantityUpdate(item.id, item.quantity + 1)}
              >
                <Text style={styles.qtyBtnText}>+</Text>
              </Pressable>
            </View>

            {/* Product name - center, truncated */}
            <Text style={styles.itemName} numberOfLines={1}>
              {item.productName}
            </Text>

            {/* Price - right */}
            <Text style={styles.itemPrice}>
              €{(item.totalPrice || 0).toFixed(2)}
            </Text>
          </View>
        ))}
      </ScrollView>

      {/* Total row */}
      <View style={styles.totalRow}>
        <Text style={styles.totalLabel}>Total</Text>
        <Text style={styles.totalValue}>€{totalAmount.toFixed(2)}</Text>
      </View>
    </View>
  );
};

const ITEM_HEIGHT = 44;
const MAX_VISIBLE_ITEMS = 4;

const styles = StyleSheet.create({
  container: {
    backgroundColor: SoftColors.bgCard,
    marginHorizontal: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    ...SoftShadows.sm,
    overflow: 'hidden',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  headerTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  headerRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
  },
  itemCount: {
    fontSize: 12,
    color: SoftColors.textMuted,
  },
  clearBtn: {
    width: 24,
    height: 24,
    borderRadius: 12,
    backgroundColor: SoftColors.errorBg,
    alignItems: 'center',
    justifyContent: 'center',
  },
  clearBtnText: {
    fontSize: 16,
    color: SoftColors.error,
    fontWeight: '600',
  },
  emptyBadge: {
    fontSize: 11,
    color: SoftColors.textMuted,
    backgroundColor: SoftColors.bgSecondary,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 2,
    borderRadius: SoftRadius.sm,
  },
  itemList: {
    maxHeight: ITEM_HEIGHT * MAX_VISIBLE_ITEMS,
  },
  itemRow: {
    flexDirection: 'row',
    alignItems: 'center',
    height: ITEM_HEIGHT,
    paddingHorizontal: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  qtyGroup: {
    flexDirection: 'row',
    alignItems: 'center',
    marginRight: SoftSpacing.sm,
  },
  qtyBtn: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: SoftColors.bgSecondary,
    alignItems: 'center',
    justifyContent: 'center',
  },
  qtyBtnText: {
    fontSize: 16,
    fontWeight: '600',
    color: SoftColors.accent,
  },
  qtyValue: {
    width: 28,
    textAlign: 'center',
    fontSize: 14,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  itemName: {
    flex: 1,
    fontSize: 13,
    color: SoftColors.textPrimary,
    marginRight: SoftSpacing.sm,
  },
  itemPrice: {
    fontSize: 13,
    fontWeight: '600',
    color: SoftColors.accent,
    minWidth: 60,
    textAlign: 'right',
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    backgroundColor: SoftColors.accentLight,
  },
  totalLabel: {
    fontSize: 14,
    fontWeight: '600',
    color: SoftColors.accentDark,
  },
  totalValue: {
    fontSize: 16,
    fontWeight: '700',
    color: SoftColors.accentDark,
  },
  loadingText: {
    padding: SoftSpacing.md,
    fontSize: 13,
    color: SoftColors.textMuted,
    textAlign: 'center',
  },
  errorText: {
    padding: SoftSpacing.md,
    fontSize: 13,
    color: SoftColors.error,
  },
});
