import React, { useEffect, useState } from 'react';
import { 
    View, 
    Text, 
    StyleSheet, 
    FlatList, 
    ActivityIndicator,
    RefreshControl,
    TouchableOpacity,
    ScrollView,
    TextInput
} from 'react-native';
import { Product, productService } from '../services/api/productService';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';

const ProductList: React.FC = () => {
    const { t } = useTranslation();
    const [products, setProducts] = useState<Product[]>([]);
    const [filteredProducts, setFilteredProducts] = useState<Product[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [refreshing, setRefreshing] = useState(false);
    const [selectedCategory, setSelectedCategory] = useState<string>('all');
    const [categories, setCategories] = useState<string[]>([]);
    const [searchQuery, setSearchQuery] = useState('');
    const [showSearch, setShowSearch] = useState(false);

    const fetchProducts = async () => {
        try {
            const data = await productService.getAllProducts();
            setProducts(data);
            setFilteredProducts(data);
            setError(null);
        } catch (err) {
            setError('Ürünler yüklenirken bir hata oluştu');
            console.error('API Hatası:', err);
        } finally {
            setLoading(false);
            setRefreshing(false);
        }
    };

    const fetchCategories = async () => {
        try {
            const categoryData = await productService.getCategories();
            setCategories(['all', ...categoryData]);
        } catch (err) {
            console.error('Kategoriler yüklenirken hata:', err);
        }
    };

    useEffect(() => {
        fetchProducts();
        fetchCategories();
    }, []);

    // Filtreleme işlemi
    useEffect(() => {
        let filtered = products;

        // Kategori filtresi
        if (selectedCategory !== 'all') {
            filtered = filtered.filter(product => product.category === selectedCategory);
        }

        // Arama filtresi
        if (searchQuery.trim()) {
            filtered = filtered.filter(product =>
                product.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
                product.description.toLowerCase().includes(searchQuery.toLowerCase()) ||
                product.category.toLowerCase().includes(searchQuery.toLowerCase())
            );
        }

        setFilteredProducts(filtered);
    }, [products, selectedCategory, searchQuery]);

    const onRefresh = () => {
        setRefreshing(true);
        fetchProducts();
        fetchCategories();
    };

    const formatPrice = (price: number) => {
        return new Intl.NumberFormat('de-DE', {
            style: 'currency',
            currency: 'EUR'
        }).format(price);
    };

    const getTaxTypeText = (taxType: string) => {
        switch (taxType) {
            case 'standard':
                return '20%';
            case 'reduced':
                return '10%';
            case 'special':
                return '13%';
            default:
                return taxType;
        }
    };

    const getCategoryColor = (category: string) => {
        const colors: Record<string, string> = {
            'Hauptgerichte': '#ff6b6b',
            'Vorspeisen': '#4ecdc4',
            'Suppen': '#45b7d1',
            'Salate': '#96ceb4',
            'Desserts': '#feca57',
            'Getränke': '#ff9ff3',
            'Alkoholische Getränke': '#54a0ff',
            'Kaffee & Tee': '#5f27cd',
            'Süßigkeiten': '#ff9f43',
            'Spezialitäten': '#00d2d3',
            'Snacks': '#ff6348',
            'Brot & Gebäck': '#cd6133'
        };
        return colors[category] || '#666';
    };

    const renderCategoryButton = ({ item }: { item: string }) => (
        <TouchableOpacity
            style={[
                styles.categoryButton,
                selectedCategory === item && styles.categoryButtonActive,
                { borderColor: selectedCategory === item ? getCategoryColor(item) : '#e0e0e0' }
            ]}
            onPress={() => setSelectedCategory(item)}
        >
            <Text style={[
                styles.categoryButtonText,
                selectedCategory === item && { color: getCategoryColor(item) }
            ]}>
                {item === 'all' ? t('products.allCategories') : item}
            </Text>
        </TouchableOpacity>
    );

    const renderProductItem = ({ item }: { item: Product }) => (
        <View style={styles.card}>
            <View style={styles.cardHeader}>
                <View style={styles.productInfo}>
                    <Text style={styles.productName}>{item.name}</Text>
                    <View style={[styles.categoryBadge, { backgroundColor: getCategoryColor(item.category) }]}>
                        <Text style={styles.categoryText}>{item.category}</Text>
                    </View>
                </View>
                <Text style={styles.price}>{formatPrice(item.price)}</Text>
            </View>
            
            {item.description && (
                <Text style={styles.description}>{item.description}</Text>
            )}
            
            <View style={styles.cardFooter}>
                <View style={styles.taxContainer}>
                    <Text style={styles.taxText}>KDV: {getTaxTypeText(item.taxType)}</Text>
                    <Text style={styles.stockText}>
                        {t('product.stock')}: {item.stockQuantity || 0} {item.unit || 'adet'}
                    </Text>
                </View>
                {item.barcode && (
                    <Text style={styles.barcodeText}>Barkod: {item.barcode}</Text>
                )}
            </View>
        </View>
    );

    if (loading && !refreshing) {
        return (
            <View style={styles.centered}>
                <ActivityIndicator size="large" color="#0000ff" />
                <Text style={styles.loadingText}>{t('common.loading')}</Text>
            </View>
        );
    }

    if (error) {
        return (
            <View style={styles.centered}>
                <Ionicons name="alert-circle-outline" size={48} color="#ff0000" />
                <Text style={styles.errorText}>{error}</Text>
                <TouchableOpacity style={styles.retryButton} onPress={fetchProducts}>
                    <Text style={styles.retryButtonText}>{t('common.retry')}</Text>
                </TouchableOpacity>
            </View>
        );
    }

    return (
        <View style={styles.container}>
            <View style={styles.header}>
                <Text style={styles.title}>{t('products.menu')}</Text>
                <TouchableOpacity 
                    style={styles.searchButton}
                    onPress={() => setShowSearch(!showSearch)}
                >
                    <Ionicons name={showSearch ? "close" : "search"} size={24} color="#666" />
                </TouchableOpacity>
            </View>

            {showSearch && (
                <View style={styles.searchContainer}>
                    <Ionicons name="search" size={20} color="#666" style={styles.searchIcon} />
                    <TextInput
                        style={styles.searchInput}
                        placeholder={t('products.searchPlaceholder')}
                        value={searchQuery}
                        onChangeText={setSearchQuery}
                        autoCapitalize="none"
                    />
                    {searchQuery.length > 0 && (
                        <TouchableOpacity onPress={() => setSearchQuery('')}>
                            <Ionicons name="close-circle" size={20} color="#666" />
                        </TouchableOpacity>
                    )}
                </View>
            )}

            <ScrollView 
                horizontal 
                showsHorizontalScrollIndicator={false}
                style={styles.categoriesContainer}
                contentContainerStyle={styles.categoriesContent}
            >
                {categories.map((category) => (
                    <View key={category}>
                        {renderCategoryButton({ item: category })}
                    </View>
                ))}
            </ScrollView>

            <FlatList
                data={filteredProducts}
                renderItem={renderProductItem}
                keyExtractor={item => item.id}
                contentContainerStyle={styles.list}
                refreshControl={
                    <RefreshControl
                        refreshing={refreshing}
                        onRefresh={onRefresh}
                    />
                }
                ListEmptyComponent={
                    <View style={styles.emptyContainer}>
                        <Ionicons name="restaurant-outline" size={48} color="#ccc" />
                        <Text style={styles.emptyText}>
                            {searchQuery || selectedCategory !== 'all' 
                                ? t('products.noProductsFound')
                                : t('products.noProducts')
                            }
                        </Text>
                    </View>
                }
            />
        </View>
    );
};

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: '#f5f5f5',
    },
    centered: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        padding: 20,
    },
    loadingText: {
        marginTop: 12,
        fontSize: 16,
        color: '#666',
    },
    header: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: 16,
        backgroundColor: '#fff',
        borderBottomWidth: 1,
        borderBottomColor: '#e0e0e0',
    },
    title: {
        fontSize: 24,
        fontWeight: 'bold',
        color: '#333',
    },
    searchButton: {
        padding: 8,
    },
    searchContainer: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: '#fff',
        marginHorizontal: 16,
        marginVertical: 8,
        paddingHorizontal: 12,
        paddingVertical: 8,
        borderRadius: 8,
        borderWidth: 1,
        borderColor: '#e0e0e0',
    },
    searchIcon: {
        marginRight: 8,
    },
    searchInput: {
        flex: 1,
        fontSize: 16,
        color: '#333',
    },
    categoriesContainer: {
        backgroundColor: '#fff',
        borderBottomWidth: 1,
        borderBottomColor: '#e0e0e0',
    },
    categoriesContent: {
        paddingHorizontal: 16,
        paddingVertical: 12,
    },
    categoryButton: {
        paddingHorizontal: 16,
        paddingVertical: 8,
        marginRight: 8,
        borderRadius: 20,
        borderWidth: 1,
        borderColor: '#e0e0e0',
        backgroundColor: '#fff',
    },
    categoryButtonActive: {
        backgroundColor: '#f0f0f0',
    },
    categoryButtonText: {
        fontSize: 14,
        color: '#666',
        fontWeight: '500',
    },
    list: {
        padding: 16,
    },
    card: {
        backgroundColor: '#fff',
        borderRadius: 12,
        padding: 16,
        marginBottom: 16,
        elevation: 2,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 2 },
        shadowOpacity: 0.1,
        shadowRadius: 4,
    },
    cardHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        marginBottom: 8,
    },
    productInfo: {
        flex: 1,
    },
    productName: {
        fontSize: 18,
        fontWeight: 'bold',
        color: '#333',
        marginBottom: 4,
    },
    categoryBadge: {
        alignSelf: 'flex-start',
        paddingHorizontal: 8,
        paddingVertical: 4,
        borderRadius: 12,
    },
    categoryText: {
        fontSize: 12,
        color: '#fff',
        fontWeight: '600',
    },
    price: {
        fontSize: 18,
        fontWeight: 'bold',
        color: '#2196F3',
    },
    description: {
        fontSize: 14,
        color: '#666',
        marginBottom: 12,
        lineHeight: 20,
    },
    cardFooter: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'flex-end',
    },
    taxContainer: {
        flex: 1,
    },
    taxText: {
        fontSize: 12,
        color: '#666',
    },
    stockText: {
        fontSize: 12,
        color: '#666',
        marginTop: 2,
    },
    barcodeText: {
        fontSize: 10,
        color: '#999',
        fontFamily: 'monospace',
    },
    emptyContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        padding: 32,
    },
    emptyText: {
        marginTop: 12,
        fontSize: 16,
        color: '#666',
        textAlign: 'center',
    },
    errorText: {
        color: '#ff0000',
        fontSize: 16,
        textAlign: 'center',
        marginBottom: 16,
    },
    retryButton: {
        backgroundColor: '#2196F3',
        paddingHorizontal: 24,
        paddingVertical: 12,
        borderRadius: 8,
    },
    retryButtonText: {
        color: '#fff',
        fontSize: 16,
        fontWeight: '600',
    },
});

export default ProductList; 