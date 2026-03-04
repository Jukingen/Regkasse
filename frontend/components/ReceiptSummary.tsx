import React, { useState, useMemo } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity } from 'react-native';

function formatMoney(amount: number): string {
  return `€${Number(amount).toFixed(2)}`;
}

export interface ReceiptSummaryItem {
  name: string;
  quantity: number;
  lineTotalGross: number;
  isModifier?: boolean;
  parentIndex?: number;
}

export interface ReceiptSummaryVatLine {
  rate: number;
  net: number;
  vat: number;
  gross: number;
}

export interface ReceiptSummaryTotals {
  totalNet: number;
  totalVat: number;
  totalGross: number;
}

export interface ReceiptSummaryReceipt {
  items: ReceiptSummaryItem[];
  totals: ReceiptSummaryTotals;
  vatBreakdown: ReceiptSummaryVatLine[];
}

interface ReceiptSummaryProps {
  receipt: ReceiptSummaryReceipt;
  mode: 'cashier' | 'customer';
}

interface LineGroup {
  id: string;
  name: string;
  quantity: number;
  lineTotalGross: number;
  modifiers: { name: string; lineTotalGross: number }[];
}

function buildGroups(items: ReceiptSummaryItem[]): LineGroup[] {
  const groups: LineGroup[] = [];
  items.forEach((item, index) => {
    if (!item.isModifier) {
      groups.push({
        id: `line-${index}`,
        name: item.name,
        quantity: item.quantity,
        lineTotalGross: item.lineTotalGross,
        modifiers: [],
      });
    } else if (groups.length > 0) {
      const last = groups[groups.length - 1];
      last.modifiers.push({ name: item.name, lineTotalGross: item.lineTotalGross });
    }
  });
  return groups;
}

export function ReceiptSummary({ receipt, mode }: ReceiptSummaryProps) {
  const [breakdownVisible, setBreakdownVisible] = useState(false);
  const isCashier = mode === 'cashier';
  const showBreakdown = isCashier || breakdownVisible;

  const groups = useMemo(() => buildGroups(receipt.items), [receipt.items]);

  const renderLine = ({ item }: { item: LineGroup }) => (
    <View style={styles.lineBlock}>
      <View style={styles.mainRow}>
        <View style={styles.nameQty}>
          <Text style={styles.productName}>{item.name}</Text>
          <Text style={styles.qty}>{item.quantity} × {formatMoney(item.lineTotalGross / item.quantity)}</Text>
        </View>
        <Text style={styles.lineTotal}>{formatMoney(item.lineTotalGross)}</Text>
      </View>
      {item.modifiers.length > 0 && (
        <View style={styles.modifiers}>
          {item.modifiers.map((mod, i) => (
            <Text key={i} style={styles.modifierText}>+ {mod.name} {formatMoney(mod.lineTotalGross)}</Text>
          ))}
        </View>
      )}
    </View>
  );

  return (
    <View style={styles.container}>
      <FlatList
        data={groups}
        renderItem={renderLine}
        keyExtractor={(item) => item.id}
        scrollEnabled={groups.length > 6}
        ListFooterComponent={
          <>
            {receipt.vatBreakdown && receipt.vatBreakdown.length > 0 && (
              <View style={styles.breakdownSection}>
                {isCashier ? (
                  <>
                    <Text style={styles.breakdownTitle}>MwSt. Übersicht</Text>
                    {receipt.vatBreakdown.map((row, i) => (
                      <View key={i} style={styles.breakdownRow}>
                        <Text style={styles.breakdownCell}>{row.rate}%</Text>
                        <Text style={styles.breakdownCellRight}>{formatMoney(row.net)}</Text>
                        <Text style={styles.breakdownCellRight}>{formatMoney(row.vat)}</Text>
                        <Text style={styles.breakdownCellRight}>{formatMoney(row.gross)}</Text>
                      </View>
                    ))}
                  </>
                ) : (
                  <>
                    <TouchableOpacity
                      style={styles.breakdownToggle}
                      onPress={() => setBreakdownVisible((v) => !v)}
                    >
                      <Text style={styles.breakdownTitle}>MwSt. Übersicht</Text>
                      <Text style={styles.toggleHint}>{breakdownVisible ? '▼' : '▶'}</Text>
                    </TouchableOpacity>
                    {showBreakdown && receipt.vatBreakdown.map((row, i) => (
                      <View key={i} style={styles.breakdownRow}>
                        <Text style={styles.breakdownCell}>{row.rate}%</Text>
                        <Text style={styles.breakdownCellRight}>{formatMoney(row.gross)}</Text>
                      </View>
                    ))}
                  </>
                )}
              </View>
            )}
            <View style={styles.totalsSection}>
              {isCashier && (
                <>
                  <View style={styles.totalRow}>
                    <Text style={styles.totalLabel}>Summe Netto</Text>
                    <Text style={styles.totalValue}>{formatMoney(receipt.totals.totalNet)}</Text>
                  </View>
                  <View style={styles.totalRow}>
                    <Text style={styles.totalLabel}>Summe MwSt.</Text>
                    <Text style={styles.totalValue}>{formatMoney(receipt.totals.totalVat)}</Text>
                  </View>
                </>
              )}
              <View style={[styles.totalRow, styles.grandTotalRow]}>
                <Text style={styles.grandTotalLabel}>Gesamt (Brutto)</Text>
                <Text style={styles.grandTotalValue}>{formatMoney(receipt.totals.totalGross)}</Text>
              </View>
            </View>
          </>
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 12,
    maxHeight: 360,
  },
  lineBlock: {
    marginBottom: 8,
    paddingBottom: 6,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  mainRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
  },
  nameQty: { flex: 1, marginRight: 8 },
  productName: { fontSize: 15, fontWeight: '600', color: '#333' },
  qty: { fontSize: 13, color: '#666', marginTop: 2 },
  lineTotal: { fontSize: 15, fontWeight: '600', color: '#333' },
  modifiers: { marginTop: 6, marginLeft: 12, paddingLeft: 8, borderLeftWidth: 2, borderLeftColor: '#ddd' },
  modifierText: { fontSize: 12, color: '#666', marginTop: 2 },
  breakdownSection: { marginTop: 16 },
  breakdownTitle: { fontSize: 14, fontWeight: '700', marginBottom: 8, color: '#333' },
  breakdownToggle: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 },
  toggleHint: { fontSize: 12, color: '#666' },
  breakdownRow: { flexDirection: 'row', paddingVertical: 4, borderBottomWidth: 1, borderBottomColor: '#f0f0f0' },
  breakdownCell: { fontSize: 13, color: '#333', flex: 1 },
  breakdownCellRight: { fontSize: 13, color: '#333', minWidth: 64, textAlign: 'right' },
  totalsSection: { marginTop: 16, paddingTop: 12, borderTopWidth: 2, borderTopColor: '#ddd' },
  totalRow: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 4 },
  totalLabel: { fontSize: 14, color: '#666' },
  totalValue: { fontSize: 14, fontWeight: '600', color: '#333' },
  grandTotalRow: { marginTop: 6, paddingTop: 8, borderTopWidth: 1, borderTopColor: '#eee' },
  grandTotalLabel: { fontSize: 16, fontWeight: '700', color: '#333' },
  grandTotalValue: { fontSize: 18, fontWeight: '700', color: '#333' },
});

export default ReceiptSummary;
