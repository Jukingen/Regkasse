import React, { useState, useCallback } from 'react';
import { View, Text, StyleSheet, TextInput, TouchableOpacity } from 'react-native';
import { useTheme } from '../../contexts/ThemeContext';
import { useFetch } from '../../hooks/useFetch';
import { API_BASE_URL } from '../../config';
import { OptimizedList } from '../../components/OptimizedList';
import { Ionicons } from '@expo/vector-icons';

interface Product {
  id: string;
  name: string;
  price: number;
  stock: number;
  tax_type: 'standard' | 'reduced' | 'special';
}

export default function ProductsScreen() {
  const { theme } = useTheme();
  const styles = createStyles(theme);
  const [searchQuery, setSearchQuery] = useState('');

  const { data: products, loading, error, refetch } = useFetch<Product[]>({
    url: `${API_BASE_URL}/products`,
    options: {
      method: 'GET',
    },
  });

  const filteredProducts = products?.filter(product =>
    product.name.toLowerCase().includes(searchQuery.toLowerCase())
  ) ?? [];

  const renderProduct = useCallback(({ item }: { item: Product }) => (
    <View style={styles.productItem}>
      <View style={styles.productInfo}>
        <Text style={styles.productName}>{item.name}</Text>
        <Text style={styles.productPrice}>{item.price.toFixed(2)} €</Text>
      </View>
      <View style={styles.productDetails}>
        <Text style={styles.stockText}>Stok: {item.stock}</Text>
        <Text style={styles.taxText}>KDV: {item.tax_type}</Text>
      </View>
    </View>
  ), [theme]);

  const ListEmptyComponent = useCallback(() => (
    <View style={styles.emptyContainer}>
      <Ionicons name="alert-circle-outline" size={48} color={theme.text} />
      <Text style={styles.emptyText}>
        {loading ? 'Yükleniyor...' : 'Ürün bulunamadı'}
      </Text>
    </View>
  ), [loading, theme]);

  return (
    <View style={styles.container}>
      <View style={styles.searchContainer}>
        <Ionicons name="search" size={20} color={theme.text} style={styles.searchIcon} />
        <TextInput
          style={styles.searchInput}
          placeholder="Ürün ara..."
          placeholderTextColor={theme.textSecondary}
          value={searchQuery}
          onChangeText={setSearchQuery}
        />
      </View>

      {error ? (
        <View style={styles.errorContainer}>
          <Text style={styles.errorText}>{error.message}</Text>
          <TouchableOpacity style={styles.retryButton} onPress={refetch}>
            <Text style={styles.retryButtonText}>Tekrar Dene</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <OptimizedList
          data={filteredProducts}
          renderItem={renderProduct}
          loading={loading}
          onRefresh={refetch}
          ListEmptyComponent={ListEmptyComponent}
          contentContainerStyle={styles.listContent}
        />
      )}
    </View>
  );
}

const createStyles = (theme: any) => StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: theme.background,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 16,
    backgroundColor: theme.card,
    borderBottomWidth: 1,
    borderBottomColor: theme.border,
  },
  searchIcon: {
    marginRight: 8,
  },
  searchInput: {
    flex: 1,
    height: 40,
    color: theme.text,
    fontSize: 16,
  },
  listContent: {
    padding: 16,
  },
  productItem: {
    backgroundColor: theme.card,
    borderRadius: 8,
    padding: 16,
    marginBottom: 12,
    elevation: 2,
    shadowColor: theme.shadow,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
  },
  productInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  productName: {
    fontSize: 16,
    fontWeight: '600',
    color: theme.text,
  },
  productPrice: {
    fontSize: 16,
    fontWeight: '600',
    color: theme.primary,
  },
  productDetails: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  stockText: {
    fontSize: 14,
    color: theme.textSecondary,
  },
  taxText: {
    fontSize: 14,
    color: theme.textSecondary,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  emptyText: {
    marginTop: 16,
    fontSize: 16,
    color: theme.textSecondary,
    textAlign: 'center',
  },
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  errorText: {
    fontSize: 16,
    color: theme.error,
    textAlign: 'center',
    marginBottom: 16,
  },
  retryButton: {
    backgroundColor: theme.primary,
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
  },
  retryButtonText: {
    color: theme.buttonText,
    fontSize: 16,
    fontWeight: '600',
  },
}); 