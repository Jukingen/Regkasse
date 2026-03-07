/**
 * POS: Add-on selection bottom sheet. Renders only group.products (no legacy modifiers).
 * Structure: ModifierSelectionBottomSheet → ModifierGroupSection → ModifierOptionRow.
 * Uses modifierSelectionUtils for radio/checkbox, toggling, validation, and disabled state.
 */
import React, { useEffect, useState, useCallback } from 'react';
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
  validateAllGroups,
} from '../utils/modifierSelectionUtils';
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
  /** Catalog-first: pass product.modifierGroups to avoid extra API call. */
  modifierGroups?: ModifierGroupDto[] | null;
  /** Initial selected option ids (e.g. from pending state). */
  initialSelected?: SelectedModifier[];
  onClose: () => void;
  /** Legacy callback; kept for compatibility. */
  onApply?: (selected: SelectedModifier[]) => void;
  /** Primary: add-on product selection – each becomes a separate cart line (flat cart). */
  onApplyAddOns?: (addOns: AddOnSelection[]) => void;
  /** Single callback: base + add-ons; one cart line per item (addItemWithAddOns). */
  onApplyWithBase?: (base: { productId: string; productName: string; price: number }, addOns: AddOnSelection[]) => void;
}

// ---------------------------------------------------------------------------
// ModifierOptionRow – single add-on option (radio or checkbox + name + price delta)
// ---------------------------------------------------------------------------
interface ModifierOptionRowProps {
  productName: string;
  priceDelta: number;
  checked: boolean;
  disabled: boolean;
  isRadio: boolean;
  onPress: () => void;
}

function ModifierOptionRow({
  productName,
  priceDelta,
  checked,
  disabled,
  isRadio,
  onPress,
}: ModifierOptionRowProps) {
  const controlStyle = isRadio
    ? [styles.radio, checked && styles.radioChecked]
    : [styles.checkbox, checked && styles.checkboxChecked];
  return (
    <Pressable
      style={[
        styles.optionRow,
        checked && styles.optionRowChecked,
        disabled && styles.optionRowDisabled,
      ]}
      onPress={onPress}
      disabled={disabled}
      accessibilityRole={isRadio ? 'radio' : 'checkbox'}
      accessibilityState={{ checked, disabled }}
    >
      <View style={controlStyle}>
        {checked && <Text style={styles.checkmark}>✓</Text>}
      </View>
      <Text style={[styles.optionName, disabled && styles.optionNameDisabled]} numberOfLines={2}>
        {productName}
      </Text>
      <Text style={[styles.optionPrice, disabled && styles.optionPriceDisabled]}>
        {priceDelta >= 0 ? `+€${priceDelta.toFixed(2)}` : `€${priceDelta.toFixed(2)}`}
      </Text>
    </Pressable>
  );
}

// ---------------------------------------------------------------------------
// ModifierGroupSection – one modifier group (title + hint + option rows from group.products only)
// ---------------------------------------------------------------------------
interface ModifierGroupSectionProps {
  group: ModifierGroupDto;
  selectedIds: Set<string>;
  onToggle: (optionId: string) => void;
}

function ModifierGroupSection({ group, selectedIds, onToggle }: ModifierGroupSectionProps) {
  const products = group.products ?? [];
  const isRadio = getGroupControlType(group) === 'radio';
  const min = group.minSelections ?? 0;
  const max = group.maxSelections;
  const required = min > 0 || Boolean(group.isRequired);

  const hintParts: string[] = [];
  if (required && min > 0) hintParts.push(`${min} erforderlich`);
  if (max != null && max > 1) hintParts.push(`max. ${max}`);
  const hint = hintParts.length > 0 ? ` (${hintParts.join(', ')})` : '';

  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>
        {group.name}
        {hint ? <Text style={styles.sectionHint}>{hint}</Text> : null}
      </Text>
      {products.map((p) => (
        <ModifierOptionRow
          key={p.productId}
          productName={p.productName}
          priceDelta={Number(p.price)}
          checked={selectedIds.has(p.productId)}
          disabled={isOptionDisabled(group, selectedIds, p.productId)}
          isRadio={isRadio}
          onPress={() => onToggle(p.productId)}
        />
      ))}
    </View>
  );
}

// ---------------------------------------------------------------------------
// ModifierSelectionBottomSheet – container, header, scroll, sticky footer
// ---------------------------------------------------------------------------
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
  onApplyWithBase,
}: ModifierSelectionBottomSheetProps) {
  const [groups, setGroups] = useState<ModifierGroupDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [fetchError, setFetchError] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    if (!visible || !productId) {
      setGroups([]);
      setFetchError(false);
      setSelectedIds(new Set());
      setValidationError(null);
      return;
    }
    setSelectedIds(new Set(initialSelected.map((m) => m.id)));
    setValidationError(null);
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
  }, [visible, productId, modifierGroupsProp, initialSelected]);

  const groupsWithProducts = groups.filter((g) => (g.products ?? []).length > 0);

  const toggleOption = useCallback((group: ModifierGroupDto, optionId: string) => {
    setSelectedIds((prev) => toggleSelectionInGroup(prev, group, optionId));
    setValidationError(null);
  }, []);

  const getSelectedAddOns = useCallback((): AddOnSelection[] => {
    const out: AddOnSelection[] = [];
    const withProducts = groups.filter((g) => (g.products ?? []).length > 0);
    for (const g of withProducts) {
      for (const p of g.products ?? []) {
        if (selectedIds.has(p.productId)) {
          out.push({
            productId: p.productId,
            productName: p.productName,
            price: Number(p.price),
          });
        }
      }
    }
    return out;
  }, [groups, selectedIds]);

  const handleApply = useCallback(() => {
    setValidationError(null);
    const validation = validateAllGroups(groupsWithProducts, selectedIds);
    if (!validation.valid) {
      const msg = validation.errors[0]?.message;
      if (msg) setValidationError(msg);
      return;
    }
    const addOns = getSelectedAddOns();
    if (onApplyWithBase) {
      onApplyWithBase({ productId, productName, price: productPrice }, addOns);
      onClose();
      return;
    }
    if (addOns.length > 0 && onApplyAddOns) onApplyAddOns(addOns);
    if (onApply) onApply([]);
    onClose();
  }, [productId, productName, productPrice, groupsWithProducts, selectedIds, getSelectedAddOns, onApplyWithBase, onApplyAddOns, onApply, onClose]);

  const extrasTotal = getSelectedAddOns().reduce((s, a) => s + a.price, 0);
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
            <Text style={styles.productName} numberOfLines={2}>
              {productName}
            </Text>
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
          ) : groupsWithProducts.length === 0 ? (
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
              {groupsWithProducts.map((group) => (
                <ModifierGroupSection
                  key={group.id}
                  group={group}
                  selectedIds={selectedIds}
                  onToggle={(optionId) => toggleOption(group, optionId)}
                />
              ))}
            </ScrollView>
          )}

          <View style={styles.footer}>
            {validationError ? (
              <Text style={styles.validationError}>{validationError}</Text>
            ) : null}
            {selectedIds.size > 0 && !validationError ? (
              <Text style={styles.totalLine}>
                + Extras: €{extrasTotal.toFixed(2)} → Gesamt: €{lineTotal.toFixed(2)}
              </Text>
            ) : null}
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
  section: {
    marginBottom: SoftSpacing.lg,
  },
  sectionTitle: {
    ...SoftTypography.label,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.sm,
    textTransform: 'uppercase',
  },
  sectionHint: {
    fontWeight: '400',
    textTransform: 'none',
  },
  optionRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.sm,
    borderRadius: SoftRadius.sm,
    marginBottom: 2,
  },
  optionRowChecked: {
    backgroundColor: SoftColors.accentLight + '40',
  },
  optionRowDisabled: {
    opacity: 0.5,
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
  optionName: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
    flex: 1,
    marginRight: SoftSpacing.sm,
  },
  optionNameDisabled: {
    color: SoftColors.textMuted,
  },
  optionPrice: {
    ...SoftTypography.body,
    color: SoftColors.accentDark,
  },
  optionPriceDisabled: {
    color: SoftColors.textMuted,
  },
  footer: {
    paddingHorizontal: SoftSpacing.lg,
    paddingTop: SoftSpacing.md,
    borderTopWidth: 1,
    borderTopColor: SoftColors.border,
  },
  validationError: {
    ...SoftTypography.bodySmall,
    color: SoftColors.error,
    marginBottom: SoftSpacing.sm,
  },
  totalLine: {
    ...SoftTypography.bodySmall,
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
