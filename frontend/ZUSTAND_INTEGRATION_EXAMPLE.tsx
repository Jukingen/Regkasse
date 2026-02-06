// ============================================
// ZUSTAND CART STORE - CASH REGISTER ENTEGRASYONU
// ============================================

// Kasse ekranında Zustand cart store kullanım örneği
// Mevcut cash-register.tsx dosyanıza entegre edebilirsiniz

import React, { useEffect } from 'react';
import { SafeAreaView, ScrollView, StyleSheet, View, Text } from 'react-native';

// Modüler component'ları import et
import { CashRegisterHeader } from '../../components/CashRegisterHeader';
import { TableSelector } from '../../components/TableSelector';
import { ProductList } from '../../components/ProductList';
import { CartDisplay } from '../../components/CartDisplay';
import { CartSummary } from '../../components/CartSummary';
import CategoryFilter from '../../components/CategoryFilter';
import PaymentModal from '../../components/PaymentModal';
import { ToastContainer } from '../../components/ToastNotification';

// Hook'ları import et
import { useCashRegister } from '../../hooks/useCashRegister';
import { useProductsUnified } from '../../hooks/useProductsUnified';

// ✨ ZUSTAND STORE IMPORT
import { useCartStore } from '../../stores/useCartStore';

// Yeni ürün API servislerini import et
import { Product } from '../../services/api/productService';

export default function CashRegisterScreen() {
    // ============================================
    // ZUSTAND CART STORE - State ve Actions
    // ============================================
    const {
        activeTableId,        // Seçili masa
        cartsByTable,         // Tüm masaların sepetleri
        loading: cartLoading, // Sepet yükleniyor mu?
        error: cartError,     // Sepet hatası
        setActiveTable,       // Masa değiştir
        addItem,              // Ürün ekle
        increment,            // Ürün miktarını artır
        decrement,            // Ürün miktarını azalt
        remove,               // Ürünü kaldır
        clearCart,            // Sepeti temizle
        checkout              // Ödeme yap
    } = useCartStore();

    // Aktif masanın sepetini al
    const currentCart = cartsByTable[activeTableId];

    // ============================================
    // DIGER HOOKS (Mevcut)
    // ============================================

    // Unified product hook - tüm ürün işlemlerini tek noktada yönet
    const {
        products,
        categories,
        loading: productsLoading,
        error: productsError,
        refreshData,
        getProductsByCategory,
    } = useProductsUnified();

    // Cash register hook'u (toast notifications için)
    const {
        paymentProcessing,
        preventDoubleClick,
        error,
        toasts,
        addToast,
        removeToast,
    } = useCashRegister();

    // Local state'ler (UI kontrolü için)
    const [selectedCategory, setSelectedCategory] = React.useState<string>('all');
    const [paymentModalVisible, setPaymentModalVisible] = React.useState(false);
    const [customerId, setCustomerId] = React.useState<string>('00000000-0000-0000-0000-000000000000');

    // ============================================
    // MASA SEÇİMİ HANDLER
    // ============================================
    const handleTableSelect = (tableNumber: number) => {
        if (tableNumber < 1 || tableNumber > 10) {
            addToast('error', 'Invalid table number', 3000);
            return;
        }

        if (activeTableId === tableNumber) {
            return; // Zaten seçili
        }

        // Zustand store'da masa değiştir
        setActiveTable(tableNumber);
        addToast('info', `Switched to table ${tableNumber}`, 2000);
    };

    // ============================================
    // ÜRÜN EKLEME HANDLER (Backend ile)
    // ============================================
    const handleProductSelect = async (product: Product) => {
        try {
            if (!activeTableId) {
                addToast('error', 'Please select a table first', 3000);
                return;
            }

            // Zustand store'un addItem fonksiyonu backend çağrısı yapar
            await addItem(product.id, 1);

            addToast('success', `${product.name} added to table ${activeTableId}`, 2000);

        } catch (error: any) {
            console.error('❌ Product add error:', error);
            addToast('error', `Failed to add ${product.name}: ${error?.message || 'Unknown error'}`, 5000);
        }
    };

    // ============================================
    // MİKTAR GÜNCELLEME HANDLER
    // ============================================
    const handleQuantityUpdate = async (productId: string, action: 'increment' | 'decrement') => {
        try {
            if (action === 'increment') {
                await increment(productId);
                addToast('success', 'Quantity updated', 1500);
            } else {
                await decrement(productId);
                addToast('success', 'Quantity updated', 1500);
            }
        } catch (error: any) {
            console.error('❌ Quantity update error:', error);
            addToast('error', `Failed to update quantity: ${error?.message}`, 3000);
        }
    };

    // ============================================
    // ÜRÜN KALDIRMA HANDLER
    // ============================================
    const handleItemRemove = async (productId: string) => {
        try {
            await remove(productId);
            addToast('info', 'Item removed from cart', 2000);
        } catch (error: any) {
            console.error('❌ Item remove error:', error);
            addToast('error', `Failed to remove item: ${error?.message}`, 3000);
        }
    };

    // ============================================
    // SEPET TEMİZLEME HANDLER
    // ============================================
    const handleClearCart = async () => {
        if (!activeTableId) {
            addToast('error', 'No table selected', 3000);
            return;
        }

        if (!currentCart || currentCart.items.length === 0) {
            addToast('warning', 'Cart is already empty', 2000);
            return;
        }

        try {
            await clearCart(); // activeTableId otomatik kullanılır
            addToast('success', `Table ${activeTableId} cart cleared`, 2000);
        } catch (error: any) {
            console.error('❌ Clear cart error:', error);
            addToast('error', `Failed to clear cart: ${error?.message}`, 3000);
        }
    };

    // ============================================
    // ÖDEME HANDLER
    // ============================================
    const handlePayment = () => {
        if (!currentCart || currentCart.items.length === 0) {
            addToast('warning', 'Cart is empty. Please add items first.', 3000);
            return;
        }

        if (!activeTableId) {
            addToast('error', 'No table selected', 3000);
            return;
        }

        setPaymentModalVisible(true);
    };

    // ============================================
    // ÖDEME BAŞARILI HANDLER
    // ============================================
    const handlePaymentSuccess = async (paymentId: string) => {
        try {
            addToast('success', `Payment successful! Payment ID: ${paymentId}`, 5000);

            // Zustand store'da checkout işlemi (sepeti temizler)
            await checkout(activeTableId);

            // İlk masaya dön
            setActiveTable(1);

        } catch (error) {
            console.error('Payment success handling error:', error);
            addToast('error', 'Payment success handling failed.', 5000);
        }
    };

    // ============================================
    // KATEGORİ DEĞİŞİMİ HANDLER
    // ============================================
    const handleCategoryChange = (category: string) => {
        setSelectedCategory(category);
    };

    // ============================================
    // EFFECTS
    // ============================================

    // Hata durumlarını göster
    useEffect(() => {
        if (cartError) {
            addToast('error', cartError, 5000);
        }
    }, [cartError]);

    useEffect(() => {
        if (productsError) {
            addToast('error', productsError, 5000);
        }
    }, [productsError]);

    // ============================================
    // CART SUMMARY HESAPLAMASI
    // ============================================
    const cartSummary = React.useMemo(() => {
        if (!currentCart || currentCart.items.length === 0) {
            return {
                totalItems: 0,
                subtotal: 0,
                totalTax: 0,
                grandTotal: 0
            };
        }

        const totalItems = currentCart.items.reduce((sum, item) => sum + item.qty, 0);
        const subtotal = currentCart.items.reduce(
            (sum, item) => sum + (item.price ?? 0) * item.qty,
            0
        );
        const totalTax = subtotal * 0.2; // 20% vergi
        const grandTotal = subtotal + totalTax;

        return {
            totalItems,
            subtotal,
            totalTax,
            grandTotal
        };
    }, [currentCart]);

    // ============================================
    // RENDER
    // ============================================
    return (
        <SafeAreaView style={styles.container}>
            {/* Toast Notifications */}
            <ToastContainer toasts={toasts} onRemove={removeToast} />

            {/* Header */}
            <CashRegisterHeader
                selectedTable={activeTableId}
                recoveryLoading={false}
            />

            {/* Scrollable Content */}
            <ScrollView style={styles.scrollContainer} showsVerticalScrollIndicator={false}>

                {/* Table Selector */}
                <TableSelector
                    selectedTable={activeTableId}
                    onTableSelect={handleTableSelect}
                    tableCarts={new Map()} // İsteğe bağlı: Masa durumlarını göster
                    recoveryData={null}
                    tableSelectionLoading={null}
                    onClearAllTables={() => {
                        // Tüm masaları temizle (isteğe bağlı)
                        addToast('info', 'Clear all tables feature coming soon', 2000);
                    }}
                />

                {/* Category Filter */}
                <View style={styles.categorySection}>
                    <Text style={styles.sectionTitle}>Categories</Text>
                    <CategoryFilter
                        selectedCategory={selectedCategory}
                        onCategoryChange={handleCategoryChange}
                        categories={categories}
                    />
                </View>

                {/* Product List */}
                <ProductList
                    categoryFilter={selectedCategory === 'all' ? undefined : selectedCategory}
                    onProductSelect={handleProductSelect}
                    showStockInfo={true}
                    showTaxInfo={true}
                />

                {/* Cart Display */}
                <View style={styles.cartSection}>
                    <Text style={styles.sectionTitle}>
                        Cart - Table {activeTableId}
                    </Text>

                    {cartLoading && <Text>Loading cart...</Text>}

                    {currentCart && currentCart.items.length > 0 ? (
                        <>
                            {currentCart.items.map((item) => (
                                <View key={item.productId} style={styles.cartItem}>
                                    <View style={styles.cartItemInfo}>
                                        <Text style={styles.cartItemName}>
                                            {item.name || item.productId}
                                        </Text>
                                        <Text style={styles.cartItemPrice}>
                                            €{((item.price ?? 0) * item.qty).toFixed(2)}
                                        </Text>
                                    </View>

                                    <View style={styles.cartItemControls}>
                                        {/* Azalt */}
                                        <Text
                                            style={styles.controlButton}
                                            onPress={() => handleQuantityUpdate(item.productId, 'decrement')}
                                        >
                                            −
                                        </Text>

                                        {/* Miktar */}
                                        <Text style={styles.quantity}>{item.qty}</Text>

                                        {/* Artır */}
                                        <Text
                                            style={styles.controlButton}
                                            onPress={() => handleQuantityUpdate(item.productId, 'increment')}
                                        >
                                            +
                                        </Text>

                                        {/* Kaldır */}
                                        <Text
                                            style={styles.removeButton}
                                            onPress={() => handleItemRemove(item.productId)}
                                        >
                                            🗑️
                                        </Text>
                                    </View>
                                </View>
                            ))}

                            {/* Cart Summary */}
                            <View style={styles.cartSummarySection}>
                                <Text>Total Items: {cartSummary.totalItems}</Text>
                                <Text>Subtotal: €{cartSummary.subtotal.toFixed(2)}</Text>
                                <Text>Tax (20%): €{cartSummary.totalTax.toFixed(2)}</Text>
                                <Text style={styles.grandTotal}>
                                    Grand Total: €{cartSummary.grandTotal.toFixed(2)}
                                </Text>
                            </View>

                            {/* Action Buttons */}
                            <View style={styles.actionButtons}>
                                <Text
                                    style={styles.clearButton}
                                    onPress={handleClearCart}
                                >
                                    Clear Cart
                                </Text>

                                <Text
                                    style={styles.paymentButton}
                                    onPress={handlePayment}
                                >
                                    Proceed to Payment
                                </Text>
                            </View>
                        </>
                    ) : (
                        <Text style={styles.emptyCart}>
                            Cart is empty. Add items from the product list.
                        </Text>
                    )}
                </View>
            </ScrollView>

            {/* PaymentModal */}
            <PaymentModal
                visible={paymentModalVisible}
                onClose={() => setPaymentModalVisible(false)}
                onSuccess={handlePaymentSuccess}
                cartItems={currentCart?.items.map(item => ({
                    id: item.productId,
                    productId: item.productId,
                    productName: item.name || item.productId,
                    quantity: item.qty,
                    unitPrice: item.price ?? 0,
                    price: item.price ?? 0,
                    totalPrice: (item.price ?? 0) * item.qty,
                    notes: item.notes
                })) || []}
                customerId={customerId}
                tableNumber={activeTableId}
            />
        </SafeAreaView>
    );
}

// ============================================
// STYLES
// ============================================
const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: '#f5f5f5',
    },
    scrollContainer: {
        flex: 1,
    },
    categorySection: {
        backgroundColor: '#fff',
        padding: 20,
        marginBottom: 10,
    },
    sectionTitle: {
        fontSize: 18,
        fontWeight: 'bold',
        marginBottom: 15,
        color: '#333',
    },
    cartSection: {
        backgroundColor: '#fff',
        padding: 20,
        marginBottom: 10,
    },
    cartItem: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingVertical: 12,
        borderBottomWidth: 1,
        borderBottomColor: '#eee',
    },
    cartItemInfo: {
        flex: 1,
    },
    cartItemName: {
        fontSize: 16,
        fontWeight: '600',
        marginBottom: 4,
    },
    cartItemPrice: {
        fontSize: 14,
        color: '#666',
    },
    cartItemControls: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 12,
    },
    controlButton: {
        fontSize: 24,
        fontWeight: 'bold',
        color: '#007AFF',
        paddingHorizontal: 12,
        paddingVertical: 4,
    },
    quantity: {
        fontSize: 16,
        fontWeight: '600',
        minWidth: 30,
        textAlign: 'center',
    },
    removeButton: {
        fontSize: 20,
        marginLeft: 8,
    },
    cartSummarySection: {
        marginTop: 20,
        paddingTop: 20,
        borderTopWidth: 2,
        borderTopColor: '#ddd',
    },
    grandTotal: {
        fontSize: 18,
        fontWeight: 'bold',
        marginTop: 8,
        color: '#007AFF',
    },
    actionButtons: {
        flexDirection: 'row',
        gap: 12,
        marginTop: 20,
    },
    clearButton: {
        flex: 1,
        backgroundColor: '#FF3B30',
        color: '#fff',
        textAlign: 'center',
        paddingVertical: 15,
        borderRadius: 8,
        fontSize: 16,
        fontWeight: '600',
    },
    paymentButton: {
        flex: 2,
        backgroundColor: '#34C759',
        color: '#fff',
        textAlign: 'center',
        paddingVertical: 15,
        borderRadius: 8,
        fontSize: 16,
        fontWeight: '600',
    },
    emptyCart: {
        textAlign: 'center',
        color: '#999',
        fontSize: 16,
        paddingVertical: 40,
    },
});
