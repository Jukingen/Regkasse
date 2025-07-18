import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  FlatList,
  Dimensions,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { Product } from '../services/api/productService';

const { width: screenWidth } = Dimensions.get('window');

interface ProductListProps {
  products: Product[];
  userFavorites: Product[];
  onAddToCart: (product: Product) => void;
  onToggleFavorite: (product: Product) => void;
}

const ProductList: React.FC<ProductListProps> = ({
  products,
  userFavorites,
  onAddToCart,
  onToggleFavorite,
}) => {
  const { t } = useTranslation();

  // DEBUG: Componentin render edildiğini göster
  console.log('ProductList render', products);

  // Container genişliğini hesapla (sol panel genişliği)
  const containerWidth = screenWidth * 0.5; // Sol panel genişliği (yaklaşık %50)
  
  // Ekran boyutuna göre sütun sayısını hesapla - daha fazla sütun için optimize edildi
  let numColumns = 2; // Varsayılan olarak 2 sütun
  
  if (screenWidth >= 768) {
    // Tablet ve büyük ekranlar için 3+ sütun
    numColumns = Math.max(3, Math.floor((containerWidth - 16) / 100));
  } else if (screenWidth >= 480) {
    // Orta boyutlu ekranlar için 3 sütun
    numColumns = 3;
  } else {
    // Küçük ekranlar için 2 sütun
    numColumns = 2;
  }
  
  // Ürün kartı genişliğini hesapla - daha küçük kartlar
  const cardWidth = numColumns === 1 
    ? containerWidth - 16 // Tek sütun için tam genişlik
    : Math.max(80, (containerWidth - 16 - (numColumns * 4)) / numColumns); // Minimum 80px genişlik

  const renderProductItem = ({ item }: { item: Product }) => (
    <TouchableOpacity
      style={styles.productCard}
      onPress={() => onAddToCart(item)}
      activeOpacity={0.85}
    >
      <View style={styles.productInfoContainer}>
        <Text style={styles.productName}>{item.name}</Text>
        <Text style={styles.productCategory}>{item.category}</Text>
      </View>
      <View style={styles.productFooterContainer}>
        <Text style={styles.productPrice}>€{item.price.toFixed(2)}</Text>
        <TouchableOpacity
          style={styles.addButton}
          onPress={() => onAddToCart(item)}
          activeOpacity={0.7}
        >
          <Ionicons name="add" size={32} color="#fff" />
        </TouchableOpacity>
      </View>
    </TouchableOpacity>
  );

  return (
    <View style={styles.productsSection}>
      <Text style={styles.sectionTitleSmall}>
        {t('cashRegister.allProducts')}
      </Text>
      <FlatList
        data={products}
        keyExtractor={(item) => item.id}
        renderItem={renderProductItem}
        numColumns={numColumns}
        showsVerticalScrollIndicator={false}
        contentContainerStyle={styles.productList}
        style={styles.flatListContainer}
        ListEmptyComponent={
          <Text style={{ textAlign: 'center', color: '#888', marginTop: 32 }}>
            {t('cashRegister.noProducts', 'Hiç ürün bulunamadı')}
          </Text>
        }
      />
      <Text style={{textAlign: 'center', color: 'red', marginTop: 8}}>TEST - ProductList aktif</Text>
    </View>
  );
};

const styles = StyleSheet.create({
  productsSection: {
    flex: 1,
    width: '100%',
    backgroundColor: '#f8fafc', // Modern açık gri arka plan
  },
  sectionTitleSmall: {
    ...Typography.caption,
    color: '#1e293b',
    marginBottom: Spacing.xs,
    paddingHorizontal: Spacing.xs,
    fontWeight: '700',
    fontSize: 12,
    letterSpacing: 0.2,
  },
  flatListContainer: {
    width: '100%',
  },
  productList: {
    paddingHorizontal: Spacing.xs,
    width: '100%',
  },
  productCard: {
    backgroundColor: '#ffffff',
    borderRadius: 8,
    padding: 6,
    margin: 2,
    alignItems: 'center',
    justifyContent: 'space-between',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
    minWidth: 60,
    minHeight: 80,
    flex: 1,
    borderWidth: 1,
    borderColor: '#e2e8f0',
  },
  productInfoContainer: {
    alignItems: 'center',
    marginBottom: 10,
    width: '100%',
  },
  productName: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1e293b',
    marginBottom: 2,
    textAlign: 'center',
    lineHeight: 14,
    letterSpacing: 0.1,
  },
  productCategory: {
    fontSize: 12,
    color: '#64748b', // Modern gri renk
    marginBottom: 8,
    textAlign: 'center',
    fontWeight: '500', // Orta kalınlık
    textTransform: 'uppercase', // Büyük harf
    letterSpacing: 0.5, // Harf aralığı
  },
  productFooterContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    width: '100%',
    paddingHorizontal: 4,
  },
  productPrice: {
    fontSize: 17, // Biraz daha büyük
    fontWeight: '800', // Çok kalın
    color: '#059669', // Modern yeşil renk
    letterSpacing: 0.3, // Harf aralığı
  },
  addButton: {
    backgroundColor: '#059669', // Modern yeşil
    borderRadius: 20, // Daha yumuşak köşeler
    padding: 10, // Biraz daha fazla padding
    marginLeft: 8,
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: '#059669',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.3, // Gölge efekti
    shadowRadius: 4,
    elevation: 3,
    // Hover efekti için
    transform: [{ scale: 1 }],
  },
});

export default ProductList; 