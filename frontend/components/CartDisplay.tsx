// Compact POS cart display - optimized for speed and minimal space
import React, { useMemo } from 'react';
import { View, Text, Pressable, ScrollView, StyleSheet } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftShadows } from '../constants/SoftTheme';
import { calculateCartTotals } from '../contexts/CartContext';
import { CartItemRow } from './CartItemRow';
import { formatPrice } from '../utils/formatPrice';

interface CartItem {
  itemId: string;
  productId: string;
  productName: string;
  quantity: number;
  qty?: number;
  unitPrice: number;
  totalPrice: number;
  taxType?: string;
  taxRate?: number;
  notes?: string;
}

interface CartDisplayProps {
  cart: any;
  selectedTable: number;
  loading: boolean;
  error: string | null;
  onQuantityUpdate: (itemId: string, action: 'increment' | 'decrement') => void;
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
  // ✅ Derive totals from items (always fresh, never stale)
  const totals = useMemo(() => {
    const items = cart?.items ?? [];
    return calculateCartTotals(items);
  }, [cart?.items, cart?.updatedAt]);

  const itemCount = totals.itemCount;

  // Loading state
  if (loading) {
    return (
      <View style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.headerTitle}>Tisch {selectedTable}</Text>
        </View>
        <Text style={styles.loadingText}>Lädt...</Text>
      </View>
    );
  }

  // Error state
  if (error) {
    return (
      <View style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.headerTitle}>Tisch {selectedTable}</Text>
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
          <Text style={styles.headerTitle}>Tisch {selectedTable}</Text>
          <Text style={styles.emptyBadge}>Leer</Text>
        </View>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Compact header with summary */}
      <View style={styles.header}>
        <Text style={styles.headerTitle}>Tisch {selectedTable}</Text>
        <View style={styles.headerRight}>
          <Text style={styles.itemCount}>{itemCount} Artikel</Text>
          <Pressable onPress={onClearCart} style={styles.clearBtn}>
            <Text style={styles.clearBtnText}>×</Text>
          </Pressable>
        </View>
      </View>

      {/* Scrollable item list with professional CartItemRow */}
      <ScrollView
        style={styles.itemList}
        showsVerticalScrollIndicator={true}
        nestedScrollEnabled={true}
      >
        {cart.items.map((item: CartItem) => {
          const safeId = item.itemId || item.productId;
          return (
            <CartItemRow
              key={safeId}
              item={item}
              onIncrease={async () => onQuantityUpdate(safeId, 'increment')}
              onDecrease={async () => onQuantityUpdate(safeId, 'decrement')}
            />
          );
        })}
      </ScrollView>
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
    width: 32, // Increased from 28
    height: 32, // Increased from 28
    borderRadius: 16,
    backgroundColor: SoftColors.bgSecondary,
    alignItems: 'center',
    justifyContent: 'center',
    // 🚀 VISIBILITY & TOUCH FIXES
    zIndex: 10,
    elevation: 5,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
  },
  qtyBtnPressed: {
    backgroundColor: SoftColors.borderLight,
    opacity: 0.8,
  },
  qtyBtnText: {
    fontSize: 18, // Increased from 16
    fontWeight: '600',
    color: SoftColors.accent,
    // Ensure text doesn't block press
    zIndex: -1,
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
