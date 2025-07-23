// Türkçe Açıklama: Ürün detay modalı - varyasyon ve seçenek seçimi ile birlikte sepete ekleme özelliği

import React, { useState, useMemo } from 'react';
import { View, Text, Modal, StyleSheet, TouchableOpacity, ScrollView, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';

import { Product, ProductVariation, ProductOption } from '../services/api/productService';
import ProductVariationSelector from './ProductVariationSelector';
import ProductOptionSelector from './ProductOptionSelector';

interface ProductDetailModalProps {
  visible: boolean;
  product: Product | null;
  onClose: () => void;
  onAddToCart: (product: Product, quantity: number, variation?: ProductVariation, options?: Record<string, any>) => void;
}

const ProductDetailModal: React.FC<ProductDetailModalProps> = ({
  visible,
  product,
  onClose,
  onAddToCart
}) => {
  const { t } = useTranslation();
  const [quantity, setQuantity] = useState(1);
  const [selectedVariation, setSelectedVariation] = useState<ProductVariation | null>(null);
  const [selectedOptions, setSelectedOptions] = useState<Record<string, any>>({});

  // Fiyat hesaplama
  const calculateTotalPrice = useMemo(() => {
    if (!product) return 0;
    
    let basePrice = product.price;
    
    // Varyasyon fiyatını ekle
    if (selectedVariation) {
      basePrice = basePrice * selectedVariation.priceMultiplier + selectedVariation.priceModifier;
    }
    
    // Seçenek fiyatlarını ekle
    let optionPrice = 0;
    if (product.options) {
      product.options.forEach(option => {
        const selectedValue = selectedOptions[option.id];
        if (selectedValue) {
          if (Array.isArray(selectedValue)) {
            // Çoklu seçim
            selectedValue.forEach(valueId => {
              const optionValue = option.optionValues.find(v => v.id === valueId);
              if (optionValue) {
                optionPrice += optionValue.priceModifier;
              }
            });
          } else if (typeof selectedValue === 'string') {
            // Tek seçim
            const optionValue = option.optionValues.find(v => v.id === selectedValue);
            if (optionValue) {
              optionPrice += optionValue.priceModifier;
            }
          }
        }
      });
    }
    
    return (basePrice + optionPrice) * quantity;
  }, [product, selectedVariation, selectedOptions, quantity]);

  const handleVariationChange = (variation: ProductVariation) => {
    setSelectedVariation(variation);
  };

  const handleOptionChange = (optionId: string, value: any) => {
    setSelectedOptions(prev => ({
      ...prev,
      [optionId]: value
    }));
  };

  const handleQuantityChange = (newQuantity: number) => {
    if (newQuantity > 0) {
      setQuantity(newQuantity);
    }
  };

  const handleAddToCart = () => {
    if (!product) return;
    
    // Zorunlu seçenekleri kontrol et
    if (product.options) {
      const requiredOptions = product.options.filter(opt => opt.isRequired);
      for (const option of requiredOptions) {
        const selectedValue = selectedOptions[option.id];
        if (!selectedValue || (Array.isArray(selectedValue) && selectedValue.length === 0)) {
          Alert.alert(
            t('product.requiredOption', 'Zorunlu Seçenek'),
            `${option.name} seçimi zorunludur.`
          );
          return;
        }
      }
    }

    onAddToCart(product, quantity, selectedVariation || undefined, selectedOptions);
    onClose();
    
    // State'i sıfırla
    setQuantity(1);
    setSelectedVariation(null);
    setSelectedOptions({});
  };

  const resetSelections = () => {
    setQuantity(1);
    setSelectedVariation(null);
    setSelectedOptions({});
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent
      onRequestClose={onClose}
    >
      <View style={styles.modalOverlay}>
        <View style={styles.modalContent}>
          {/* Header */}
          <View style={styles.header}>
            <Text style={styles.productName}>{product.name}</Text>
            <TouchableOpacity onPress={onClose} style={styles.closeButton}>
              <Ionicons name="close" size={24} color="#666" />
            </TouchableOpacity>
          </View>

          <ScrollView style={styles.body} showsVerticalScrollIndicator={false}>
            {/* Ürün Açıklaması */}
            {product.description && (
              <View style={styles.descriptionContainer}>
                <Text style={styles.description}>{product.description}</Text>
              </View>
            )}

            {/* Varyasyon Seçici */}
            {product.variations && product.variations.length > 0 && (
              <ProductVariationSelector
                variations={product.variations}
                selectedVariationId={selectedVariation?.id}
                basePrice={product.price}
                onVariationChange={handleVariationChange}
              />
            )}

            {/* Seçenek Seçici */}
            {product.options && product.options.length > 0 && (
              <View style={styles.optionsContainer}>
                <Text style={styles.sectionTitle}>{t('product.options', 'Zusatzoptionen')}</Text>
                <ProductOptionSelector
                  options={product.options}
                  selectedOptions={selectedOptions}
                  onOptionChange={handleOptionChange}
                />
              </View>
            )}

            {/* Miktar Seçici */}
            <View style={styles.quantityContainer}>
              <Text style={styles.sectionTitle}>{t('product.quantity', 'Menge')}</Text>
              <View style={styles.quantitySelector}>
                <TouchableOpacity
                  style={styles.quantityButton}
                  onPress={() => handleQuantityChange(quantity - 1)}
                  disabled={quantity <= 1}
                >
                  <Ionicons name="remove" size={20} color={quantity <= 1 ? "#ccc" : "#666"} />
                </TouchableOpacity>
                <Text style={styles.quantityText}>{quantity}</Text>
                <TouchableOpacity
                  style={styles.quantityButton}
                  onPress={() => handleQuantityChange(quantity + 1)}
                >
                  <Ionicons name="add" size={20} color="#666" />
                </TouchableOpacity>
              </View>
            </View>

            {/* Toplam Fiyat */}
            <View style={styles.totalContainer}>
              <Text style={styles.totalLabel}>{t('product.totalPrice', 'Gesamtpreis')}</Text>
              <Text style={styles.totalPrice}>€{calculateTotalPrice.toFixed(2)}</Text>
            </View>
          </ScrollView>

          {/* Footer */}
          <View style={styles.footer}>
            <TouchableOpacity style={styles.resetButton} onPress={resetSelections}>
              <Text style={styles.resetButtonText}>{t('common.reset', 'Zurücksetzen')}</Text>
            </TouchableOpacity>
            <TouchableOpacity style={styles.addToCartButton} onPress={handleAddToCart}>
              <Ionicons name="cart" size={20} color="#fff" style={styles.cartIcon} />
              <Text style={styles.addToCartButtonText}>
                {t('product.addToCart', 'Zum Warenkorb hinzufügen')}
              </Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.5)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    backgroundColor: '#fff',
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    maxHeight: '90%',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  productName: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    flex: 1,
  },
  closeButton: {
    padding: 4,
  },
  body: {
    padding: 16,
    maxHeight: 500,
  },
  descriptionContainer: {
    marginBottom: 16,
  },
  description: {
    fontSize: 14,
    color: '#666',
    lineHeight: 20,
  },
  optionsContainer: {
    marginBottom: 16,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  quantityContainer: {
    marginBottom: 16,
  },
  quantitySelector: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 16,
  },
  quantityButton: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: '#f5f5f5',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: '#ddd',
  },
  quantityText: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    minWidth: 30,
    textAlign: 'center',
  },
  totalContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 16,
    borderTopWidth: 1,
    borderTopColor: '#eee',
    marginTop: 8,
  },
  totalLabel: {
    fontSize: 16,
    fontWeight: '500',
    color: '#333',
  },
  totalPrice: {
    fontSize: 20,
    fontWeight: '700',
    color: '#1976d2',
  },
  footer: {
    flexDirection: 'row',
    padding: 16,
    borderTopWidth: 1,
    borderTopColor: '#eee',
    gap: 12,
  },
  resetButton: {
    flex: 1,
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 8,
    backgroundColor: '#f5f5f5',
    alignItems: 'center',
  },
  resetButtonText: {
    fontSize: 14,
    fontWeight: '500',
    color: '#666',
  },
  addToCartButton: {
    flex: 2,
    flexDirection: 'row',
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 8,
    backgroundColor: '#1976d2',
    alignItems: 'center',
    justifyContent: 'center',
  },
  cartIcon: {
    marginRight: 8,
  },
  addToCartButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#fff',
  },
});

export default ProductDetailModal; 