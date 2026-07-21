// =============================================================================
// POS Cash Register – maximum cashier speed, minimal taps
// =============================================================================
// UX: Tap product row => add to cart. Products with add-on groups (group.products)
//     open ModifierSelectionBottomSheet; on Fertig → base + add-ons as flat cart lines.
//     Products without add-on groups add directly. Inline chips (legacy path) still
//     supported for existing cart lines. State: cart (context), modifierSheetProduct (sheet),
//     pendingModifiersByProduct (legacy chip state).
//
// WORKING HOURS: never gate this screen. Do NOT add a closed-hours early return.
// Restaurant hours are display-only (Header / WorkingHoursStatus). Cashiers always work.
// =============================================================================

import { Ionicons } from '@expo/vector-icons';
import { useFocusEffect, useRouter } from 'expo-router';
import React, { useState, useMemo, useCallback, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { StyleSheet, Text, TextStyle, View, ViewStyle, Pressable, Alert } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { BillSplitMergeSheet } from '../../components/BillSplitMergeSheet';
import { CartDisplay } from '../../components/CartDisplay';
import { CartSummary } from '../../components/CartSummary';
import { CashRegisterHeader } from '../../components/CashRegisterHeader';
import CategoryFilter from '../../components/CategoryFilter';
import CustomerSelectionSheet from '../../components/CustomerSelectionSheet';
import { FavoritesBar } from '../../components/FavoritesBar';
import { LicenseModeIndicator } from '../../components/LicenseModeIndicator';
import { ModifierSelectionBottomSheet } from '../../components/ModifierSelectionBottomSheet';
import { ProductList } from '../../components/ProductList';
import { TableSelector } from '../../components/TableSelector';
import { ToastContainer } from '../../components/ToastNotification';
import {
  SoftColors,
  SoftRadius,
  SoftShadows,
  SoftSpacing,
  SoftTypography,
  Space8,
} from '../../constants/SoftTheme';
import { TAB_BAR_HEIGHT } from '../../constants/breakpoints';
import { POS_ENSURE_READY_ON_ENTRY } from '../../constants/posFeatureFlags';
import { useCart, getCartDisplayTotals } from '../../contexts/CartContext';
import { usePosRegisterReadiness } from '../../contexts/PosRegisterReadinessContext';
import { useCashRegisterCart } from '../../hooks/useCashRegisterCart';
import { useFavorites } from '../../hooks/useFavorites';
import { useProductDisplayLocale } from '../../hooks/useProductDisplayLocale';
import { useProductsUnified } from '../../hooks/useProductsUnified';
import { useTableOrdersRecoveryOptimized } from '../../hooks/useTableOrdersRecoveryOptimized';
import { customerService, isWalkInCustomerId } from '../../services/api/customerService';
import type { AddOnSelection } from '../../services/api/productModifiersService';
import { Product } from '../../services/api/productService';
import { formatPrice } from '../../utils/formatPrice';
import { consumeMergeSheetRequest } from '../../utils/pendingPosNav';
import { isValidPosCashRegisterId } from '../../utils/posCashRegister';
import {
  isReadinessRegisterDecommissioned,
  isReadinessOpenRegisterGateActive,
  isReadinessStartbelegGateActive,
  registerGateAlertMessage,
  buildPosRegisterGateContext,
  POS_DECOMMISSIONED_SALES_BLOCK_MESSAGE_DE,
} from '../../utils/posRegisterGateCopy';
import { resolveProductDisplayName } from '../../utils/productLocalization';
import type { ProductTextLocale } from '../../utils/productLocalization';

/** POS modifier selection (quantity independent from product qty). Cart is source of truth. */
type SelectedModifier = { id: string; name: string; price: number; quantity?: number };

/** Presentational: step number + section title (used for Category and Summary headers). */
function SectionHeader({
  step,
  title,
  rowStyle,
  stepStyle,
  titleStyle,
}: {
  step: string;
  title: string;
  rowStyle?: ViewStyle;
  stepStyle?: TextStyle;
  titleStyle?: TextStyle;
}) {
  return (
    <View style={[styles.sectionTitleRow, rowStyle]}>
      <Text style={[styles.stepLabel, stepStyle]}>{step}</Text>
      <Text style={[styles.sectionTitle, titleStyle]} accessibilityRole="header">
        {title}
      </Text>
    </View>
  );
}

/** Presentational: cart summary block (critical strip + CartDisplay + CartSummary). */
function POSSummaryBlock({
  activeTableId,
  summaryTotals,
  cart,
  cartLoading,
  cartError,
  onQuantityUpdate,
  onItemRemove,
  onClearCart,
  onRemoveModifier,
  incrementModifier,
  decrementModifier,
  onPayment,
  paddingBottom,
  saleCustomer,
  onOpenCustomerSheet,
  onClearCustomer,
  onOpenSplitSheet,
  onOpenMergeSheet,
  benefitSummaryCount,
  t,
}: {
  activeTableId: number;
  summaryTotals: { itemCount: number; grandTotalGross: number };
  cart: any;
  cartLoading: boolean;
  cartError: string | null;
  onQuantityUpdate: (itemId: string, action: 'increment' | 'decrement') => Promise<void>;
  onItemRemove: (itemId: string) => Promise<void>;
  onClearCart: () => Promise<void>;
  onRemoveModifier: (itemId: string, m: { id: string }) => void;
  incrementModifier: (itemId: string, modifierId: string) => void;
  decrementModifier: (itemId: string, modifierId: string) => void;
  onPayment: () => void;
  paddingBottom: number;
  saleCustomer?: { id: string; name: string; customerNumber?: string } | null;
  onOpenCustomerSheet?: () => void;
  onClearCustomer?: () => void;
  onOpenSplitSheet?: () => void;
  onOpenMergeSheet?: () => void;
  /** Assignment count from benefit-summary; show badge when > 0. */
  benefitSummaryCount?: number | null;
  t: (key: string, options?: Record<string, string | number>) => string;
}) {
  const showBenefitBadge = (benefitSummaryCount ?? 0) > 0;
  const benefitBadgeText =
    (benefitSummaryCount ?? 0) === 1
      ? t('checkout:posFlow.personal.benefitSingle')
      : (benefitSummaryCount ?? 0) > 1
        ? t('checkout:posFlow.personal.benefitMultiple', { count: benefitSummaryCount ?? 0 })
        : '';
  return (
    <View style={[styles.summaryBlock, { paddingBottom }]}>
      <SectionHeader
        step="4"
        title={t('checkout:posFlow.section.summary')}
        rowStyle={styles.summaryBlockHeader}
        titleStyle={styles.summaryBlockTitle}
      />
      {onOpenCustomerSheet && (
        <View style={styles.personalStrip}>
          <Text style={styles.personalLabel}>{t('checkout:posFlow.personal.label')}</Text>
          {saleCustomer ? (
            <>
              <Text style={styles.personalValue} numberOfLines={1}>
                {saleCustomer.name}
              </Text>
              {showBenefitBadge && benefitBadgeText ? (
                <Text style={styles.benefitBadge} numberOfLines={1}>
                  {benefitBadgeText}
                </Text>
              ) : null}
              <Pressable style={styles.personalBtn} onPress={onOpenCustomerSheet}>
                <Text style={styles.personalBtnText}>{t('checkout:posFlow.personal.change')}</Text>
              </Pressable>
              <Pressable style={styles.personalBtn} onPress={onClearCustomer}>
                <Text style={styles.personalBtnText}>{t('checkout:posFlow.personal.remove')}</Text>
              </Pressable>
            </>
          ) : (
            <>
              <Text style={styles.personalValue}>{t('checkout:posFlow.personal.none')}</Text>
              <Pressable style={styles.personalSetzenBtn} onPress={onOpenCustomerSheet}>
                <Text style={styles.personalSetzenText}>{t('checkout:posFlow.personal.set')}</Text>
              </Pressable>
            </>
          )}
        </View>
      )}
      {(onOpenSplitSheet || onOpenMergeSheet) && summaryTotals.itemCount > 0 ? (
        <View style={styles.tableOpsRow}>
          {onOpenSplitSheet ? (
            <Pressable style={styles.tableOpsBtn} onPress={onOpenSplitSheet}>
              <Ionicons name="git-branch-outline" size={16} color={SoftColors.accent} />
              <Text style={styles.tableOpsBtnText}>Teilen</Text>
            </Pressable>
          ) : null}
          {onOpenMergeSheet ? (
            <Pressable style={styles.tableOpsBtn} onPress={onOpenMergeSheet}>
              <Ionicons name="git-merge-outline" size={16} color={SoftColors.accent} />
              <Text style={styles.tableOpsBtnText}>Zusammenführen</Text>
            </Pressable>
          ) : null}
        </View>
      ) : null}
      <View style={styles.criticalStrip}>
        <Text style={styles.criticalStripText} numberOfLines={1} ellipsizeMode="tail">
          {t('checkout:posFlow.summaryStrip', {
            table: activeTableId,
            itemCount: summaryTotals.itemCount,
            total: formatPrice(summaryTotals.grandTotalGross),
          })}
        </Text>
      </View>
      <CartDisplay
        cart={cart}
        selectedTable={activeTableId}
        loading={cartLoading}
        error={cartError}
        onQuantityUpdate={onQuantityUpdate}
        onItemRemove={onItemRemove}
        onClearCart={onClearCart}
        onRemoveModifier={onRemoveModifier}
        onIncrementModifier={incrementModifier}
        onDecrementModifier={decrementModifier}
      />
      <CartSummary cart={cart} loading={cartLoading} error={cartError} onPayment={onPayment} />
    </View>
  );
}

// -----------------------------------------------------------------------------
// Hook: Sepet ekleme + modifier pending state (tek tık akışı, minimal re-render)
// -----------------------------------------------------------------------------
function usePOSOrderFlow(
  addItem: (
    productId: string,
    qty?: number,
    options?: { productName?: string; unitPrice?: number }
  ) => Promise<void>,
  activeTableId: number,
  addToast: (
    type: 'error' | 'success' | 'info' | 'warning',
    message: string,
    duration?: number
  ) => void,
  t: (key: string, options?: Record<string, string | number>) => string,
  displayLocale: ProductTextLocale
) {
  const [pendingModifiersByProduct, setPendingModifiersByProduct] = useState<
    Record<string, SelectedModifier[]>
  >({});

  const handleAddProduct = useCallback(
    async (product: Product) => {
      if (!activeTableId) {
        addToast('error', t('checkout:posFlow.toast.selectTableFirst'), 3000);
        return;
      }
      try {
        const displayName = resolveProductDisplayName(product, displayLocale);
        await addItem(product.id, 1, {
          productName: displayName,
          unitPrice: product.price ?? 0,
        });
        addToast(
          'success',
          t('checkout:posFlow.toast.productAddedToTable', {
            name: displayName,
            table: activeTableId,
          }),
          2000
        );
        setPendingModifiersByProduct((prev) => {
          const next = { ...prev };
          delete next[product.id];
          return next;
        });
      } catch (error: any) {
        addToast(
          'error',
          t('checkout:posFlow.toast.productAddError', {
            name: resolveProductDisplayName(product, displayLocale),
            reason: error?.message || t('common:error'),
          }),
          5000
        );
      }
    },
    [addItem, activeTableId, addToast, t, displayLocale]
  );

  const handleAddAddOn = useCallback(
    async (addOn: AddOnSelection) => {
      if (!activeTableId) {
        addToast('error', t('checkout:posFlow.toast.selectTableFirst'), 3000);
        return;
      }
      try {
        await addItem(addOn.productId, 1, {
          productName: addOn.productName,
          unitPrice: addOn.price,
        });
        addToast(
          'success',
          t('checkout:posFlow.toast.addOnAdded', { name: addOn.productName }),
          2000
        );
      } catch (e: any) {
        addToast('error', e?.message ?? t('checkout:posFlow.toast.addOnAddFailed'), 3000);
      }
    },
    [activeTableId, addItem, addToast, t]
  );

  return {
    pendingModifiersByProduct,
    handleAddProduct,
    handleAddAddOn,
  };
}

/**
 * POS cash register — always operational.
 * No working-hours check, no closed-hours early return, no order intake gate.
 * (Restaurant schedule is display-only via Header / WorkingHoursStatus.)
 */
export default function CashRegisterScreen() {
  const { t } = useTranslation(['checkout', 'common']);
  const router = useRouter();
  const [tableSelectionLoading, setTableSelectionLoading] = useState<number | null>(null);
  const [customerSheetVisible, setCustomerSheetVisible] = useState(false);
  const [splitMergeVisible, setSplitMergeVisible] = useState(false);
  const [splitMergeMode, setSplitMergeMode] = useState<'split' | 'merge'>('split');
  const { favorites, removeFavorite, toggleFavorite, isFavorite } = useFavorites();
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  /** Add-on bottom sheet: product with add-on groups; on Fertig → addItemWithAddOns (base + add-on lines). */
  const [modifierSheetProduct, setModifierSheetProduct] = useState<Product | null>(null);
  /** Assignment-level benefit count for current sale customer; null when not loaded or guest. */
  const [benefitSummaryCount, setBenefitSummaryCount] = useState<number | null>(null);
  const benefitFetchRef = useRef<string | null>(null);

  const { categories } = useProductsUnified();
  const { toasts, addToast, removeToast } = useCashRegisterCart();
  const {
    recoveryData,
    isLoading: recoveryLoading,
    provisioningMessage: recoveryProvisioningMessage,
  } = useTableOrdersRecoveryOptimized();
  const {
    activeTableId,
    currentCart,
    cartsByTable,
    loading: cartLoading,
    error: cartError,
    switchTable,
    addItem,
    addItemWithAddOns,
    removeByItemId,
    clearCart,
    getCartForTable,
    updateItemQuantityByItemId,
    incrementModifier,
    decrementModifier,
    removeModifier,
    setIsPaymentModalVisible,
    saleCustomer,
    setSaleCustomer,
    splitCartItems,
    mergeTableCarts,
  } = useCart();

  const posReadiness = usePosRegisterReadiness();

  useFocusEffect(
    useCallback(() => {
      if (!POS_ENSURE_READY_ON_ENTRY) return undefined;
      let cancelled = false;
      void (async () => {
        await posReadiness.refreshAsync();
        if (cancelled) return;
      })();
      return () => {
        cancelled = true;
      };
    }, [posReadiness.refreshAsync])
  );

  useFocusEffect(
    useCallback(() => {
      if (consumeMergeSheetRequest()) {
        setSplitMergeMode('merge');
        setSplitMergeVisible(true);
      }
    }, [])
  );

  const handleOpenPaymentHistory = useCallback(() => {
    const registerId = posReadiness.data?.effectiveRegisterId?.trim();
    if (!isValidPosCashRegisterId(registerId)) {
      Alert.alert(
        'Keine Kasse ausgewählt',
        'Bitte wählen Sie zuerst unter Einstellungen → Kasse eine Registrierkasse aus oder starten Sie eine Schicht.'
      );
      return;
    }
    router.push('/(screens)/PaymentHistoryScreen' as const);
  }, [posReadiness.data?.effectiveRegisterId, router]);

  // Fetch benefit-summary when sale customer changes (skip guest); request guard to avoid race.
  useEffect(() => {
    const customerId = saleCustomer?.id ?? null;
    if (!customerId || isWalkInCustomerId(customerId)) {
      setBenefitSummaryCount(null);
      benefitFetchRef.current = null;
      return;
    }
    benefitFetchRef.current = customerId;
    let cancelled = false;
    (async () => {
      try {
        const summary = await customerService.getBenefitSummary(customerId);
        if (cancelled || benefitFetchRef.current !== customerId) return;
        setBenefitSummaryCount(summary?.assignedBenefitCount ?? 0);
      } catch (_e) {
        if (cancelled || benefitFetchRef.current !== customerId) return;
        setBenefitSummaryCount(0);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [saleCustomer?.id]);

  // Use context's currentCart so summary/checkout always follow active table (no stale table cart)
  const cart = currentCart;
  const insets = useSafeAreaInsets();
  const footerBottomPadding = TAB_BAR_HEIGHT + insets.bottom;

  // ——— Derived UI state (from cart / tables) ———
  const summaryTotals = useMemo(() => getCartDisplayTotals(cart), [cart]);
  /** productId → modifiers on the last cart line (for chip selection display). */
  const lastCartItemModifiersByProductId = useMemo(() => {
    const map: Record<string, { id: string; name: string; price: number }[]> = {};
    const items = cart?.items ?? [];
    for (let i = items.length - 1; i >= 0; i--) {
      const item = items[i];
      if (!item.productId) continue;
      if (!(item.productId in map)) map[item.productId] = item.modifiers ?? [];
    }
    return map;
  }, [cart?.items]);

  const productDisplayLocale = useProductDisplayLocale();

  const { pendingModifiersByProduct, handleAddProduct, handleAddAddOn } = usePOSOrderFlow(
    addItem,
    activeTableId,
    addToast,
    t,
    productDisplayLocale
  );
  /** Merged modifier selection per product: last cart line or pending (for inline chips). */
  const selectedModifiersForProduct = useMemo(() => {
    const out: Record<string, SelectedModifier[]> = {};
    const pids = new Set([
      ...Object.keys(lastCartItemModifiersByProductId),
      ...Object.keys(pendingModifiersByProduct),
    ]);
    pids.forEach((pid) => {
      out[pid] = lastCartItemModifiersByProductId[pid]?.length
        ? lastCartItemModifiersByProductId[pid]
        : (pendingModifiersByProduct[pid] ?? []);
    });
    return out;
  }, [lastCartItemModifiersByProductId, pendingModifiersByProduct]);

  const handleApplyWithBase = useCallback(
    async (
      base: { productId: string; productName: string; price: number },
      addOns: { productId: string; productName: string; price: number }[]
    ) => {
      try {
        await addItemWithAddOns(base.productId, base.productName, base.price, addOns);
        addToast(
          'success',
          t('checkout:posFlow.toast.baseProductAdded', { name: base.productName }),
          2000
        );
      } catch (e: any) {
        addToast('error', e?.message ?? t('checkout:posFlow.toast.addingFailed'), 3000);
      }
      setModifierSheetProduct(null);
    },
    [addItemWithAddOns, addToast]
  );

  /** Table number → { items, totalItems } for table selector badges. */
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

  // ——— Event handlers ———
  const handleCategoryChange = useCallback((categoryId: string | null) => {
    setSelectedCategoryId(categoryId);
  }, []);

  // Contract: User can always switch table; having items on current table must not block.
  const handleTableSelect = useCallback(
    async (tableNumber: number) => {
      if (!tableNumber || tableNumber < 1 || tableNumber > 10) {
        addToast('error', t('checkout:posFlow.toast.invalidTableNumber'), 3000);
        return;
      }

      if (activeTableId === tableNumber) {
        return;
      }

      setTableSelectionLoading(tableNumber);
      const clearLoading = () => {
        setTableSelectionLoading(null);
      };
      const loadingTimeout = setTimeout(clearLoading, 8000);

      try {
        // Timeout so loading always clears even if fetch hangs (regression: stuck loading blocked other tables)
        await Promise.race([
          switchTable(tableNumber),
          new Promise<never>((_, reject) =>
            setTimeout(() => {
              reject(new Error('Table switch timeout'));
            }, 6000)
          ),
        ]);
        addToast(
          'info',
          t('checkout:posFlow.toast.switchingToTable', { table: tableNumber }),
          2000
        );
      } catch (error) {
        console.error('❌ Masa seçim hatası:', error);
        addToast('error', t('checkout:posFlow.toast.tableSwitchFailed'), 3000);
      } finally {
        clearTimeout(loadingTimeout);
        setTimeout(clearLoading, 300);
      }
    },
    [activeTableId, switchTable, addToast]
  );

  const handleQuantityUpdate = useCallback(
    async (itemId: string, action: 'increment' | 'decrement') => {
      if (!activeTableId) return;

      const currentCart = getCartForTable(activeTableId);
      const item = currentCart?.items?.find(
        (i: any) => (i.itemId || i.id || i.productId) === itemId
      );
      if (!item) return;

      const currentQty = (item as any).quantity ?? item.qty ?? 0;
      const newQty = action === 'increment' ? currentQty + 1 : currentQty - 1;

      try {
        await updateItemQuantityByItemId(itemId, newQty);
      } catch (err: any) {
        addToast('error', t('checkout:posFlow.toast.updateFailed'), 2000);
      }
    },
    [activeTableId, getCartForTable, updateItemQuantityByItemId, addToast]
  );

  const handleItemRemove = useCallback(
    async (itemId: string) => {
      if (!activeTableId) return;
      try {
        await removeByItemId(itemId);
        addToast('success', t('checkout:posFlow.toast.itemRemoved'), 2000);
      } catch {
        addToast('error', t('checkout:posFlow.toast.itemRemoveFailed'), 3000);
      }
    },
    [activeTableId, removeByItemId, addToast]
  );

  const handleClearCart = useCallback(async () => {
    if (!activeTableId) {
      addToast('error', t('checkout:posFlow.toast.selectTableFirst'), 3000);
      return;
    }

    try {
      await clearCart(activeTableId);
      addToast(
        'success',
        t('checkout:posFlow.toast.cartClearedForTable', { table: activeTableId }),
        2000
      );
    } catch (err) {
      console.error(`❌ Error clearing table ${activeTableId}:`, err);
      addToast(
        'error',
        t('checkout:posFlow.toast.tableClearFailed', { table: activeTableId }),
        3000
      );
    }
  }, [activeTableId, clearCart, addToast]);

  const handleClearAllTables = useCallback(async () => {
    try {
      if (!activeTableId) {
        addToast('error', t('checkout:posFlow.toast.selectTableFirst'), 3000);
        return;
      }

      // Capture active table explicitly to avoid race conditions
      const targetTableId = activeTableId;

      // Call clearCart directly for the active table
      // Note: We use clearCart from context, which handles the API call and local state update
      await clearCart(targetTableId);

      // ❌ REMOVED: switchTable(1); - This was forcing the UI to jump to table 1
      // ✅ Behavior: UI stays on the same table (targetTableId)

      addToast('success', t('checkout:posFlow.toast.tableCleared', { table: targetTableId }), 3000);
    } catch (err: any) {
      console.error('❌ Error clearing table:', err);
      addToast('error', err?.message ?? t('checkout:posFlow.toast.tableClearGenericFailed'), 3000);
    }
  }, [activeTableId, clearCart, addToast]);

  const handlePayment = useCallback(() => {
    if (!cart?.items?.length) {
      addToast('warning', t('checkout:posFlow.toast.emptyCart'), 3000);
      return;
    }

    if (!activeTableId) {
      addToast('error', t('checkout:posFlow.toast.selectTableFirst'), 3000);
      return;
    }

    if (isReadinessRegisterDecommissioned(posReadiness.data)) {
      Alert.alert('Verkauf', POS_DECOMMISSIONED_SALES_BLOCK_MESSAGE_DE);
      return;
    }
    if (
      isReadinessStartbelegGateActive(posReadiness.data, {
        ensureReadyEnabled: POS_ENSURE_READY_ON_ENTRY,
      })
    ) {
      Alert.alert(
        'Startbeleg erforderlich',
        'Bitte zuerst den fiskalischen Startbeleg erstellen, bevor Sie zur Zahlung wechseln.'
      );
      return;
    }
    if (
      isReadinessOpenRegisterGateActive(posReadiness.data, {
        ensureReadyEnabled: POS_ENSURE_READY_ON_ENTRY,
      })
    ) {
      const gateCtx = buildPosRegisterGateContext({
        settingsLoadFailed: false,
        registerListFailureKind: null,
        registerListLoading: false,
        registerPicklistCount: 0,
        readiness: {
          loading: false,
          error: false,
          nextAction: posReadiness.data?.nextAction ?? null,
          messageCode: posReadiness.data?.messageCode ?? null,
          registerStatus: posReadiness.data?.registerStatus ?? null,
        },
      });
      Alert.alert('Kasse geschlossen', registerGateAlertMessage(gateCtx));
      return;
    }

    setIsPaymentModalVisible(true);
  }, [
    cart?.items?.length,
    activeTableId,
    setIsPaymentModalVisible,
    addToast,
    posReadiness.data,
    t,
  ]);

  // Top inset is applied once in (tabs)/_layout (licenseStatusBar); bottom by tab bar + footerBottomPadding.
  return (
    <View style={styles.container}>
      <LicenseModeIndicator />
      {/* Toast Notifications */}
      <ToastContainer toasts={toasts} onRemove={removeToast} />

      {/* Customer identification sheet for customer attachment */}
      <CustomerSelectionSheet
        visible={customerSheetVisible}
        onClose={() => {
          setCustomerSheetVisible(false);
        }}
        onSelect={(c) => {
          setSaleCustomer(c);
          setCustomerSheetVisible(false);
        }}
      />

      <BillSplitMergeSheet
        visible={splitMergeVisible}
        mode={splitMergeMode}
        activeTableId={activeTableId}
        cartItems={cart?.items ?? []}
        onClose={() => {
          setSplitMergeVisible(false);
        }}
        onSplitItems={(target, ids) => splitCartItems(activeTableId, target, ids)}
        onMergeTables={(source, target) => mergeTableCarts(source, target)}
      />

      {/* Add-on selection bottom sheet: base + add-ons as flat cart lines */}
      {modifierSheetProduct && (
        <ModifierSelectionBottomSheet
          visible
          productId={modifierSheetProduct.id}
          productName={resolveProductDisplayName(modifierSheetProduct, productDisplayLocale)}
          productPrice={modifierSheetProduct.price ?? 0}
          modifierGroups={modifierSheetProduct.modifierGroups ?? undefined}
          onClose={() => {
            setModifierSheetProduct(null);
          }}
          onApplyWithBase={handleApplyWithBase}
        />
      )}

      {/* Header */}
      <CashRegisterHeader
        selectedTable={activeTableId}
        recoveryLoading={recoveryLoading}
        provisioningMessage={recoveryProvisioningMessage}
        onOpenPaymentHistory={handleOpenPaymentHistory}
      />

      {/* Root List - ProductList acts as the main scrollable container */}
      {/* Stock info intentionally hidden from cashier UI. Stock management is handled in admin panel. Kept in code for potential future POS usage. */}
      <ProductList
        categoryFilterId={selectedCategoryId}
        pendingModifiersByProduct={selectedModifiersForProduct}
        onAddProduct={handleAddProduct}
        onAddAddOn={handleAddAddOn}
        onOpenAddOnSheet={setModifierSheetProduct}
        onLongPressProduct={(product) => {
          const wasFavorite = isFavorite(product.id);
          void toggleFavorite(product.id)
            .then(() => {
              addToast(
                'info',
                wasFavorite ? 'Aus Favoriten entfernt' : 'Zu Favoriten hinzugefügt',
                2000
              );
            })
            .catch(() => {
              addToast('error', 'Favorit konnte nicht gespeichert werden', 3000);
            });
        }}
        showStockInfo={false}
        showTaxInfo
        ListHeaderComponent={
          <>
            <FavoritesBar
              favorites={favorites}
              removeFavorite={removeFavorite}
              onProductAdded={(name) => {
                addToast(
                  'success',
                  t('checkout:posFlow.toast.productAddedToTable', {
                    name,
                    table: activeTableId,
                  }),
                  2000
                );
              }}
            />
            {/* Table Selector */}
            <TableSelector
              selectedTable={activeTableId}
              onTableSelect={handleTableSelect}
              tableCarts={tableCartsMap}
              recoveryData={recoveryData}
              tableSelectionLoading={tableSelectionLoading}
              onClearAllTables={handleClearAllTables}
            />

            {/* Step 2: Category – flow: Tisch → Kategorie → Produkte → Zusammenfassung */}
            <View style={styles.categorySection}>
              <SectionHeader step="2" title={t('checkout:posFlow.section.category')} />
              <CategoryFilter
                categories={categories}
                selectedCategoryId={selectedCategoryId}
                onCategoryChange={handleCategoryChange}
              />
            </View>
          </>
        }
        ListFooterComponent={
          <POSSummaryBlock
            activeTableId={activeTableId}
            summaryTotals={summaryTotals}
            cart={cart}
            cartLoading={cartLoading}
            cartError={cartError}
            onQuantityUpdate={handleQuantityUpdate}
            onItemRemove={handleItemRemove}
            onClearCart={handleClearCart}
            onRemoveModifier={(itemId, m) => removeModifier(itemId, m.id)}
            incrementModifier={incrementModifier}
            decrementModifier={decrementModifier}
            onPayment={handlePayment}
            paddingBottom={footerBottomPadding}
            saleCustomer={saleCustomer}
            onOpenCustomerSheet={() => {
              setCustomerSheetVisible(true);
            }}
            onClearCustomer={() => {
              setSaleCustomer(null);
            }}
            onOpenSplitSheet={() => {
              router.push({
                pathname: '/(screens)/SplitScreen',
                params: { tableNumber: String(activeTableId) },
              });
            }}
            onOpenMergeSheet={() => {
              setSplitMergeMode('merge');
              setSplitMergeVisible(true);
            }}
            benefitSummaryCount={benefitSummaryCount}
            t={t}
          />
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },

  tableOpsRow: {
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    marginBottom: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.xs,
  },
  tableOpsBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingVertical: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.border,
    backgroundColor: SoftColors.bgCard,
  },
  tableOpsBtnText: {
    fontSize: 13,
    fontWeight: '600',
    color: SoftColors.accent,
  },
  categorySection: {
    backgroundColor: SoftColors.bgCard,
    paddingVertical: SoftSpacing.md,
    paddingHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  sectionTitleRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: SoftSpacing.xs,
    marginBottom: Space8[1],
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
  summaryBlock: {
    backgroundColor: SoftColors.bgCard,
    marginTop: SoftSpacing.md,
    marginHorizontal: SoftSpacing.sm,
    marginBottom: SoftSpacing.lg,
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    overflow: 'hidden',
    ...SoftShadows.sm,
  },
  summaryBlockHeader: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.md,
    paddingTop: SoftSpacing.md,
    paddingBottom: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  summaryBlockTitle: {
    ...SoftTypography.h2,
    color: SoftColors.textPrimary,
    flex: 1,
  },
  criticalStrip: {
    paddingHorizontal: SoftSpacing.md,
    paddingBottom: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  criticalStripText: {
    ...SoftTypography.label,
    color: SoftColors.textSecondary,
  },
  personalStrip: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  personalLabel: {
    ...SoftTypography.label,
    color: SoftColors.textSecondary,
  },
  personalValue: {
    flex: 1,
    minWidth: 60,
    ...SoftTypography.bodySmall,
    color: SoftColors.textPrimary,
  },
  personalBtn: {
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 4,
  },
  personalBtnText: {
    ...SoftTypography.label,
    color: SoftColors.accent,
  },
  personalSetzenBtn: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.xs,
    borderWidth: 1,
    borderColor: SoftColors.accent,
    borderRadius: SoftRadius.sm,
  },
  personalSetzenText: {
    ...SoftTypography.label,
    color: SoftColors.accent,
  },
  benefitBadge: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
  },
});
