import { Ionicons } from '@expo/vector-icons';
import React, { useCallback, useEffect, useState } from 'react';
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

import { BarcodeScannerModal } from './BarcodeScannerModal';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useVerticalFeatures } from '../contexts/VerticalProfileContext';
import { listProductImeis, type ProductImeiDto } from '../services/api/imeiService';

export type ImeiPickerModalProps = {
  visible: boolean;
  productId: string | null;
  productName?: string;
  onClose: () => void;
  onSelect: (imei: string) => void;
};

export function ImeiPickerModal({
  visible,
  productId,
  productName,
  onClose,
  onSelect,
}: ImeiPickerModalProps) {
  const { t } = useTranslation(['verticalProfiles', 'common']);
  const { posFeatures } = useVerticalFeatures();
  const imeiEnabled = posFeatures.imeiTracking === true;
  const [items, setItems] = useState<ProductImeiDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [scannerOpen, setScannerOpen] = useState(false);

  const load = useCallback(async () => {
    if (!productId) return;
    setLoading(true);
    setError(null);
    try {
      const rows = await listProductImeis(productId, 'InStock');
      setItems(rows);
    } catch {
      setError(t('verticalProfiles:screens.imei.loadFailed'));
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [productId, t]);

  useEffect(() => {
    if (!imeiEnabled) return;
    if (visible && productId) {
      void load();
    } else {
      setItems([]);
      setError(null);
      setScannerOpen(false);
    }
  }, [imeiEnabled, visible, productId, load]);

  const handleSelect = useCallback(
    (imei: string) => {
      const code = imei.trim();
      if (!code) return;
      onSelect(code);
      onClose();
    },
    [onClose, onSelect]
  );

  if (!imeiEnabled) return null;

  return (
    <>
      <Modal visible={visible} animationType="slide" onRequestClose={onClose}>
        <View style={styles.wrap} accessibilityLabel={t('verticalProfiles:screens.imei.title')}>
          <View style={styles.header}>
            <Text style={styles.title}>{t('verticalProfiles:screens.imei.title')}</Text>
            <Pressable onPress={onClose} accessibilityRole="button" accessibilityLabel={t('common:close')}>
              <Ionicons name="close" size={24} color={SoftColors.textPrimary} />
            </Pressable>
          </View>
          {productName ? <Text style={styles.subtitle}>{productName}</Text> : null}
          <Pressable
            style={styles.scanBtn}
            onPress={() => setScannerOpen(true)}
            accessibilityRole="button"
            accessibilityLabel={t('verticalProfiles:screens.imei.scan')}
          >
            <Ionicons name="scan-outline" size={20} color={SoftColors.textInverse} />
            <Text style={styles.scanBtnText}>{t('verticalProfiles:screens.imei.scan')}</Text>
          </Pressable>
          {loading ? <ActivityIndicator color={SoftColors.accent} /> : null}
          {error ? <Text style={styles.error}>{error}</Text> : null}
          {!loading && items.length === 0 && !error ? (
            <Text style={styles.empty}>{t('verticalProfiles:screens.imei.empty')}</Text>
          ) : null}
          <ScrollView>
            {items.map((row) => (
              <Pressable
                key={row.id}
                style={styles.row}
                onPress={() => handleSelect(row.imei)}
                accessibilityRole="button"
                accessibilityLabel={t('verticalProfiles:screens.imei.select', { imei: row.imei })}
              >
                <Text style={styles.imei}>{row.imei}</Text>
                <Text style={styles.status}>{t('verticalProfiles:screens.imei.inStock')}</Text>
              </Pressable>
            ))}
          </ScrollView>
        </View>
      </Modal>
      <BarcodeScannerModal
        visible={scannerOpen}
        title={t('verticalProfiles:screens.imei.scan')}
        hint={t('verticalProfiles:screens.imei.scanHint')}
        onClose={() => setScannerOpen(false)}
        onScan={handleSelect}
      />
    </>
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
  scanBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    backgroundColor: SoftColors.accentDark,
    borderRadius: SoftRadius.md,
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    alignSelf: 'flex-start',
    marginBottom: SoftSpacing.md,
  },
  scanBtnText: {
    ...SoftTypography.body,
    color: SoftColors.textInverse,
    fontWeight: '600',
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
  imei: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
    fontVariant: ['tabular-nums'],
  },
  status: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: 2,
  },
});
