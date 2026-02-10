import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { apiClient } from '../services/api/config';

// ============================================
// TYPE DEFINITIONS
// ============================================

export interface CartItem {
    productId: string;
    productName: string; // ✅ Renamed from name, made required
    price?: number;
    qty: number;
    unitPrice?: number;
    totalPrice?: number;
    notes?: string;
    itemId?: string; // Backend ID
}

export interface Cart {
    items: CartItem[];
    updatedAt?: number;
    cartId?: string;
}

// ============================================
// HELPER: Calculate Cart Totals from Items
// ============================================
export const calculateCartTotals = (items: CartItem[]) => {
    const subtotal = items.reduce((sum, item) => {
        const unitPrice = item.unitPrice || item.price || 0;
        const itemTotal = unitPrice * item.qty;
        return sum + itemTotal;
    }, 0);

    const tax = subtotal * 0.20; // 20% tax rate
    const grandTotal = subtotal + tax;

    return {
        subtotal,
        tax,
        grandTotal,
        itemCount: items.reduce((sum, item) => sum + item.qty, 0),
    };
};

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
    addItem: (productId: string, quantity?: number) => Promise<void>;
    increment: (productId: string) => Promise<void>;
    decrement: (productId: string) => Promise<void>;
    remove: (productId: string) => Promise<void>;
    clearCart: (tableNumber?: number) => Promise<void>;
    checkout: (tableNumber?: number) => Promise<void>;
    getCartForTable: (tableNumber: number) => Cart;
    updateItemQuantity: (productId: string, quantity: number) => Promise<void>; // ✅ Added for CashRegister
    fetchTableCart: (tableNumber: number) => Promise<void>; // ✅ Added

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
    const fetchTableCart = useCallback(async (tableNumber: number) => {
        // Prevent duplicate fetch for same table (Brute-force Guard)
        // NOTE: We allow re-fetching if explicitly requested via switchTable (resetting ref might be needed if we want forced refresh)
        if (lastFetchedTableIdRef.current === tableNumber) {
            console.log(`🛡️ [CartContext] Skipping fetch for Table ${tableNumber} (Already fetched/In-progress)`);
            return;
        }

        lastFetchedTableIdRef.current = tableNumber;
        setLoading(true);
        console.log(`🔄 [CartContext] Fetching fresh data for Table ${tableNumber}...`);
        try {
            // GET /cart/current?tableNumber=X (equivalent to GET /Table/{id})
            const response = await apiClient.get<AddItemResponse['cart']>(`/cart/current?tableNumber=${tableNumber}`);

            if (response && (response.items || response.Items)) {
                const backendItems = response.items || response.Items || [];
                const localItems: CartItem[] = backendItems.map((item: any) => {
                    // Check logic for ProductName
                    const pName = item.ProductName || item.productName;
                    if (!pName) {
                        console.error("[Cart] ❌ Missing ProductName for item:", item);
                    }

                    return {
                        productId: item.ProductId || item.productId,
                        productName: pName || 'Unknown Product', // ✅ Fallback
                        price: item.UnitPrice || item.unitPrice || 0,
                        qty: item.Quantity || item.quantity || 0,
                        unitPrice: item.UnitPrice || item.unitPrice || 0,
                        totalPrice: item.TotalPrice || item.totalPrice || 0,
                        notes: item.Notes || item.notes,
                        itemId: item.Id || item.id
                    };
                });

                setCartsByTable(prev => ({
                    ...prev,
                    [tableNumber]: {
                        items: localItems,
                        updatedAt: Date.now(),
                        cartId: response.CartId || response.cartId
                    }
                }));
                console.log(`✅ [CartContext] Table ${tableNumber} updated with ${localItems.length} items`);
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

    // Actions
    const addItem = useCallback(async (productId: string, quantity: number = 1) => {
        const currentCart = cartsByTable[activeTableId] || { items: [] };

        console.log(`➕ [CartContext] Adding item to table ${activeTableId}:`, { productId, quantity });

        // PHASE 1: Optimistic Update
        const existingItemIndex = currentCart.items.findIndex(item => item.productId === productId);
        let optimisticCart: Cart;

        if (existingItemIndex !== -1) {
            optimisticCart = {
                ...currentCart,
                items: currentCart.items.map((item, index) =>
                    index === existingItemIndex
                        ? { ...item, qty: item.qty + quantity }
                        : item
                ),
                updatedAt: Date.now()
            };
        } else {
            optimisticCart = {
                ...currentCart,
                items: [
                    ...currentCart.items,
                    {
                        productId,
                        productName: 'Loading...',
                        price: 0,
                        qty: quantity,
                        unitPrice: 0,
                        totalPrice: 0
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

        // PHASE 2: Backend Call
        try {
            const response = await apiClient.post<AddItemResponse>('/cart/add-item', {
                productId,
                quantity,
                tableNumber: activeTableId
            });

            if (response.cart) {
                const backendCart = response.cart;
                const backendItems = backendCart.Items || backendCart.items || [];

                const localItems: CartItem[] = backendItems.map((item: any) => ({
                    productId: item.ProductId || item.productId,
                    productName: item.ProductName || item.productName || 'Unknown Product',
                    price: item.UnitPrice || item.unitPrice || 0,
                    qty: item.Quantity || item.quantity || 0,
                    unitPrice: item.UnitPrice || item.unitPrice || 0,
                    totalPrice: item.TotalPrice || item.totalPrice || 0,
                    notes: item.Notes || item.notes,
                    itemId: item.Id || item.id
                }));

                setCartsByTable(prev => ({
                    ...prev,
                    [activeTableId]: {
                        items: localItems,
                        updatedAt: Date.now(),
                        cartId: backendCart.CartId || backendCart.cartId
                    }
                }));
            }
        } catch (err: any) {
            const msg = err?.data?.message || err?.message || 'Failed to add item';
            console.error('❌ [CartContext] Failed:', msg);
            setError(msg);

            // Rollback
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: currentCart
            }));
            throw new Error(msg);
        }
    }, [activeTableId, cartsByTable]);

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
    }, [activeTableId, cartsByTable, remove, setLoading, setError]);

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
                    const unitPrice = i.unitPrice || i.price || 0;
                    return {
                        ...i,
                        qty: newQuantity,
                        totalPrice: unitPrice * newQuantity, // ✅ Recalculate for UI
                    };
                }
                return i;
            });
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: {
                    ...currentCart,
                    items: updatedItems,
                    updatedAt: Date.now() // ✅ Force re-render
                }
            }));

            // PUT /cart/items/{itemId} with {quantity}
            await apiClient.put(`/Cart/items/${item.itemId}`, {
                Quantity: newQuantity,
                Notes: item.notes || ""
            });
            console.log('✅ [increment] Quantity updated successfully');
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
    }, [activeTableId, cartsByTable, setLoading, setError]);

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
                    const unitPrice = i.unitPrice || i.price || 0;
                    return {
                        ...i,
                        qty: newQuantity,
                        totalPrice: unitPrice * newQuantity, // ✅ Recalculate for UI
                    };
                }
                return i;
            });
            setCartsByTable(prev => ({
                ...prev,
                [activeTableId]: {
                    ...currentCart,
                    items: updatedItems,
                    updatedAt: Date.now() // ✅ Force re-render
                }
            }));

            // PUT /cart/items/{itemId} with {quantity}
            await apiClient.put(`/Cart/items/${item.itemId}`, {
                Quantity: newQuantity,
                Notes: item.notes || ""
            });
            console.log('✅ [decrement] Quantity updated successfully');
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
    }, [activeTableId, cartsByTable, remove, setLoading, setError]);

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
            clearCart,
            checkout,
            getCartForTable,
            updateItemQuantity: updateItemQuantity,
            fetchTableCart, // Kept exposed but switchTable is preferred
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
