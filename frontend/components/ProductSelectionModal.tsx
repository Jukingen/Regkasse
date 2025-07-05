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
  ActivityIndicator,
  Dimensions
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { productService, Product } from '../services/api/productService';
import { BarcodeScannerService, BarcodeResult } from '../services/BarcodeScanner';

interface ProductSelectionModalProps {
  visible: boolean;
  onClose: () => void;
  onSelectProduct: (product: Product, quantity: number) => void;
  products?: Product[];
  searchQuery: string;
  loading: boolean;
}

const { width: screenWidth } = Dimensions.get('window');

const ProductSelectionModal: React.FC<ProductSelectionModalProps> = ({
  visible,
  onClose,
  onSelectProduct,
  products = [],
  searchQuery,
  loading,
}) => {
  const { t } = useTranslation();
  const [searchText, setSearchText] = useState(searchQuery);
  const [selectedQuantity, setSelectedQuantity] = useState(1);
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [showScanner, setShowScanner] = useState(false);
  const [scanning, setScanning] = useState(false);
  const [barcodeScanner] = useState(() => BarcodeScannerService.getInstance());

  // Güvenli ürün listesi
  const safeProducts = Array.isArray(products) ? products : [];

  // Filtrelenmiş ürünler
  const filteredProducts = safeProducts.filter(product =>
    product && product.name && 
    (product.name.toLowerCase().includes(searchText.toLowerCase()) ||
     (product.barcode && product.barcode.includes(searchText)))
  );

  // Barkod tarama işlemi
  const handleBarcodeScanned = async (result: BarcodeResult) => {
    try {
      setScanning(true);
      
      // Barkod ile ürün ara
      const product = safeProducts.find(p => p && p.barcode === result.data);
      
      if (product) {
        setSelectedProduct(product);
        setSelectedQuantity(1);
        setSearchText(product.barcode || '');
        setShowScanner(false);
        
        // Başarılı tarama bildirimi
        Alert.alert(
          t('barcode.product_found'),
          `${product.name} - ${product.price.toFixed(2)}€`,
          [{ text: t('common.ok') }]
        );
      } else {
        // Ürün bulunamadı
        Alert.alert(
          t('barcode.product_not_found'),
          t('barcode.try_manual_search'),
          [{ text: t('common.ok') }]
        );
      }
    } catch (error) {
      console.error('Barcode scan error:', error);
      Alert.alert(
        t('errors.scan_failed'),
        t('errors.try_again'),
        [{ text: t('common.ok') }]
      );
    } finally {
      setScanning(false);
    }
  };

  // Barkod tarayıcısını başlat
  const startBarcodeScanner = async () => {
    try {
      const hasPermission = await barcodeScanner.checkPermissions();
      
      if (!hasPermission) {
        const granted = await barcodeScanner.requestPermissions();
        if (!granted) {
          Alert.alert(
            t('barcode.permission_required'),
            t('barcode.camera_permission_needed'),
            [{ text: t('common.ok') }]
          );
          return;
        }
      }
      
      setShowScanner(true);
      setScanning(true);
      
      await barcodeScanner.startScanning(
        handleBarcodeScanned,
        (error) => {
          console.error('Scanner error:', error);
          setScanning(false);
          Alert.alert(t('errors.scanner_error'), error, [{ text: t('common.ok') }]);
        }
      );
    } catch (error) {
      console.error('Failed to start scanner:', error);
      Alert.alert(
        t('errors.scanner_start_failed'),
        t('errors.try_again'),
        [{ text: t('common.ok') }]
      );
    }
  };

  // Barkod tarayıcısını durdur
  const stopBarcodeScanner = () => {
    barcodeScanner.stopScanning();
    setShowScanner(false);
    setScanning(false);
  };

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
      setShowScanner(false);
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
        {item.barcode && (
          <Text style={styles.productBarcode}>
            {t('product.barcode')}: {item.barcode}
          </Text>
        )}
      </View>
      {selectedProduct?.id === item.id && (
        <Ionicons name="checkmark-circle" size={24} color="#4CAF50" />
      )}
    </TouchableOpacity>
  );

  // Modal kapatıldığında tarayıcıyı durdur
  useEffect(() => {
    if (!visible) {
      stopBarcodeScanner();
    }
  }, [visible]);

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
            {/* Arama ve Barkod Tarama */}
            <View style={styles.searchContainer}>
              <View style={styles.searchInputContainer}>
                <Ionicons name="search" size={20} color="#666" />
                <TextInput
                  style={styles.searchInput}
                  placeholder={t('search.products')}
                  value={searchText}
                  onChangeText={setSearchText}
                />
              </View>
              <TouchableOpacity
                style={styles.scanButton}
                onPress={startBarcodeScanner}
                disabled={scanning}
              >
                <Ionicons 
                  name={scanning ? "scan" : "scan-outline"} 
                  size={24} 
                  color={scanning ? "#4CAF50" : "#666"} 
                />
              </TouchableOpacity>
            </View>

            {/* Barkod Tarayıcı */}
            {showScanner && (
              <View style={styles.scannerContainer}>
                <View style={styles.scannerHeader}>
                  <Text style={styles.scannerTitle}>{t('barcode.scanning')}</Text>
                  <TouchableOpacity onPress={stopBarcodeScanner}>
                    <Ionicons name="close" size={24} color="#666" />
                  </TouchableOpacity>
                </View>
                <View style={styles.scannerContent}>
                  {scanning ? (
                    <ActivityIndicator size="large" color="#4CAF50" />
                  ) : (
                    <Text style={styles.scannerText}>{t('barcode.ready')}</Text>
                  )}
                </View>
              </View>
            )}

            {/* Ürün Listesi */}
            {loading ? (
              <View style={styles.loadingContainer}>
                <ActivityIndicator size="large" color="#4CAF50" />
                <Text style={styles.loadingText}>{t('common.loading')}</Text>
              </View>
            ) : filteredProducts.length === 0 ? (
              <View style={styles.emptyContainer}>
                <Ionicons name="search-outline" size={48} color="#ccc" />
                <Text style={styles.emptyText}>
                  {searchText ? t('search.no_results') : t('product.no_products')}
                </Text>
              </View>
            ) : (
              <FlatList
                data={filteredProducts}
                renderItem={renderProductItem}
                keyExtractor={(item) => item.id}
                style={styles.productList}
                showsVerticalScrollIndicator={false}
              />
            )}

            {/* Seçili Ürün Detayları */}
            {selectedProduct && (
              <View style={styles.selectedProductContainer}>
                <View style={styles.selectedProductInfo}>
                  <Text style={styles.selectedProductName}>{selectedProduct.name}</Text>
                  <Text style={styles.selectedProductPrice}>
                    {selectedProduct.price.toFixed(2)}€
                  </Text>
                </View>
                <View style={styles.quantityContainer}>
                  <TouchableOpacity
                    style={styles.quantityButton}
                    onPress={() => setSelectedQuantity(Math.max(1, selectedQuantity - 1))}
                  >
                    <Ionicons name="remove" size={20} color="#666" />
                  </TouchableOpacity>
                  <Text style={styles.quantityText}>{selectedQuantity}</Text>
                  <TouchableOpacity
                    style={styles.quantityButton}
                    onPress={() => setSelectedQuantity(selectedQuantity + 1)}
                  >
                    <Ionicons name="add" size={20} color="#666" />
                  </TouchableOpacity>
                </View>
                <TouchableOpacity
                  style={styles.confirmButton}
                  onPress={handleConfirmSelection}
                >
                  <Text style={styles.confirmButtonText}>
                    {t('common.add')} ({selectedQuantity})
                  </Text>
                </TouchableOpacity>
              </View>
            )}
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
    width: screenWidth * 0.9,
    maxWidth: 500,
    height: '80%',
    backgroundColor: 'white',
    borderRadius: 12,
    overflow: 'hidden',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
  },
  closeButton: {
    padding: 4,
  },
  content: {
    flex: 1,
    padding: 16,
  },
  searchContainer: {
    flexDirection: 'row',
    marginBottom: 16,
    gap: 8,
  },
  searchInputContainer: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#f5f5f5',
    borderRadius: 8,
    paddingHorizontal: 12,
  },
  searchInput: {
    flex: 1,
    padding: 12,
    fontSize: 16,
  },
  scanButton: {
    backgroundColor: '#4CAF50',
    borderRadius: 8,
    padding: 12,
    justifyContent: 'center',
    alignItems: 'center',
  },
  scannerContainer: {
    backgroundColor: '#f9f9f9',
    borderRadius: 8,
    marginBottom: 16,
    overflow: 'hidden',
  },
  scannerHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 12,
    backgroundColor: '#e0e0e0',
  },
  scannerTitle: {
    fontSize: 16,
    fontWeight: '500',
  },
  scannerContent: {
    padding: 32,
    alignItems: 'center',
  },
  scannerText: {
    fontSize: 16,
    color: '#666',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 16,
    fontSize: 16,
    color: '#666',
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  emptyText: {
    marginTop: 16,
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
  },
  productList: {
    flex: 1,
  },
  productItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 12,
    backgroundColor: '#f9f9f9',
    borderRadius: 8,
    marginBottom: 8,
  },
  productItemSelected: {
    backgroundColor: '#e3f2fd',
    borderColor: '#2196F3',
    borderWidth: 1,
  },
  productInfo: {
    flex: 1,
  },
  productName: {
    fontSize: 16,
    fontWeight: '500',
    marginBottom: 4,
  },
  productPrice: {
    fontSize: 14,
    color: '#2196F3',
    fontWeight: '600',
  },
  productStock: {
    fontSize: 12,
    color: '#666',
    marginTop: 2,
  },
  productTax: {
    fontSize: 12,
    color: '#666',
    marginTop: 2,
  },
  productBarcode: {
    fontSize: 12,
    color: '#999',
    marginTop: 2,
  },
  selectedProductContainer: {
    backgroundColor: '#f9f9f9',
    borderRadius: 8,
    padding: 16,
    marginTop: 16,
  },
  selectedProductInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  selectedProductName: {
    fontSize: 16,
    fontWeight: '500',
    flex: 1,
  },
  selectedProductPrice: {
    fontSize: 16,
    fontWeight: '600',
    color: '#2196F3',
  },
  quantityContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 12,
  },
  quantityButton: {
    backgroundColor: 'white',
    borderRadius: 20,
    width: 40,
    height: 40,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  quantityText: {
    fontSize: 18,
    fontWeight: '600',
    marginHorizontal: 20,
    minWidth: 30,
    textAlign: 'center',
  },
  confirmButton: {
    backgroundColor: '#4CAF50',
    borderRadius: 8,
    padding: 12,
    alignItems: 'center',
  },
  confirmButtonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
  },
});

export default ProductSelectionModal; 