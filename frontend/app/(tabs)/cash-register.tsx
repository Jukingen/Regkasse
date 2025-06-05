import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, Alert } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';
import { Ionicons } from '@expo/vector-icons';

interface CartItem {
    id: string;
    name: string;
    price: number;
    quantity: number;
    taxType: 'standard' | 'reduced' | 'special';
}

export default function CashRegisterScreen() {
    const { t } = useTranslation();
    const { user } = useAuth();
    const [cart, setCart] = useState<CartItem[]>([]);
    const [total, setTotal] = useState(0);

    // Sepet toplamını hesapla
    useEffect(() => {
        const newTotal = cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
        setTotal(newTotal);
    }, [cart]);

    const handleAddItem = () => {
        // TODO: Ürün seçme modalını aç
        Alert.alert('Info', 'Ürün seçme özelliği yakında eklenecek');
    };

    const handlePayment = async () => {
        if (cart.length === 0) {
            Alert.alert(
                t('cash_register.error.title'),
                t('cash_register.error.empty_cart')
            );
            return;
        }

        try {
            // TODO: TSE imzası al ve fiş kes
            Alert.alert('Info', 'Ödeme işlemi yakında eklenecek');
        } catch (error) {
            Alert.alert(
                t('cash_register.error.title'),
                t('cash_register.error.payment_failed')
            );
        }
    };

    return (
        <View style={styles.container}>
            <View style={styles.header}>
                <Text style={styles.headerText}>
                    {t('cash_register.welcome', { name: user?.username })}
                </Text>
            </View>

            <ScrollView style={styles.cartContainer}>
                {cart.map((item) => (
                    <View key={item.id} style={styles.cartItem}>
                        <Text style={styles.itemName}>{item.name}</Text>
                        <Text style={styles.itemPrice}>
                            {item.quantity}x {item.price.toFixed(2)}€
                        </Text>
                    </View>
                ))}
                {cart.length === 0 && (
                    <Text style={styles.emptyCart}>
                        {t('cash_register.empty_cart')}
                    </Text>
                )}
            </ScrollView>

            <View style={styles.footer}>
                <View style={styles.totalContainer}>
                    <Text style={styles.totalLabel}>{t('cash_register.total')}</Text>
                    <Text style={styles.totalAmount}>{total.toFixed(2)}€</Text>
                </View>

                <View style={styles.buttonContainer}>
                    <TouchableOpacity
                        style={[styles.button, styles.addButton]}
                        onPress={handleAddItem}
                    >
                        <Ionicons name="add-circle-outline" size={24} color="white" />
                        <Text style={styles.buttonText}>{t('cash_register.add_item')}</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                        style={[styles.button, styles.payButton]}
                        onPress={handlePayment}
                    >
                        <Ionicons name="cash-outline" size={24} color="white" />
                        <Text style={styles.buttonText}>{t('cash_register.pay')}</Text>
                    </TouchableOpacity>
                </View>
            </View>
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
    itemName: {
        fontSize: 16,
    },
    itemPrice: {
        fontSize: 16,
        fontWeight: 'bold',
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
    totalContainer: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        marginBottom: 20,
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
    buttonText: {
        color: 'white',
        fontSize: 16,
        fontWeight: 'bold',
        marginLeft: 8,
    },
}); 