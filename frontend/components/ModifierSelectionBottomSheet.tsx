/**
 * POS: Extras (Zutaten) seçim UI'ı – sadece "Edit" ile açılır, bilgi için modal yok.
 * Bottom sheet stilinde; "Fertig" ile seçimi uygular, sepete ekleme satırda yapılır.
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

interface ModifierSelectionBottomSheetProps {
  visible: boolean;
  productId: string;
  productName: string;
  productPrice: number;
  /** Başlangıçta seçili modifier'lar (pending state'ten) */
  initialSelected?: SelectedModifier[];
  onClose: () => void;
  /** Seçimi uygula – parent pending state'i günceller */
  onApply: (selected: SelectedModifier[]) => void;
}

export function ModifierSelectionBottomSheet({
  visible,
  productId,
  productName,
  productPrice,
  initialSelected = [],
  onClose,
  onApply,
}: ModifierSelectionBottomSheetProps) {
  const [groups, setGroups] = useState<ModifierGroupDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Açıldığında pending seçimi yükle; initialSelected sadece açılışta kullanılır (re-render'da sıfırlamamak için deps'te yok)
  useEffect(() => {
    if (!visible || !productId) {
      setGroups([]);
      setSelectedIds(new Set());
      return;
    }
    setSelectedIds(new Set(initialSelected.map((m) => m.id)));
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
    return () => {
      cancelled = true;
    };
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

  const handleApply = () => {
    onApply(getSelectedModifiers());
    onClose();
  };

  const modifierTotal = getSelectedModifiers().reduce((s, m) => s + m.price, 0);
  const lineTotal = productPrice + modifierTotal;

  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={onClose}
    >
      <Pressable style={styles.overlay} onPress={onClose}>
        <Pressable style={styles.sheet} onPress={(e) => e.stopPropagation()}>
          <View style={styles.handle} />
          <View style={styles.header}>
            <Text style={styles.productName} numberOfLines={2}>{productName}</Text>
            <Text style={styles.productPrice}>€{productPrice.toFixed(2)}</Text>
          </View>

          {loading ? (
            <View style={styles.loadingWrap}>
              <ActivityIndicator size="small" color={SoftColors.accent} />
              <Text style={styles.loadingText}>Extras werden geladen…</Text>
            </View>
          ) : groups.length === 0 ? (
            <View style={styles.emptyWrap}>
              <Text style={styles.emptyText}>Keine Extras für dieses Produkt.</Text>
              <Text style={styles.emptySub}>Fertig tippen zum Schließen.</Text>
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
              <Pressable style={styles.applyBtn} onPress={handleApply}>
                <Text style={styles.applyBtnText}>Fertig</Text>
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
    justifyContent: 'flex-end',
  },
  sheet: {
    backgroundColor: SoftColors.bgCard,
    borderTopLeftRadius: SoftRadius.xl,
    borderTopRightRadius: SoftRadius.xl,
    maxHeight: '75%',
    paddingBottom: SoftSpacing.lg,
  },
  handle: {
    width: 40,
    height: 4,
    backgroundColor: SoftColors.border,
    borderRadius: 2,
    alignSelf: 'center',
    marginTop: SoftSpacing.sm,
    marginBottom: SoftSpacing.xs,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.lg,
    paddingBottom: SoftSpacing.md,
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
    ...SoftTypography.caption,
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
  applyBtn: {
    flex: 1,
    paddingVertical: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
    alignItems: 'center',
  },
  applyBtnText: {
    ...SoftTypography.body,
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
});
