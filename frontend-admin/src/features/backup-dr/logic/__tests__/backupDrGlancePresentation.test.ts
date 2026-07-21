import { describe, expect, it } from 'vitest';

import {
  REAL_DUMP_PATH_BANNER_ALERT_TYPE,
  mapEvidenceHeadlineToneToAlertType,
  mapExternalCopyVariantToAlertType,
  mapOperatorValidityStripToAlertType,
} from '@/features/backup-dr/logic/backupDrGlancePresentation';

describe('backupDrGlancePresentation', () => {
  it('maps operator validity “success” semantics to info Alert (no green completion cue)', () => {
    expect(mapOperatorValidityStripToAlertType('success')).toBe('info');
  });

  it('preserves warning strip as warning Alert', () => {
    expect(mapOperatorValidityStripToAlertType('warning')).toBe('warning');
  });

  it('maps info strip to info Alert', () => {
    expect(mapOperatorValidityStripToAlertType('info')).toBe('info');
  });

  it('maps evidence headline success tone to info Alert (strongWithinApi text unchanged; frame not green)', () => {
    expect(mapEvidenceHeadlineToneToAlertType('success')).toBe('info');
  });

  it('maps evidence warning tone to warning Alert', () => {
    expect(mapEvidenceHeadlineToneToAlertType('warning')).toBe('warning');
  });

  it('maps neutral and info tones to info Alert', () => {
    expect(mapEvidenceHeadlineToneToAlertType('info')).toBe('info');
    expect(mapEvidenceHeadlineToneToAlertType('neutral')).toBe('info');
  });

  it('real dump path banner uses info (prerequisite, not recovery proof)', () => {
    expect(REAL_DUMP_PATH_BANNER_ALERT_TYPE).toBe('info');
  });
});

describe('mapExternalCopyVariantToAlertType', () => {
  it('treats external lifecycle metadata as warning (not neutral “info”)', () => {
    expect(mapExternalCopyVariantToAlertType('externalLifecycleOk')).toBe('warning');
  });

  it('keeps staging and unknown as info', () => {
    expect(mapExternalCopyVariantToAlertType('staging')).toBe('info');
    expect(mapExternalCopyVariantToAlertType('unknown')).toBe('info');
  });
});
