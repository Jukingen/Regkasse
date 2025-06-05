import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, Alert } from 'react-native';
import { useTranslation } from 'react-i18next';
import { Ionicons } from '@expo/vector-icons';

interface Product {
    id: string;
    name: string;
    price: number;
    taxType: 'standard' | 'reduced' | 'special';
    stock: number;
    barcode?: string;
}

export default function ProductsScreen() {
    const { t } = useTranslation();
    const [products, setProducts] = useState<Product[]>([]);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        loadProducts();
    }, []);

    const loadProducts = async () => {
        try {
            // TODO: API'den ürünleri yükle
            const mockProducts: Product[] = [
                {
                    id: '1',
                    name: 'Test Ürün 1',
                    price: 9.99,
                    taxType: 'standard',
                    stock: 100,
                    barcode: '123456789'
                },
                {
                    id: '2',
                    name: 'Test Ürün 2',
                    price: 19.99,
                    taxType: 'reduced',
                    stock: 50,
                    barcode: '987654321'
                }
            ];
            setProducts(mockProducts);
        } catch (error) {
            Alert.alert(
                t('products.error.title'),
                t('products.error.load_failed')
            );
        } finally {
            setIsLoading(false);
        }
    };

    const handleAddProduct = () => {
        // TODO: Ürün ekleme modalını aç
        Alert.alert('Info', 'Ürün ekleme özelliği yakında eklenecek');
    };

    const handleEditProduct = (product: Product) => {
        // TODO: Ürün düzenleme modalını aç
        Alert.alert('Info', `${product.name} düzenleme özelliği yakında eklenecek`);
    };

    const renderProduct = ({ item }: { item: Product }) => (
        <TouchableOpacity
            style={styles.productItem}
            onPress={() => handleEditProduct(item)}
        >
            <View style={styles.productInfo}>
                <Text style={styles.productName}>{item.name}</Text>
                <Text style={styles.productDetails}>
                    {item.price.toFixed(2)}€ • {t(`tax.${item.taxType}`)} • {item.stock} {t('products.stock')}
                </Text>
                {item.barcode && (
                    <Text style={styles.barcode}>{item.barcode}</Text>
                )}
            </View>
            <Ionicons name="chevron-forward" size={24} color="#666" />
        </TouchableOpacity>
    );

    return (
        <View style={styles.container}>
            <View style={styles.header}>
                <Text style={styles.headerText}>{t('products.title')}</Text>
                <TouchableOpacity
                    style={styles.addButton}
                    onPress={handleAddProduct}
                >
                    <Ionicons name="add" size={24} color="white" />
                </TouchableOpacity>
            </View>

            <FlatList
                data={products}
                renderItem={renderProduct}
                keyExtractor={(item) => item.id}
                contentContainerStyle={styles.list}
                ListEmptyComponent={
                    <Text style={styles.emptyText}>
                        {isLoading ? t('products.loading') : t('products.empty')}
                    </Text>
                }
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
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: 20,
        backgroundColor: '#007AFF',
    },
    headerText: {
        fontSize: 20,
        fontWeight: 'bold',
        color: 'white',
    },
    addButton: {
        width: 40,
        height: 40,
        borderRadius: 20,
        backgroundColor: '#34C759',
        justifyContent: 'center',
        alignItems: 'center',
    },
    list: {
        padding: 20,
    },
    productItem: {
        flexDirection: 'row',
        alignItems: 'center',
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
    productInfo: {
        flex: 1,
    },
    productName: {
        fontSize: 16,
        fontWeight: 'bold',
        marginBottom: 4,
    },
    productDetails: {
        fontSize: 14,
        color: '#666',
        marginBottom: 2,
    },
    barcode: {
        fontSize: 12,
        color: '#999',
    },
    emptyText: {
        textAlign: 'center',
        fontSize: 16,
        color: '#666',
        marginTop: 20,
    },
}); 