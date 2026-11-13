import { Ionicons } from '@expo/vector-icons';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityIndicator,
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';

import { SoftColors, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { listProductVariants, type ProductVariantDto } from '../services/api/variantService';

export type VariantPickerModalProps = {
  visible: boolean;
  productId: string | null;
  productName?: string;
  productPrice: number;
  onClose: () => void;
  onSelect: (variant: ProductVariantDto) => void;
};

function formatEuro(amount: number): string {
  return new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' }).format(amount);
}

export function VariantPickerModal({
  visible,
  productId,
  productName,
  productPrice,
  onClose,
  onSelect,
}: VariantPickerModalProps) {
  const { t } = useTranslation(['products', 'common']);
  const [items, setItems] = useState<ProductVariantDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!productId) return;
    setLoading(true);
    setError(null);
    try {
      const rows = await listProductVariants(productId);
      setItems(rows.filter((row) => row.isActive));
    } catch {
      setError(t('common:error'));
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [productId, t]);

  useEffect(() => {
    if (visible && productId) {
      void load();
    } else {
      setItems([]);
      setError(null);
    }
  }, [visible, productId, load]);

  const allOutOfStock = useMemo(
    () => !loading && items.length > 0 && items.every((row) => row.stock <= 0),
    [items, loading]
  );

  const handleSelect = useCallback(
    (variant: ProductVariantDto) => {
      if (variant.stock <= 0) return;
      onSelect(variant);
      onClose();
    },
    [onClose, onSelect]
  );

  return (
    <Modal visible={visible} animationType="slide" onRequestClose={onClose}>
      <View style={styles.wrap} accessibilityLabel={t('products:variantPickerTitle')}>
        <View style={styles.header}>
          <Text style={styles.title}>{t('products:variantPickerTitle')}</Text>
          <Pressable onPress={onClose} accessibilityRole="button" accessibilityLabel={t('common:close')}>
            <Ionicons name="close" size={24} color={SoftColors.textPrimary} />
          </Pressable>
        </View>
        {productName ? <Text style={styles.subtitle}>{productName}</Text> : null}
        {loading ? <ActivityIndicator color={SoftColors.accent} /> : null}
        {error ? <Text style={styles.error}>{error}</Text> : null}
        {allOutOfStock ? <Text style={styles.empty}>{t('products:variantAllOutOfStock')}</Text> : null}
        <ScrollView>
          {items.map((row) => {
            const outOfStock = row.stock <= 0;
            const effective = productPrice + row.priceDelta;
            return (
              <Pressable
                key={row.id}
                style={styles.row}
                disabled={outOfStock}
                onPress={() => handleSelect(row)}
                accessibilityRole="button"
                accessibilityState={{ disabled: outOfStock }}
                accessibilityLabel={`${row.variantLabel} ${row.sku}`}
              >
                <Text style={styles.label}>{row.variantLabel}</Text>
                <Text style={styles.meta}>{row.sku}</Text>
                <Text style={styles.meta}>{formatEuro(effective)}</Text>
                {outOfStock ? <Text style={styles.out}>{t('products:variantOutOfStock')}</Text> : null}
              </Pressable>
            );
          })}
        </ScrollView>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
    padding: SoftSpacing.lg,
    paddingTop: SoftSpacing.xl,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: SoftSpacing.sm,
  },
  title: {
    ...SoftTypography.h2,
    color: SoftColors.textPrimary,
  },
  subtitle: {
    ...SoftTypography.body,
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.md,
  },
  error: {
    ...SoftTypography.body,
    color: SoftColors.error,
    marginBottom: SoftSpacing.sm,
  },
  empty: {
    ...SoftTypography.body,
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.sm,
  },
  row: {
    paddingVertical: SoftSpacing.md,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: SoftColors.border,
  },
  label: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
  },
  meta: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: 2,
  },
  out: {
    ...SoftTypography.caption,
    color: SoftColors.error,
    marginTop: 2,
  },
});
