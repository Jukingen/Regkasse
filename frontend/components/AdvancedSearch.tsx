import React, { useState, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  TouchableOpacity,
  Modal,
  FlatList,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { Product } from '../services/api/productService';
import { BarcodeScannerService, BarcodeResult } from '../services/BarcodeScanner';
import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';

interface AdvancedSearchProps {
  searchQuery: string;
  onSearchChange: (query: string) => void;
  onProductSelect: (product: Product) => void;
  products: Product[];
  loading: boolean;
}

interface SearchHistoryItem {
  id: string;
  query: string;
  timestamp: number;
}

const AdvancedSearch: React.FC<AdvancedSearchProps> = ({
  searchQuery,
  onSearchChange,
  onProductSelect,
  products,
  loading,
}) => {
  const { t } = useTranslation();
  const [showSearchModal, setShowSearchModal] = useState(false);
  const [searchHistory, setSearchHistory] = useState<SearchHistoryItem[]>([]);
  const [showScanner, setShowScanner] = useState(false);
  const [scanning, setScanning] = useState(false);
  const [barcodeScanner] = useState(() => BarcodeScannerService.getInstance());

  // Filtrelenmiş ürünler
  const filteredProducts = (products || []).filter(product =>
    product.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
    (product.barcode && product.barcode.includes(searchQuery)) ||
    (product.category && product.category.toLowerCase().includes(searchQuery.toLowerCase()))
  );

  // Arama geçmişine ekle
  const addToSearchHistory = useCallback((query: string) => {
    if (!query.trim()) return;
    
    setSearchHistory(prev => {
      const existing = prev.find(item => item.query.toLowerCase() === query.toLowerCase());
      if (existing) {
        // Mevcut öğeyi en üste taşı
        return [
          { ...existing, timestamp: Date.now() },
          ...prev.filter(item => item.id !== existing.id)
        ].slice(0, 10); // Son 10 aramayı tut
      }
      
      return [
        { id: Date.now().toString(), query, timestamp: Date.now() },
        ...prev
      ].slice(0, 10);
    });
  }, []);

  // Barkod tarama işlemi
  const handleBarcodeScanned = async (result: BarcodeResult) => {
    try {
      setScanning(true);
      
      // Barkod ile ürün ara
      const product = (products || []).find(p => p.barcode === result.data);
      
      if (product) {
        onProductSelect(product);
        setShowScanner(false);
        setShowSearchModal(false);
        
        Alert.alert(
          t('barcode.product_found'),
          `${product.name} - ${product.price.toFixed(2)}€`,
          [{ text: t('common.ok') }]
        );
      } else {
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

  // Arama geçmişinden seç
  const selectFromHistory = (query: string) => {
    onSearchChange(query);
    setShowSearchModal(false);
  };

  // Arama geçmişini temizle
  const clearSearchHistory = () => {
    setSearchHistory([]);
  };

  const renderSearchHistoryItem = ({ item }: { item: SearchHistoryItem }) => (
    <TouchableOpacity
      style={styles.historyItem}
      onPress={() => selectFromHistory(item.query)}
    >
      <Ionicons name="time-outline" size={16} color={Colors.light.textSecondary} />
      <Text style={styles.historyText}>{item.query}</Text>
    </TouchableOpacity>
  );

  const renderProductItem = ({ item }: { item: Product }) => (
    <TouchableOpacity
      style={styles.productItem}
      onPress={() => {
        onProductSelect(item);
        setShowSearchModal(false);
      }}
    >
      <View style={styles.productInfo}>
        <Text style={styles.productName}>{item.name}</Text>
        <Text style={styles.productPrice}>{item.price.toFixed(2)}€</Text>
        {item.barcode && (
          <Text style={styles.productBarcode}>Barkod: {item.barcode}</Text>
        )}
      </View>
      <Ionicons name="chevron-forward" size={20} color={Colors.light.textSecondary} />
    </TouchableOpacity>
  );

  return (
    <View style={styles.container}>
      {/* Ana arama çubuğu */}
      <View style={styles.searchContainer}>
        <View style={styles.searchInputContainer}>
          <Ionicons name="search" size={20} color={Colors.light.textSecondary} />
          <TextInput
            style={styles.searchInput}
            placeholder={t('search.products')}
            value={searchQuery}
            onChangeText={(text) => {
              onSearchChange(text);
              if (text.trim()) {
                addToSearchHistory(text);
              }
            }}
            onSubmitEditing={() => setShowSearchModal(true)}
          />
          {searchQuery.length > 0 && (
            <TouchableOpacity
              onPress={() => onSearchChange('')}
              style={styles.clearButton}
            >
              <Ionicons name="close-circle" size={20} color={Colors.light.textSecondary} />
            </TouchableOpacity>
          )}
        </View>
        
        <TouchableOpacity
          style={styles.scanButton}
          onPress={startBarcodeScanner}
          disabled={scanning}
        >
          <Ionicons 
            name={scanning ? "scan" : "scan-outline"} 
            size={24} 
            color={Colors.light.surface} 
          />
        </TouchableOpacity>
        
        <TouchableOpacity
          style={styles.advancedButton}
          onPress={() => setShowSearchModal(true)}
        >
          <Ionicons name="options-outline" size={24} color={Colors.light.primary} />
        </TouchableOpacity>
      </View>

      {/* Gelişmiş arama modalı */}
      <Modal
        visible={showSearchModal}
        animationType="slide"
        transparent={true}
        onRequestClose={() => setShowSearchModal(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{t('search.advanced')}</Text>
              <TouchableOpacity
                onPress={() => setShowSearchModal(false)}
                style={styles.closeButton}
              >
                <Ionicons name="close" size={24} color={Colors.light.text} />
              </TouchableOpacity>
            </View>

            <View style={styles.modalBody}>
              {/* Arama geçmişi */}
              {searchHistory.length > 0 && (
                <View style={styles.historySection}>
                  <View style={styles.sectionHeader}>
                    <Text style={styles.sectionTitle}>{t('search.recent')}</Text>
                    <TouchableOpacity onPress={clearSearchHistory}>
                      <Text style={styles.clearHistoryText}>{t('search.clear')}</Text>
                    </TouchableOpacity>
                  </View>
                  <FlatList
                    data={searchHistory}
                    renderItem={renderSearchHistoryItem}
                    keyExtractor={(item) => item.id}
                    showsVerticalScrollIndicator={false}
                  />
                </View>
              )}

              {/* Arama sonuçları */}
              {searchQuery.length > 0 && (
                <View style={styles.resultsSection}>
                  <Text style={styles.sectionTitle}>
                    {t('search.results')} ({filteredProducts.length})
                  </Text>
                  <FlatList
                    data={filteredProducts}
                    renderItem={renderProductItem}
                    keyExtractor={(item) => item.id}
                    showsVerticalScrollIndicator={false}
                    ListEmptyComponent={
                      <Text style={styles.noResultsText}>
                        {t('search.no_results')}
                      </Text>
                    }
                  />
                </View>
              )}
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    marginBottom: Spacing.md,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
  },
  searchInputContainer: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    borderWidth: 1,
    borderColor: Colors.light.border,
    minHeight: 44,
  },
  searchInput: {
    flex: 1,
    marginLeft: Spacing.sm,
    ...Typography.body,
    color: Colors.light.text,
  },
  clearButton: {
    padding: Spacing.xs,
  },
  scanButton: {
    backgroundColor: Colors.light.primary,
    borderRadius: BorderRadius.md,
    padding: Spacing.sm,
    minHeight: 44,
    minWidth: 44,
    justifyContent: 'center',
    alignItems: 'center',
  },
  advancedButton: {
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.md,
    padding: Spacing.sm,
    minHeight: 44,
    minWidth: 44,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    backgroundColor: Colors.light.surface,
    borderTopLeftRadius: BorderRadius.xl,
    borderTopRightRadius: BorderRadius.xl,
    maxHeight: '80%',
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  modalTitle: {
    ...Typography.h3,
    color: Colors.light.text,
  },
  closeButton: {
    padding: Spacing.xs,
  },
  modalBody: {
    padding: Spacing.md,
  },
  historySection: {
    marginBottom: Spacing.lg,
  },
  sectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  sectionTitle: {
    ...Typography.h3,
    color: Colors.light.text,
  },
  clearHistoryText: {
    ...Typography.bodySmall,
    color: Colors.light.primary,
  },
  historyItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: Spacing.sm,
    paddingHorizontal: Spacing.md,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.sm,
    marginBottom: Spacing.xs,
  },
  historyText: {
    ...Typography.body,
    color: Colors.light.text,
    marginLeft: Spacing.sm,
  },
  resultsSection: {
    flex: 1,
  },
  productItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: Spacing.md,
    paddingHorizontal: Spacing.md,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.sm,
    marginBottom: Spacing.xs,
  },
  productInfo: {
    flex: 1,
  },
  productName: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '500',
    marginBottom: Spacing.xs,
  },
  productPrice: {
    ...Typography.bodySmall,
    color: Colors.light.primary,
    fontWeight: '600',
  },
  productBarcode: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    marginTop: Spacing.xs,
  },
  noResultsText: {
    ...Typography.body,
    color: Colors.light.textSecondary,
    textAlign: 'center',
    padding: Spacing.lg,
  },
});

export default AdvancedSearch; 