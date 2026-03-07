/**
 * POS: Modern add-on modal (Square/Toast style). Source: group.products only.
 * Radio when min=1 & max=1; checkbox when max>1. Catalog-first via modifierGroups prop.
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
  type AddOnSelection,
} from '../services/api/productModifiersService';
import {
  getGroupControlType,
  toggleSelectionInGroup,
  isOptionDisabled,
} from '../utils/modifierSelectionUtils';
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
  /** Catalog-first: pass product.modifierGroups to avoid extra API call. */
  modifierGroups?: ModifierGroupDto[] | null;
  onClose: () => void;
  /** @deprecated Phase C: not used; add-on flow only via onAddAddOns. Kept for type compat. */
  onAdd?: (selectedModifiers: SelectedModifier[]) => void;
  /** Add-on product selection – each becomes a separate cart line (flat cart). */
  onAddAddOns?: (addOns: AddOnSelection[]) => void;
}

export function ModifierSelectionModal({
  visible,
  productId,
  productName,
  productPrice,
  modifierGroups: modifierGroupsProp,
  onClose,
  onAdd,
  onAddAddOns,
}: ModifierSelectionModalProps) {
  const [groups, setGroups] = useState<ModifierGroupDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [fetchError, setFetchError] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (!visible || !productId) {
      setGroups([]);
      setFetchError(false);
      setSelectedIds(new Set());
      return;
    }
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
    return () => { cancelled = true; };
  }, [visible, productId, modifierGroupsProp]);

  const toggleProduct = (group: ModifierGroupDto, p: AddOnGroupProductItemDto) => {
    setSelectedIds((prev) => toggleSelectionInGroup(prev, group, p.productId));
  };

  const getSelectedAddOns = (): AddOnSelection[] => {
    const out: AddOnSelection[] = [];
    for (const g of groups) {
      for (const p of g.products ?? []) {
        if (selectedIds.has(p.productId)) out.push({ productId: p.productId, productName: p.productName, price: Number(p.price) });
      }
    }
    return out;
  };

  /** Phase C: add-on products only; no legacy modifier branch. */
  const handleAdd = () => {
    const addOns = getSelectedAddOns();
    if (addOns.length > 0 && onAddAddOns) onAddAddOns(addOns);
    onClose();
  };

  const addOnTotal = getSelectedAddOns().reduce((s, a) => s + a.price, 0);
  const extrasTotal = addOnTotal;
  const lineTotal = productPrice + extrasTotal;

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
          ) : fetchError ? (
            <View style={styles.emptyWrap}>
              <Text style={styles.emptyText}>Fehler beim Laden der Extras.</Text>
              <Text style={styles.emptySub}>Bitte erneut versuchen oder schließen.</Text>
            </View>
          ) : (() => {
            const groupsWithProducts = groups.filter((g) => (g.products ?? []).length > 0);
            return groupsWithProducts.length === 0 ? (
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
                {groupsWithProducts.map((group) => {
                  const singleChoice = getGroupControlType(group) === 'radio';
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
                            disabled={isOptionDisabled(group, selectedIds, p.productId)}
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
