import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TextInput,
  ScrollView,
  Alert,
  ActivityIndicator,
} from 'react-native';

import { Colors, Spacing, BorderRadius } from '../constants/Colors';
import { couponService, Coupon, CouponValidationResult } from '../services/api/couponService';

interface CouponModalProps {
  visible: boolean;
  onClose: () => void;
  onCouponApplied: (coupon: Coupon, discountAmount: number) => void;
  totalAmount: number;
  customerId?: string;
}

export default function CouponModal({
  visible,
  onClose,
  onCouponApplied,
  totalAmount,
  customerId,
}: CouponModalProps) {
  const { t } = useTranslation();
  const [couponCode, setCouponCode] = useState('');
  const [activeCoupons, setActiveCoupons] = useState<Coupon[]>([]);
  const [loading, setLoading] = useState(false);
  const [validating, setValidating] = useState(false);
  const [validationResult, setValidationResult] = useState<CouponValidationResult | null>(null);

  useEffect(() => {
    if (visible) {
      loadActiveCoupons();
    }
  }, [visible]);

  const loadActiveCoupons = async () => {
    try {
      setLoading(true);
      const coupons = await couponService.getActiveCoupons();
      setActiveCoupons(coupons);
    } catch (error) {
      console.error('Failed to load active coupons:', error);
      Alert.alert('Error', 'Failed to load active coupons');
    } finally {
      setLoading(false);
    }
  };

  const validateCoupon = async () => {
    if (!couponCode.trim()) {
      Alert.alert('Error', 'Please enter a coupon code');
      return;
    }

    try {
      setValidating(true);
      const result = await couponService.validateCoupon({
        code: couponCode.trim(),
        totalAmount,
        customerId,
      });
      setValidationResult(result);
    } catch (error: any) {
      console.error('Failed to validate coupon:', error);
      Alert.alert('Error', error.message || 'Failed to validate coupon');
    } finally {
      setValidating(false);
    }
  };

  const applyCoupon = () => {
    if (validationResult?.isValid && validationResult.coupon) {
      onCouponApplied(validationResult.coupon, validationResult.discountAmount);
      setCouponCode('');
      setValidationResult(null);
      onClose();
    }
  };

  const selectCoupon = (coupon: Coupon) => {
    setCouponCode(coupon.code);
    validateCoupon();
  };

  const formatDiscountValue = (coupon: Coupon) => {
    switch (coupon.discountType) {
      case 'Percentage':
        return `${coupon.discountValue}%`;
      case 'FixedAmount':
        return `€${coupon.discountValue.toFixed(2)}`;
      case 'BuyOneGetOne':
        return 'BOGO';
      case 'FreeShipping':
        return 'Free Shipping';
      default:
        return '';
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString();
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent
      onRequestClose={onClose}
    >
      <View style={styles.overlay}>
        <View style={styles.container}>
          <View style={styles.header}>
            <Text style={styles.title}>
              <Ionicons name="pricetag" size={24} color={Colors.light.primary} />
              {' '}Kupon Kodu
            </Text>
            <TouchableOpacity style={styles.closeButton} onPress={onClose}>
              <Ionicons name="close" size={24} color={Colors.light.textSecondary} />
            </TouchableOpacity>
          </View>

          <View style={styles.inputSection}>
            <Text style={styles.label}>Kupon Kodu</Text>
            <View style={styles.inputContainer}>
              <TextInput
                style={styles.input}
                value={couponCode}
                onChangeText={setCouponCode}
                placeholder="Kupon kodunu girin..."
                autoCapitalize="characters"
                autoCorrect={false}
              />
              <TouchableOpacity
                style={[styles.validateButton, validating && styles.validateButtonDisabled]}
                onPress={validateCoupon}
                disabled={validating}
              >
                {validating ? (
                  <ActivityIndicator size="small" color="white" />
                ) : (
                  <Ionicons name="checkmark" size={20} color="white" />
                )}
              </TouchableOpacity>
            </View>
          </View>

          {validationResult && (
            <View style={[
              styles.validationResult,
              validationResult.isValid ? styles.validResult : styles.invalidResult
            ]}>
              <Ionicons
                name={validationResult.isValid ? "checkmark-circle" : "close-circle"}
                size={24}
                color={validationResult.isValid ? Colors.light.success : Colors.light.error}
              />
              <Text style={[
                styles.validationText,
                validationResult.isValid ? styles.validText : styles.invalidText
              ]}>
                {validationResult.isValid ? validationResult.message : validationResult.errorMessage}
              </Text>
              {validationResult.isValid && (
                <Text style={styles.discountAmount}>
                  İndirim: €{validationResult.discountAmount.toFixed(2)}
                </Text>
              )}
            </View>
          )}

          {validationResult?.isValid && (
            <TouchableOpacity style={styles.applyButton} onPress={applyCoupon}>
              <Ionicons name="checkmark" size={20} color="white" />
              <Text style={styles.applyButtonText}>Kuponu Uygula</Text>
            </TouchableOpacity>
          )}

          <View style={styles.divider}>
            <Text style={styles.dividerText}>Aktif Kuponlar</Text>
          </View>

          <ScrollView style={styles.couponsList}>
            {loading ? (
              <ActivityIndicator size="large" color={Colors.light.primary} />
            ) : activeCoupons.length === 0 ? (
              <Text style={styles.noCouponsText}>Aktif kupon bulunmuyor</Text>
            ) : (
              activeCoupons.map((coupon) => (
                <TouchableOpacity
                  key={coupon.id}
                  style={styles.couponItem}
                  onPress={() => selectCoupon(coupon)}
                >
                  <View style={styles.couponHeader}>
                    <Text style={styles.couponCode}>{coupon.code}</Text>
                    <Text style={styles.couponDiscount}>
                      {formatDiscountValue(coupon)}
                    </Text>
                  </View>
                  <Text style={styles.couponName}>{coupon.name}</Text>
                  {coupon.description && (
                    <Text style={styles.couponDescription}>{coupon.description}</Text>
                  )}
                  <View style={styles.couponDetails}>
                    <Text style={styles.couponDetail}>
                      Min. Tutar: €{coupon.minimumAmount.toFixed(2)}
                    </Text>
                    <Text style={styles.couponDetail}>
                      Geçerlilik: {formatDate(coupon.validFrom)} - {formatDate(coupon.validUntil)}
                    </Text>
                    {coupon.usageLimit > 0 && (
                      <Text style={styles.couponDetail}>
                        Kullanım: {coupon.usedCount}/{coupon.usageLimit}
                      </Text>
                    )}
                  </View>
                </TouchableOpacity>
              ))
            )}
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  container: {
    width: '90%',
    maxWidth: 500,
    maxHeight: '80%',
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.lg,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.25,
    shadowRadius: 3.84,
    elevation: 5,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  title: {
    fontSize: 20,
    fontWeight: '600',
    color: Colors.light.text,
    flexDirection: 'row',
    alignItems: 'center',
  },
  closeButton: {
    padding: Spacing.xs,
  },
  inputSection: {
    padding: Spacing.lg,
  },
  label: {
    fontSize: 16,
    fontWeight: '500',
    color: Colors.light.text,
    marginBottom: Spacing.sm,
  },
  inputContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
  },
  input: {
    flex: 1,
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    fontSize: 16,
  },
  validateButton: {
    backgroundColor: Colors.light.primary,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    justifyContent: 'center',
    alignItems: 'center',
  },
  validateButtonDisabled: {
    opacity: 0.6,
  },
  validationResult: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    marginHorizontal: Spacing.lg,
    borderRadius: BorderRadius.md,
    gap: Spacing.sm,
  },
  validResult: {
    backgroundColor: Colors.light.success + '20',
    borderWidth: 1,
    borderColor: Colors.light.success,
  },
  invalidResult: {
    backgroundColor: Colors.light.error + '20',
    borderWidth: 1,
    borderColor: Colors.light.error,
  },
  validationText: {
    flex: 1,
    fontSize: 14,
  },
  validText: {
    color: Colors.light.success,
  },
  invalidText: {
    color: Colors.light.error,
  },
  discountAmount: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.light.success,
  },
  applyButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.success,
    padding: Spacing.md,
    margin: Spacing.lg,
    borderRadius: BorderRadius.md,
    gap: Spacing.sm,
  },
  applyButtonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
  },
  divider: {
    paddingHorizontal: Spacing.lg,
    paddingVertical: Spacing.md,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  dividerText: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.light.textSecondary,
  },
  couponsList: {
    flex: 1,
    paddingHorizontal: Spacing.lg,
  },
  noCouponsText: {
    textAlign: 'center',
    color: Colors.light.textSecondary,
    fontSize: 16,
    paddingVertical: Spacing.xl,
  },
  couponItem: {
    backgroundColor: Colors.light.surface,
    padding: Spacing.md,
    marginBottom: Spacing.sm,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  couponHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  couponCode: {
    fontSize: 18,
    fontWeight: '600',
    color: Colors.light.primary,
  },
  couponDiscount: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.light.success,
  },
  couponName: {
    fontSize: 16,
    fontWeight: '500',
    color: Colors.light.text,
    marginBottom: Spacing.xs,
  },
  couponDescription: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.sm,
  },
  couponDetails: {
    gap: Spacing.xs,
  },
  couponDetail: {
    fontSize: 12,
    color: Colors.light.textSecondary,
  },
}); 