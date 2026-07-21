import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  CREATE_TENANT_WIZARD_DRAFT_KEY,
  buildCreateTenantWizardDraft,
  clampCreateTenantWizardDraftStep,
  clearCreateTenantWizardDraft,
  readCreateTenantWizardDraft,
  sanitizeCreateTenantWizardDataForDraft,
  writeCreateTenantWizardDraft,
} from '@/features/super-admin/components/CreateTenantWizard/createTenantWizardDraft';
import { createEmptyWizardData } from '@/features/super-admin/components/CreateTenantWizard/types';

describe('createTenantWizardDraft', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    window.localStorage.clear();
  });

  it('strips adminPassword from sanitized draft data', () => {
    const data = {
      ...createEmptyWizardData(),
      name: 'Cafe Muster',
      adminPassword: 'SuperSecret1!',
      passwordMode: 'manual' as const,
    };
    const sanitized = sanitizeCreateTenantWizardDataForDraft(data);
    expect(sanitized.adminPassword).toBe('');
    expect(sanitized.passwordMode).toBe('manual');
    expect(sanitized.name).toBe('Cafe Muster');
  });

  it('clamps step index to form steps only', () => {
    expect(clampCreateTenantWizardDraftStep(-1)).toBe(0);
    expect(clampCreateTenantWizardDraftStep(2.9)).toBe(2);
    expect(clampCreateTenantWizardDraftStep(99)).toBe(3);
  });

  it('persists and restores draft without password', () => {
    writeCreateTenantWizardDraft(
      {
        ...createEmptyWizardData(),
        name: 'Cafe Muster GmbH',
        slug: 'cafe-muster',
        email: 'info@cafe-muster.at',
        adminEmail: 'admin@cafe-muster.at',
        adminPassword: 'MustNotPersist1!',
        passwordMode: 'manual',
      },
      2
    );

    const raw = window.localStorage.getItem(CREATE_TENANT_WIZARD_DRAFT_KEY);
    expect(raw).toBeTruthy();
    expect(raw).not.toContain('MustNotPersist1!');

    const draft = readCreateTenantWizardDraft();
    expect(draft).not.toBeNull();
    expect(draft?.stepIndex).toBe(2);
    expect(draft?.data.name).toBe('Cafe Muster GmbH');
    expect(draft?.data.adminPassword).toBe('');
    expect(draft?.data.passwordMode).toBe('manual');
  });

  it('returns null for empty drafts', () => {
    writeCreateTenantWizardDraft(createEmptyWizardData(), 0);
    expect(readCreateTenantWizardDraft()).toBeNull();
  });

  it('clears draft storage', () => {
    writeCreateTenantWizardDraft(
      { ...createEmptyWizardData(), name: 'X', slug: 'x', email: 'a@b.at' },
      1
    );
    clearCreateTenantWizardDraft();
    expect(readCreateTenantWizardDraft()).toBeNull();
  });

  it('buildCreateTenantWizardDraft always sets version and empty password', () => {
    const built = buildCreateTenantWizardDraft(
      { ...createEmptyWizardData(), name: 'A', adminPassword: 'secret' },
      1,
      '2026-07-20T00:00:00.000Z'
    );
    expect(built.version).toBe(1);
    expect(built.updatedAt).toBe('2026-07-20T00:00:00.000Z');
    expect(built.data.adminPassword).toBe('');
  });
});
