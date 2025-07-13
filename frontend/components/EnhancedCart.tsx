import React, { useState, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  TextInput,
  Alert,
  Animated,
  Dimensions,
  Vibration,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { CartItem } from '../types/cart';

interface EnhancedCartProps {
  items: CartItem[];
  onUpdateQuantity: (productId: string, quantity: number) => void;
  onRemoveItem: (productId: string) => void;
  onUpdateNotes: (productId: string, notes: string) => void;
  onApplyDiscount: (productId: string, discount: number) => void;
  onClearCart: () => void;
  onSaveCart: () => void;
  onLoadCart: () => void;
  onBulkAction?: (action: 'increase' | 'decrease' | 'remove', productIds: string[]) => void;
}

const { width: screenWidth } = Dimensions.get('window');

const EnhancedCart: React.FC<EnhancedCartProps> = ({
  items,
  onUpdateQuantity,
  onRemoveItem,
  onUpdateNotes,
  onApplyDiscount,
  onClearCart,
  onSaveCart,
  onLoadCart,
  onBulkAction,
}) => {
  const { t } = useTranslation();
  const [expandedItem, setExpandedItem] = useState<string | null>(null);
  const [showDiscountInput, setShowDiscountInput] = useState<string | null>(null);
  const [discountValue, setDiscountValue] = useState('');
  const [selectedItems, setSelectedItems] = useState<Set<string>>(new Set());
  const [isSelectionMode, setIsSelectionMode] = useState(false);
  const [quickQuantity, setQuickQuantity] = useState<string>('');

  // Animasyon değerleri
  const slideAnimation = useRef(new Animated.Value(0)).current;
  const scaleAnimation = useRef(new Animated.Value(1)).current;
  const itemAnimations = useRef<{ [key: string]: Animated.Value }>({}).current;

  // Hızlı miktar değiştirme
  const quickQuantityOptions = ['1', '2', '3', '5', '10'];

  const calculateSubtotal = () => {
    return items.reduce((sum, item) => {
      const itemTotal = item.product.price * item.quantity;
      const discount = item.discount || 0;
      return sum + (itemTotal - discount);
    }, 0);
  };

  const calculateTax = () => {
    return items.reduce((sum, item) => {
      const itemTotal = item.product.price * item.quantity;
      const discount = item.discount || 0;
      const taxableAmount = itemTotal - discount;
      
      let taxRate = 0.20; // default standard
      switch (item.product.taxType) {
        case 'reduced': taxRate = 0.10; break;
        case 'special': taxRate = 0.13; break;
      }
      
      return sum + (taxableAmount * taxRate);
    }, 0);
  };

  const calculateTotal = () => {
    return calculateSubtotal() + calculateTax();
  };

  const getTaxDetails = () => {
    const taxGroups: { [key: string]: number } = {};
    
    items.forEach(item => {
      const itemTotal = item.product.price * item.quantity;
      const discount = item.discount || 0;
      const taxableAmount = itemTotal - discount;
      
      let taxRate = 0.20;
      let taxType = 'standard';
      
      switch (item.product.taxType) {
        case 'reduced': 
          taxRate = 0.10; 
          taxType = 'reduced';
          break;
        case 'special': 
          taxRate = 0.13; 
          taxType = 'special';
          break;
      }
      
      if (!taxGroups[taxType]) {
        taxGroups[taxType] = 0;
      }
      taxGroups[taxType] += taxableAmount * taxRate;
    });
    
    return Object.entries(taxGroups).map(([type, amount]) => ({
      type,
      amount,
      rate: type === 'reduced' ? 0.10 : type === 'special' ? 0.13 : 0.20
    }));
  };

  const handleDiscountApply = (productId: string) => {
    const discount = parseFloat(discountValue);
    if (isNaN(discount) || discount < 0) {
      Alert.alert(t('cart.invalidDiscount'), t('cart.enterValidAmount'));
      return;
    }
    
    const item = items.find(i => i.product.id === productId);
    if (item && discount > item.product.price * item.quantity) {
      Alert.alert(t('cart.discountTooHigh'), t('cart.discountExceedsTotal'));
      return;
    }
    
    onApplyDiscount(productId, discount);
    setShowDiscountInput(null);
    setDiscountValue('');
    
    // Başarı animasyonu
    Animated.sequence([
      Animated.timing(scaleAnimation, {
        toValue: 1.1,
        duration: 150,
        useNativeDriver: true,
      }),
      Animated.timing(scaleAnimation, {
        toValue: 1,
        duration: 150,
        useNativeDriver: true,
      }),
    ]).start();
  };

  const handleQuickQuantityChange = (productId: string, newQuantity: string) => {
    const quantity = parseInt(newQuantity);
    if (!isNaN(quantity) && quantity > 0) {
      onUpdateQuantity(productId, quantity);
      Vibration.vibrate(50); // Haptic feedback
    }
  };

  const toggleItemSelection = (productId: string) => {
    const newSelection = new Set(selectedItems);
    if (newSelection.has(productId)) {
      newSelection.delete(productId);
    } else {
      newSelection.add(productId);
    }
    setSelectedItems(newSelection);
    
    if (newSelection.size > 0 && !isSelectionMode) {
      setIsSelectionMode(true);
    } else if (newSelection.size === 0) {
      setIsSelectionMode(false);
    }
  };

  const handleBulkAction = (action: 'increase' | 'decrease' | 'remove') => {
    if (selectedItems.size === 0) return;

    const selectedIds = Array.from(selectedItems);
    
    switch (action) {
      case 'increase':
        selectedIds.forEach(id => {
          const item = items.find(i => i.product.id === id);
          if (item) {
            onUpdateQuantity(id, item.quantity + 1);
          }
        });
        break;
      case 'decrease':
        selectedIds.forEach(id => {
          const item = items.find(i => i.product.id === id);
          if (item && item.quantity > 1) {
            onUpdateQuantity(id, item.quantity - 1);
          }
        });
        break;
      case 'remove':
        Alert.alert(
          t('cart.bulkRemoveTitle'),
          t('cart.bulkRemoveMessage', { count: selectedItems.size }),
          [
            { text: t('common.cancel'), style: 'cancel' },
            {
              text: t('cart.remove'),
              style: 'destructive',
              onPress: () => {
                selectedIds.forEach(id => onRemoveItem(id));
                setSelectedItems(new Set());
                setIsSelectionMode(false);
              }
            }
          ]
        );
        break;
    }
  };

  const clearSelection = () => {
    setSelectedItems(new Set());
    setIsSelectionMode(false);
  };

  const renderCartItem = (item: CartItem, index: number) => {
    const isExpanded = expandedItem === item.product.id;
    const isDiscountInputVisible = showDiscountInput === item.product.id;
    const isSelected = selectedItems.has(item.product.id);
    
    // Animasyon değerini sadece bir kez oluştur
    if (!itemAnimations[item.product.id]) {
      itemAnimations[item.product.id] = new Animated.Value(1);
    }

    const itemAnimation = itemAnimations[item.product.id];

    return (
      <Animated.View 
        key={item.product.id} 
        style={[
          styles.cartItem,
          isSelected && styles.cartItemSelected,
          {
            transform: [{ scale: itemAnimation }],
            opacity: itemAnimation,
          }
        ]}
        // Animasyon performansını artır
        shouldRasterizeIOS={true}
        renderToHardwareTextureAndroid={true}
      >
        {/* Seçim Modu Checkbox */}
        {isSelectionMode && (
          <TouchableOpacity
            style={[styles.checkbox, isSelected && styles.checkboxSelected]}
            onPress={() => toggleItemSelection(item.product.id)}
          >
            {isSelected && (
              <Ionicons name="checkmark" size={16} color="white" />
            )}
          </TouchableOpacity>
        )}

        <TouchableOpacity
          style={styles.cartItemHeader}
          onPress={() => setExpandedItem(isExpanded ? null : item.product.id)}
          activeOpacity={0.7}
        >
          <View style={styles.productInfo}>
            <Text style={styles.productName}>{item.product.name}</Text>
            <Text style={styles.productPrice}>
              €{(item.product.price * item.quantity).toFixed(2)}
            </Text>
            {item.discount && item.discount > 0 && (
              <Text style={styles.discountText}>
                -€{item.discount.toFixed(2)} {t('cart.discount')}
              </Text>
            )}
          </View>
          
          <View style={styles.quantityControls}>
            {/* Hızlı Miktar Değiştirme */}
            <View style={styles.quickQuantityContainer}>
              <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                {quickQuantityOptions.map((qty) => (
                  <TouchableOpacity
                    key={qty}
                    style={[
                      styles.quickQuantityButton,
                      item.quantity === parseInt(qty) && styles.quickQuantityButtonActive
                    ]}
                    onPress={() => handleQuickQuantityChange(item.product.id, qty)}
                  >
                    <Text style={[
                      styles.quickQuantityText,
                      item.quantity === parseInt(qty) && styles.quickQuantityTextActive
                    ]}>
                      {qty}
                    </Text>
                  </TouchableOpacity>
                ))}
              </ScrollView>
            </View>

            <View style={styles.quantityRow}>
              <TouchableOpacity
                style={styles.quantityButton}
                onPress={() => onUpdateQuantity(item.product.id, item.quantity - 1)}
              >
                <Ionicons name="remove" size={16} color={Colors.light.text} />
              </TouchableOpacity>
              <Text style={styles.quantityText}>{item.quantity}</Text>
              <TouchableOpacity
                style={styles.quantityButton}
                onPress={() => onUpdateQuantity(item.product.id, item.quantity + 1)}
              >
                <Ionicons name="add" size={16} color={Colors.light.text} />
              </TouchableOpacity>
            </View>
            
            <TouchableOpacity
              style={styles.expandButton}
              onPress={() => setExpandedItem(isExpanded ? null : item.product.id)}
            >
              <Ionicons 
                name={isExpanded ? "chevron-up" : "chevron-down"} 
                size={20} 
                color={Colors.light.textSecondary} 
              />
            </TouchableOpacity>
          </View>
        </TouchableOpacity>

        {/* Genişletilmiş Detaylar */}
        {isExpanded && (
          <Animated.View 
            style={[
              styles.expandedDetails,
              {
                transform: [{
                  scale: slideAnimation.interpolate({
                    inputRange: [0, 1],
                    outputRange: [0.8, 1],
                  })
                }],
                opacity: slideAnimation,
              }
            ]}
          >
            {/* Notlar */}
            <View style={styles.detailSection}>
              <Text style={styles.detailLabel}>{t('cart.notes')}:</Text>
              <TextInput
                style={styles.notesInput}
                placeholder={t('cart.addNotes')}
                value={item.notes || ''}
                onChangeText={(text) => onUpdateNotes(item.product.id, text)}
                multiline
                maxLength={200}
              />
            </View>

            {/* İndirim */}
            <View style={styles.detailSection}>
              <View style={styles.discountRow}>
                <Text style={styles.detailLabel}>{t('cart.discount')}:</Text>
                <TouchableOpacity
                  style={styles.discountButton}
                  onPress={() => setShowDiscountInput(isDiscountInputVisible ? null : item.product.id)}
                >
                  <Ionicons name="pricetag-outline" size={16} color={Colors.light.primary} />
                  <Text style={styles.discountButtonText}>
                    {item.discount ? `€${item.discount.toFixed(2)}` : t('cart.addDiscount')}
                  </Text>
                </TouchableOpacity>
              </View>
              
              {isDiscountInputVisible && (
                <View style={styles.discountInputContainer}>
                  <TextInput
                    style={styles.discountInput}
                    placeholder="0.00"
                    value={discountValue}
                    onChangeText={setDiscountValue}
                    keyboardType="decimal-pad"
                  />
                  <TouchableOpacity
                    style={styles.applyDiscountButton}
                    onPress={() => handleDiscountApply(item.product.id)}
                  >
                    <Text style={styles.applyDiscountText}>{t('cart.apply')}</Text>
                  </TouchableOpacity>
                </View>
              )}
            </View>

            {/* Ürün Detayları */}
            <View style={styles.productDetails}>
              <Text style={styles.detailText}>
                {t('cart.unitPrice')}: €{item.product.price.toFixed(2)}
              </Text>
              <Text style={styles.detailText}>
                {t('cart.tax')}: {t(`tax.${item.product.taxType}`)} ({Math.round(getTaxRate(item.product.taxType) * 100)}%)
              </Text>
              <Text style={styles.detailText}>
                {t('cart.stock')}: {item.product.stock}
              </Text>
            </View>

            {/* Kaldır Butonu */}
            <TouchableOpacity
              style={styles.removeButton}
              onPress={() => {
                Alert.alert(
                  t('cart.removeItem'),
                  t('cart.removeItemConfirm'),
                  [
                    { text: t('common.cancel'), style: 'cancel' },
                    { 
                      text: t('cart.remove'), 
                      style: 'destructive', 
                      onPress: () => {
                        // Kaldırma animasyonu
                        Animated.timing(itemAnimation, {
                          toValue: 0,
                          duration: 300,
                          useNativeDriver: true,
                        }).start(() => {
                          onRemoveItem(item.product.id);
                        });
                      }
                    }
                  ]
                );
              }}
            >
              <Ionicons name="trash-outline" size={16} color={Colors.light.error} />
              <Text style={styles.removeButtonText}>{t('cart.removeItem')}</Text>
            </TouchableOpacity>
          </Animated.View>
        )}
      </Animated.View>
    );
  };

  const getTaxRate = (taxType: string) => {
    switch (taxType) {
      case 'reduced': return 0.10;
      case 'special': return 0.13;
      default: return 0.20;
    }
  };

  // Animasyonları başlat
  React.useEffect(() => {
    Animated.timing(slideAnimation, {
      toValue: 1,
      duration: 300,
      useNativeDriver: true,
    }).start();
  }, [expandedItem]);

  if (items.length === 0) {
    return (
      <View style={styles.emptyCart}>
        <Animated.View style={{ transform: [{ scale: scaleAnimation }] }}>
          <Ionicons name="cart-outline" size={64} color={Colors.light.textSecondary} />
        </Animated.View>
        <Text style={styles.emptyCartTitle}>{t('cart.emptyCart')}</Text>
        <Text style={styles.emptyCartSubtitle}>{t('cart.addItemsToStart')}</Text>
        
        <View style={styles.emptyCartActions}>
          <TouchableOpacity style={styles.loadCartButton} onPress={onLoadCart}>
            <Ionicons name="folder-open-outline" size={20} color={Colors.light.primary} />
            <Text style={styles.loadCartText}>{t('cart.loadSavedCart')}</Text>
          </TouchableOpacity>
        </View>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Seçim Modu Toolbar */}
      {isSelectionMode && (
        <Animated.View 
          style={[
            styles.selectionToolbar,
            {
              transform: [{
                translateY: slideAnimation.interpolate({
                  inputRange: [0, 1],
                  outputRange: [-50, 0],
                })
              }]
            }
          ]}
        >
          <View style={styles.selectionInfo}>
            <Text style={styles.selectionText}>
              {selectedItems.size} {t('cart.itemsSelected')}
            </Text>
          </View>
          
          <View style={styles.selectionActions}>
            <TouchableOpacity
              style={styles.bulkActionButton}
              onPress={() => handleBulkAction('increase')}
            >
              <Ionicons name="add" size={16} color={Colors.light.primary} />
            </TouchableOpacity>
            
            <TouchableOpacity
              style={styles.bulkActionButton}
              onPress={() => handleBulkAction('decrease')}
            >
              <Ionicons name="remove" size={16} color={Colors.light.warning} />
            </TouchableOpacity>
            
            <TouchableOpacity
              style={styles.bulkActionButton}
              onPress={() => handleBulkAction('remove')}
            >
              <Ionicons name="trash-outline" size={16} color={Colors.light.error} />
            </TouchableOpacity>
            
            <TouchableOpacity
              style={styles.bulkActionButton}
              onPress={clearSelection}
            >
              <Ionicons name="close" size={16} color={Colors.light.textSecondary} />
            </TouchableOpacity>
          </View>
        </Animated.View>
      )}

      {/* Sepet Başlığı - Kompakt */}
      <View style={styles.cartHeader}>
        <View style={styles.cartTitleContainer}>
          <Ionicons name="cart" size={20} color={Colors.light.primary} />
          <Text style={styles.cartTitle}>{t('cart.cart')}</Text>
          <Text style={styles.itemCount}>({items.length})</Text>
        </View>
      </View>

      {/* Sepet Öğeleri - Genişletilmiş Scroll Alanı */}
      <ScrollView 
        style={styles.cartItems} 
        showsVerticalScrollIndicator={true}
        contentContainerStyle={styles.cartItemsContent}
      >
        {items.map((item, index) => renderCartItem(item, index))}
      </ScrollView>

      {/* Sepet Özeti - Kompakt */}
      <View style={styles.cartSummary}>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>Zwischensumme:</Text>
          <Text style={styles.summaryValue}>€{calculateSubtotal().toFixed(2)}</Text>
        </View>
        
        {/* Toplam İndirim */}
        {items.some(item => item.discount && item.discount > 0) && (
          <View style={styles.summaryRow}>
            <Text style={styles.summaryLabel}>Rabatt:</Text>
            <Text style={styles.summaryDiscount}>
              -€{items.reduce((sum, item) => sum + (item.discount || 0), 0).toFixed(2)}
            </Text>
          </View>
        )}
        
        {getTaxDetails().map((taxDetail, index) => (
          <View key={index} style={styles.summaryRow}>
            <Text style={styles.summaryLabel}>
              MwSt. ({Math.round(taxDetail.rate * 100)}%):
            </Text>
            <Text style={styles.summaryValue}>€{taxDetail.amount.toFixed(2)}</Text>
          </View>
        ))}
        
        <View style={styles.summaryDivider} />
        
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>Gesamtsumme:</Text>
          <Text style={styles.summaryTotal}>€{calculateTotal().toFixed(2)}</Text>
        </View>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.surface,
  },
  selectionToolbar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: Colors.light.primary + '20',
    padding: Spacing.xs,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  selectionInfo: {
    flex: 1,
  },
  selectionText: {
    ...Typography.caption,
    color: Colors.light.primary,
    fontWeight: '600',
  },
  selectionActions: {
    flexDirection: 'row',
    gap: Spacing.xs,
  },
  bulkActionButton: {
    padding: Spacing.xs,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.sm,
  },
  cartHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.xs,
    backgroundColor: Colors.light.background,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  cartTitleContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.xs,
  },
  cartTitle: {
    ...Typography.caption,
    color: Colors.light.text,
    fontWeight: '600',
  },
  itemCount: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    backgroundColor: Colors.light.primary + '20',
    paddingHorizontal: Spacing.xs,
    paddingVertical: 1,
    borderRadius: BorderRadius.sm,
    fontSize: 10,
  },
  cartItems: {
    flex: 1,
  },
  cartItemsContent: {
    paddingBottom: Spacing.lg,
  },
  cartItem: {
    backgroundColor: Colors.light.background,
    marginHorizontal: Spacing.xs,
    marginVertical: Spacing.xs,
    borderRadius: BorderRadius.sm,
    padding: Spacing.xs,
    borderWidth: 1,
    borderColor: Colors.light.border,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 1,
    },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
  },
  cartItemSelected: {
    borderColor: Colors.light.primary,
    backgroundColor: Colors.light.primary + '10',
  },
  checkbox: {
    width: 24,
    height: 24,
    borderRadius: BorderRadius.sm,
    borderWidth: 2,
    borderColor: Colors.light.border,
    justifyContent: 'center',
    alignItems: 'center',
    marginLeft: Spacing.sm,
  },
  checkboxSelected: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  cartItemHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: Spacing.xs,
  },
  productInfo: {
    flex: 1,
    marginRight: Spacing.xs,
  },
  productName: {
    ...Typography.caption,
    color: Colors.light.text,
    fontWeight: '600',
    marginBottom: Spacing.xs,
    fontSize: 12,
  },
  productPrice: {
    ...Typography.caption,
    color: Colors.light.primary,
    fontWeight: '600',
    fontSize: 11,
  },
  discountText: {
    ...Typography.caption,
    color: Colors.light.success,
    fontWeight: '600',
    fontSize: 10,
  },
  quantityControls: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: Spacing.xs,
  },
  quickQuantityContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.xs,
    marginBottom: Spacing.xs,
  },
  quickQuantityButton: {
    paddingHorizontal: Spacing.xs,
    paddingVertical: Spacing.xs,
    backgroundColor: Colors.light.primary + '20',
    borderRadius: BorderRadius.sm,
    borderWidth: 1,
    borderColor: Colors.light.primary,
  },
  quickQuantityButtonActive: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  quickQuantityText: {
    ...Typography.caption,
    color: Colors.light.primary,
    fontWeight: '600',
    fontSize: 10,
  },
  quickQuantityTextActive: {
    color: 'white',
  },
  quantityRow: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
  },
  quantityButton: {
    width: 24,
    height: 24,
    borderRadius: BorderRadius.sm,
    backgroundColor: Colors.light.background,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  quantityText: {
    ...Typography.caption,
    color: Colors.light.text,
    marginHorizontal: Spacing.sm,
    minWidth: 16,
    textAlign: 'center',
    fontSize: 11,
  },
  expandButton: {
    padding: Spacing.xs,
  },
  expandedDetails: {
    padding: Spacing.xs,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
    marginTop: Spacing.xs,
  },
  detailSection: {
    marginBottom: Spacing.xs,
  },
  detailLabel: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.xs,
    fontSize: 10,
  },
  notesInput: {
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.sm,
    padding: Spacing.xs,
    ...Typography.caption,
    color: Colors.light.text,
    minHeight: 40,
    fontSize: 11,
  },
  discountRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  discountButton: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.xs,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.sm,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  discountButtonText: {
    ...Typography.caption,
    color: Colors.light.primary,
    marginLeft: Spacing.xs,
    fontSize: 10,
  },
  discountInputContainer: {
    flexDirection: 'row',
    marginTop: Spacing.xs,
  },
  discountInput: {
    flex: 1,
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.sm,
    padding: Spacing.xs,
    marginRight: Spacing.xs,
    ...Typography.caption,
    color: Colors.light.text,
    fontSize: 11,
  },
  applyDiscountButton: {
    backgroundColor: Colors.light.primary,
    paddingHorizontal: Spacing.sm,
    paddingVertical: Spacing.xs,
    borderRadius: BorderRadius.sm,
    justifyContent: 'center',
  },
  applyDiscountText: {
    ...Typography.caption,
    color: 'white',
    fontWeight: '600',
    fontSize: 10,
  },
  productDetails: {
    marginBottom: Spacing.xs,
  },
  detailText: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.xs,
    fontSize: 10,
  },
  removeButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    padding: Spacing.xs,
    backgroundColor: Colors.light.error + '10',
    borderRadius: BorderRadius.sm,
  },
  removeButtonText: {
    ...Typography.caption,
    color: Colors.light.error,
    marginLeft: Spacing.xs,
    fontSize: 10,
  },
  cartSummary: {
    padding: Spacing.sm,
    backgroundColor: Colors.light.background,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  summaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: Spacing.xs,
  },
  summaryLabel: {
    ...Typography.caption,
    color: Colors.light.text,
    fontSize: 11,
  },
  summaryValue: {
    ...Typography.caption,
    color: Colors.light.text,
    fontSize: 11,
  },
  summaryTotal: {
    ...Typography.caption,
    color: Colors.light.text,
    fontWeight: '600',
    fontSize: 12,
  },
  summaryDiscount: {
    ...Typography.caption,
    color: Colors.light.success,
    fontWeight: '600',
    fontSize: 11,
  },
  summaryDivider: {
    height: 1,
    backgroundColor: Colors.light.border,
    marginVertical: Spacing.xs,
  },
  emptyCart: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: Spacing.lg,
  },
  emptyCartTitle: {
    ...Typography.h3,
    color: Colors.light.text,
    marginTop: Spacing.md,
    fontSize: 16,
  },
  emptyCartSubtitle: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    textAlign: 'center',
    marginTop: Spacing.sm,
    fontSize: 12,
  },
  emptyCartActions: {
    marginTop: Spacing.lg,
  },
  loadCartButton: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.sm,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  loadCartText: {
    ...Typography.caption,
    color: Colors.light.primary,
    marginLeft: Spacing.sm,
    fontSize: 12,
  },
});

export default EnhancedCart; 