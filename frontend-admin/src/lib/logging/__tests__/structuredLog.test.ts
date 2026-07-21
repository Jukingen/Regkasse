import { describe, expect, it } from 'vitest';

import { redactTechnicalLogArg } from '@/lib/logging/redact';
import { LOG_LEVEL_NUM } from '@/lib/logging/types';
import { buildStructuredLogRecord } from '@/lib/logging/emitStructuredLog';

describe('buildStructuredLogRecord', () => {
  it('includes timestamp, level, message, and service', () => {
    const record = buildStructuredLogRecord('info', ['hello', { component: 'Test' }]);
    expect(record.time).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    expect(record.level).toBe('info');
    expect(record.levelNum).toBe(LOG_LEVEL_NUM.info);
    expect(record.msg).toBe('hello');
    expect(record.service).toBe('frontend-admin');
    expect(record.component).toBe('Test');
  });

  it('redacts secrets inside field objects', () => {
    const record = buildStructuredLogRecord('warn', ['x', { password: 'nope', ok: 1 }]);
    expect(record.password).toBe('[REDACTED]');
    expect(record.ok).toBe(1);
  });
});

describe('redactTechnicalLogArg (logging package)', () => {
  it('redacts authorization header values by key', () => {
    expect(redactTechnicalLogArg({ authorization: 'Bearer abc' })).toEqual({
      authorization: '[REDACTED]',
    });
  });
});
