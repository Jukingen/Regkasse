/**
 * Role presets: getPresetKeysInCatalog filtering, preset apply semantics (replace selection).
 */
import { describe, expect, it } from 'vitest';

import {
  PRESET_HR_MANAGER,
  PRESET_KASA_OPERASYON,
  PRESET_MAGAZA_YONETICISI,
  PRESET_RAPOR_GORUNTULEME,
  PRESET_READ_ONLY,
  ROLE_PRESETS,
  findRolePresetById,
  getPresetKeysInCatalog,
  getRolePresetPreview,
} from '../rolePresets';

describe('rolePresets', () => {
  describe('getPresetKeysInCatalog', () => {
    it('returns only preset keys that exist in catalog', () => {
      const catalog = new Set(['sale.view', 'order.create', 'cart.view', 'report.view']);
      const result = getPresetKeysInCatalog(PRESET_KASA_OPERASYON, catalog);
      expect(result).toContain('sale.view');
      expect(result).toContain('order.create');
      expect(result).toContain('cart.view');
      expect(result).not.toContain('tse.sign');
      expect(result.length).toBeLessThanOrEqual(PRESET_KASA_OPERASYON.permissionKeys.length);
    });

    it('returns empty array when catalog is empty', () => {
      const result = getPresetKeysInCatalog(PRESET_RAPOR_GORUNTULEME, new Set<string>());
      expect(result).toEqual([]);
    });

    it('accepts catalog as string array', () => {
      const catalog = ['report.view', 'audit.view'];
      const result = getPresetKeysInCatalog(PRESET_RAPOR_GORUNTULEME, catalog);
      expect(result).toContain('report.view');
      expect(result).toContain('audit.view');
    });
  });

  describe('preset catalog', () => {
    it('includes store-manager, read-only, and hr-manager templates', () => {
      expect(findRolePresetById('magaza-yoneticisi')).toBe(PRESET_MAGAZA_YONETICISI);
      expect(findRolePresetById('read-only')).toBe(PRESET_READ_ONLY);
      expect(findRolePresetById('hr-manager')).toBe(PRESET_HR_MANAGER);
    });

    it('each preset has description and at least one permission key', () => {
      for (const preset of ROLE_PRESETS) {
        expect(preset.permissionKeys.length).toBeGreaterThan(0);
        expect(preset.id).toBeTruthy();
        expect(preset.label).toBeTruthy();
        expect(preset.description.length).toBeGreaterThan(0);
      }
    });
  });

  describe('getRolePresetPreview', () => {
    it('returns permission count, highlights, and group distribution', () => {
      const catalog = [
        ...PRESET_READ_ONLY.permissionKeys,
        'sale.manage', // not in preset
      ];
      const preview = getRolePresetPreview(PRESET_READ_ONLY, catalog);
      expect(preview.permissionCount).toBe(PRESET_READ_ONLY.permissionKeys.length);
      expect(preview.highlightKeys.length).toBeGreaterThan(0);
      expect(preview.groupDistribution.length).toBeGreaterThan(0);
      expect(preview.groupDistribution.every((g) => g.count > 0)).toBe(true);
    });

    it('filters to catalog when provided', () => {
      const preview = getRolePresetPreview(PRESET_HR_MANAGER, ['user.view', 'role.view']);
      expect(preview.permissionCount).toBe(2);
      expect(preview.highlightKeys).toEqual(expect.arrayContaining(['role.view']));
    });
  });

  describe('preset apply semantics', () => {
    it('applying preset replaces current selection (draft becomes preset keys in catalog)', () => {
      const catalogSet = new Set(['sale.view', 'report.view', 'audit.view', 'invoice.view']);
      const applied = getPresetKeysInCatalog(PRESET_RAPOR_GORUNTULEME, catalogSet);
      expect(applied).toEqual(
        expect.arrayContaining(['report.view', 'audit.view', 'sale.view', 'invoice.view'])
      );
      expect(new Set(applied).size).toBe(applied.length);
    });
  });

  describe('dirty-state', () => {
    it('draft differs from saved when preset applied and preset keys differ from saved', () => {
      const catalogSet = new Set(['sale.view', 'report.view']);
      const savedPermissions = ['sale.view'];
      const appliedDraft = getPresetKeysInCatalog(PRESET_RAPOR_GORUNTULEME, catalogSet);
      const savedSet = new Set(savedPermissions);
      const draftSet = new Set(appliedDraft);
      const dirty =
        draftSet.size !== savedSet.size ||
        Array.from(draftSet).some((p) => !savedSet.has(p)) ||
        Array.from(savedSet).some((p) => !draftSet.has(p));
      expect(dirty).toBe(true);
    });
  });

  describe('manual override after preset', () => {
    it('after preset apply, adding a key yields draft = preset keys + new key', () => {
      const catalogSet = new Set(['report.view', 'audit.view']);
      const applied = getPresetKeysInCatalog(PRESET_RAPOR_GORUNTULEME, catalogSet);
      const draftAfterPreset = new Set(applied);
      draftAfterPreset.add('invoice.view');
      expect(draftAfterPreset.has('invoice.view')).toBe(true);
      expect(draftAfterPreset.size).toBe(applied.length + 1);
    });

    it('after preset apply, removing a key yields draft = preset keys minus one', () => {
      const catalogSet = new Set(['report.view', 'audit.view', 'sale.view']);
      const applied = getPresetKeysInCatalog(PRESET_RAPOR_GORUNTULEME, catalogSet);
      const draftAfterPreset = new Set(applied);
      draftAfterPreset.delete('audit.view');
      expect(draftAfterPreset.has('audit.view')).toBe(false);
      expect(draftAfterPreset.size).toBe(applied.length - 1);
    });
  });
});
