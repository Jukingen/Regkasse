import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { apiClient } from '../services/api/config';
import type { AddItemToCartRequest } from '../services/api/cartService';

// ============================================
// TYPE DEFINITIONS
// ============================================

/** Sepet satırında seçilen tek bir modifier (Extra Zutaten). price = priceDelta per unit. */
export interface CartItemModifier {
    id: string;
    name: string;
    price: number;
}

/** Payment payload için: modifierId, name, priceDelta (groupId opsiyonel). */
export type ModifierSelection = { modifierId: string; name: string; priceDelta: number; groupId?: string };

export interface CartItem {
    /** Satır kimliği: itemId (backend) veya clientId (iyimser ekleme). */
    itemId?: string;
    /** İyimser eklemede satır anahtarı; backend itemId gelene kadar kullanılır */
    clientId?: string;
    productId: string;
    productName: string;
    price?: number;
    qty: number;
    unitPrice?: number;
    /** Satır toplamı = (unitPrice + extrasPerUnitTotal) * qty. */
    totalPrice?: number;
    notes?: string;
    taxRate?: number;
    taxType?: number | string;
    /** Extra Zutaten – sadece bu satır için seçilen modifier'lar (extrasPerUnitTotal = sum(price)). */
    modifiers?: CartItemModifier[];
}

export interface Cart {
    items: CartItem[];
    updatedAt?: number;
    cartId?: string;
    /** Backend toplamları - FE HİÇBİR vergi/total hesaplaması yapmaz */
    subtotalGross?: number;
    subtotalNet?: number;
    includedTaxTotal?: number;
    grandTotalGross?: number;
    taxSummary?: Array<{ taxType: number; taxRatePct: number; netAmount: number; taxAmount: number; grossAmount: number }>;
}

/** Backend alanlarını seçip UI için formatlar - HESAPLAMA YOK */
export function getCartDisplayTotals(cart: Cart | null | undefined): {
    subtotalGross: number; includedTaxTotal: number; grandTotalGross: number; itemCount: number; taxSummary?: Cart['taxSummary'];
} {
    const itemCount = (cart?.items ?? []).reduce((sum, i) => sum + (i.qty ?? 0), 0);
    return {
        subtotalGross: cart?.subtotalGross ?? 0,
        includedTaxTotal: cart?.includedTaxTotal ?? 0,
        grandTotalGross: cart?.grandTotalGross ?? 0,
        itemCount,
        taxSummary: cart?.taxSummary
    };
}

export interface CartsByTable {
    [tableNumber: number]: Cart;
}

interface CartContextType {
    // State
    activeTableId: number;
    cartsByTable: CartsByTable;
    loading: boolean;
    error: string | null;
    isPaymentModalVisible: boolean;

    // Actions
    setActiveTable: (tableNumber: number) => void;
    setIsPaymentModalVisible: (visible: boolean) => void;
    switchTable: (tableNumber: number) => Promise<void>; // ✅ Added
    addItem: (productId: string, quantity?: number, options?: { modifiers?: CartItemModifier[]; productName?: string; unitPrice?: number }) => Promise<void>;
    increment: (productId: string) => Promise<void>;
    decrement: (productId: string) => Promise<void>;
    remove: (productId: string) => Promise<void>;
    removeByItemId: (itemId: string) => Promise<void>;
    clearCart: (tableNumber?: number) => Promise<void>;
    checkout: (tableNumber?: number) => Promise<void>;
    getCartForTable: (tableNumber: number) => Cart;
    updateItemQuantity: (productId: string, quantity: number) => Promise<void>;
    updateItemQuantityByItemId: (itemId: string, quantity: number) => Promise<void>;
    fetchTableCart: (tableNumber: number) => Promise<void>;
    /** Sepetteki bir satıra extra ekle/çıkar (sadece local state; API yok). cartItemId = itemId ?? clientId */
    toggleExtraOnCartItem: (cartItemId: string, modifier: CartItemModifier) => void;

    // Helpers
    setLoading: (loading: boolean) => void;
    setError: (error: string | null) => void;
}

// ============================================
// API RESPONSE TYPES
// ============================================
interface AddItemResponse {
    message: string;
    cart: any; // Backend type
}

const CartContext = createContext<CartContextType | undefined>(undefined);

export const CartProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
    // State
    const [activeTableId, setActiveTableId] = useState<number>(1);
    const [cartsByTable, setCartsByTable] = useState<CartsByTable>({});
    const [loading, setLoadingState] = useState<boolean>(false);
    const [error, setErrorState] = useState<string | null>(null);
    const [isPaymentModalVisible, setIsPaymentModalVisible] = useState<boolean>(false);

    // Load from AsyncStorage on mount
    useEffect(() => {
        const loadState = async () => {
            try {
                const savedState = await AsyncStorage.getItem('cart-storage');
                if (savedState) {
                    const parsed = JSON.parse(savedState);
                    if (parsed.state) {
                        if (parsed.state.activeTableId) setActiveTableId(parsed.state.activeTableId);
                        if (parsed.state.cartsByTable) setCartsByTable(parsed.state.cartsByTable);
                    }
                }
            } catch (e) {
                console.error('Failed to load cart state', e);
            }
        };
        loadState();
    }, []);

    // Save to AsyncStorage on change
    useEffect(() => {
        const saveState = async () => {
            try {
                const stateToSave = {
                    state: {
                        activeTableId,
                        cartsByTable
                    },
                    version: 0
                };
                await AsyncStorage.setItem('cart-storage', JSON.stringify(stateToSave));
            } catch (e) {
                console.warn('Failed to save cart state', e);
            }
        };
        saveState();
    }, [activeTableId, cartsByTable]);

    // Helpers
    const setLoading = useCallback((l: boolean) => setLoadingState(l), []);
    const setError = useCallback((e: string | null) => setErrorState(e), []);
    // ✅ Helper: Get cart for specific table (or empty)
    const getCartForTable = useCallback((tableNumber: number): Cart => {
        return cartsByTable[tableNumber] || { items: [] };
    }, [cartsByTable]);

    // ✅ Ref to track last fetched table to prevent duplicate calls/loops
    const lastFetchedTableIdRef = React.useRef<number | null>(null);

    // ✅ Helper: Fetch fresh cart data from backend for a specific table
    const fetchTableCart = useCallback(async (tableNumber: number, forceRefresh = false) => {
        // Prevent duplicate fetch for same table (Brute-force Guard)
        if (!forceRefresh && lastFetchedTableIdRef.current === tableNumber) {
            console.log(`🛡️ [CartContext] Skipping fetch for Table ${tableNumber} (Already fetched/In-progress)`);
            return;
        }

        if (forceRefresh) lastFetchedTableIdRef.current = null;
        lastFetchedTableIdRef.current = tableNumber;
        setLoading(true);
        console.log(`🔄 [CartContext] Fetching fresh data for Table ${tableNumber}...`);
        try {
            // GET /cart/current?tableNumber=X (equivalent to GET /Table/{id})
            const response = await apiClient.get<AddItemResponse['cart']>(`/cart/current?tableNumber=${tableNumber}`);

            if (response && (response.items || response.Items)) {
                const backendItems = response.items || response.Items || [];

                setCartsByTable(prev => {
                    const currentItems = prev[tableNumber]?.items ?? [];
                    const localItems: CartItem[] = backendItems.map((item: any) => {
                        const pName = item.ProductName || item.productName;
                        if (!pName) {
                            console.error("[Cart] ❌ Missing ProductName for item:", item);
                        }
                        const backendItemId = item.Id ?? item.id;
                        const existing = currentItems.find((e: CartItem) => (e.itemId ?? e.clientId) === backendItemId);
                        const backendMods = item.SelectedModifiers ?? item.selectedModifiers ?? item.Modifiers ?? item.modifiers;
                        let modifierList: CartItemModifier[] | undefined;
                        if (Array.isArray(backendMods) && backendMods.length) {
                            modifierList = backendMods.map((m: any) => ({
                                id: m.Id ?? m.id,
                                name: m.Name ?? m.name,
                                price: Number(m.Price ?? m.price ?? 0)
                            }));
                        } else if (existing?.modifiers?.length) {
                            modifierList = existing.modifiers;
                        }
                        const basePrice = item.UnitPrice ?? item.unitPrice ?? 0;
                        const qty = item.Quantity ?? item.quantity ?? 0;
                        const extrasPerUnit = (modifierList ?? []).reduce((s, m) => s + m.price, 0);
                        const totalPrice = (basePrice + extrasPerUnit) * qty;

                        return {
                            productId: item.ProductId || item.productId,
                            productName: pName || 'Unknown Product',
                            price: basePrice + extrasPerUnit,
                            qty,
                            unitPrice: basePrice,
                            totalPrice,
                            notes: item.Notes ?? item.notes,
                            itemId: backendItemId,
                            clientId: existing?.clientId,
                            taxRate: item.TaxRate ?? item.taxRate,
                            taxType: item.TaxType ?? item.taxType,
                            modifiers: modifierList
                        };
                    });

                    const grandTotalGross = localItems.reduce((s, i) => s + (i.totalPrice ?? 0), 0);
                    return {
                        ...prev,
                        [tableNumber]: {
                            items: localItems,
                            updatedAt: Date.now(),
                            cartId: response.CartId || response.cartId,
                            subtotalGross: response.SubtotalGross ?? response.subtotalGross ?? grandTotalGross,
                            subtotalNet: response.SubtotalNet ?? response.subtotalNet,
                            includedTaxTotal: response.IncludedTaxTotal ?? response.includedTaxTotal,
                            grandTotalGross: response.GrandTotalGross ?? response.grandTotalGross ?? grandTotalGross,
                            taxSummary: response.TaxSummary ?? response.taxSummary
                        }
                    };
                });
                console.log(`✅ [CartContext] Table ${tableNumber} updated with ${backendItems.length} items`);
            } else {
                // If response is null or no items, implies empty cart or new table
                setCartsByTable(prev => ({
                    ...prev,
                    [tableNumber]: { items: [], updatedAt: Date.now() }
                }));
            }
        } catch (err: any) {
            console.error(`❌ [CartContext] Failed to fetch table ${tableNumber}:`, err);
            // setError causes global error state which might block UI. 
            // Better to show toast or just log? User asked for English error message.
            setError(`Failed to load table ${tableNumber}: ${err.message || 'Unknown error'}`);

            // Allow retry if failed
            lastFetchedTableIdRef.current = null;
        } finally {
            setLoading(false);
        }
    }, [setLoading, setError]);

    const setActiveTable = useCallback((id: number) => {
        if (id !== activeTableId) {
            setActiveTableId(id);
        }
    }, [activeTableId]);

    // ✅ NEW: Imperative Table Switch Logic
    const switchTable = useCallback(async (tableNumber: number) => {
        if (tableNumber === activeTableId) {
            // Optional: Allow refresh if clicked again? For now, simple return to avoid loop.
            // If user explicitly clicks same table, maybe we SHOULD refresh?
            // But for loop prevention, let's keep it safe.
            console.log(`[CartContext] Already on table ${tableNumber}, skipping switch.`);
            return;
        }

        console.log(`[CartContext] Switching active table: ${activeTableId} -> ${tableNumber}`);
        setActiveTableId(tableNumber);

        // Reset fetch guard for the new table to ensure we definitely fetch
        // (Use with caution if calling rapidly)
        if (lastFetchedTableIdRef.current === tableNumber) {
            lastFetchedTableIdRef.current = null;
        }

        await fetchTableCart(tableNumber);
    }, [activeTableId, fetchTableCart]);

    /** Aynı ürün + aynı modifier set = aynı satır (karşılaştırma anahtarı). */
    const getModifierKey = useCallback((modifiers: CartItemModifier[] | undefined) => {
        if (!modifiers?.length) return '';
        return [...modifiers].map(m => m.id).sort().join(',');
    }, []);

    // Actions
    const addItem = useCallback(async (
        productId: string,
        quantity: number = 1,
        options?: { modifiers?: CartItemModifier[]; productName?: string; unitPrice?: number }
    ) => {
        const currentCart = cartsByTable[activeTableId] || { items: [] };
        const modifiers = options?.modifiers ?? [];
        const modKey = getModifierKey(modifiers);
        const productName = options?.productName ?? 'Loading...';
        const unitPrice = options?.unitPrice ?? 0;
        const modifierTotal = modifiers.reduce((s, m) => s + m.price, 0);
        const lineUnitPrice = unitPrice + modifierTotal;
        const lineTotalPrice = lineUnitPrice * quantity;

        console.log(`➕ [CartContext] Adding item to table ${activeTableId}:`, { productId, quantity, modifiersCount: modifiers.length });

        // Aynı productId + aynı modifier set = mevcut satıra miktar ekle
        const existingItemIndex = currentCart.items.findIndex(
            item => item.productId === productId && getModifierKey(item.modifiers) === modKey
        );
        let optimisticCart: Cart;

        if (existingItemIndex !== -1) {
            const item = currentCart.items[existingItemIndex];
            const newQty = item.qty + quantity;
            const modSum = (item.modifiers ?? []).reduce((s, m) => s + m.price, 0);
            const newTotal = ((item.unitPrice ?? 0) + modSum) * newQty;
            optimisticCart = {
                ...currentCart,
                items: currentCart.items.map((it, index) =>
                    index === existingItemIndex ? { ...it, qty: newQty, totalPrice: newTotal } : it
                ),
                updatedAt: Date.now()
            };
        } else {
            const clientId = `ci-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
            optimisticCart = {
                ...currentCart,
                items: [
                    ...currentCart.items,
                    {
                        productId,
                        productName,
                        price: lineUnitPrice,
                        qty: quantity,
                        unitPrice: unitPrice,
                        totalPrice: lineTotalPrice,
                        modifiers: modifiers.length ? [...modifiers] : undefined,
                        itemId: undefined,
                        clientId
                    }
                ],
                updatedAt: Date.now()
            };
        }

        setCartsByTable(prev => ({
            ...prev,
            [activeTableId]: optimisticCart
        }));
        setError(null);

        // PHASE 2: Backend Call – backend contract: selectedModifiers (not modifierIds)
        try {
            const body: AddItemToCartRequest = {
                productId,
                quantity,
                tableNumber: activeTableId
            };
            if (modifiers.length) {
                body.selectedModifiers = modifiers.map(m => ({ id: m.id }));
            }

            const response = await apiClient.post<AddItemResponse>('/cart/add-item', body);

            if (response.cart) {
                const backendCart = response.cart;
                const backendItems = backendCart.Items || backendCart.items || [];

                const existingItems = currentCart.items;
                const localItems: CartItem[] = backendItems.map((item: any, index: number) => {
                    const backendItemId = item.Id ?? item.id;
                    const mods = item.SelectedModifiers ?? item.selectedModifiers ?? item.Modifiers ?? item.modifiers;
                    let modifierList: CartItemModifier[] | undefined;
                    if (Array.isArray(mods) && mods.length) {
                        modifierList = mods.map((m: any) => ({
                            id: m.Id ?? m.id,
                            name: m.Name ?? m.name,
                            price: Number(m.Price ?? m.price ?? 0)
                        }));
                    } else {
                        const existing = existingItems.find((e: CartItem) => e.itemId === backendItemId || (e.productId === (item.ProductId || item.productId) && !e.itemId));
                        if (existing?.modifiers?.length) modifierList = existing.modifiers;
                    }
                    const existingByIndex = existingItems[index];
                    return {
                        productId: item.ProductId || item.productId,
                        productName: item.ProductName || item.productName || 'Unknown Product',
                        price: item.UnitPrice || item.unitPrice || 0,
                        qty: item.Quantity || item.quantity || 0,
                        unitPrice: item.UnitPrice ?? item.unitPrice ?? 0,
                        totalPrice: item.TotalPrice || item.totalPrice || 0,
                        notes: item.Notes || item.notes,
                        itemId: item.Id || item.id,
                        clientId: existingByIndex?.clientId,
                        taxRate: item.TaxRate ?? item.taxRate,
                        taxType: item.TaxType ?? item.taxType,
                        modifiers: modifierList
                    };
                });

                setCartsByTable(prev => ({
                    ...prev,
                    [activeTableId]: {
                        items: localItems,
                        updatedAt: Date.now(),
                        cartId: backendCart.CartId || backendCart.cartId,
                        subtotalGross: backendCart.SubtotalGross ?? backendCart.subtotalGross,
                        subtotalNet: backendCart.SubtotalNet ?? backendCart.subtotalNet,
                        includedTaxTotal: backendCart.IncludedTaxTotal ?? backendCart.includedTaxTotal,
                        grandTotalGross: backendCart.GrandTotalGross ?? backendCart.grandTotalGross,
                        taxSummary: backendCart.TaxSummary ?? backendCart.taxSummary
                    }
                }));
            }
        } catch (err: any) {
            const msg = err?.data?.message || err?.message || 'Failed to add item';
            console.error('❌ [CartContext] Failed:', msg);
            setError(msg);

            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: currentCart
            }));
            throw new Error(msg);
        }
    }, [activeTableId, cartsByTable, getModifierKey]);

    const remove = useCallback(async (productId: string) => {
        const currentCart = cartsByTable[activeTableId];
        if (!currentCart) {
            console.warn('⚠️ [remove] No cart found for table', activeTableId);
            return;
        }

        // Find the item in local state
        const item = currentCart.items.find(i => i.productId === productId);
        if (!item) {
            console.warn('⚠️ [remove] Item not found in cart', { productId });
            return;
        }

        if (!item.itemId) {
            console.error('❌ [remove] Item missing itemId', { productId, item });
            setError('Cannot remove item: missing item ID');
            return;
        }

        if (!currentCart.cartId) {
            console.error('❌ [remove] Cart missing cartId');
            setError('Cannot remove item: invalid cart');
            return;
        }

        console.log('🗑️ [remove] Removing item', { productId, itemId: item.itemId, cartId: currentCart.cartId });

        setLoading(true);
        try {
            // Optimistic update
            const updatedItems = currentCart.items.filter(i => i.productId !== productId);
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: { ...currentCart, items: updatedItems, updatedAt: Date.now() }
            }));

            // Backend DELETE using itemId directly from local state
            await apiClient.delete(`/Cart/items/${item.itemId}`);

            console.log('✅ [remove] Item removed successfully');
        } catch (err: any) {
            console.error('❌ [remove] Failed to remove item', err);
            setError(err.message || 'Failed to remove item');

            // Rollback on error
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: currentCart
            }));
        } finally {
            setLoading(false);
        }
    }, [activeTableId, cartsByTable, setLoading, setError]);

    // ✅ Helper: Update specific quantity
    const updateItemQuantity = useCallback(async (productId: string, quantity: number) => {
        const currentCart = cartsByTable[activeTableId];
        if (!currentCart) {
            console.warn('⚠️ [updateItemQuantity] No cart found for table', activeTableId);
            return;
        }

        // If quantity is 0 or negative, remove the item
        if (quantity <= 0) {
            await remove(productId);
            return;
        }

        const item = currentCart.items.find(i => i.productId === productId);
        if (!item) {
            console.warn('⚠️ [updateItemQuantity] Item not found', { productId });
            return;
        }

        if (!item.itemId) {
            console.error('❌ [updateItemQuantity] Item missing itemId', { productId, item });
            setError('Cannot update item: missing item ID');
            return;
        }

        console.log('🔄 [updateItemQuantity] Updating quantity', { itemId: item.itemId, oldQty: item.qty, newQty: quantity });

        setLoading(true);
        try {
            // Optimistic update
            const updatedItems = currentCart.items.map(i => i.productId === productId ? { ...i, qty: quantity } : i);
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: {
                    ...currentCart,
                    items: updatedItems,
                    updatedAt: Date.now()
                }
            }));

            // PUT /cart/items/{itemId} with {quantity} - using itemId directly from state
            await apiClient.put(`/Cart/items/${item.itemId}`, {
                Quantity: quantity,
                Notes: item.notes || ""
            });

            console.log('✅ [updateItemQuantity] Quantity updated successfully');
            await fetchTableCart(activeTableId, true);
        } catch (e: any) {
            console.error('❌ [updateItemQuantity] Failed to update quantity', e);
            setError(e.message || 'Failed to update quantity');

            // Rollback on error
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: currentCart
            }));
        } finally {
            setLoading(false);
        }
    }, [activeTableId, cartsByTable, remove, setLoading, setError, fetchTableCart]);

    const removeByItemId = useCallback(async (itemId: string) => {
        const currentCart = cartsByTable[activeTableId];
        if (!currentCart) return;
        const lineKey = (i: CartItem) => i.itemId ?? `${i.productId}-${getModifierKey(i.modifiers)}`;
        const item = currentCart.items.find(i => lineKey(i) === itemId);
        if (!item) {
            setError('Item not found');
            return;
        }
        if (!item.itemId) {
            setLoading(true);
            try {
                const updatedItems = currentCart.items.filter(i => lineKey(i) !== itemId);
                setCartsByTable(prev => ({
                    ...prev,
                    [activeTableId]: { ...currentCart, items: updatedItems, updatedAt: Date.now() }
                }));
            } finally {
                setLoading(false);
            }
            return;
        }
        setLoading(true);
        try {
            const updatedItems = currentCart.items.filter(i => lineKey(i) !== itemId);
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: { ...currentCart, items: updatedItems, updatedAt: Date.now() }
            }));
            await apiClient.delete(`/Cart/items/${item.itemId}`);
            await fetchTableCart(activeTableId, true);
        } catch (err: any) {
            setError(err?.message || 'Failed to remove item');
            setCartsByTable(prev => ({ ...prev, [activeTableId]: currentCart }));
        } finally {
            setLoading(false);
        }
    }, [activeTableId, cartsByTable, getModifierKey, setLoading, setError, fetchTableCart]);

    const updateItemQuantityByItemId = useCallback(async (itemId: string, quantity: number) => {
        const currentCart = cartsByTable[activeTableId];
        if (!currentCart) return;
        if (quantity <= 0) {
            await removeByItemId(itemId);
            return;
        }
        const lineKey = (i: CartItem) => i.itemId ?? `${i.productId}-${getModifierKey(i.modifiers)}`;
        const item = currentCart.items.find(i => lineKey(i) === itemId);
        if (!item) {
            setError('Item not found');
            return;
        }
        if (!item.itemId) {
            setLoading(true);
            try {
                const modSum = (item.modifiers ?? []).reduce((s, m) => s + m.price, 0);
                const newTotal = ((item.unitPrice ?? 0) + modSum) * quantity;
                const updatedItems = currentCart.items.map(i =>
                    lineKey(i) === itemId ? { ...i, qty: quantity, totalPrice: newTotal } : i
                );
                setCartsByTable(prev => ({
                    ...prev,
                    [activeTableId]: { ...currentCart, items: updatedItems, updatedAt: Date.now() }
                }));
            } finally {
                setLoading(false);
            }
            return;
        }
        setLoading(true);
        try {
            const modSum = (item.modifiers ?? []).reduce((s, m) => s + m.price, 0);
            const newTotal = ((item.unitPrice ?? 0) + modSum) * quantity;
            const updatedItems = currentCart.items.map(i =>
                (i.itemId || i.productId) === itemId ? { ...i, qty: quantity, totalPrice: newTotal } : i
            );
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: { ...currentCart, items: updatedItems, updatedAt: Date.now() }
            }));
            await apiClient.put(`/Cart/items/${item.itemId}`, {
                Quantity: quantity,
                Notes: item.notes || ''
            });
            await fetchTableCart(activeTableId, true);
        } catch (e: any) {
            setError(e?.message || 'Failed to update quantity');
            setCartsByTable(prev => ({ ...prev, [activeTableId]: currentCart }));
        } finally {
            setLoading(false);
        }
    }, [activeTableId, cartsByTable, removeByItemId, setLoading, setError, fetchTableCart]);

    /** Sepetteki bir satıra extra ekle/çıkar; sadece local state güncellenir, API yok. */
    const toggleExtraOnCartItem = useCallback((cartItemId: string, modifier: CartItemModifier) => {
        const currentCart = cartsByTable[activeTableId];
        if (!currentCart?.items?.length) return;

        const lineKey = (i: CartItem) => i.itemId ?? i.clientId;
        const index = currentCart.items.findIndex((i) => lineKey(i) === cartItemId);
        if (index === -1) return;

        const item = currentCart.items[index];
        const mods = item.modifiers ?? [];
        const has = mods.some((m) => m.id === modifier.id);
        const nextMods = has ? mods.filter((m) => m.id !== modifier.id) : [...mods, { id: modifier.id, name: modifier.name, price: modifier.price }];
        const basePrice = item.unitPrice ?? item.price ?? 0;
        const extrasPerUnit = nextMods.reduce((s, m) => s + m.price, 0);
        const qty = item.qty ?? 0;
        const newTotalPrice = (basePrice + extrasPerUnit) * qty;

        const updatedItems = currentCart.items.map((it, i) =>
            i === index
                ? {
                      ...it,
                      modifiers: nextMods.length ? nextMods : undefined,
                      totalPrice: newTotalPrice,
                      price: basePrice + extrasPerUnit
                  }
                : it
        );

        const grandTotalGross = updatedItems.reduce((s, i) => s + (i.totalPrice ?? 0), 0);

        setCartsByTable((prev) => ({
            ...prev,
            [activeTableId]: {
                ...currentCart,
                items: updatedItems,
                subtotalGross: grandTotalGross,
                grandTotalGross,
                updatedAt: Date.now()
            }
        }));
    }, [activeTableId, cartsByTable]);

    const increment = useCallback(async (productId: string) => {
        const currentCart = cartsByTable[activeTableId];
        if (!currentCart) {
            console.warn('⚠️ [increment] No cart found for table', activeTableId);
            return;
        }

        const item = currentCart.items.find(i => i.productId === productId);
        if (!item || !item.itemId) {
            console.error('❌ [increment] Item or itemId missing', { productId, item });
            setError('Invalid cart item');
            return;
        }

        const newQuantity = item.qty + 1;
        console.log('🔼 [increment] Updating quantity', { itemId: item.itemId, oldQty: item.qty, newQty: newQuantity });

        setLoading(true);
        try {
            // Optimistic update - recalculate totalPrice for UI
            const updatedItems = currentCart.items.map(i => {
                if (i.productId === productId) {
                    const basePrice = i.unitPrice ?? i.price ?? 0;
                    const extrasPerUnit = (i.modifiers ?? []).reduce((s, m) => s + m.price, 0);
                    return {
                        ...i,
                        qty: newQuantity,
                        totalPrice: (basePrice + extrasPerUnit) * newQuantity,
                    };
                }
                return i;
            });
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: {
                    ...currentCart,
                    items: updatedItems,
                    updatedAt: Date.now()
                }
            }));

            // PUT /cart/items/{itemId} with {quantity}
            await apiClient.put(`/Cart/items/${item.itemId}`, {
                Quantity: newQuantity,
                Notes: item.notes || ""
            });
            console.log('✅ [increment] Quantity updated successfully');
            // Totals'ı güncellemek için backend'den taze veri çek (FE KDV hesaplamaz)
            await fetchTableCart(activeTableId, true);
        } catch (e: any) {
            console.error('❌ [increment] Failed to update quantity', e);
            setError('Failed to update item quantity');

            // Rollback optimistic update
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: currentCart
            }));
        } finally {
            setLoading(false);
        }
    }, [activeTableId, cartsByTable, setLoading, setError, fetchTableCart]);

    const decrement = useCallback(async (productId: string) => {
        const currentCart = cartsByTable[activeTableId];
        if (!currentCart) {
            console.warn('⚠️ [decrement] No cart found for table', activeTableId);
            return;
        }

        const item = currentCart.items.find(i => i.productId === productId);
        if (!item || !item.itemId) {
            console.error('❌ [decrement] Item or itemId missing', { productId, item });
            setError('Invalid cart item');
            return;
        }

        // If quantity is 1, DELETE the item instead of decrementing
        if (item.qty <= 1) {
            console.log('🔽 [decrement] Quantity is 1, removing item instead');
            await remove(productId);
            return;
        }

        const newQuantity = item.qty - 1;
        console.log('🔽 [decrement] Updating quantity', { itemId: item.itemId, oldQty: item.qty, newQty: newQuantity });

        setLoading(true);
        try {
            // Optimistic update - recalculate totalPrice for UI
            const updatedItems = currentCart.items.map(i => {
                if (i.productId === productId) {
                    const basePrice = i.unitPrice ?? i.price ?? 0;
                    const extrasPerUnit = (i.modifiers ?? []).reduce((s, m) => s + m.price, 0);
                    return {
                        ...i,
                        qty: newQuantity,
                        totalPrice: (basePrice + extrasPerUnit) * newQuantity,
                    };
                }
                return i;
            });
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: {
                    ...currentCart,
                    items: updatedItems,
                    updatedAt: Date.now()
                }
            }));

            // PUT /cart/items/{itemId} with {quantity}
            await apiClient.put(`/Cart/items/${item.itemId}`, {
                Quantity: newQuantity,
                Notes: item.notes || ""
            });
            console.log('✅ [decrement] Quantity updated successfully');
            await fetchTableCart(activeTableId, true);
        } catch (e: any) {
            console.error('❌ [decrement] Failed to update quantity', e);
            setError('Failed to update item quantity');

            // Rollback optimistic update
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: currentCart
            }));
        } finally {
            setLoading(false);
        }
    }, [activeTableId, cartsByTable, remove, setLoading, setError, fetchTableCart]);

    const clearCart = useCallback(async (tableNumber?: number) => {
        const target = tableNumber ?? activeTableId;
        setLoading(true);
        try {
            await apiClient.post(`/cart/clear?tableNumber=${target}`);

            // ✅ Fix: Don't delete the key, just empty the items
            setCartsByTable(prev => ({
                ...prev,
                [target]: {
                    ...prev[target],
                    items: [],
                    updatedAt: Date.now()
                }
            }));
        } catch (e) {
            console.error(e);
            throw e; // Re-throw to allow component to handle error
        } finally {
            setLoading(false);
        }
    }, [activeTableId]);

    const checkout = useCallback(async (tableNumber?: number) => {
        const target = tableNumber ?? activeTableId;
        await clearCart(target);
    }, [clearCart, activeTableId]);


    return (
        <CartContext.Provider value={{
            activeTableId,
            cartsByTable,
            loading,
            error,
            isPaymentModalVisible,
            setActiveTable,
            setIsPaymentModalVisible,
            switchTable, // ✅ Exposed
            addItem,
            increment,
            decrement,
            remove,
            removeByItemId,
            clearCart,
            checkout,
            getCartForTable,
            updateItemQuantity,
            updateItemQuantityByItemId,
            fetchTableCart,
            toggleExtraOnCartItem,
            setLoading,
            setError
        }}>
            {children}
        </CartContext.Provider>
    );
};

export const useCart = () => {
    const context = useContext(CartContext);
    if (!context) throw new Error('useCart must be used within CartProvider');
    return context;
};
