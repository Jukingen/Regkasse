/**
 * Fiş satırı: Phase 2 flat = one main line per product (modifiers empty). Legacy = main + modifier lines (name may include "+ " from backend).
 * ReceiptSummary FlatList item renderer.
 */
import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { formatPrice } from '../utils/formatPrice';
import type { ReceiptItemDTO } from '../types/ReceiptDTO';
import { SoftColors, SoftSpacing } from '../constants/SoftTheme';

export interface ReceiptLineItemProps {
  /** Ana ürün satırı */
  main: ReceiptItemDTO;
  /** Bu ürüne ait modifier/extras satırları */
  modifiers?: ReceiptItemDTO[];
  /** Küçük tipografi (customer view) */
  compact?: boolean;
}

export function ReceiptLineItem({ main, modifiers = [], compact }: ReceiptLineItemProps) {
  const lineTotalGross = main.lineTotalGross ?? main.totalPrice;

  return (
    <View style={styles.wrapper}>
      <View style={[styles.mainRow, compact && styles.mainRowCompact]}>
        <View style={styles.nameQty}>
          <Text style={[styles.productName, compact && styles.productNameCompact]} numberOfLines={2}>
            {main.name}
          </Text>
          <Text style={[styles.qty, compact && styles.qtyCompact]}>
            {main.quantity} × {formatPrice(main.unitPrice)}
          </Text>
        </View>
        <Text style={[styles.lineTotal, compact && styles.lineTotalCompact]}>
          {formatPrice(lineTotalGross)}
        </Text>
      </View>
      {modifiers.length > 0 && (
        <View style={styles.modifiers}>
          {modifiers.map((mod, idx) => (
            <View key={mod.itemId ?? idx} style={styles.modifierRow}>
              <Text style={[styles.modifierText, compact && styles.modifierTextCompact]}>
                {mod.name?.startsWith('+') ? mod.name : `+ ${mod.name}`}
              </Text>
              <Text style={[styles.modifierPrice, compact && styles.modifierTextCompact]}>
                {formatPrice(mod.totalPrice)}
              </Text>
            </View>
          ))}
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: {
    marginBottom: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  mainRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
  },
  mainRowCompact: {
    marginBottom: 2,
  },
  nameQty: {
    flex: 1,
    marginRight: SoftSpacing.sm,
  },
  productName: {
    fontSize: 15,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  productNameCompact: {
    fontSize: 14,
  },
  qty: {
    fontSize: 13,
    color: SoftColors.textSecondary,
    marginTop: 2,
  },
  qtyCompact: {
    fontSize: 12,
  },
  lineTotal: {
    fontSize: 15,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  lineTotalCompact: {
    fontSize: 14,
  },
  modifiers: {
    marginTop: 4,
    marginLeft: SoftSpacing.md,
    paddingLeft: SoftSpacing.sm,
    borderLeftWidth: 2,
    borderLeftColor: SoftColors.border,
  },
  modifierRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: 2,
  },
  modifierText: {
    fontSize: 13,
    color: SoftColors.textSecondary,
    flex: 1,
  },
  modifierTextCompact: {
    fontSize: 12,
  },
  modifierPrice: {
    fontSize: 13,
    color: SoftColors.textSecondary,
    marginLeft: SoftSpacing.sm,
  },
});

export default ReceiptLineItem;
