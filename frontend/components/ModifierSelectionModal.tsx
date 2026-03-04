/**
 * POS: Ürün seçildiğinde açılan Extra Zutaten modal'ı.
 * Gruplar (Saucen, Extras) ve her gruptaki modifier'lar checkbox ile seçilir.
 * "Hinzufügen" ile sepete eklenir; modifier seçmeden de eklenebilir.
 */
import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import {
  getProductModifierGroups,
  type ModifierGroupDto,
  type ModifierDto,
} from '../services/api/productModifiersService';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography } from '../constants/SoftTheme';

export interface SelectedModifier {
  id: string;
  name: string;
  price: number;
}

interface ModifierSelectionModalProps {
  visible: boolean;
  productId: string;
  productName: string;
  productPrice: number;
  onClose: () => void;
  onAdd: (selectedModifiers: SelectedModifier[]) => void;
}

export function ModifierSelectionModal({
  visible,
  productId,
  productName,
  productPrice,
  onClose,
  onAdd,
}: ModifierSelectionModalProps) {
  const [groups, setGroups] = useState<ModifierGroupDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (!visible || !productId) {
      setGroups([]);
      setSelectedIds(new Set());
      return;
    }
    let cancelled = false;
    setLoading(true);
    getProductModifierGroups(productId)
      .then((data) => {
        if (!cancelled) setGroups(data);
      })
      .catch(() => {
        if (!cancelled) setGroups([]);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [visible, productId]);

  const toggleModifier = (m: ModifierDto) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(m.id)) next.delete(m.id);
      else next.add(m.id);
      return next;
    });
  };

  const getSelectedModifiers = (): SelectedModifier[] => {
    const out: SelectedModifier[] = [];
    for (const g of groups) {
      for (const m of g.modifiers) {
        if (selectedIds.has(m.id)) out.push({ id: m.id, name: m.name, price: Number(m.price) });
      }
    }
    return out;
  };

  const handleAdd = () => {
    onAdd(getSelectedModifiers());
    onClose();
  };

  const modifierTotal = getSelectedModifiers().reduce((s, m) => s + m.price, 0);
  const lineTotal = productPrice + modifierTotal;

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onClose}
    >
      <Pressable style={styles.overlay} onPress={onClose}>
        <Pressable style={styles.card} onPress={(e) => e.stopPropagation()}>
          <View style={styles.header}>
            <Text style={styles.productName} numberOfLines={2}>{productName}</Text>
            <Text style={styles.productPrice}>€{productPrice.toFixed(2)}</Text>
          </View>

          {loading ? (
            <View style={styles.loadingWrap}>
              <ActivityIndicator size="small" color={SoftColors.accent} />
              <Text style={styles.loadingText}>Extra Zutaten werden geladen…</Text>
            </View>
          ) : groups.length === 0 ? (
            <View style={styles.emptyWrap}>
              <Text style={styles.emptyText}>Keine Extra Zutaten für dieses Produkt.</Text>
              <Text style={styles.emptySub}>Tipp: Hinzufügen zum direkten Eintrag.</Text>
            </View>
          ) : (
            <ScrollView
              style={styles.scroll}
              contentContainerStyle={styles.scrollContent}
              keyboardShouldPersistTaps="handled"
              showsVerticalScrollIndicator={false}
            >
              {groups.map((group) => (
                <View key={group.id} style={styles.group}>
                  <Text style={styles.groupName}>{group.name}</Text>
                  {group.modifiers.map((m) => {
                    const checked = selectedIds.has(m.id);
                    return (
                      <Pressable
                        key={m.id}
                        style={[styles.modifierRow, checked && styles.modifierRowChecked]}
                        onPress={() => toggleModifier(m)}
                      >
                        <View style={[styles.checkbox, checked && styles.checkboxChecked]}>
                          {checked && <Text style={styles.checkmark}>✓</Text>}
                        </View>
                        <Text style={styles.modifierName} numberOfLines={1}>{m.name}</Text>
                        <Text style={styles.modifierPrice}>€{Number(m.price).toFixed(2)}</Text>
                      </Pressable>
                    );
                  })}
                </View>
              ))}
            </ScrollView>
          )}

          <View style={styles.footer}>
            {selectedIds.size > 0 && (
              <Text style={styles.totalLine}>
                + Extras: €{modifierTotal.toFixed(2)} → Gesamt: €{lineTotal.toFixed(2)}
              </Text>
            )}
            <View style={styles.buttons}>
              <Pressable style={styles.cancelBtn} onPress={onClose}>
                <Text style={styles.cancelBtnText}>Abbrechen</Text>
              </Pressable>
              <Pressable style={styles.addBtn} onPress={handleAdd}>
                <Text style={styles.addBtnText}>Hinzufügen</Text>
              </Pressable>
            </View>
          </View>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: SoftColors.overlay,
    justifyContent: 'center',
    padding: SoftSpacing.lg,
  },
  card: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    maxHeight: '80%',
    paddingBottom: SoftSpacing.lg,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: SoftSpacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.border,
  },
  productName: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
    flex: 1,
    marginRight: SoftSpacing.sm,
  },
  productPrice: {
    ...SoftTypography.h3,
    color: SoftColors.accentDark,
  },
  loadingWrap: {
    padding: SoftSpacing.xxl,
    alignItems: 'center',
    gap: SoftSpacing.md,
  },
  loadingText: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
  },
  emptyWrap: {
    padding: SoftSpacing.xxl,
    alignItems: 'center',
  },
  emptyText: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
  },
  emptySub: {
    ...SoftTypography.small,
    color: SoftColors.textMuted,
    marginTop: SoftSpacing.xs,
  },
  scroll: {
    maxHeight: 320,
  },
  scrollContent: {
    padding: SoftSpacing.lg,
    paddingBottom: SoftSpacing.md,
  },
  group: {
    marginBottom: SoftSpacing.lg,
  },
  groupName: {
    ...SoftTypography.label,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.sm,
    textTransform: 'uppercase',
  },
  modifierRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.sm,
    borderRadius: SoftRadius.sm,
    marginBottom: 2,
  },
  modifierRowChecked: {
    backgroundColor: SoftColors.accentLight + '40',
  },
  checkbox: {
    width: 24,
    height: 24,
    borderRadius: 6,
    borderWidth: 2,
    borderColor: SoftColors.border,
    marginRight: SoftSpacing.md,
    alignItems: 'center',
    justifyContent: 'center',
  },
  checkboxChecked: {
    backgroundColor: SoftColors.accent,
    borderColor: SoftColors.accent,
  },
  checkmark: {
    color: SoftColors.textInverse,
    fontSize: 14,
    fontWeight: '700',
  },
  modifierName: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
    flex: 1,
  },
  modifierPrice: {
    ...SoftTypography.body,
    color: SoftColors.accentDark,
    marginLeft: SoftSpacing.sm,
  },
  footer: {
    paddingHorizontal: SoftSpacing.lg,
    paddingTop: SoftSpacing.md,
    borderTopWidth: 1,
    borderTopColor: SoftColors.border,
  },
  totalLine: {
    ...SoftTypography.small,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.sm,
  },
  buttons: {
    flexDirection: 'row',
    gap: SoftSpacing.md,
  },
  cancelBtn: {
    flex: 1,
    paddingVertical: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgSecondary,
    alignItems: 'center',
  },
  cancelBtnText: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
  },
  addBtn: {
    flex: 1,
    paddingVertical: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
    alignItems: 'center',
  },
  addBtnText: {
    ...SoftTypography.body,
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
});
