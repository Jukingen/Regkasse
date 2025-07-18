// Türkçe Açıklama: Bu ekran kasiyer için sade, hızlı ve modern bir ana satış arayüzü sunar. TSE durumu, aktif masa, toplam tutar üstte sabitlenmiş; ürünler backend API'den çekilir, yüklenirken spinner, hata varsa uyarı gösterilir; ürünler büyük kartlarla ortada; sepet ve büyük işlem butonları altta yer alır. Kod linter uyumludur ve kasiyer dostu tasarlanmıştır.
import React, { useContext, useEffect } from 'react';
import { View, StyleSheet, Text, TouchableOpacity, ScrollView, SafeAreaView, ActivityIndicator } from 'react-native';

import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';

import ProductList from '../../components/ProductList';
import { TseStatusIndicator } from '../../components/TseStatusIndicator';
import { TableSlotContext } from '../../contexts/TableSlotContext';
import { useProductOperations } from '../../hooks/useProductOperations';
import * as productService from '../../services/api/productService';
import { Product } from '../../services/api/productService';
import CartBar from '../../components/CartBar';
import { useApiCart } from '../../hooks/useApiCart';

const CashRegisterScreen = () => {
  const { products, productsActions } = useProductOperations();
  const { activeSlot, setActiveSlot, slots } = useContext(TableSlotContext);
  const { t } = useTranslation();
  const { cart, loading: cartLoading, addItem, removeItem, updateQuantity, clearCart } = useApiCart();
  const defaultCart = { id: '', items: [], total: 0, discount: 0, vat: 0, grandTotal: 0 };

  // Ekran ilk açıldığında ürünleri yükle
  useEffect(() => {
    productsActions.execute();
    // DEBUG: API'den doğrudan productService.getProducts ile veri çek
    productService.getProducts().then((data) => {
      console.log('DEBUG: productService.getProducts() sonucu:', data);
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // DEBUG: API'den dönen ürün verisini ve ProductList'e giden products prop'unu logla
  console.log('productsState:', products);
  console.log('ProductList props.products:', products.data);

  // TSE cihazı uyarısı
  const [tseWarning, setTseWarning] = React.useState<string | null>(null);
  const handleTseStatusChange = (status: any) => {
    if (!status?.isConnected || !status?.canCreateInvoices) {
      let msg = t('errors.tseConnectionRequired', 'TSE device connection required');
      if (status?.errorMessage) msg += `: ${status.errorMessage}`;
      setTseWarning(msg + ' - e-Signatur nicht vorhanden');
    } else {
      setTseWarning(null);
    }
  };

  // Ürün ekleme işlemi
  const handleAddToCart = (product: Product) => {
    addItem(product.id, 1);
  };

  // Ana işlem butonları
  const handleCompleteSale = () => {
    // Satışı tamamla işlemi
  };
  const handleCancelSale = () => {
    // Satışı iptal et
  };
  const handleDayEnd = () => {
    // Gün sonu işlemi
  };
  const handlePrintReceipt = () => {
    // Fiş yazdır
  };

  // ProductList'e gönderilecek ürünleri hazırla (sadece aktif ve taxType string)
  const productListData = (Array.isArray(products.data) ? products.data : [])
    .filter((p: any) => p.isActive === true || p.isActive === 'true' || p.isActive === 1 || typeof p.isActive === 'undefined')
    .map((p: any) => ({
      ...p,
      taxType: p.taxType === 0 ? 'Standard' : p.taxType === 1 ? 'Reduced' : 'Special'
    }));

  // DEBUG: ProductList'e gönderilen ürünleri logla
  console.log('ProductListData:', productListData);

  return (
    <SafeAreaView style={styles.safeArea}>
      {/* Üst Bar: TSE durumu, aktif masa, toplam */}
      <View style={styles.topBar}>
        <TseStatusIndicator onStatusChange={handleTseStatusChange} />
        <View style={styles.slotInfoRow}>
          <Text style={styles.slotLabel}>{t('cashRegister.activeSlot', 'Aktif Masa/Satış')}:</Text>
          <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.slotScroll}>
            {[...Array(9)].map((_, i) => {
              const slotNumber = i + 1;
              const isActive = activeSlot === slotNumber;
              return (
                <TouchableOpacity
                  key={slotNumber}
                  style={[styles.slotButton, isActive && styles.activeSlotButton]}
                  onPress={() => setActiveSlot(slotNumber)}
                >
                  <Text style={[styles.slotButtonText, isActive && styles.activeSlotButtonText]}>{slotNumber}</Text>
                </TouchableOpacity>
              );
            })}
          </ScrollView>
        </View>
        <View style={styles.totalRow}>
          <Text style={styles.totalLabel}>{t('cashRegister.total', 'Toplam')}:</Text>
          <Text style={styles.totalValue}>€{Number(cart?.grandTotal ?? 0).toFixed(2)}</Text>
        </View>
        {tseWarning && (
          <View style={styles.tseWarningBar}>
            <Text style={styles.tseWarningText}>{tseWarning}</Text>
          </View>
        )}
      </View>

      {/* Orta Alan: Ürünler */}
      <View style={styles.productListWrapper}>
        {products.loading ? (
          <View style={styles.loadingContainer}>
            <ActivityIndicator size="large" color="#1976d2" />
            <Text style={styles.loadingText}>{t('cashRegister.loadingProducts', 'Ürünler yükleniyor...')}</Text>
          </View>
        ) : products.error ? (
          <View style={styles.errorContainer}>
            <Ionicons name="alert-circle" size={40} color="#d32f2f" />
            <Text style={styles.errorText}>{t('cashRegister.productLoadError', 'Ürünler yüklenirken hata oluştu')}</Text>
            <Text style={styles.errorTextSmall}>{products.error}</Text>
          </View>
        ) : (
          <ProductList
            products={productListData}
            userFavorites={[]}
            onAddToCart={handleAddToCart}
            onToggleFavorite={() => {}}
          />
        )}
      </View>

      {/* Sepet ve Alt Bar */}
      <View style={styles.bottomBar}>
        <CartBar
          cart={cart ?? defaultCart}
          loading={cartLoading}
          onRemove={removeItem}
          onUpdateQty={updateQuantity}
          onClear={clearCart}
        />
        <View style={styles.actionButtonsRow}>
          <TouchableOpacity style={[styles.actionButton, styles.completeButton]} onPress={handleCompleteSale}>
            <Ionicons name="checkmark-circle" size={32} color="#fff" />
            <Text style={styles.actionButtonText}>{t('cashRegister.completeSale', 'Satışı Tamamla')}</Text>
          </TouchableOpacity>
          <TouchableOpacity style={[styles.actionButton, styles.cancelButton]} onPress={handleCancelSale}>
            <Ionicons name="close-circle" size={32} color="#fff" />
            <Text style={styles.actionButtonText}>{t('cashRegister.cancelSale', 'İptal')}</Text>
          </TouchableOpacity>
          <TouchableOpacity style={[styles.actionButton, styles.printButton]} onPress={handlePrintReceipt}>
            <Ionicons name="print" size={32} color="#fff" />
            <Text style={styles.actionButtonText}>{t('cashRegister.printReceipt', 'Fiş Yazdır')}</Text>
          </TouchableOpacity>
          <TouchableOpacity style={[styles.actionButton, styles.dayEndButton]} onPress={handleDayEnd}>
            <Ionicons name="calendar" size={32} color="#fff" />
            <Text style={styles.actionButtonText}>{t('cashRegister.dayEnd', 'Gün Sonu')}</Text>
          </TouchableOpacity>
        </View>
      </View>
    </SafeAreaView>
  );
};

const styles = StyleSheet.create({
  safeArea: {
    flex: 1,
    backgroundColor: '#f8fafc',
    minHeight: 0,
    overflow: 'visible',
  },
  topBar: {
    backgroundColor: '#fff',
    paddingVertical: 8,
    paddingHorizontal: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  slotInfoRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 4,
    marginBottom: 4,
  },
  slotLabel: {
    fontWeight: 'bold',
    fontSize: 13,
    marginRight: 8,
  },
  slotScroll: {
    flexGrow: 0,
  },
  slotButton: {
    backgroundColor: '#e0e0e0',
    borderRadius: 8,
    paddingHorizontal: 14,
    paddingVertical: 8,
    marginRight: 6,
  },
  activeSlotButton: {
    backgroundColor: '#1976d2',
  },
  slotButtonText: {
    fontSize: 13,
    color: '#222',
    fontWeight: 'bold',
  },
  activeSlotButtonText: {
    color: '#fff',
  },
  totalRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 4,
    marginBottom: 4,
  },
  totalLabel: {
    fontSize: 13,
    fontWeight: 'bold',
    color: '#059669',
    marginRight: 8,
  },
  totalValue: {
    fontSize: 15,
    fontWeight: 'bold',
    color: '#059669',
  },
  tseWarningBar: {
    backgroundColor: '#d32f2f',
    padding: 8,
    borderRadius: 8,
    marginTop: 6,
    marginBottom: 2,
    width: '100%',
    alignSelf: 'center',
  },
  tseWarningText: {
    color: '#fff',
    fontWeight: 'bold',
    textAlign: 'center',
    fontSize: 11,
  },
  productListWrapper: {
    flex: 1,
    minHeight: 0,
    width: '100%',
    paddingHorizontal: 4,
    paddingTop: 2,
    overflow: 'visible',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 10,
    fontSize: 11,
    color: '#1976d2',
    fontWeight: 'bold',
  },
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  errorText: {
    color: '#d32f2f',
    fontSize: 11,
    fontWeight: 'bold',
    textAlign: 'center',
    marginTop: 8,
  },
  errorTextSmall: {
    color: '#d32f2f',
    fontSize: 9,
    textAlign: 'center',
    marginTop: 4,
  },
  bottomBar: {
    backgroundColor: '#fff',
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
    paddingVertical: 8,
    paddingHorizontal: 8,
    minHeight: 90,
  },
  actionButtonsRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 8,
    // gap: 8, // React Native desteklemez, kaldırıldı
  },
  actionButton: {
    flex: 1,
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#1976d2',
    borderRadius: 6,
    paddingVertical: 4,
    marginHorizontal: 2,
  },
  actionButtonText: {
    color: '#fff',
    fontWeight: 'bold',
    fontSize: 10,
    marginTop: 2,
    textAlign: 'center',
  },
  completeButton: {
    backgroundColor: '#059669',
  },
  cancelButton: {
    backgroundColor: '#d32f2f',
  },
  printButton: {
    backgroundColor: '#1976d2',
  },
  dayEndButton: {
    backgroundColor: '#ff9800',
  },
});

export default CashRegisterScreen; 