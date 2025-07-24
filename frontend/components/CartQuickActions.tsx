// Türkçe Açıklama: Bu component, sepet için hızlı aksiyonları ve özet bilgileri gösterir. Tüm hesaplamalar backend tarafından yapılır ve burada sadece gösterilir. CartItem ve Product tipleri backend ile uyumlu.
import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  Alert,
  Vibration,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { CartItem } from '../types/cart';

interface CartQuickActionsProps {
  cart: CartItem[];
  onClearCart: () => void;
  onSaveCart: () => void;
  onLoadCart: () => void;
  onApplyDiscount: (discountType: string, value: number) => void;
  onSplitBill: () => void;
  onHoldOrder: () => void;
  onPrintReceipt: () => void;
}

const CartQuickActions: React.FC<CartQuickActionsProps> = ({
  cart,
  onClearCart,
  onSaveCart,
  onLoadCart,
  onApplyDiscount,
  onSplitBill,
  onHoldOrder,
  onPrintReceipt,
}) => {
  const { t } = useTranslation();
  const [showDiscountOptions, setShowDiscountOptions] = useState(false);

  const handleClearCart = () => {
    if (cart?.items?.length === 0) return;
    
    Alert.alert(
      t('cart.clearCart', 'Sepeti Temizle'),
      t('cart.clearCartConfirm', 'Tüm ürünler sepetten kaldırılacak. Bu işlem geri alınamaz.'),
      [
        { text: t('common.cancel', 'İptal'), style: 'cancel' },
        {
          text: t('cart.clear', 'Temizle'),
          style: 'destructive',
          onPress: () => {
            onClearCart();
            Vibration.vibrate(50);
          }
        }
      ]
    );
  };

  const handleDiscount = (discountType: string) => {
    setShowDiscountOptions(false);
    
    Alert.prompt(
      t('cart.applyDiscount', 'Apply Discount'),
      t('cart.enterDiscountValue', `Enter ${discountType} value:`),
      [
        { text: t('common.cancel', 'Cancel'), style: 'cancel' },
        {
          text: t('cart.apply', 'Apply'),
          onPress: (value) => {
            const numValue = parseFloat(value || '0');
            if (!isNaN(numValue) && numValue > 0) {
              onApplyDiscount(discountType, numValue);
              Vibration.vibrate(50);
            }
          }
        }
      ],
      'plain-text'
    );
  };

  const handleSplitBill = () => {
    if (cart?.items?.length === 0) {
      Alert.alert(t('cart.emptyCart', 'Empty Cart'), t('cart.emptyCartMsg', 'Cart is empty. Add items first.'));
      return;
    }
    
    Alert.alert(
      t('cart.splitBill', 'Split Bill'),
      t('cart.splitBillHowMany', 'How many ways to split?'),
      [
        { text: t('common.cancel', 'Cancel'), style: 'cancel' },
        { text: t('cart.split2', '2 Ways'), onPress: () => onSplitBill() },
        { text: t('cart.split3', '3 Ways'), onPress: () => onSplitBill() },
        { text: t('cart.split4', '4 Ways'), onPress: () => onSplitBill() },
      ]
    );
  };

  const handleHoldOrder = () => {
    if (cart?.items?.length === 0) {
      Alert.alert(t('cart.emptyCart', 'Empty Cart'), t('cart.emptyCartMsg', 'Cart is empty. Add items first.'));
      return;
    }
    
    Alert.alert(
      t('cart.holdOrder', 'Hold Order'),
      t('cart.holdOrderMsg', 'This will save the current cart and clear it. Continue?'),
      [
        { text: t('common.cancel', 'Cancel'), style: 'cancel' },
        {
          text: t('cart.hold', 'Hold'),
          onPress: () => {
            onHoldOrder();
            Vibration.vibrate(50);
          }
        }
      ]
    );
  };

  const quickActions = [
    {
      id: 'clear',
      icon: 'trash-outline',
      label: t('cart.clear', 'Clear'),
      color: Colors.light.error,
      onPress: handleClearCart,
      disabled: cart?.items?.length === 0,
    },
    {
      id: 'save',
      icon: 'save-outline',
      label: t('cart.save', 'Save'),
      color: Colors.light.primary,
      onPress: onSaveCart,
      disabled: cart?.items?.length === 0,
    },
    {
      id: 'load',
      icon: 'folder-open-outline',
      label: t('cart.load', 'Load'),
      color: Colors.light.primary,
      onPress: onLoadCart,
    },
    {
      id: 'discount',
      icon: 'pricetag-outline',
      label: t('cart.discount', 'Discount'),
      color: Colors.light.warning,
      onPress: () => setShowDiscountOptions(true),
      disabled: cart?.items?.length === 0,
    },
    {
      id: 'split',
      icon: 'git-branch-outline',
      label: t('cart.split', 'Split'),
      color: Colors.light.info,
      onPress: handleSplitBill,
      disabled: cart?.items?.length === 0,
    },
    {
      id: 'hold',
      icon: 'pause-outline',
      label: t('cart.hold', 'Hold'),
      color: Colors.light.secondary,
      onPress: handleHoldOrder,
      disabled: cart?.items?.length === 0,
    },
    {
      id: 'print',
      icon: 'print-outline',
      label: t('cart.print', 'Print'),
      color: Colors.light.success,
      onPress: onPrintReceipt,
      disabled: cart?.items?.length === 0,
    },
  ];

  return (
    <View style={styles.container}>
      {/* Cart Summary */}
      <View style={styles.summaryContainer}>
        <View style={styles.summaryItem}>
          <Text style={styles.summaryLabel}>{t('cart.items', 'Items')}</Text>
          <Text style={styles.summaryValue}>{cart?.items?.length ?? 0}</Text>
        </View>
        <View style={styles.summaryItem}>
          <Text style={styles.summaryLabel}>{t('cart.total', 'Total')}</Text>
          <Text style={styles.summaryValue}>€{cart?.totalAmount.toFixed(2) ?? '0.00'}</Text>
        </View>
      </View>

      {/* Quick Actions */}
      <ScrollView 
        horizontal 
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.actionsContainer}
      >
        {quickActions.map(action => (
          <TouchableOpacity
            key={action.id}
            style={[
              styles.actionButton,
              { backgroundColor: action.color },
              action.disabled && styles.actionButtonDisabled
            ]}
            onPress={action.onPress}
            disabled={action.disabled}
          >
            <Ionicons 
              name={action.icon as any} 
              size={20} 
              color="white" 
            />
            <Text style={styles.actionLabel}>{action.label}</Text>
          </TouchableOpacity>
        ))}
      </ScrollView>

      {/* Discount Options Modal */}
      {showDiscountOptions && (
        <View style={styles.discountOverlay}>
          <View style={styles.discountModal}>
            <View style={styles.discountHeader}>
              <Text style={styles.discountTitle}>{t('cart.selectDiscountType', 'Select Discount Type')}</Text>
              <TouchableOpacity onPress={() => setShowDiscountOptions(false)}>
                <Ionicons name="close" size={24} color={Colors.light.text} />
              </TouchableOpacity>
            </View>
            
            <View style={styles.discountOptions}>
              <TouchableOpacity
                style={styles.discountOption}
                onPress={() => handleDiscount('percentage')}
              >
                <Ionicons name="percent" size={24} color={Colors.light.primary} />
                <Text style={styles.discountOptionText}>{t('cart.percentage', 'Percentage (%)')}</Text>
              </TouchableOpacity>
              
              <TouchableOpacity
                style={styles.discountOption}
                onPress={() => handleDiscount('amount')}
              >
                <Ionicons name="cash" size={24} color={Colors.light.primary} />
                <Text style={styles.discountOptionText}>{t('cart.fixedAmount', 'Fixed Amount (€)')}</Text>
              </TouchableOpacity>
              
              <TouchableOpacity
                style={styles.discountOption}
                onPress={() => handleDiscount('buyOneGetOne')}
              >
                <Ionicons name="gift" size={24} color={Colors.light.primary} />
                <Text style={styles.discountOptionText}>{t('cart.buyOneGetOne', 'Buy 1 Get 1')}</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    marginBottom: Spacing.md,
  },
  summaryContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: Spacing.md,
    paddingBottom: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  summaryItem: {
    alignItems: 'center',
  },
  summaryLabel: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.xs,
  },
  summaryValue: {
    ...Typography.h4,
    color: Colors.light.text,
    fontWeight: '600',
  },
  actionsContainer: {
    gap: Spacing.sm,
  },
  actionButton: {
    alignItems: 'center',
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    minWidth: 80,
    gap: Spacing.xs,
  },
  actionButtonDisabled: {
    opacity: 0.5,
  },
  actionLabel: {
    ...Typography.bodySmall,
    color: 'white',
    fontWeight: '500',
    textAlign: 'center',
  },
  discountOverlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
    zIndex: 1000,
  },
  discountModal: {
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.lg,
    padding: Spacing.lg,
    width: '80%',
    maxWidth: 400,
  },
  discountHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.lg,
  },
  discountTitle: {
    ...Typography.h4,
    color: Colors.light.text,
  },
  discountOptions: {
    gap: Spacing.md,
  },
  discountOption: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    gap: Spacing.md,
  },
  discountOptionText: {
    ...Typography.body,
    color: Colors.light.text,
  },
});

export default CartQuickActions; 