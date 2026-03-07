/**
 * Pure POS modifier/add-on selection and validation helpers.
 * No UI; use for radio/checkbox behavior, toggling, and validation.
 */

/** Minimal group shape for selection logic (products = add-on options). */
export interface ModifierGroupSelectionShape {
  id: string;
  minSelections: number;
  maxSelections: number | null;
  isRequired?: boolean;
  products?: Array<{ productId: string }>;
}

/** Result of validating a single group. */
export interface GroupValidationResult {
  valid: boolean;
  message?: string;
}

/** Result of validating all groups. */
export interface AllGroupsValidationResult {
  valid: boolean;
  errors: Array<{ groupId: string; message: string }>;
}

/**
 * Control type for the group: radio (exactly one) or checkbox (zero or more up to max).
 */
export function getGroupControlType(
  group: ModifierGroupSelectionShape
): 'radio' | 'checkbox' {
  const min = group.minSelections ?? 0;
  const max = group.maxSelections;
  return min === 1 && max === 1 ? 'radio' : 'checkbox';
}

/**
 * Returns the set of selected option IDs after toggling the given option in the group.
 * For radio: clears other options in the group and adds this one (or removes if already selected).
 * For checkbox: toggles this option, respecting maxSelections.
 */
export function toggleSelectionInGroup(
  currentSelected: Set<string>,
  group: ModifierGroupSelectionShape,
  optionId: string
): Set<string> {
  const next = new Set(currentSelected);
  const optionIdsInGroup = (group.products ?? []).map((p) => p.productId);
  const isRadio = getGroupControlType(group) === 'radio';

  if (isRadio) {
    optionIdsInGroup.forEach((id) => next.delete(id));
    if (!currentSelected.has(optionId)) next.add(optionId);
    return next;
  }

  if (next.has(optionId)) {
    next.delete(optionId);
    return next;
  }

  const max = group.maxSelections ?? Infinity;
  const countInGroup = optionIdsInGroup.filter((id) => next.has(id)).length;
  if (countInGroup >= max) return currentSelected;

  next.add(optionId);
  return next;
}

/**
 * True when the option cannot be selected because the group has reached maxSelections
 * and this option is not already selected.
 */
export function isOptionDisabled(
  group: ModifierGroupSelectionShape,
  selectedIds: Set<string>,
  optionId: string
): boolean {
  if (selectedIds.has(optionId)) return false;
  const max = group.maxSelections;
  if (max == null) return false;
  const optionIdsInGroup = (group.products ?? []).map((p) => p.productId);
  const countInGroup = optionIdsInGroup.filter((id) => selectedIds.has(id)).length;
  return countInGroup >= max;
}

/**
 * True when the group effectively requires at least one selection (minSelections > 0 or isRequired).
 */
export function isGroupRequired(group: ModifierGroupSelectionShape): boolean {
  const min = group.minSelections ?? 0;
  return min > 0 || Boolean(group.isRequired);
}

/**
 * Validates a single group: selection count must be between minSelections and maxSelections.
 */
export function validateGroup(
  group: ModifierGroupSelectionShape,
  selectedIds: Set<string>
): GroupValidationResult {
  const optionIdsInGroup = (group.products ?? []).map((p) => p.productId);
  const count = optionIdsInGroup.filter((id) => selectedIds.has(id)).length;
  const min = group.minSelections ?? 0;
  const max = group.maxSelections;

  if (count < min) {
    return {
      valid: false,
      message: min === 1 ? 'Bitte eine Option wählen.' : `Mindestens ${min} Optionen wählen.`,
    };
  }
  if (max != null && count > max) {
    return {
      valid: false,
      message: max === 1 ? 'Nur eine Option erlaubt.' : `Maximal ${max} Optionen erlaubt.`,
    };
  }
  return { valid: true };
}

/**
 * Validates all groups. Returns valid: false and the first set of errors if any group fails.
 */
export function validateAllGroups(
  groups: ModifierGroupSelectionShape[],
  selectedIds: Set<string>
): AllGroupsValidationResult {
  const errors: Array<{ groupId: string; message: string }> = [];
  for (const group of groups) {
    const result = validateGroup(group, selectedIds);
    if (!result.valid && result.message) {
      errors.push({ groupId: group.id, message: result.message });
    }
  }
  return {
    valid: errors.length === 0,
    errors,
  };
}
