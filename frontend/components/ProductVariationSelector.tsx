// Türkçe Açıklama: Ürün varyasyonları (boyut, porsiyon) seçimi için görsel komponent. Fiyat değişikliklerini otomatik hesaplar ve gösterir.

import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, ScrollView } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';

export interface ProductVariation {
  id: string;
  name: string;
  description?: string;
  priceModifier: number;
  priceMultiplier: number;
  isDefault: boolean;
  sortOrder: number;
  isActive: boolean;
  icon?: string;
  color?: string;
}

interface ProductVariationSelectorProps {
  variations: ProductVariation[];
  selectedVariationId?: string;
  basePrice: number;
  onVariationChange: (variation: ProductVariation) => void;
  disabled?: boolean;
}

const ProductVariationSelector: React.FC<ProductVariationSelectorProps> = ({
  variations,
  selectedVariationId,
  basePrice,
  onVariationChange,
  disabled = false
}) => {
  const { t } = useTranslation();

  // Aktif varyasyonları sırala
  const activeVariations = variations
    .filter(v => v.isActive)
    .sort((a, b) => a.sortOrder - b.sortOrder);

  // Varsayılan varyasyonu bul
  const defaultVariation = activeVariations.find(v => v.isDefault) || activeVariations[0];
  
  // Seçili varyasyonu belirle
  const selectedVariation = selectedVariationId 
    ? activeVariations.find(v => v.id === selectedVariationId) 
    : defaultVariation;

  // Fiyat hesaplama
  const calculatePrice = (variation: ProductVariation) => {
    const modifiedPrice = basePrice * variation.priceMultiplier + variation.priceModifier;
    return Math.max(0, modifiedPrice);
  };

  const handleVariationSelect = (variation: ProductVariation) => {
    if (!disabled) {
      onVariationChange(variation);
    }
  };

  if (activeVariations.length === 0) {
    return null;
  }

  return (
    <View style={styles.container}>
      <Text style={styles.title}>{t('product.variations', 'Seçenekler')}</Text>
      <ScrollView 
        horizontal 
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.scrollContainer}
      >
        {activeVariations.map((variation) => {
          const isSelected = selectedVariation?.id === variation.id;
          const variationPrice = calculatePrice(variation);
          const priceDifference = variationPrice - basePrice;
          
          return (
            <TouchableOpacity
              key={variation.id}
              style={[
                styles.variationButton,
                {
                  backgroundColor: isSelected ? (variation.color || '#1976d2') : '#f5f5f5',
                  borderColor: variation.color || '#1976d2',
                }
              ]}
              onPress={() => handleVariationSelect(variation)}
              disabled={disabled}
              activeOpacity={0.7}
            >
              {variation.icon && (
                <Ionicons
                  name={variation.icon as any}
                  size={20}
                  color={isSelected ? '#fff' : (variation.color || '#1976d2')}
                  style={styles.icon}
                />
              )}
              <Text style={[
                styles.variationName,
                { color: isSelected ? '#fff' : '#333' }
              ]}>
                {variation.name}
              </Text>
              <Text style={[
                styles.variationPrice,
                { color: isSelected ? '#fff' : '#666' }
              ]}>
                €{variationPrice.toFixed(2)}
              </Text>
              {priceDifference !== 0 && (
                <Text style={[
                  styles.priceDifference,
                  { color: isSelected ? '#fff' : (priceDifference > 0 ? '#d32f2f' : '#388e3c') }
                ]}>
                  {priceDifference > 0 ? '+' : ''}{priceDifference.toFixed(2)} €
                </Text>
              )}
            </TouchableOpacity>
          );
        })}
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    marginVertical: 8,
  },
  title: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 8,
    color: '#333',
  },
  scrollContainer: {
    paddingHorizontal: 4,
    gap: 8,
  },
  variationButton: {
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderRadius: 8,
    borderWidth: 2,
    alignItems: 'center',
    minWidth: 80,
    minHeight: 60,
  },
  icon: {
    marginBottom: 4,
  },
  variationName: {
    fontSize: 14,
    fontWeight: '600',
    textAlign: 'center',
    marginBottom: 2,
  },
  variationPrice: {
    fontSize: 12,
    fontWeight: '500',
    textAlign: 'center',
  },
  priceDifference: {
    fontSize: 10,
    fontWeight: '500',
    textAlign: 'center',
    marginTop: 2,
  },
});

export default ProductVariationSelector; 