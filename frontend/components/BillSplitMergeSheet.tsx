import { Ionicons } from '@expo/vector-icons';
import React, { useCallback, useMemo, useState } from 'react';
import {
  Alert,
  FlatList,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import type { CartItem } from '../contexts/CartContext';
import { VALID_TABLE_NUMBERS } from '../utils/tableCartUtils';
import { WaveLoader } from '../src/components/common/WaveLoader';

type Mode = 'split' | 'merge';

export type BillSplitMergeSheetProps = {
  visible: boolean;
  mode: Mode;
  activeTableId: number;
  cartItems: CartItem[];
  onClose: () => void;
  onSplitItems: (targetTable: number, itemIds: string[]) => Promise<void>;
  onMergeTables: (sourceTable: number, targetTable: number) => Promise<void>;
};

export function BillSplitMergeSheet({
  visible,
  mode,
  activeTableId,
  cartItems,
  onClose,
  onSplitItems,
  onMergeTables,
}: BillSplitMergeSheetProps) {
  const [targetTable, setTargetTable] = useState<number | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [busy, setBusy] = useState(false);

  const tableOptions = useMemo(
    () => VALID_TABLE_NUMBERS.filter((n) => n !== activeTableId),
    [activeTableId]
  );

  const toggleItem = useCallback((itemId: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(itemId)) next.delete(itemId);
      else next.add(itemId);
      return next;
    });
  }, []);

  const resolveItemId = (item: CartItem) => item.itemId ?? item.clientId ?? item.productId;

  const handleConfirm = useCallback(async () => {
    if (targetTable == null) {
      Alert.alert('Hinweis', 'Bitte Zieltisch wählen.');
      return;
    }
    setBusy(true);
    try {
      if (mode === 'split') {
        if (selectedIds.size === 0) {
          Alert.alert('Hinweis', 'Bitte mindestens eine Position auswählen.');
          return;
        }
        await onSplitItems(targetTable, Array.from(selectedIds));
      } else {
        await onMergeTables(activeTableId, targetTable);
      }
      onClose();
    } catch {
      Alert.alert('Fehler', mode === 'split' ? 'Teilen fehlgeschlagen.' : 'Zusammenführen fehlgeschlagen.');
    } finally {
      setBusy(false);
    }
  }, [targetTable, mode, selectedIds, onSplitItems, onMergeTables, activeTableId, onClose]);

  const title = mode === 'split' ? 'Rechnung teilen' : 'Tische zusammenführen';
  const subtitle =
    mode === 'split'
      ? `Positionen von Tisch ${activeTableId} auf anderen Tisch verschieben`
      : `Alle Positionen von Tisch ${activeTableId} mit Zieltisch verbinden`;

  return (
    <Modal visible={visible} animationType="slide" presentationStyle="pageSheet" onRequestClose={onClose}>
      <View style={styles.container}>
        <View style={styles.header}>
          <Pressable onPress={onClose} style={styles.iconBtn}>
            <Ionicons name="close" size={24} color={SoftColors.textPrimary} />
          </Pressable>
          <View style={styles.headerText}>
            <Text style={styles.title}>{title}</Text>
            <Text style={styles.subtitle}>{subtitle}</Text>
          </View>
        </View>

        <Text style={styles.sectionLabel}>Zieltisch</Text>
        <View style={styles.tableRow}>
          {tableOptions.map((n) => (
            <Pressable
              key={n}
              style={[styles.tableChip, targetTable === n && styles.tableChipActive]}
              onPress={() => setTargetTable(n)}
            >
              <Text style={[styles.tableChipText, targetTable === n && styles.tableChipTextActive]}>
                {n}
              </Text>
            </Pressable>
          ))}
        </View>

        {mode === 'split' ? (
          <>
            <Text style={styles.sectionLabel}>Positionen</Text>
            <FlatList
              data={cartItems}
              keyExtractor={(item) => resolveItemId(item)}
              style={styles.list}
              renderItem={({ item }) => {
                const id = resolveItemId(item);
                const checked = selectedIds.has(id);
                return (
                  <Pressable style={styles.lineRow} onPress={() => toggleItem(id)}>
                    <Ionicons
                      name={checked ? 'checkbox' : 'square-outline'}
                      size={22}
                      color={checked ? SoftColors.accent : SoftColors.textMuted}
                    />
                    <Text style={styles.lineName} numberOfLines={1}>
                      {item.productName} ×{item.qty}
                    </Text>
                  </Pressable>
                );
              }}
              ListEmptyComponent={<Text style={styles.empty}>Keine Positionen</Text>}
            />
          </>
        ) : null}

        <Pressable
          style={[styles.confirmBtn, busy && styles.confirmBtnDisabled]}
          onPress={() => void handleConfirm()}
          disabled={busy}
        >
          {busy ? (
            <WaveLoader size={20} color={SoftColors.textInverse} />
          ) : (
            <Text style={styles.confirmText}>{mode === 'split' ? 'Teilen' : 'Zusammenführen'}</Text>
          )}
        </Pressable>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: SoftColors.bgPrimary, padding: SoftSpacing.md },
  header: { flexDirection: 'row', alignItems: 'flex-start', marginBottom: SoftSpacing.md, gap: SoftSpacing.sm },
  iconBtn: { padding: SoftSpacing.xs },
  headerText: { flex: 1 },
  title: { ...SoftTypography.h2, color: SoftColors.textPrimary },
  subtitle: { ...SoftTypography.caption, color: SoftColors.textMuted, marginTop: 4 },
  sectionLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.xs,
    marginTop: SoftSpacing.sm,
  },
  tableRow: { flexDirection: 'row', flexWrap: 'wrap', gap: SoftSpacing.xs },
  tableChip: {
    minWidth: 44,
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.border,
    alignItems: 'center',
  },
  tableChipActive: { backgroundColor: SoftColors.accent, borderColor: SoftColors.accent },
  tableChipText: { fontWeight: '600', color: SoftColors.textPrimary },
  tableChipTextActive: { color: SoftColors.textInverse },
  list: { flex: 1, marginTop: SoftSpacing.xs },
  lineRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.border,
  },
  lineName: { flex: 1, fontSize: 15, color: SoftColors.textPrimary },
  empty: { color: SoftColors.textMuted, textAlign: 'center', marginTop: SoftSpacing.lg },
  confirmBtn: {
    backgroundColor: SoftColors.accent,
    paddingVertical: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    alignItems: 'center',
    marginTop: SoftSpacing.md,
  },
  confirmBtnDisabled: { opacity: 0.6 },
  confirmText: { color: SoftColors.textInverse, fontWeight: '700', fontSize: 16 },
});
