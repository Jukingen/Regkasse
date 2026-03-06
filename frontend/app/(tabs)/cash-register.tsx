// =============================================================================
// POS Ana Ekran (Cash Register) – maximum cashier speed, minimal taps
// =============================================================================
// UX rule: Tap product row => instantly add 1 item to cart (always).
// If product has modifiers, show inline Extras under that row. Extras apply to
// the most recently added cart line: lastCartItemIdByProductId[productId].
// Tapping a chip toggles that line's modifiers (optimistic update + API). Same product again = new active line.
//
// State: cart.items[] with modifiers; lastCartItemIdByProductId derived from cart;
// pendingModifiersByProduct only when no cart line exists for that product.
// Modifiers included in final payment payload.
// =============================================================================

import React, { useState, useMemo, useCallback } from 'react';
import { SafeAreaView, StyleSheet, View, Text } from 'react-native';

import { CashRegisterHeader } from '../../components/CashRegisterHeader';
import { TableSelector } from '../../components/TableSelector';
import { ProductList } from '../../components/ProductList';
import { CartDisplay } from '../../components/CartDisplay';
import { CartSummary } from '../../components/CartSummary';
import CategoryFilter from '../../components/CategoryFilter';
/** POS modifier selection (quantity independent from product qty). Cart is source of truth. */
type SelectedModifier = { id: string; name: string; price: number; quantity?: number };
import { ToastContainer } from '../../components/ToastNotification';

import { useCashRegister } from '../../hooks/useCashRegister';
import { useTableOrdersRecoveryOptimized } from '../../hooks/useTableOrdersRecoveryOptimized';
import { useProductsUnified } from '../../hooks/useProductsUnified';
import { useCart } from '../../contexts/CartContext';

import { Product } from '../../services/api/productService';
import type { ModifierOptionItem } from '../../components/ModifierOptionChips';
import { SoftColors, SoftSpacing, SoftTypography } from '../../constants/SoftTheme';

// -----------------------------------------------------------------------------
// Hook: Sepet ekleme + modifier pending state (tek tık akışı, minimal re-render)
// -----------------------------------------------------------------------------
function usePOSOrderFlow(
  addItem: (productId: string, qty?: number, options?: { modifiers?: SelectedModifier[]; productName?: string; unitPrice?: number }) => Promise<void>,
  activeTableId: number,
  addToast: (type: 'error' | 'success' | 'info' | 'warning', message: string, duration?: number) => void,
  lastCartItemIdByProductId: Record<string, string>,
  addModifier: (cartItemId: string, modifier: { id: string; name: string; price: number; quantity?: number }) => Promise<void>
) {
  const [pendingModifiersByProduct, setPendingModifiersByProduct] = useState<Record<string, SelectedModifier[]>>({});

  const handleAddProduct = useCallback(
    async (product: Product, modifiers: SelectedModifier[]) => {
      if (!activeTableId) {
        addToast('error', 'Bitte zuerst Tisch wählen', 3000);
        return;
      }
      try {
        const withQty = modifiers.map((m) => ({ ...m, quantity: m.quantity ?? 1 }));
        await addItem(product.id, 1, {
          modifiers: withQty,
          productName: product.name,
          unitPrice: product.price ?? 0,
        });
        addToast('success', `${product.name} zu Tisch ${activeTableId} hinzugefügt`, 2000);
        setPendingModifiersByProduct((prev) => {
          const next = { ...prev };
          delete next[product.id];
          return next;
        });
      } catch (error: any) {
        addToast('error', `${product.name}: ${error?.message || 'Fehler'}`, 5000);
      }
    },
    [addItem, activeTableId, addToast]
  );

  /** Rule A: Product not in cart → add product + modifier. Rule B: Product in cart → add modifier to active line. (Legacy) */
  const handleAddModifier = useCallback(
    async (product: Product, modifier: ModifierOptionItem) => {
      if (!activeTableId) {
        addToast('error', 'Bitte zuerst Tisch wählen', 3000);
        return;
      }
      const cartItemId = lastCartItemIdByProductId[product.id];
      if (cartItemId) {
        try {
          await addModifier(cartItemId, { id: modifier.id, name: modifier.name, price: modifier.price, quantity: 1 });
        } catch (e: any) {
          addToast('error', e?.message || 'Extras konnten nicht hinzugefügt werden.', 3000);
        }
        return;
      }
      try {
        await addItem(product.id, 1, {
          modifiers: [{ id: modifier.id, name: modifier.name, price: modifier.price, quantity: 1 }],
          productName: product.name,
          unitPrice: product.price ?? 0,
        });
        addToast('success', `${product.name} + ${modifier.name} hinzugefügt`, 2000);
        setPendingModifiersByProduct((prev) => {
          const next = { ...prev };
          delete next[product.id];
          return next;
        });
      } catch (error: any) {
        addToast('error', `${product.name}: ${error?.message || 'Fehler'}`, 5000);
      }
    },
    [activeTableId, lastCartItemIdByProductId, addModifier, addItem, addToast]
  );

  /** Faz 1: Sellable add-on tıklandığında sepette ayrı satır; modifier state’e gitmez. */
  const handleAddAddOn = useCallback(
    async (addOn: { productId: string; productName: string; price: number }) => {
      if (!activeTableId) {
        addToast('error', 'Bitte zuerst Tisch wählen', 3000);
        return;
      }
      try {
        await addItem(addOn.productId, 1, {
          productName: addOn.productName,
          unitPrice: addOn.price,
        });
        addToast('success', `${addOn.productName} hinzugefügt`, 2000);
      } catch (e: any) {
        addToast('error', e?.message ?? 'Add-on konnte nicht hinzugefügt werden.', 3000);
      }
    },
    [activeTableId, addItem, addToast]
  );

  return {
    pendingModifiersByProduct,
    handleAddProduct,
    handleAddModifier,
    handleAddAddOn,
  };
}

export default function CashRegisterScreen() {
  // Unified product hook - tüm ürün işlemlerini tek noktada yönet
  const {
    products,
    categories,
    loading: productsLoading,
    error: productsError,
    refreshData,
    getProductsByCategory,
    searchProducts
  } = useProductsUnified();

  // Cash register hook'u
  const {
    paymentProcessing,
    preventDoubleClick,
    error,
    toasts,
    addToast,
    removeToast,
    clearCurrentCart
  } = useCashRegister();


  // Table orders recovery hook'u
  const {
    recoveryData,
    isRecoveryCompleted,
    isLoading: recoveryLoading,
    provisioningMessage: recoveryProvisioningMessage,
  } = useTableOrdersRecoveryOptimized();

  // ✅ Cart Context Usage
  const {
    activeTableId,
    cartsByTable,
    loading: cartLoading,
    error: cartError,
    switchTable,
    addItem,
    increment,
    decrement,
    remove,
    removeByItemId,
    clearCart,
    getCartForTable,
    updateItemQuantity: contextUpdateItemQuantity,
    updateItemQuantityByItemId,
    addModifier,
    incrementModifier,
    decrementModifier,
    removeModifier,
    isPaymentModalVisible,
    setIsPaymentModalVisible
  } = useCart();

  const [tableSelectionLoading, setTableSelectionLoading] = useState<number | null>(null);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);

  const cart = getCartForTable(activeTableId);

  /** productId → cartItemId of the last-added cart line for that product (active for modifier toggles). */
  const lastCartItemIdByProductId = useMemo(() => {
    const map: Record<string, string> = {};
    const items = cart?.items ?? [];
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      const pid = item.productId;
      const id = item.itemId ?? (item as any).clientId;
      if (pid && id) map[pid] = String(id);
    }
    return map;
  }, [cart?.items]);

  // Ürün bazında son satırdaki seçili extras (chip'lerin seçili state'i için)
  const lastCartItemModifiersByProductId = useMemo(() => {
    const map: Record<string, Array<{ id: string; name: string; price: number }>> = {};
    const items = cart?.items ?? [];
    for (let i = items.length - 1; i >= 0; i--) {
      const item = items[i];
      if (!item.productId) continue;
      if (!(item.productId in map)) map[item.productId] = item.modifiers ?? [];
    }
    return map;
  }, [cart?.items]);

  const {
    pendingModifiersByProduct,
    handleAddProduct,
    handleAddModifier,
    handleAddAddOn,
  } = usePOSOrderFlow(
    addItem,
    activeTableId,
    addToast,
    lastCartItemIdByProductId,
    addModifier
  );

  // Chip'lerde gösterilecek seçim: önce sepetteki son satır, yoksa pending
  const selectedModifiersForProduct = useMemo(() => {
    const out: Record<string, SelectedModifier[]> = {};
    const pids = new Set([
      ...Object.keys(lastCartItemModifiersByProductId),
      ...Object.keys(pendingModifiersByProduct)
    ]);
    pids.forEach((pid) => {
      out[pid] =
        lastCartItemModifiersByProductId[pid]?.length
          ? lastCartItemModifiersByProductId[pid]
          : pendingModifiersByProduct[pid] ?? [];
    });
    return out;
  }, [lastCartItemModifiersByProductId, pendingModifiersByProduct]);

  // Masa badge counter için cartsByTable'dan Map türet (anlık güncelleme için tek kaynak)
  // Boş masalar dahil tüm entry'ler eklenir; Clear All sonrası badge 0 olur
  const tableCartsMap = useMemo(() => {
    const map = new Map<number, { items: any[]; totalItems: number }>();
    for (const [tableNumStr, cartData] of Object.entries(cartsByTable)) {
      const tableNum = Number(tableNumStr);
      const items = cartData?.items ?? [];
      const totalItems = items.reduce((s: number, i: any) => s + (i.qty ?? 0), 0);
      map.set(tableNum, { items, totalItems });
    }
    return map;
  }, [cartsByTable]);

  // ---------------------------------------------------------------------------
  // ADAPTERS for Context (Mapping old hook API to Context API)
  // ---------------------------------------------------------------------------

  const addToCart = async (item: any, tableId: number) => {
    try {
      if (tableId !== activeTableId) await switchTable(tableId);
      await addItem(item.productId, item.quantity);
      return { success: true };
    } catch (e: any) {
      return { success: false, message: e.message };
    }
  };

  const removeFromCart = async (tableId: number, itemId: string) => {
    try {
      // Note: Context uses productId, but cash-register might pass itemId.
      // Assuming itemId here IS productId for now due to check above.
      await remove(itemId);
      return { success: true };
    } catch (e: any) {
      console.error(e);
      return { success: false };
    }
  };

  const updateItemQuantity = async (tableId: number, itemId: string, qty: number) => {
    try {
      await contextUpdateItemQuantity(itemId, qty);
      return { success: true };
    } catch (e) { console.error(e); return { success: false }; }
  };

  // Mock load function as Context manages loading state via useEffect/actions
  const loadCartForTable = async (tableId: number) => {
    // In Context, data is either there or will be loaded/optimistic.
    // We can trigger a refresh if context had a refresh method, but for now just return current state.
    // If we really need fresh data, we could call an API here, but let's trust the Context.
    return { success: true, cart: getCartForTable(tableId) };
  };

  const clearAllTables = async () => {
    // Context doesn't have clearAllTables yet, but we can clear current.
    // Or implement it. For now, specific table clear.
    try {
      await clearCart(activeTableId);
      return { success: true, message: 'Table cleared' };
    } catch (e: any) {
      return { success: false, message: e.message || 'Failed' };
    }
  };

  // ---------------------------------------------------------------------------

  const handleCategoryChange = useCallback((categoryId: string | null) => {
    setSelectedCategoryId(categoryId);
  }, []);

  const handleTableSelect = useCallback(async (tableNumber: number) => {
    try {
      if (!tableNumber || tableNumber < 1 || tableNumber > 10) {
        addToast('error', 'Invalid table number', 3000);
        return;
      }

      if (activeTableId === tableNumber) {
        return;
      }

      if (tableSelectionLoading !== null) {
        return;
      }

      setTableSelectionLoading(tableNumber);

      // IMPERATIVE SWITCH
      await switchTable(tableNumber);

      addToast('info', `Switching to table ${tableNumber}`, 2000);

      setTimeout(() => {
        setTableSelectionLoading(null);
      }, 500);

    } catch (error) {
      console.error('❌ Masa seçim hatası:', error);
      addToast('error', 'Failed to switch table', 3000);
      setTableSelectionLoading(null);
    }
  }, [activeTableId, switchTable, addToast]);

  const handleQuantityUpdate = useCallback(async (itemId: string, action: 'increment' | 'decrement') => {
    if (!activeTableId) return;

    const currentCart = getCartForTable(activeTableId);
    const item = currentCart?.items?.find((i: any) => (i.itemId || i.id || i.productId) === itemId);
    if (!item) return;

    const currentQty = (item as any).quantity ?? item.qty ?? 0;
    const newQty = action === 'increment' ? currentQty + 1 : currentQty - 1;

    try {
      await updateItemQuantityByItemId(itemId, newQty);
    } catch (err: any) {
      addToast('error', 'Update failed', 2000);
    }
  }, [activeTableId, getCartForTable, updateItemQuantityByItemId, addToast]);

  const handleItemRemove = useCallback(async (itemId: string) => {
    if (!activeTableId) return;
    try {
      await removeByItemId(itemId);
      addToast('success', 'Artikel entfernt', 2000);
    } catch {
      addToast('error', 'Artikel konnte nicht entfernt werden.', 3000);
    }
  }, [activeTableId, removeByItemId, addToast]);

  const handleClearCart = useCallback(async () => {
    if (!activeTableId) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    try {
      await clearCart(activeTableId);
      addToast('success', `Cart cleared for Table ${activeTableId}`, 2000);

    } catch (error) {
      console.error(`❌ Error clearing table ${activeTableId}:`, error);
      addToast('error', `Failed to clear table ${activeTableId}`, 3000);
    }
  }, [activeTableId, clearCart, addToast]);

  const handleClearAllTables = useCallback(async () => {
    try {
      if (!activeTableId) {
        addToast('error', 'No table selected', 3000);
        return;
      }

      // Capture active table explicitly to avoid race conditions
      const targetTableId = activeTableId;

      // Call clearCart directly for the active table
      // Note: We use clearCart from context, which handles the API call and local state update
      await clearCart(targetTableId);

      // ❌ REMOVED: switchTable(1); - This was forcing the UI to jump to table 1
      // ✅ Behavior: UI stays on the same table (targetTableId)

      addToast('success', `Table ${targetTableId} cleared successfully`, 3000);

    } catch (error: any) {
      console.error('❌ Error clearing table:', error);
      addToast('error', error.message || 'Error clearing table', 3000);
    }
  }, [activeTableId, clearCart, addToast]);

  const handlePayment = useCallback(() => {
    if (!cart?.items?.length) {
      addToast('warning', 'Cart is empty. Please add items first.', 3000);
      return;
    }

    if (!activeTableId) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }
    setIsPaymentModalVisible(true);
  }, [cart?.items?.length, activeTableId, setIsPaymentModalVisible, addToast]);

  return (
    <SafeAreaView style={styles.container}>
      {/* Toast Notifications */}
      <ToastContainer toasts={toasts} onRemove={removeToast} />

      {/* Header */}
      <CashRegisterHeader
        selectedTable={activeTableId}
        recoveryLoading={recoveryLoading}
        provisioningMessage={recoveryProvisioningMessage}
      />

      {/* Root List - ProductList acts as the main scrollable container */}
      {/* Stock info intentionally hidden from cashier UI. Stock management is handled in admin panel. Kept in code for potential future POS usage. */}
      <ProductList
        categoryFilterId={selectedCategoryId}
        pendingModifiersByProduct={selectedModifiersForProduct}
        onAddProduct={handleAddProduct}
        onAddModifier={handleAddModifier}
        onAddAddOn={handleAddAddOn}
        showStockInfo={false}
        showTaxInfo={true}
        ListHeaderComponent={
          <>
            {/* Table Selector */}
            <TableSelector
              selectedTable={activeTableId}
              onTableSelect={handleTableSelect}
              tableCarts={tableCartsMap}
              recoveryData={recoveryData}
              tableSelectionLoading={tableSelectionLoading}
              onClearAllTables={handleClearAllTables}
            />

            {/* Category Filter */}
            <View style={styles.categorySection}>
              <Text style={styles.sectionTitle}>Categories</Text>
              <CategoryFilter
                categories={categories}
                selectedCategoryId={selectedCategoryId}
                onCategoryChange={handleCategoryChange}
              />
            </View>
          </>
        }
        ListFooterComponent={
          <>
            {/* Cart Display */}
            <CartDisplay
              cart={cart}
              selectedTable={activeTableId}
              loading={cartLoading}
              error={cartError}
              onQuantityUpdate={handleQuantityUpdate}
              onItemRemove={handleItemRemove}
              onClearCart={handleClearCart}
              onRemoveModifier={(itemId, m) => removeModifier(itemId, m.id)}
              onIncrementModifier={incrementModifier}
              onDecrementModifier={decrementModifier}
            />

            {/* Cart Summary & Payment Button */}
            <CartSummary
              cart={cart}
              loading={cartLoading}
              error={error}
              paymentProcessing={paymentProcessing}
              preventDoubleClick={preventDoubleClick}
              onPayment={handlePayment}
            />
          </>
        } 
      />

    </SafeAreaView>
  );
}

// -----------------------------------------------------------------------------
// REFACTOR SUMMARY
// -----------------------------------------------------------------------------
// New / updated: usePOSOrderFlow (in-file hook), lastCartItemIdByProductId (derived).
// Components used: ProductList, ProductRow, ProductGridCard, ModifierOptionChips
//   (inline Extras under product row), CartDisplay, CartSummary.
// State: cart (context), pendingModifiersByProduct (local), lastCartItemIdByProductId (derived).
// UX before: Tap product → Zutaten modal → Hinzufügen. After: Tap row → add instantly;
//   Extras inline under row; no modal; no Add button.

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },

  categorySection: {
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
}); 