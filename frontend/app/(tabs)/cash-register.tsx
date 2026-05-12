// =============================================================================
// POS Cash Register – maximum cashier speed, minimal taps
// =============================================================================
// UX: Tap product row => add to cart. Products with add-on groups (group.products)
//     open ModifierSelectionBottomSheet; on Fertig → base + add-ons as flat cart lines.
//     Products without add-on groups add directly. Inline chips (legacy path) still
//     supported for existing cart lines. State: cart (context), modifierSheetProduct (sheet),
//     pendingModifiersByProduct (legacy chip state).
// =============================================================================

import { Ionicons } from '@expo/vector-icons';
import { useFocusEffect } from '@react-navigation/native';
import { useRouter } from 'expo-router';
import React, { useState, useMemo, useCallback, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { SafeAreaView, StyleSheet, Text, TextStyle, View, ViewStyle, Pressable, Alert } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { CartDisplay } from '../../components/CartDisplay';
import { CartSummary } from '../../components/CartSummary';
import { CashRegisterHeader } from '../../components/CashRegisterHeader';
import CategoryFilter from '../../components/CategoryFilter';
import CustomerSelectionSheet from '../../components/CustomerSelectionSheet';
import { ModifierSelectionBottomSheet } from '../../components/ModifierSelectionBottomSheet';
import { ProductList } from '../../components/ProductList';
import { TableSelector } from '../../components/TableSelector';
import { ToastContainer } from '../../components/ToastNotification';
import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftTypography, Space8 } from '../../constants/SoftTheme';
import { TAB_BAR_HEIGHT } from '../../constants/breakpoints';
import { POS_ENSURE_READY_ON_ENTRY } from '../../constants/posFeatureFlags';
import { useCart, getCartDisplayTotals } from '../../contexts/CartContext';
import { usePosRegisterReadiness } from '../../contexts/PosRegisterReadinessContext';
import { useCashRegister } from '../../hooks/useCashRegister';
import { useProductsUnified } from '../../hooks/useProductsUnified';
import { useTableOrdersRecoveryOptimized } from '../../hooks/useTableOrdersRecoveryOptimized';
import { customerService, isWalkInCustomerId } from '../../services/api/customerService';
import type { AddOnSelection } from '../../services/api/productModifiersService';
import { Product } from '../../services/api/productService';
import {
  getMonatsbelegStatus,
  type MonatsbelegStatusDto,
} from '../../services/api/rksvSpecialReceiptsService';
import { formatPrice } from '../../utils/formatPrice';
import { isValidPosCashRegisterId } from '../../utils/posCashRegister';
import {
  isReadinessRegisterDecommissioned,
  isReadinessStartbelegGateActive,
  POS_DECOMMISSIONED_SALES_BLOCK_MESSAGE_DE,
} from '../../utils/posRegisterGateCopy';

/** POS modifier selection (quantity independent from product qty). Cart is source of truth. */
type SelectedModifier = { id: string; name: string; price: number; quantity?: number };

/** RKSV Monatsbeleg reminder strip (Vienna month); yellow dismissible, red must be resolved. */
function MonatsbelegWarningBannerStrip({
  monatsbelegStatus,
  yellowDismissed,
  onDismissYellow,
  onNavigateCreate,
  t,
}: {
  monatsbelegStatus:
    | { phase: 'idle' }
    | { phase: 'loading' }
    | { phase: 'success'; data: MonatsbelegStatusDto }
    | { phase: 'error' };
  yellowDismissed: boolean;
  onDismissYellow: () => void;
  onNavigateCreate: () => void;
  t: (key: string, options?: Record<string, string | number>) => string;
}) {
  if (monatsbelegStatus.phase !== 'success') {
    return null;
  }
  const { warningLevel, daysUntilDeadline } = monatsbelegStatus.data;
  if (warningLevel !== 'yellow' && warningLevel !== 'red') {
    return null;
  }
  if (warningLevel === 'yellow' && yellowDismissed) {
    return null;
  }

  const isRed = warningLevel === 'red';
  const message = isRed
    ? t('checkout:posFlow.monatsbelegBanner.red')
    : t('checkout:posFlow.monatsbelegBanner.yellow', { days: daysUntilDeadline });

  return (
    <View
      style={[styles.mbBanner, isRed ? styles.mbBannerRed : styles.mbBannerYellow]}
      accessibilityRole="alert"
    >
      <Text style={[styles.mbBannerText, isRed && styles.mbBannerTextRed]}>{message}</Text>
      <View style={styles.mbBannerRow}>
        <Pressable
          onPress={onNavigateCreate}
          style={({ pressed }) => [styles.mbBannerCta, pressed && styles.mbBannerCtaPressed]}
          accessibilityRole="button"
          accessibilityLabel={t('checkout:posFlow.monatsbelegBanner.createNow')}
        >
          <Text style={styles.mbBannerCtaText}>{t('checkout:posFlow.monatsbelegBanner.createNow')}</Text>
        </Pressable>
        {!isRed ? (
          <Pressable
            onPress={onDismissYellow}
            style={({ pressed }) => [styles.mbDismissBtn, pressed && styles.mbBannerCtaPressed]}
            accessibilityRole="button"
            accessibilityLabel={t('checkout:posFlow.monatsbelegBanner.dismissA11y')}
            hitSlop={8}
          >
            <Ionicons name="close" size={22} color={SoftColors.textPrimary} />
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}

/** RKSV: prominent notice when the current Vienna month Monatsbeleg is past the 7th-day grace (server: currentMonthOverdue). */
function MonatsbelegCurrentMonthOverduePanel({
  monatsbelegStatus,
  onNavigateCreate,
  t,
}: {
  monatsbelegStatus:
    | { phase: 'idle' }
    | { phase: 'loading' }
    | { phase: 'success'; data: MonatsbelegStatusDto }
    | { phase: 'error' };
  onNavigateCreate: () => void;
  t: (key: string, options?: Record<string, string | number>) => string;
}) {
  if (monatsbelegStatus.phase !== 'success' || monatsbelegStatus.data.currentMonthOverdue !== true) {
    return null;
  }
  return (
    <View style={styles.mbOverduePanel} accessibilityRole="alert">
      <Text style={styles.mbOverdueTitle}>{t('checkout:posFlow.monatsbelegCurrentMonthOverdue.title')}</Text>
      <Text style={styles.mbOverdueBody}>{t('checkout:posFlow.monatsbelegCurrentMonthOverdue.description')}</Text>
      <Pressable
        onPress={onNavigateCreate}
        style={({ pressed }) => [styles.mbBannerCta, pressed && styles.mbBannerCtaPressed]}
        accessibilityRole="button"
        accessibilityLabel={t('checkout:posFlow.monatsbelegCurrentMonthOverdue.cta')}
      >
        <Text style={styles.mbBannerCtaText}>{t('checkout:posFlow.monatsbelegCurrentMonthOverdue.cta')}</Text>
      </Pressable>
    </View>
  );
}

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
      <Text style={[styles.sectionTitle, titleStyle]} accessibilityRole="header">{title}</Text>
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
      <SectionHeader step="4" title={t('checkout:posFlow.section.summary')} rowStyle={styles.summaryBlockHeader} titleStyle={styles.summaryBlockTitle} />
      {onOpenCustomerSheet && (
        <View style={styles.personalStrip}>
          <Text style={styles.personalLabel}>{t('checkout:posFlow.personal.label')}</Text>
          {saleCustomer ? (
            <>
              <Text style={styles.personalValue} numberOfLines={1}>{saleCustomer.name}</Text>
              {showBenefitBadge && benefitBadgeText ? (
                <Text style={styles.benefitBadge} numberOfLines={1}>{benefitBadgeText}</Text>
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
      <CartSummary
        cart={cart}
        loading={cartLoading}
        error={cartError}
        onPayment={onPayment}
      />
    </View>
  );
}

// -----------------------------------------------------------------------------
// Hook: Sepet ekleme + modifier pending state (tek tık akışı, minimal re-render)
// -----------------------------------------------------------------------------
function usePOSOrderFlow(
  addItem: (productId: string, qty?: number, options?: { productName?: string; unitPrice?: number }) => Promise<void>,
  activeTableId: number,
  addToast: (type: 'error' | 'success' | 'info' | 'warning', message: string, duration?: number) => void,
  t: (key: string, options?: Record<string, string | number>) => string
) {
  const [pendingModifiersByProduct, setPendingModifiersByProduct] = useState<Record<string, SelectedModifier[]>>({});

  const handleAddProduct = useCallback(
    async (product: Product) => {
      if (!activeTableId) {
        addToast('error', t('checkout:posFlow.toast.selectTableFirst'), 3000);
        return;
      }
      try {
        await addItem(product.id, 1, {
          productName: product.name,
          unitPrice: product.price ?? 0,
        });
        addToast('success', t('checkout:posFlow.toast.productAddedToTable', { name: product.name, table: activeTableId }), 2000);
        setPendingModifiersByProduct((prev) => {
          const next = { ...prev };
          delete next[product.id];
          return next;
        });
      } catch (error: any) {
        addToast('error', t('checkout:posFlow.toast.productAddError', { name: product.name, reason: error?.message || t('common:error') }), 5000);
      }
    },
    [addItem, activeTableId, addToast, t]
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
        addToast('success', t('checkout:posFlow.toast.addOnAdded', { name: addOn.productName }), 2000);
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

export default function CashRegisterScreen() {
  const { t } = useTranslation(['checkout', 'common']);
  const router = useRouter();
  const [monatsbelegStatus, setMonatsbelegStatus] = useState<
    | { phase: 'idle' }
    | { phase: 'loading' }
    | { phase: 'success'; data: MonatsbelegStatusDto }
    | { phase: 'error' }
  >({ phase: 'idle' });
  const [yellowBannerDismissed, setYellowBannerDismissed] = useState(false);
  const prevMbWarningLevelRef = useRef<string | null>(null);
  const [tableSelectionLoading, setTableSelectionLoading] = useState<number | null>(null);
  const [customerSheetVisible, setCustomerSheetVisible] = useState(false);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  /** Add-on bottom sheet: product with add-on groups; on Fertig → addItemWithAddOns (base + add-on lines). */
  const [modifierSheetProduct, setModifierSheetProduct] = useState<Product | null>(null);
  /** Assignment-level benefit count for current sale customer; null when not loaded or guest. */
  const [benefitSummaryCount, setBenefitSummaryCount] = useState<number | null>(null);
  const benefitFetchRef = useRef<string | null>(null);

  const { categories } = useProductsUnified();
  const { toasts, addToast, removeToast } = useCashRegister();
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
  } = useCart();

  const posReadiness = usePosRegisterReadiness();

  useFocusEffect(
    useCallback(() => {
      const id = posReadiness.data?.effectiveRegisterId?.trim();
      if (!id || !isValidPosCashRegisterId(id)) {
        setMonatsbelegStatus({ phase: 'idle' });
        return;
      }
      let cancelled = false;
      setMonatsbelegStatus({ phase: 'loading' });
      getMonatsbelegStatus(id)
        .then((data) => {
          if (cancelled) return;
          setMonatsbelegStatus({ phase: 'success', data });
        })
        .catch(() => {
          if (cancelled) return;
          setMonatsbelegStatus({ phase: 'error' });
        });
      return () => {
        cancelled = true;
      };
    }, [posReadiness.data?.effectiveRegisterId])
  );

  useEffect(() => {
    if (monatsbelegStatus.phase !== 'success') return;
    const w = monatsbelegStatus.data.warningLevel;
    if (w !== prevMbWarningLevelRef.current) {
      prevMbWarningLevelRef.current = w;
      setYellowBannerDismissed(false);
    }
  }, [monatsbelegStatus]);

  const handleMonatsbelegNavigate = useCallback(() => {
    router.push('/(tabs)/settings' as const);
  }, [router]);

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

  const {
    pendingModifiersByProduct,
    handleAddProduct,
    handleAddAddOn,
  } = usePOSOrderFlow(addItem, activeTableId, addToast, t);
  /** Merged modifier selection per product: last cart line or pending (for inline chips). */
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

  const handleApplyWithBase = useCallback(
    async (base: { productId: string; productName: string; price: number }, addOns: { productId: string; productName: string; price: number }[]) => {
      try {
        await addItemWithAddOns(base.productId, base.productName, base.price, addOns);
        addToast('success', t('checkout:posFlow.toast.baseProductAdded', { name: base.productName }), 2000);
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
  const handleTableSelect = useCallback(async (tableNumber: number) => {
    if (!tableNumber || tableNumber < 1 || tableNumber > 10) {
      addToast('error', t('checkout:posFlow.toast.invalidTableNumber'), 3000);
      return;
    }

    if (activeTableId === tableNumber) {
      return;
    }

    setTableSelectionLoading(tableNumber);
    const clearLoading = () => setTableSelectionLoading(null);
    const loadingTimeout = setTimeout(clearLoading, 8000);

    try {
      // Timeout so loading always clears even if fetch hangs (regression: stuck loading blocked other tables)
      await Promise.race([
        switchTable(tableNumber),
        new Promise<never>((_, reject) =>
          setTimeout(() => reject(new Error('Table switch timeout')), 6000)
        ),
      ]);
      addToast('info', t('checkout:posFlow.toast.switchingToTable', { table: tableNumber }), 2000);
    } catch (error) {
      console.error('❌ Masa seçim hatası:', error);
      addToast('error', t('checkout:posFlow.toast.tableSwitchFailed'), 3000);
    } finally {
      clearTimeout(loadingTimeout);
      setTimeout(clearLoading, 300);
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
      addToast('error', t('checkout:posFlow.toast.updateFailed'), 2000);
    }
  }, [activeTableId, getCartForTable, updateItemQuantityByItemId, addToast]);

  const handleItemRemove = useCallback(async (itemId: string) => {
    if (!activeTableId) return;
    try {
      await removeByItemId(itemId);
      addToast('success', t('checkout:posFlow.toast.itemRemoved'), 2000);
    } catch {
      addToast('error', t('checkout:posFlow.toast.itemRemoveFailed'), 3000);
    }
  }, [activeTableId, removeByItemId, addToast]);

  const handleClearCart = useCallback(async () => {
    if (!activeTableId) {
      addToast('error', t('checkout:posFlow.toast.selectTableFirst'), 3000);
      return;
    }

    try {
      await clearCart(activeTableId);
      addToast('success', t('checkout:posFlow.toast.cartClearedForTable', { table: activeTableId }), 2000);

    } catch (err) {
      console.error(`❌ Error clearing table ${activeTableId}:`, err);
      addToast('error', t('checkout:posFlow.toast.tableClearFailed', { table: activeTableId }), 3000);
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
    if (isReadinessStartbelegGateActive(posReadiness.data, { ensureReadyEnabled: POS_ENSURE_READY_ON_ENTRY })) {
      Alert.alert(
        'Startbeleg erforderlich',
        'Bitte zuerst den fiskalischen Startbeleg erstellen, bevor Sie zur Zahlung wechseln.'
      );
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

  return (
    <SafeAreaView style={styles.container}>
      {/* Toast Notifications */}
      <ToastContainer toasts={toasts} onRemove={removeToast} />

      {/* Customer identification sheet for customer attachment */}
      <CustomerSelectionSheet
        visible={customerSheetVisible}
        onClose={() => setCustomerSheetVisible(false)}
        onSelect={(c) => {
          setSaleCustomer(c);
          setCustomerSheetVisible(false);
        }}
      />

      {/* Add-on selection bottom sheet: base + add-ons as flat cart lines */}
      {modifierSheetProduct && (
        <ModifierSelectionBottomSheet
          visible
          productId={modifierSheetProduct.id}
          productName={modifierSheetProduct.name}
          productPrice={modifierSheetProduct.price ?? 0}
          modifierGroups={modifierSheetProduct.modifierGroups ?? undefined}
          onClose={() => setModifierSheetProduct(null)}
          onApplyWithBase={handleApplyWithBase}
        />
      )}

      {/* Header */}
      <CashRegisterHeader
        selectedTable={activeTableId}
        recoveryLoading={recoveryLoading}
        provisioningMessage={recoveryProvisioningMessage}
      />

      <MonatsbelegWarningBannerStrip
        monatsbelegStatus={monatsbelegStatus}
        yellowDismissed={yellowBannerDismissed}
        onDismissYellow={() => setYellowBannerDismissed(true)}
        onNavigateCreate={handleMonatsbelegNavigate}
        t={t}
      />

      <MonatsbelegCurrentMonthOverduePanel
        monatsbelegStatus={monatsbelegStatus}
        onNavigateCreate={handleMonatsbelegNavigate}
        t={t}
      />

      {/* Root List - ProductList acts as the main scrollable container */}
      {/* Stock info intentionally hidden from cashier UI. Stock management is handled in admin panel. Kept in code for potential future POS usage. */}
      <ProductList
        categoryFilterId={selectedCategoryId}
        pendingModifiersByProduct={selectedModifiersForProduct}
        onAddProduct={handleAddProduct}
        onAddAddOn={handleAddAddOn}
        onOpenAddOnSheet={setModifierSheetProduct}
        showStockInfo={false}
        showTaxInfo
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
            onOpenCustomerSheet={() => setCustomerSheetVisible(true)}
            onClearCustomer={() => setSaleCustomer(null)}
            benefitSummaryCount={benefitSummaryCount}
            t={t}
          />
        } 
      />

    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },

  mbBanner: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: SoftColors.borderLight,
    gap: SoftSpacing.sm,
  },
  mbBannerYellow: {
    backgroundColor: SoftColors.warningBg,
  },
  mbBannerRed: {
    backgroundColor: SoftColors.errorBg,
    borderLeftWidth: 4,
    borderLeftColor: SoftColors.error,
  },
  mbBannerText: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textPrimary,
    fontWeight: '600',
  },
  mbBannerTextRed: {
    color: SoftColors.error,
  },
  mbBannerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: SoftSpacing.sm,
  },
  mbBannerCta: {
    alignSelf: 'flex-start',
    backgroundColor: SoftColors.accent,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
  },
  mbBannerCtaPressed: {
    opacity: 0.85,
  },
  mbBannerCtaText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
    ...SoftTypography.label,
  },
  mbDismissBtn: {
    padding: SoftSpacing.xs,
    marginLeft: 'auto',
  },

  mbOverduePanel: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.md,
    backgroundColor: SoftColors.warningBg,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: SoftColors.borderLight,
    borderLeftWidth: 4,
    borderLeftColor: SoftColors.warning,
    gap: SoftSpacing.xs,
  },
  mbOverdueTitle: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
    fontWeight: '700',
  },
  mbOverdueBody: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textPrimary,
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