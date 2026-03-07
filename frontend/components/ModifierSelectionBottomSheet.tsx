/**
 * POS: Modern add-on selection (Square/Toast style). Source: group.products only. Structure: Product name/price then modifier groups then Add to cart. Radio when min=1 max=1; checkbox when max>1. seçim UI'ı – sadece "Edit" ile açılır, bilgi için modal yok.
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
  type AddOnGroupProductItemDto,
} from '../services/api/productModifiersService';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography } from '../constants/SoftTheme';

/** Single choice = radio (min=1, max=1); multi = checkbox. */
function isSingleChoiceGroup(g: ModifierGroupDto): boolean {
  const min = g.minSelections ?? 0;
  const max = g.maxSelections;
  return min === 1 && max === 1;
}

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
  /** Catalog-first: pass product.modifierGroups to avoid extra API call. */
  modifierGroups?: ModifierGroupDto[] | null;
  /** Başlangıçta seçili modifier'lar (pending state'ten) */
  initialSelected?: SelectedModifier[];
  onClose: () => void;
  /** Legacy: modifier seçimi uygula (satıra bağlı). */
  onApply?: (selected: SelectedModifier[]) => void;
  /** Primary: add-on product seçimi – her biri sepette ayrı satır (flat cart). */
  onApplyAddOns?: (addOns: { productId: string; productName: string; price: number }[]) => void;
}

export function ModifierSelectionBottomSheet({
  visible,
  productId,
  productName,
  productPrice,
  modifierGroups: modifierGroupsProp,
  initialSelected = [],
  onClose,
  onApply,
  onApplyAddOns,
}: ModifierSelectionBottomSheetProps) {
  const [groups, setGroups] = useState<ModifierGroupDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [fetchError, setFetchError] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Katalogdan gelen gruplar varsa fetch yok; yoksa tek seferlik getProductModifierGroups (modal açıldığında)
  useEffect(() => {
    if (!visible || !productId) {
      setGroups([]);
      setFetchError(false);
      setSelectedIds(new Set());
      return;
    }
    setSelectedIds(new Set(initialSelected.map((m) => m.id)));
    setFetchError(false);
    if (Array.isArray(modifierGroupsProp) && modifierGroupsProp.length > 0) {
      setGroups(modifierGroupsProp);
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    getProductModifierGroups(productId)
      .then((data) => {
        if (!cancelled) {
          setGroups(data);
          setFetchError(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setGroups([]);
          setFetchError(true);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [visible, productId, modifierGroupsProp]);

  const toggleProduct = (group: ModifierGroupDto, p: AddOnGroupProductItemDto) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      const id = p.productId;
      const productIdsInGroup = (group.products ?? []).map((x) => x.productId);
      if (isSingleChoiceGroup(group)) {
        productIdsInGroup.forEach((pid) => next.delete(pid));
        if (!next.has(id)) next.add(id);
        return next;
      }
      if (next.has(id)) {
        next.delete(id);
        return next;
      }
      const max = group.maxSelections ?? Infinity;
      const countInGroup = productIdsInGroup.filter((pid) => next.has(pid)).length;
      if (countInGroup >= max) return prev;
      next.add(id);
      return next;
    });
  };

  const getSelectedAddOns = (): { productId: string; productName: string; price: number }[] => {
    const out: { productId: string; productName: string; price: number }[] = [];
    for (const g of groups) {
      for (const p of g.products ?? []) {
        if (selectedIds.has(p.productId)) out.push({ productId: p.productId, productName: p.productName, price: Number(p.price) });
      }
    }
    return out;
  };

  /** Phase C: only add-on products; legacy modifier selection removed. */
  const handleApply = () => {
    const addOns = getSelectedAddOns();
    if (addOns.length > 0 && onApplyAddOns) onApplyAddOns(addOns);
    if (onApply) onApply([]);
    onClose();
  };

  const addOnTotal = getSelectedAddOns().reduce((s, a) => s + a.price, 0);
  const extrasTotal = addOnTotal;
  const lineTotal = productPrice + extrasTotal;

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
          ) : fetchError ? (
            <View style={styles.emptyWrap}>
              <Text style={styles.emptyText}>Fehler beim Laden der Extras.</Text>
              <Text style={styles.emptySub}>Bitte erneut versuchen oder schließen.</Text>
            </View>
          ) : (() => {
            const groupsWithProducts = groups.filter((g) => (g.products ?? []).length > 0);
            return groupsWithProducts.length === 0 ? (
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
                {groupsWithProducts.map((group) => {
                  const singleChoice = isSingleChoiceGroup(group);
                  const minReq = group.minSelections ?? 0;
                  const requiredLabel = group.isRequired ? ` (${minReq} erforderlich)` : '';
                  return (
                    <View key={group.id} style={styles.group}>
                      <Text style={styles.groupName}>{group.name}{requiredLabel}</Text>
                      {(group.products ?? []).map((p) => {
                        const checked = selectedIds.has(p.productId);
                        return (
                          <Pressable
                            key={p.productId}
                            style={[styles.modifierRow, checked && styles.modifierRowChecked]}
                            onPress={() => toggleProduct(group, p)}
                          >
                            <View style={[singleChoice ? styles.radio : styles.checkbox, checked && (singleChoice ? styles.radioChecked : styles.checkboxChecked)]}>
                              {checked && <Text style={styles.checkmark}>✓</Text>}
                            </View>
                            <Text style={styles.modifierName} numberOfLines={1}>{p.productName}</Text>
                            <Text style={styles.modifierPrice}>€{Number(p.price).toFixed(2)}</Text>
                          </Pressable>
                        );
                      })}
                    </View>
                  );
                })}
              </ScrollView>
            );
          })()}

          <View style={styles.footer}>
            {selectedIds.size > 0 && (
              <Text style={styles.totalLine}>
                + Extras: €{extrasTotal.toFixed(2)} → Gesamt: €{lineTotal.toFixed(2)}
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
  radio: {
    width: 24,
    height: 24,
    borderRadius: 12,
    borderWidth: 2,
    borderColor: SoftColors.border,
    marginRight: SoftSpacing.md,
    alignItems: 'center',
    justifyContent: 'center',
  },
  radioChecked: {
    backgroundColor: SoftColors.accent,
    borderColor: SoftColors.accent,
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
