import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { apiClient } from '../services/api/config';
import type { AddItemToCartRequest } from '../services/api/cartService';
import type { AddOnSelection } from '../services/api/productModifiersService';
import { getCartForTableNumber } from '../utils/tableCartUtils';

// ============================================
// TYPE DEFINITIONS
// ============================================

/** Sepet satırında seçilen modifier; quantity bağımsız (ürün miktarıyla çarpılmaz). price = priceDelta per unit of modifier. */
export interface CartItemModifier {
    id: string;
    name: string;
    price: number;
    quantity: number;
    groupId?: string;
}

/** Payment payload için: modifierId, name, priceDelta, quantity (groupId opsiyonel). */
export type ModifierSelection = { modifierId: string; name: string; priceDelta: number; quantity?: number; groupId?: string };

export interface CartItem {
    /** Satır kimliği: itemId (backend) veya clientId (iyimser ekleme). */
    itemId?: string;
    /** İyimser eklemede satır anahtarı; backend itemId gelene kadar kullanılır */
    clientId?: string;
    productId: string;
    productName: string;
    price?: number;
    qty: number;
    /** Ürün baz birim fiyatı (modifier'sız). */
    unitPrice?: number;
    /** Satır toplamı = (unitPrice * qty) + sum(modifier.price * modifier.quantity). */
    totalPrice?: number;
    notes?: string;
    taxRate?: number;
    taxType?: number | string;
    /** Extra Zutaten – her biri kendi quantity ile; toplam = sum(price * quantity). */
    modifiers?: CartItemModifier[];
}

/** Satır toplamı: (ürün birim fiyat * ürün miktarı) + (her modifier: price * quantity). */
export function getCartLineTotal(item: CartItem): number {
    const base = (item.unitPrice ?? item.price ?? 0) * (item.qty ?? 0);
    const modTotal = (item.modifiers ?? []).reduce((s, m) => s + m.price * (m.quantity ?? 1), 0);
    return base + modTotal;
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
    // State: activeTableId is the single source of truth for "selected table"; currentCart is always that table's cart.
    activeTableId: number;
    /** Derived: cart for activeTableId only. Use this for summary/checkout so UI never shows another table's cart. */
    currentCart: Cart;
    cartsByTable: CartsByTable;
    loading: boolean;
    error: string | null;
    isPaymentModalVisible: boolean;

    // Actions
    setActiveTable: (tableNumber: number) => void;
    setIsPaymentModalVisible: (visible: boolean) => void;
    switchTable: (tableNumber: number) => Promise<void>; // ✅ Added
    addItem: (productId: string, quantity?: number, options?: { modifiers?: CartItemModifier[]; productName?: string; unitPrice?: number }) => Promise<void>;
    /** Base + add-ons: one line for base, one per add-on (flat cart). No parent_product_id in backend; order implies link. */
    addItemWithAddOns: (baseProductId: string, baseProductName: string, baseUnitPrice: number, addOns: AddOnSelection[]) => Promise<void>;
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
    /** Modifier miktarı bağımsız: ekle (qty 1 veya mevcutsa +1), artır, azalt, kaldır. cartItemId = itemId ?? clientId */
    addModifier: (cartItemId: string, modifier: Omit<CartItemModifier, 'quantity'> & { quantity?: number }) => Promise<void>;
    incrementModifier: (cartItemId: string, modifierId: string) => Promise<void>;
    decrementModifier: (cartItemId: string, modifierId: string) => Promise<void>;
    removeModifier: (cartItemId: string, modifierId: string) => Promise<void>;

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
    // ✅ Helper: Get cart for specific table (or empty). Uses shared util for table-switching contract.
    const getCartForTable = useCallback((tableNumber: number): Cart => {
        return getCartForTableNumber(cartsByTable, tableNumber);
    }, [cartsByTable]);

    // Single derived cart for active table – keeps summary/checkout in sync with selected table.
    // Regression: never use another table's cart. See __tests__/tableSwitchingContract.test.ts
    const currentCart = getCartForTableNumber(cartsByTable, activeTableId);

    // ✅ Ref to track last fetched table to prevent duplicate calls/loops
    const lastFetchedTableIdRef = React.useRef<number | null>(null);
    // Ref for current table so switchTable never uses stale closure on rapid table change
    const activeTableIdRef = React.useRef<number>(activeTableId);
    React.useEffect(() => {
        activeTableIdRef.current = activeTableId;
    }, [activeTableId]);

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
                            const byId = new Map<string, CartItemModifier>();
                            for (const m of backendMods) {
                                const id = m.Id ?? m.id;
                                const qty = Number(m.Quantity ?? m.quantity ?? 1);
                                const existing = byId.get(id);
                                if (existing) existing.quantity += qty;
                                else byId.set(id, { id, name: m.Name ?? m.name, price: Number(m.Price ?? m.price ?? 0), quantity: qty, groupId: m.GroupId ?? m.groupId });
                            }
                            modifierList = Array.from(byId.values());
                        } else if (existing?.modifiers?.length) {
                            modifierList = existing.modifiers;
                        }
                        const basePrice = item.UnitPrice ?? item.unitPrice ?? 0;
                        const qty = item.Quantity ?? item.quantity ?? 0;
                        const modTotal = (modifierList ?? []).reduce((s, m) => s + m.price * (m.quantity ?? 1), 0);
                        const totalPrice = basePrice * qty + modTotal;

                        return {
                            productId: item.ProductId || item.productId,
                            productName: pName || 'Unknown Product',
                            price: basePrice,
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

    // Table switch contract: Update activeTableId immediately so UI selected state and cart summary stay in sync; then fetch target table cart.
    const switchTable = useCallback(async (tableNumber: number) => {
        const current = activeTableIdRef.current;
        if (tableNumber === current) {
            console.log(`[CartContext] Already on table ${tableNumber}, skipping switch.`);
            return;
        }

        console.log(`[CartContext] Switching active table: ${current} -> ${tableNumber}`);
        setActiveTableId(tableNumber);
        activeTableIdRef.current = tableNumber;

        if (lastFetchedTableIdRef.current === tableNumber) {
            lastFetchedTableIdRef.current = null;
        }

        await fetchTableCart(tableNumber);
    }, [fetchTableCart]);

    /** Aynı ürün + aynı modifier set (id:qty) = aynı satır; merge'de sadece ürün miktarı artar. */
    const getModifierKey = useCallback((modifiers: CartItemModifier[] | undefined) => {
        if (!modifiers?.length) return '';
        return [...modifiers]
            .sort((a, b) => a.id.localeCompare(b.id))
            .map(m => `${m.id}:${m.quantity ?? 1}`)
            .join(',');
    }, []);

    // Actions
    const addItem = useCallback(async (
        productId: string,
        quantity: number = 1,
        options?: { modifiers?: CartItemModifier[]; productName?: string; unitPrice?: number }
    ) => {
        const currentCart = cartsByTable[activeTableId] || { items: [] };
        const rawMods = options?.modifiers ?? [];
        const modifiers: CartItemModifier[] = rawMods.map(m => ({
            id: m.id,
            name: m.name,
            price: m.price,
            quantity: m.quantity ?? 1,
            groupId: m.groupId
        }));
        const modKey = getModifierKey(modifiers);
        const productName = options?.productName ?? 'Loading...';
        const baseUnitPrice = options?.unitPrice ?? 0;
        const modifierTotal = modifiers.reduce((s, m) => s + m.price * m.quantity, 0);
        const lineTotalPrice = baseUnitPrice * quantity + modifierTotal;

        console.log(`➕ [CartContext] Adding item to table ${activeTableId}:`, { productId, quantity, modifiersCount: modifiers.length });

        // Aynı productId + aynı modifier set (id:qty) = mevcut satıra sadece ürün miktarı ekle; modifier miktarları değişmez
        const existingItemIndex = currentCart.items.findIndex(
            item => item.productId === productId && getModifierKey(item.modifiers) === modKey
        );
        let optimisticCart: Cart;

        if (existingItemIndex !== -1) {
            const item = currentCart.items[existingItemIndex];
            const newQty = item.qty + quantity;
            const modTotal = (item.modifiers ?? []).reduce((s, m) => s + m.price * (m.quantity ?? 1), 0);
            const newTotal = (item.unitPrice ?? item.price ?? 0) * newQty + modTotal;
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
                        price: baseUnitPrice,
                        qty: quantity,
                        unitPrice: baseUnitPrice,
                        totalPrice: lineTotalPrice,
                        modifiers: modifiers.length ? modifiers : undefined,
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

        // Phase D PR-B: add-item no longer sends selectedModifiers; add-ons are separate lines.
        // Guard: do not add selectedModifiers to body (POS add-on = separate cart lines).
        try {
            const body: AddItemToCartRequest = {
                productId,
                quantity,
                tableNumber: activeTableId
            };

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
                        const byId = new Map<string, CartItemModifier>();
                        for (const m of mods) {
                            const id = m.Id ?? m.id;
                            const existing = byId.get(id);
                            const qty = Number(m.Quantity ?? m.quantity ?? 1);
                            if (existing) existing.quantity += qty;
                            else byId.set(id, { id, name: m.Name ?? m.name, price: Number(m.Price ?? m.price ?? 0), quantity: qty, groupId: m.GroupId ?? m.groupId });
                        }
                        modifierList = Array.from(byId.values());
                    } else {
                        const existing = existingItems.find((e: CartItem) => e.itemId === backendItemId || (e.productId === (item.ProductId || item.productId) && !e.itemId));
                        if (existing?.modifiers?.length) modifierList = existing.modifiers;
                    }
                    const existingByIndex = existingItems[index];
                    const qty = item.Quantity ?? item.quantity ?? 0;
                    const baseUnit = item.UnitPrice ?? item.unitPrice ?? 0;
                    const modTotal = (modifierList ?? []).reduce((s, m) => s + m.price * (m.quantity ?? 1), 0);
                    return {
                        productId: item.ProductId || item.productId,
                        productName: item.ProductName || item.productName || 'Unknown Product',
                        price: baseUnit,
                        qty,
                        unitPrice: baseUnit,
                        totalPrice: modifierList?.length ? baseUnit * qty + modTotal : (item.TotalPrice ?? item.totalPrice ?? baseUnit * qty),
                        notes: item.Notes ?? item.notes,
                        itemId: item.Id ?? item.id,
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

    /** Base + add-ons: one line for base (no modifiers), then one line per add-on. Flat cart; parent_product_id not in backend. */
    const addItemWithAddOns = useCallback(async (
        baseProductId: string,
        baseProductName: string,
        baseUnitPrice: number,
        addOns: AddOnSelection[]
    ) => {
        await addItem(baseProductId, 1, { productName: baseProductName, unitPrice: baseUnitPrice });
        for (const a of addOns) {
            await addItem(a.productId, 1, { productName: a.productName, unitPrice: a.price });
        }
    }, [addItem]);

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
        const lineKey = (i: CartItem) => i.itemId ?? i.clientId ?? `${i.productId}-${getModifierKey(i.modifiers)}`;
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
            const status = err?.response?.status;
            if (status === 404) {
                setError(null);
                await fetchTableCart(activeTableId, true);
                return;
            }
            const msg = err?.response?.data?.message || err?.message || 'Failed to remove item';
            setError(msg);
            setCartsByTable(prev => ({ ...prev, [activeTableId]: currentCart }));
            throw new Error(msg);
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
        const lineKey = (i: CartItem) => i.itemId ?? i.clientId ?? `${i.productId}-${getModifierKey(i.modifiers)}`;
        const item = currentCart.items.find(i => lineKey(i) === itemId);
        if (!item) {
            setError('Item not found');
            return;
        }
        const basePrice = item.unitPrice ?? item.price ?? 0;
        const modTotal = (item.modifiers ?? []).reduce((s, m) => s + m.price * (m.quantity ?? 1), 0);
        const newTotal = basePrice * quantity + modTotal;
        if (!item.itemId) {
            setLoading(true);
            try {
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
            const updatedItems = currentCart.items.map(i =>
                lineKey(i) === itemId ? { ...i, qty: quantity, totalPrice: newTotal } : i
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

    const lineKey = (i: CartItem) => i.itemId ?? i.clientId;
    const recalcLineTotal = (it: CartItem) =>
        (it.unitPrice ?? it.price ?? 0) * (it.qty ?? 0) +
        (it.modifiers ?? []).reduce((s, m) => s + m.price * (m.quantity ?? 1), 0);

    const persistModifiers = useCallback(
        async (item: CartItem, nextMods: CartItemModifier[]) => {
            if (!item.itemId) return;
            try {
                // FE sends Quantity per modifier; backend may only persist id until API supports modifier quantity.
                await apiClient.put(`/Cart/items/${item.itemId}`, {
                    Quantity: item.qty ?? 0,
                    Notes: item.notes ?? '',
                    SelectedModifiers: nextMods.map((m) => ({ Id: m.id, Quantity: m.quantity ?? 1 }))
                });
            } catch (e: any) {
                setError(e?.message || 'Failed to update modifiers');
                throw e;
            }
        },
        [setError]
    );

    const addModifier = useCallback(
        async (cartItemId: string, modifier: Omit<CartItemModifier, 'quantity'> & { quantity?: number }) => {
            const currentCart = cartsByTable[activeTableId];
            if (!currentCart?.items?.length) return;
            const index = currentCart.items.findIndex((i) => lineKey(i) === cartItemId);
            if (index === -1) return;
            const item = currentCart.items[index];
            const mods = item.modifiers ?? [];
            const qty = modifier.quantity ?? 1;
            const existing = mods.find((m) => m.id === modifier.id);
            const nextMods: CartItemModifier[] = existing
                ? mods.map((m) => (m.id === modifier.id ? { ...m, quantity: m.quantity + qty } : m))
                : [...mods, { id: modifier.id, name: modifier.name, price: modifier.price, quantity: qty, groupId: modifier.groupId }];
            const totalPrice = recalcLineTotal({ ...item, modifiers: nextMods });
            const updatedItems = currentCart.items.map((it, i) =>
                i === index ? { ...it, modifiers: nextMods, totalPrice } : it
            );
            const grandTotalGross = updatedItems.reduce((s, i) => s + (i.totalPrice ?? 0), 0);
            setCartsByTable((prev) => ({
                ...prev,
                [activeTableId]: { ...currentCart, items: updatedItems, subtotalGross: grandTotalGross, grandTotalGross, updatedAt: Date.now() }
            }));
            try {
                await persistModifiers(item, nextMods);
            } catch {
                setCartsByTable((prev) => ({ ...prev, [activeTableId]: currentCart }));
            }
        },
        [activeTableId, cartsByTable, persistModifiers]
    );

    const incrementModifier = useCallback(
        async (cartItemId: string, modifierId: string) => {
            const currentCart = cartsByTable[activeTableId];
            if (!currentCart?.items?.length) return;
            const index = currentCart.items.findIndex((i) => lineKey(i) === cartItemId);
            if (index === -1) return;
            const item = currentCart.items[index];
            const mods = item.modifiers ?? [];
            const nextMods = mods.map((m) => (m.id === modifierId ? { ...m, quantity: (m.quantity ?? 1) + 1 } : m));
            const totalPrice = recalcLineTotal({ ...item, modifiers: nextMods });
            const updatedItems = currentCart.items.map((it, i) =>
                i === index ? { ...it, modifiers: nextMods, totalPrice } : it
            );
            const grandTotalGross = updatedItems.reduce((s, i) => s + (i.totalPrice ?? 0), 0);
            setCartsByTable((prev) => ({
                ...prev,
                [activeTableId]: { ...currentCart, items: updatedItems, subtotalGross: grandTotalGross, grandTotalGross, updatedAt: Date.now() }
            }));
            try {
                await persistModifiers(item, nextMods);
            } catch {
                setCartsByTable((prev) => ({ ...prev, [activeTableId]: currentCart }));
            }
        },
        [activeTableId, cartsByTable, persistModifiers]
    );

    const decrementModifier = useCallback(
        async (cartItemId: string, modifierId: string) => {
            const currentCart = cartsByTable[activeTableId];
            if (!currentCart?.items?.length) return;
            const index = currentCart.items.findIndex((i) => lineKey(i) === cartItemId);
            if (index === -1) return;
            const item = currentCart.items[index];
            const mods = item.modifiers ?? [];
            const existing = mods.find((m) => m.id === modifierId);
            const nextQty = (existing?.quantity ?? 1) - 1;
            const nextMods =
                nextQty <= 0 ? mods.filter((m) => m.id !== modifierId) : mods.map((m) => (m.id === modifierId ? { ...m, quantity: nextQty } : m));
            const totalPrice = recalcLineTotal({ ...item, modifiers: nextMods });
            const updatedItems = currentCart.items.map((it, i) =>
                i === index ? { ...it, modifiers: nextMods.length ? nextMods : undefined, totalPrice } : it
            );
            const grandTotalGross = updatedItems.reduce((s, i) => s + (i.totalPrice ?? 0), 0);
            setCartsByTable((prev) => ({
                ...prev,
                [activeTableId]: { ...currentCart, items: updatedItems, subtotalGross: grandTotalGross, grandTotalGross, updatedAt: Date.now() }
            }));
            try {
                await persistModifiers(item, nextMods);
            } catch {
                setCartsByTable((prev) => ({ ...prev, [activeTableId]: currentCart }));
            }
        },
        [activeTableId, cartsByTable, persistModifiers]
    );

    const removeModifier = useCallback(
        async (cartItemId: string, modifierId: string) => {
            const currentCart = cartsByTable[activeTableId];
            if (!currentCart?.items?.length) return;
            const index = currentCart.items.findIndex((i) => lineKey(i) === cartItemId);
            if (index === -1) return;
            const item = currentCart.items[index];
            const nextMods = (item.modifiers ?? []).filter((m) => m.id !== modifierId);
            const totalPrice = recalcLineTotal({ ...item, modifiers: nextMods });
            const updatedItems = currentCart.items.map((it, i) =>
                i === index ? { ...it, modifiers: nextMods.length ? nextMods : undefined, totalPrice } : it
            );
            const grandTotalGross = updatedItems.reduce((s, i) => s + (i.totalPrice ?? 0), 0);
            setCartsByTable((prev) => ({
                ...prev,
                [activeTableId]: { ...currentCart, items: updatedItems, subtotalGross: grandTotalGross, grandTotalGross, updatedAt: Date.now() }
            }));
            try {
                await persistModifiers(item, nextMods);
            } catch {
                setCartsByTable((prev) => ({ ...prev, [activeTableId]: currentCart }));
            }
        },
        [activeTableId, cartsByTable, persistModifiers]
    );

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
            // Optimistic update – only product qty changes; modifier quantities unchanged. totalPrice = base * qty + sum(mod.price * mod.quantity)
            const updatedItems = currentCart.items.map(i => {
                if (i.productId === productId) {
                    const basePrice = i.unitPrice ?? i.price ?? 0;
                    const modTotal = (i.modifiers ?? []).reduce((s, m) => s + m.price * (m.quantity ?? 1), 0);
                    return {
                        ...i,
                        qty: newQuantity,
                        totalPrice: basePrice * newQuantity + modTotal,
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
            // Optimistic update – only product qty; modifier quantities unchanged
            const updatedItems = currentCart.items.map(i => {
                if (i.productId === productId) {
                    const basePrice = i.unitPrice ?? i.price ?? 0;
                    const modTotal = (i.modifiers ?? []).reduce((s, m) => s + m.price * (m.quantity ?? 1), 0);
                    return {
                        ...i,
                        qty: newQuantity,
                        totalPrice: basePrice * newQuantity + modTotal,
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
            currentCart,
            cartsByTable,
            loading,
            error,
            isPaymentModalVisible,
            setActiveTable,
            setIsPaymentModalVisible,
            switchTable, // ✅ Exposed
            addItem,
            addItemWithAddOns,
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
            addModifier,
            incrementModifier,
            decrementModifier,
            removeModifier,
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
