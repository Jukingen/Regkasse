import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, Alert, Modal } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';
import { useSystem } from '../../contexts/SystemContext';
import { Ionicons } from '@expo/vector-icons';
import ProductSelectionModal from '../../components/ProductSelectionModal';
import { Product } from '../../services/api/productService';
import { paymentService, PaymentRequest } from '../../services/api/paymentService';
import { receiptService } from '../../services/api/receiptService';
import { tseService } from '../../services/api/tseService';
import { offlineManager } from '../../services/offline/OfflineManager';

interface CartItem {
    id: string;
    name: string;
    price: number;
    quantity: number;
    taxType: 'standard' | 'reduced' | 'special';
    productId: string;
}

export default function CashRegisterScreen() {
    const { t } = useTranslation();
    const { user } = useAuth();
    const { config, isOnline, isOfflineMode, isHybridMode, canWorkOffline } = useSystem();
    const [cart, setCart] = useState<CartItem[]>([]);
    const [total, setTotal] = useState(0);
    const [showProductModal, setShowProductModal] = useState(false);
    const [isProcessing, setIsProcessing] = useState(false);
    const [tseStatus, setTseStatus] = useState<any>(null);
    const [offlineProducts, setOfflineProducts] = useState<Product[]>([]);

    // TSE durumunu kontrol et
    useEffect(() => {
        if (config?.tseSettings.required) {
            checkTseStatus();
            const interval = setInterval(checkTseStatus, 30000);
            return () => clearInterval(interval);
        }
    }, [config]);

    // Çevrimdışı ürünleri yükle
    useEffect(() => {
        if (canWorkOffline) {
            loadOfflineProducts();
        }
    }, [canWorkOffline]);

    // Sepet toplamını hesapla
    useEffect(() => {
        const newTotal = cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
        setTotal(newTotal);
    }, [cart]);

    const checkTseStatus = async () => {
        try {
            const status = await tseService.getStatus();
            setTseStatus(status);
        } catch (error) {
            console.error('TSE status check failed:', error);
            // Çevrimdışı modda TSE hatası tolere edilebilir
            if (!canWorkOffline) {
                setTseStatus({ isConnected: false });
            }
        }
    };

    const loadOfflineProducts = async () => {
        try {
            const products = await offlineManager.getOfflineProducts();
            setOfflineProducts(products);
        } catch (error) {
            console.error('Offline products load failed:', error);
        }
    };

    const handleProductSelect = (product: Product, quantity: number) => {
        const existingItem = cart.find(item => item.productId === product.id);
        
        if (existingItem) {
            setCart(cart.map(item => 
                item.productId === product.id 
                    ? { ...item, quantity: item.quantity + quantity }
                    : item
            ));
        } else {
            const newItem: CartItem = {
                id: `${product.id}-${Date.now()}`,
                name: product.name,
                price: product.price,
                quantity,
                taxType: product.taxType,
                productId: product.id
            };
            setCart([...cart, newItem]);
        }
    };

    const handleRemoveItem = (itemId: string) => {
        setCart(cart.filter(item => item.id !== itemId));
    };

    const handleUpdateQuantity = (itemId: string, newQuantity: number) => {
        if (newQuantity <= 0) {
            handleRemoveItem(itemId);
            return;
        }
        
        setCart(cart.map(item => 
            item.id === itemId 
                ? { ...item, quantity: newQuantity }
                : item
        ));
    };

    const handlePayment = async () => {
        if (cart.length === 0) {
            Alert.alert(
                t('cash_register.error.title'),
                t('cash_register.error.empty_cart')
            );
            return;
        }

        // TSE kontrolü
        if (config?.tseSettings.required && !tseStatus?.isConnected) {
            if (!canWorkOffline || !config.tseSettings.offlineAllowed) {
                Alert.alert(
                    t('cash_register.error.title'),
                    t('cash_register.error.tse_not_connected')
                );
                return;
            }
        }

        setIsProcessing(true);

        try {
            // Ödeme isteği oluştur
            const paymentRequest: PaymentRequest = {
                amount: total,
                method: 'cash',
                items: cart.map(item => ({
                    productId: item.productId,
                    quantity: item.quantity,
                    price: item.price,
                    taxType: item.taxType
                })),
                tseRequired: config?.tseSettings.required || false
            };

            let payment;
            let receipt;

            if (isOnline || isHybridMode) {
                // Online ödeme
                payment = await paymentService.processPayment(paymentRequest);
                receipt = await receiptService.createReceipt(payment.id);
                
                // Fişi yazdır
                const printSuccess = await receiptService.printReceipt(receipt);
                
                if (!printSuccess && config?.printerSettings.required) {
                    throw new Error('Print failed');
                }
            } else {
                // Çevrimdışı ödeme
                const offlinePaymentId = await offlineManager.saveOfflinePayment(paymentRequest);
                
                // Çevrimdışı fiş oluştur
                const offlineReceipt = {
                    id: `offline_${Date.now()}`,
                    receiptNumber: `OFF-${Date.now()}`,
                    items: cart.map(item => ({
                        productName: item.name,
                        quantity: item.quantity,
                        price: item.price,
                        taxType: item.taxType,
                        totalPrice: item.price * item.quantity
                    })),
                    subtotal: total - totalTax,
                    taxStandard: cart.filter(item => item.taxType === 'standard').reduce((sum, item) => sum + calculateTaxAmount(item), 0),
                    taxReduced: cart.filter(item => item.taxType === 'reduced').reduce((sum, item) => sum + calculateTaxAmount(item), 0),
                    taxSpecial: cart.filter(item => item.taxType === 'special').reduce((sum, item) => sum + calculateTaxAmount(item), 0),
                    total: total,
                    paymentMethod: 'cash',
                    timestamp: new Date().toISOString(),
                    cashierId: user?.id || 'unknown'
                };

                await offlineManager.saveOfflineReceipt(offlineReceipt);
                
                // Çevrimdışı yazdırma kuyruğuna ekle
                if (config?.printerSettings.offlineQueue) {
                    await receiptService.queueOfflinePrint(offlineReceipt);
                }
            }

            Alert.alert(
                t('cash_register.success.title'),
                isOnline ? t('cash_register.success.payment_completed') : t('cash_register.success.offline_payment_saved'),
                [
                    {
                        text: 'Tamam',
                        onPress: () => {
                            setCart([]); // Sepeti temizle
                        }
                    }
                ]
            );
        } catch (error) {
            console.error('Payment error:', error);
            Alert.alert(
                t('cash_register.error.title'),
                t('cash_register.error.payment_failed')
            );
        } finally {
            setIsProcessing(false);
        }
    };

    const calculateTaxAmount = (item: CartItem) => {
        const taxRates = {
            standard: 0.20,
            reduced: 0.10,
            special: 0.13
        };
        return (item.price * item.quantity * taxRates[item.taxType]);
    };

    const totalTax = cart.reduce((sum, item) => sum + calculateTaxAmount(item), 0);

    const getConnectionStatus = () => {
        if (isOfflineMode) {
            return { text: 'Çevrimdışı Mod', color: '#FF9500', icon: 'cloud-offline' };
        } else if (isHybridMode) {
            return { text: isOnline ? 'Hibrit Mod (Çevrimiçi)' : 'Hibrit Mod (Çevrimdışı)', color: isOnline ? '#34C759' : '#FF9500', icon: isOnline ? 'cloud' : 'cloud-offline' };
        } else {
            return { text: isOnline ? 'Çevrimiçi' : 'Bağlantı Yok', color: isOnline ? '#34C759' : '#FF3B30', icon: isOnline ? 'cloud' : 'cloud-offline' };
        }
    };

    const connectionStatus = getConnectionStatus();

    return (
        <View style={styles.container}>
            <View style={styles.header}>
                <Text style={styles.headerText}>
                    {t('cash_register.welcome', { name: user?.username })}
                </Text>
                
                <View style={styles.statusContainer}>
                    <View style={styles.connectionStatus}>
                        <Ionicons 
                            name={connectionStatus.icon as any} 
                            size={16} 
                            color={connectionStatus.color} 
                        />
                        <Text style={[styles.statusText, { color: connectionStatus.color }]}>
                            {connectionStatus.text}
                        </Text>
                    </View>
                    
                    {config?.tseSettings.required && tseStatus && (
                        <View style={styles.tseStatus}>
                            <Ionicons 
                                name={tseStatus.isConnected ? "checkmark-circle" : "close-circle"} 
                                size={16} 
                                color={tseStatus.isConnected ? "#34C759" : "#FF3B30"} 
                            />
                            <Text style={styles.tseStatusText}>
                                TSE: {tseStatus.isConnected ? 'Bağlı' : 'Bağlı Değil'}
                            </Text>
                        </View>
                    )}
                </View>
            </View>

            <ScrollView style={styles.cartContainer}>
                {cart.map((item) => (
                    <View key={item.id} style={styles.cartItem}>
                        <View style={styles.itemInfo}>
                            <Text style={styles.itemName}>{item.name}</Text>
                            <Text style={styles.itemTax}>
                                {t(`tax.${item.taxType}`)} ({item.taxType === 'standard' ? '20%' : item.taxType === 'reduced' ? '10%' : '13%'})
                            </Text>
                        </View>
                        <View style={styles.itemActions}>
                            <View style={styles.quantityContainer}>
                                <TouchableOpacity 
                                    style={styles.quantityButton}
                                    onPress={() => handleUpdateQuantity(item.id, item.quantity - 1)}
                                >
                                    <Ionicons name="remove" size={16} color="#007AFF" />
                                </TouchableOpacity>
                                <Text style={styles.quantityText}>{item.quantity}</Text>
                                <TouchableOpacity 
                                    style={styles.quantityButton}
                                    onPress={() => handleUpdateQuantity(item.id, item.quantity + 1)}
                                >
                                    <Ionicons name="add" size={16} color="#007AFF" />
                                </TouchableOpacity>
                            </View>
                            <Text style={styles.itemPrice}>
                                {(item.price * item.quantity).toFixed(2)}€
                            </Text>
                            <TouchableOpacity 
                                style={styles.removeButton}
                                onPress={() => handleRemoveItem(item.id)}
                            >
                                <Ionicons name="trash-outline" size={20} color="#FF3B30" />
                            </TouchableOpacity>
                        </View>
                    </View>
                ))}
                {cart.length === 0 && (
                    <Text style={styles.emptyCart}>
                        {t('cash_register.empty_cart')}
                    </Text>
                )}
            </ScrollView>

            <View style={styles.footer}>
                <View style={styles.summaryContainer}>
                    <View style={styles.summaryRow}>
                        <Text style={styles.summaryLabel}>{t('cash_register.subtotal')}</Text>
                        <Text style={styles.summaryValue}>{(total - totalTax).toFixed(2)}€</Text>
                    </View>
                    <View style={styles.summaryRow}>
                        <Text style={styles.summaryLabel}>{t('cash_register.tax')}</Text>
                        <Text style={styles.summaryValue}>{totalTax.toFixed(2)}€</Text>
                    </View>
                    <View style={[styles.summaryRow, styles.totalRow]}>
                        <Text style={styles.totalLabel}>{t('cash_register.total')}</Text>
                        <Text style={styles.totalAmount}>{total.toFixed(2)}€</Text>
                    </View>
                </View>

                <View style={styles.buttonContainer}>
                    <TouchableOpacity
                        style={[styles.button, styles.addButton]}
                        onPress={() => setShowProductModal(true)}
                    >
                        <Ionicons name="add-circle-outline" size={24} color="white" />
                        <Text style={styles.buttonText}>{t('cash_register.add_item')}</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                        style={[styles.button, styles.payButton, isProcessing && styles.disabledButton]}
                        onPress={handlePayment}
                        disabled={isProcessing || cart.length === 0}
                    >
                        <Ionicons name="cash-outline" size={24} color="white" />
                        <Text style={styles.buttonText}>
                            {isProcessing ? t('cash_register.processing') : t('cash_register.pay')}
                        </Text>
                    </TouchableOpacity>
                </View>
            </View>

            <ProductSelectionModal
                visible={showProductModal}
                onClose={() => setShowProductModal(false)}
                onProductSelect={handleProductSelect}
            />
        </View>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: '#f5f5f5',
    },
    header: {
        padding: 20,
        backgroundColor: '#007AFF',
    },
    headerText: {
        fontSize: 20,
        fontWeight: 'bold',
        color: 'white',
        marginBottom: 10,
    },
    statusContainer: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    connectionStatus: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    statusText: {
        color: 'white',
        fontSize: 14,
        marginLeft: 5,
        fontWeight: 'bold',
    },
    tseStatus: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    tseStatusText: {
        color: 'white',
        fontSize: 14,
        marginLeft: 5,
    },
    cartContainer: {
        flex: 1,
        padding: 20,
    },
    cartItem: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        padding: 15,
        backgroundColor: 'white',
        borderRadius: 10,
        marginBottom: 10,
        shadowColor: '#000',
        shadowOffset: {
            width: 0,
            height: 1,
        },
        shadowOpacity: 0.2,
        shadowRadius: 1.41,
        elevation: 2,
    },
    itemInfo: {
        flex: 1,
    },
    itemName: {
        fontSize: 16,
        fontWeight: 'bold',
        marginBottom: 5,
    },
    itemTax: {
        fontSize: 12,
        color: '#666',
    },
    itemActions: {
        alignItems: 'flex-end',
    },
    quantityContainer: {
        flexDirection: 'row',
        alignItems: 'center',
        marginBottom: 5,
    },
    quantityButton: {
        width: 30,
        height: 30,
        borderRadius: 15,
        backgroundColor: '#f0f0f0',
        justifyContent: 'center',
        alignItems: 'center',
    },
    quantityText: {
        marginHorizontal: 10,
        fontSize: 16,
        fontWeight: 'bold',
    },
    itemPrice: {
        fontSize: 16,
        fontWeight: 'bold',
        color: '#007AFF',
        marginBottom: 5,
    },
    removeButton: {
        padding: 5,
    },
    emptyCart: {
        textAlign: 'center',
        fontSize: 16,
        color: '#666',
        marginTop: 20,
    },
    footer: {
        padding: 20,
        backgroundColor: 'white',
        borderTopWidth: 1,
        borderTopColor: '#ddd',
    },
    summaryContainer: {
        marginBottom: 20,
    },
    summaryRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        marginBottom: 5,
    },
    summaryLabel: {
        fontSize: 16,
        color: '#666',
    },
    summaryValue: {
        fontSize: 16,
        fontWeight: 'bold',
    },
    totalRow: {
        borderTopWidth: 1,
        borderTopColor: '#ddd',
        paddingTop: 10,
        marginTop: 10,
    },
    totalLabel: {
        fontSize: 18,
        fontWeight: 'bold',
    },
    totalAmount: {
        fontSize: 18,
        fontWeight: 'bold',
        color: '#007AFF',
    },
    buttonContainer: {
        flexDirection: 'row',
        justifyContent: 'space-between',
    },
    button: {
        flex: 1,
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 15,
        borderRadius: 10,
        marginHorizontal: 5,
    },
    addButton: {
        backgroundColor: '#34C759',
    },
    payButton: {
        backgroundColor: '#007AFF',
    },
    disabledButton: {
        backgroundColor: '#ccc',
    },
    buttonText: {
        color: 'white',
        fontSize: 16,
        fontWeight: 'bold',
        marginLeft: 8,
    },
}); 