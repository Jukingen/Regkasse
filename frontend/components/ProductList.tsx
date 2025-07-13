import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  FlatList,
  Dimensions,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
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

  // Container genişliğini hesapla (sol panel genişliği)
  const containerWidth = screenWidth * 0.5; // Sol panel genişliği (yaklaşık %50)
  
  // Ekran boyutuna göre sütun sayısını hesapla
  let numColumns = 1; // Varsayılan olarak tek sütun
  
  if (screenWidth >= 768) {
    // Tablet ve büyük ekranlar için 2+ sütun
    numColumns = Math.max(2, Math.floor((containerWidth - 16) / 130));
  } else if (screenWidth >= 480) {
    // Orta boyutlu ekranlar için 2 sütun
    numColumns = 2;
  } else {
    // Küçük ekranlar için tek sütun
    numColumns = 1;
  }
  
  // Ürün kartı genişliğini hesapla
  const cardWidth = numColumns === 1 
    ? containerWidth - 16 // Tek sütun için tam genişlik
    : Math.max(120, (containerWidth - 16 - (numColumns * 8)) / numColumns);

  const renderProductItem = ({ item }: { item: Product }) => (
    <TouchableOpacity
      style={[
        styles.productItem, 
        { width: cardWidth },
        numColumns === 1 && styles.productItemSingle // Tek sütun için özel stil
      ]}
      onPress={() => onAddToCart(item)}
    >
      <View style={[
        styles.productHeader,
        numColumns === 1 && styles.productHeaderSingle
      ]}>
        <TouchableOpacity
          style={styles.favoriteButton}
          onPress={(e) => {
            e.stopPropagation();
            onToggleFavorite(item);
          }}
        >
          <Ionicons 
            name={userFavorites.some(fav => fav.id === item.id) ? "heart" : "heart-outline"} 
            size={16} 
            color={userFavorites.some(fav => fav.id === item.id) ? Colors.light.primary : Colors.light.textSecondary} 
          />
        </TouchableOpacity>
      </View>
      
      <View style={[
        styles.productInfo,
        numColumns === 1 && styles.productInfoSingle
      ]}>
        <Text style={[
          styles.productName, 
          numColumns === 1 && styles.productNameSingle
        ]} numberOfLines={2}>
          {item.name}
        </Text>
        <Text style={[
          styles.productCategory,
          numColumns === 1 && styles.productCategorySingle
        ]} numberOfLines={1}>
          {item.category}
        </Text>
      </View>
      
      <View style={[
        styles.productFooter,
        numColumns === 1 && styles.productFooterSingle
      ]}>
        <Text style={[
          styles.priceText,
          numColumns === 1 && styles.priceTextSingle
        ]}>€{item.price.toFixed(2)}</Text>
        <View style={styles.addButton}>
          <Ionicons name="add" size={16} color="white" />
        </View>
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
      />
    </View>
  );
};

const styles = StyleSheet.create({
  productsSection: {
    flex: 1,
    width: '100%',
  },
  sectionTitleSmall: {
    ...Typography.caption,
    color: Colors.light.text,
    marginBottom: Spacing.sm,
    paddingHorizontal: Spacing.sm,
    fontWeight: '600',
    fontSize: 12,
  },
  flatListContainer: {
    width: '100%',
  },
  productList: {
    paddingHorizontal: Spacing.xs,
    width: '100%',
  },
  productItem: {
    backgroundColor: Colors.light.background,
    margin: Spacing.xs,
    borderRadius: BorderRadius.sm,
    borderWidth: 1,
    borderColor: Colors.light.border,
    padding: Spacing.xs,
    minWidth: 120,
    maxWidth: 160,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 1,
    },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
  },
  productHeader: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    marginBottom: Spacing.xs,
  },
  favoriteButton: {
    padding: Spacing.xs,
    borderRadius: BorderRadius.sm,
  },
  productInfo: {
    flex: 1,
    marginBottom: Spacing.xs,
  },
  productName: {
    ...Typography.caption,
    color: Colors.light.text,
    fontWeight: '600',
    fontSize: 11,
    marginBottom: Spacing.xs,
    textAlign: 'center',
  },
  productCategory: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    fontSize: 10,
    textAlign: 'center',
  },
  productFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  priceText: {
    ...Typography.caption,
    color: Colors.light.primary,
    fontWeight: '600',
    fontSize: 11,
  },
  addButton: {
    backgroundColor: Colors.light.primary,
    width: 24,
    height: 24,
    borderRadius: BorderRadius.sm,
    justifyContent: 'center',
    alignItems: 'center',
  },
  // Tek sütun görünümü için özel stiller
  productItemSingle: {
    width: '100%',
    maxWidth: '100%',
    minWidth: '100%',
    padding: Spacing.sm,
  },
  productHeaderSingle: {
    marginBottom: Spacing.xs,
  },
  productInfoSingle: {
    marginBottom: Spacing.xs,
  },
  productNameSingle: {
    fontSize: 12,
    marginBottom: Spacing.xs,
    textAlign: 'left',
  },
  productCategorySingle: {
    fontSize: 10,
    textAlign: 'left',
  },
  productFooterSingle: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  priceTextSingle: {
    fontSize: 12,
  },
});

export default ProductList; 