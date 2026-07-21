import { describe, expect, it } from 'vitest';

import {
  assertRksvPublicEnvironmentForProductionBuild,
  stripBomAndTrimRksvEnv,
} from '../../../../scripts/assertRksvPublicEnvironmentForBuild.mjs';

describe('assertRksvPublicEnvironmentForProductionBuild (next.config gate)', () => {
  it('no-ops when argv does not include build', () => {
    expect(() =>
      assertRksvPublicEnvironmentForProductionBuild({
        argv: ['node', 'next', 'dev'],
        envValue: undefined,
      })
    ).not.toThrow();
  });

  it('allows TEST and PROD (case-insensitive, BOM trimmed)', () => {
    expect(() =>
      assertRksvPublicEnvironmentForProductionBuild({
        argv: ['node', 'next', 'build'],
        envValue: 'test',
      })
    ).not.toThrow();
    expect(() =>
      assertRksvPublicEnvironmentForProductionBuild({
        argv: ['node', 'next', 'build'],
        envValue: '\uFEFFPROD',
      })
    ).not.toThrow();
  });

  it('throws when unset or empty during build', () => {
    expect(() =>
      assertRksvPublicEnvironmentForProductionBuild({
        argv: ['node', 'next', 'build'],
        envValue: undefined,
      })
    ).toThrow(/must be TEST or PROD/);
    expect(() =>
      assertRksvPublicEnvironmentForProductionBuild({
        argv: ['node', 'next', 'build'],
        envValue: '   ',
      })
    ).toThrow(/\(unset or empty\)/);
  });

  it('throws when value is not TEST|PROD during build', () => {
    expect(() =>
      assertRksvPublicEnvironmentForProductionBuild({
        argv: ['node', 'next', 'build'],
        envValue: 'STAGING',
      })
    ).toThrow(/"STAGING"/);
  });

  it('stripBomAndTrimRksvEnv matches client parser trimming', () => {
    expect(stripBomAndTrimRksvEnv('\uFEFF TEST ')).toBe('TEST');
    expect(stripBomAndTrimRksvEnv(undefined)).toBe('');
  });
});
