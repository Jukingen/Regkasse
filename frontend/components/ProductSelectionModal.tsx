import React, { useState, useEffect } from 'react';
import {
  Modal,
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  TextInput,
  FlatList,
  Alert,
  ActivityIndicator
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { productService, Product } from '../services/api/productService';

interface ProductSelectionModalProps {
  visible: boolean;
  onClose: () => void;
  onSelectProduct: (product: Product, quantity: number) => void;
  products: Product[];
  searchQuery: string;
  loading: boolean;
}

const ProductSelectionModal: React.FC<ProductSelectionModalProps> = ({
  visible,
  onClose,
  onSelectProduct,
  products,
  searchQuery,
  loading,
}) => {
  const { t } = useTranslation();
  const [searchText, setSearchText] = useState(searchQuery);
  const [selectedQuantity, setSelectedQuantity] = useState(1);
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);

  // Filtrelenmiş ürünler
  const filteredProducts = products.filter(product =>
    product.name.toLowerCase().includes(searchText.toLowerCase()) ||
    product.barcode?.includes(searchText)
  );

  const handleProductSelect = (product: Product) => {
    setSelectedProduct(product);
    setSelectedQuantity(1);
  };

  const handleConfirmSelection = () => {
    if (selectedProduct) {
      if (selectedQuantity > selectedProduct.stock) {
        Alert.alert(
          t('errors.stock_insufficient'),
          t('errors.stock_not_enough', { available: selectedProduct.stock }),
          [{ text: t('common.ok') }]
        );
        return;
      }
      
      onSelectProduct(selectedProduct, selectedQuantity);
      setSelectedProduct(null);
      setSelectedQuantity(1);
      setSearchText('');
    }
  };

  const renderProductItem = ({ item }: { item: Product }) => (
    <TouchableOpacity
      style={[
        styles.productItem,
        selectedProduct?.id === item.id && styles.productItemSelected
      ]}
      onPress={() => handleProductSelect(item)}
    >
      <View style={styles.productInfo}>
        <Text style={styles.productName}>{item.name}</Text>
        <Text style={styles.productPrice}>{item.price.toFixed(2)}€</Text>
        <Text style={styles.productStock}>
          {t('product.stock')}: {item.stock} {item.unit}
        </Text>
        <Text style={styles.productTax}>
          {t(`tax.${item.taxType}`)} ({item.taxType === 'standard' ? '20%' : item.taxType === 'reduced' ? '10%' : '13%'})
        </Text>
      </View>
      {selectedProduct?.id === item.id && (
        <Ionicons name="checkmark-circle" size={24} color="#4CAF50" />
      )}
    </TouchableOpacity>
  );

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent={true}
      onRequestClose={onClose}
    >
      <View style={styles.overlay}>
        <View style={styles.modal}>
          <View style={styles.header}>
            <Text style={styles.title}>{t('product.select')}</Text>
            <TouchableOpacity onPress={onClose} style={styles.closeButton}>
              <Ionicons name="close" size={24} color="#666" />
            </TouchableOpacity>
          </View>

          <View style={styles.content}>
            {/* Arama */}
            <View style={styles.searchContainer}>
              <Ionicons name="search" size={20} color="#666" />
              <TextInput
                style={styles.searchInput}
                placeholder={t('search.products')}
                value={searchText}
                onChangeText={setSearchText}
              />
            </View>

            {/* Ürün Listesi */}
            {loading ? (
              <View style={styles.loadingContainer}>
                <ActivityIndicator size="large" color="#2196F3" />
                <Text style={styles.loadingText}>{t('common.loading')}</Text>
              </View>
            ) : (
              <FlatList
                data={filteredProducts}
                renderItem={renderProductItem}
                keyExtractor={(item) => item.id}
                style={styles.productList}
                showsVerticalScrollIndicator={false}
                ListEmptyComponent={
                  <View style={styles.emptyContainer}>
                    <Ionicons name="search-outline" size={48} color="#ccc" />
                    <Text style={styles.emptyText}>{t('product.no_products_found')}</Text>
                  </View>
                }
              />
            )}

            {/* Seçili Ürün Detayı */}
            {selectedProduct && (
              <View style={styles.selectedProductContainer}>
                <Text style={styles.selectedProductTitle}>{t('product.selected')}</Text>
                <View style={styles.selectedProductInfo}>
                  <Text style={styles.selectedProductName}>{selectedProduct.name}</Text>
                  <Text style={styles.selectedProductPrice}>
                    {selectedProduct.price.toFixed(2)}€
                  </Text>
                </View>
                
                <View style={styles.quantityContainer}>
                  <Text style={styles.quantityLabel}>{t('product.quantity')}</Text>
                  <View style={styles.quantityControls}>
                    <TouchableOpacity
                      style={styles.quantityButton}
                      onPress={() => setSelectedQuantity(Math.max(1, selectedQuantity - 1))}
                    >
                      <Ionicons name="remove" size={20} color="#F44336" />
                    </TouchableOpacity>
                    <Text style={styles.quantityText}>{selectedQuantity}</Text>
                    <TouchableOpacity
                      style={styles.quantityButton}
                      onPress={() => setSelectedQuantity(selectedQuantity + 1)}
                      disabled={selectedQuantity >= selectedProduct.stock}
                    >
                      <Ionicons name="add" size={20} color="#4CAF50" />
                    </TouchableOpacity>
                  </View>
                </View>
                
                <View style={styles.totalContainer}>
                  <Text style={styles.totalLabel}>{t('product.total')}</Text>
                  <Text style={styles.totalAmount}>
                    {(selectedProduct.price * selectedQuantity).toFixed(2)}€
                  </Text>
                </View>
              </View>
            )}
          </View>

          {/* Butonlar */}
          <View style={styles.footer}>
            <TouchableOpacity
              style={styles.cancelButton}
              onPress={onClose}
            >
              <Text style={styles.cancelButtonText}>{t('common.cancel')}</Text>
            </TouchableOpacity>
            
            <TouchableOpacity
              style={[
                styles.confirmButton,
                !selectedProduct && styles.confirmButtonDisabled
              ]}
              onPress={handleConfirmSelection}
              disabled={!selectedProduct}
            >
              <Ionicons name="add" size={20} color="white" />
              <Text style={styles.confirmButtonText}>{t('product.add_to_cart')}</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  modal: {
    backgroundColor: 'white',
    borderRadius: 16,
    width: '95%',
    height: '90%',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 20,
    fontWeight: '600',
  },
  closeButton: {
    padding: 4,
  },
  content: {
    flex: 1,
    padding: 20,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#f5f5f5',
    borderRadius: 8,
    padding: 12,
    marginBottom: 16,
  },
  searchInput: {
    flex: 1,
    marginLeft: 8,
    fontSize: 16,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 12,
    fontSize: 16,
    color: '#666',
  },
  productList: {
    flex: 1,
  },
  productItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 16,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    marginBottom: 8,
  },
  productItemSelected: {
    borderColor: '#4CAF50',
    backgroundColor: '#f0f8f0',
  },
  productInfo: {
    flex: 1,
  },
  productName: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 4,
  },
  productPrice: {
    fontSize: 18,
    fontWeight: '700',
    color: '#2196F3',
    marginBottom: 4,
  },
  productStock: {
    fontSize: 14,
    color: '#666',
    marginBottom: 2,
  },
  productTax: {
    fontSize: 12,
    color: '#999',
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
  selectedProductContainer: {
    backgroundColor: '#f9f9f9',
    borderRadius: 8,
    padding: 16,
    marginTop: 16,
  },
  selectedProductTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 12,
  },
  selectedProductInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
  },
  selectedProductName: {
    fontSize: 16,
    fontWeight: '500',
    flex: 1,
  },
  selectedProductPrice: {
    fontSize: 18,
    fontWeight: '700',
    color: '#2196F3',
  },
  quantityContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  quantityLabel: {
    fontSize: 16,
    fontWeight: '500',
  },
  quantityControls: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  quantityButton: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: '#f0f0f0',
    justifyContent: 'center',
    alignItems: 'center',
  },
  quantityText: {
    fontSize: 18,
    fontWeight: '600',
    minWidth: 24,
    textAlign: 'center',
  },
  totalContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
  },
  totalLabel: {
    fontSize: 16,
    fontWeight: '600',
  },
  totalAmount: {
    fontSize: 20,
    fontWeight: '700',
    color: '#4CAF50',
  },
  footer: {
    flexDirection: 'row',
    padding: 20,
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
    gap: 12,
  },
  cancelButton: {
    flex: 1,
    padding: 16,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    alignItems: 'center',
  },
  cancelButtonText: {
    fontSize: 16,
    color: '#666',
  },
  confirmButton: {
    flex: 2,
    backgroundColor: '#4CAF50',
    padding: 16,
    borderRadius: 8,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  confirmButtonDisabled: {
    backgroundColor: '#ccc',
  },
  confirmButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: 'white',
  },
});

export default ProductSelectionModal; 