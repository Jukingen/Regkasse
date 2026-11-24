import { describe, expect, it } from '@jest/globals';

import {
  isEditableTarget,
  POS_FUNCTION_KEYS,
  resolvePosShortcut,
} from '../utils/posKeyboardShortcuts';

const free = { editableTarget: false };
const typing = { editableTarget: true };

describe('resolvePosShortcut', () => {
  it('maps F1 to F6 to the POS actions', () => {
    expect(resolvePosShortcut({ key: 'F1' }, free)).toBe('help');
    expect(resolvePosShortcut({ key: 'F2' }, free)).toBe('customer');
    expect(resolvePosShortcut({ key: 'F3' }, free)).toBe('receipts');
    expect(resolvePosShortcut({ key: 'F4' }, free)).toBe('kitchen');
    expect(resolvePosShortcut({ key: 'F5' }, free)).toBe('clearCart');
    expect(resolvePosShortcut({ key: 'F6' }, free)).toBe('pay');
    expect(POS_FUNCTION_KEYS.map((entry) => entry.key)).toEqual(['F1', 'F2', 'F3', 'F4', 'F5', 'F6']);
  });

  it('maps Escape and ? and leaves other keys alone', () => {
    expect(resolvePosShortcut({ key: 'Escape' }, free)).toBe('escape');
    expect(resolvePosShortcut({ key: '?', shiftKey: true }, free)).toBe('help');
    expect(resolvePosShortcut({ key: 'F7' }, free)).toBeNull();
    expect(resolvePosShortcut({ key: 'a' }, free)).toBeNull();
    expect(resolvePosShortcut({ key: 'Enter' }, free)).toBeNull();
  });

  it('keeps ? for typing but lets F-keys and Escape through while an input is focused', () => {
    expect(resolvePosShortcut({ key: '?' }, typing)).toBeNull();
    expect(resolvePosShortcut({ key: 'F6' }, typing)).toBe('pay');
    expect(resolvePosShortcut({ key: 'Escape' }, typing)).toBe('escape');
  });

  it('never steals browser combos, auto-repeat or IME input', () => {
    expect(resolvePosShortcut({ key: 'F5', ctrlKey: true }, free)).toBeNull();
    expect(resolvePosShortcut({ key: 'F5', metaKey: true }, free)).toBeNull();
    expect(resolvePosShortcut({ key: 'F4', altKey: true }, free)).toBeNull();
    expect(resolvePosShortcut({ key: 'F5', shiftKey: true }, free)).toBeNull();
    expect(resolvePosShortcut({ key: 'F6', repeat: true }, free)).toBeNull();
    expect(resolvePosShortcut({ key: 'Escape', isComposing: true }, free)).toBeNull();
  });
});

describe('isEditableTarget', () => {
  it('detects inputs, textareas, selects and contenteditable', () => {
    expect(isEditableTarget({ tagName: 'INPUT' })).toBe(true);
    expect(isEditableTarget({ tagName: 'textarea' })).toBe(true);
    expect(isEditableTarget({ tagName: 'SELECT' })).toBe(true);
    expect(isEditableTarget({ tagName: 'DIV', isContentEditable: true })).toBe(true);
  });

  it('ignores everything else', () => {
    expect(isEditableTarget({ tagName: 'DIV' })).toBe(false);
    expect(isEditableTarget({ tagName: 'BUTTON' })).toBe(false);
    expect(isEditableTarget(null)).toBe(false);
    expect(isEditableTarget('input')).toBe(false);
  });
});
