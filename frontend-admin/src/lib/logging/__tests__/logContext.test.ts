import { afterEach, describe, expect, it } from 'vitest';

import {
  bindLogContext,
  clearLogContext,
  compactLogContext,
  getLogContext,
  getOrCreateClientSessionId,
} from '@/lib/logging/logContext';
import {
  buildStructuredLogRecord,
  shouldEmitToConsole,
} from '@/lib/logging/emitStructuredLog';

describe('logContext', () => {
  afterEach(() => {
    clearLogContext();
  });

  it('binds and clears ambient context', () => {
    bindLogContext({ component: 'Test', userId: 'u1', tenantId: null });
    expect(getLogContext()).toMatchObject({ component: 'Test', userId: 'u1' });
    clearLogContext();
    expect(getLogContext()).toEqual({});
  });

  it('compacts empty values', () => {
    expect(compactLogContext({ component: ' X ', userId: '', sessionId: null })).toEqual({
      component: 'X',
    });
  });

  it('creates a client session id in jsdom', () => {
    const a = getOrCreateClientSessionId();
    const b = getOrCreateClientSessionId();
    expect(a).toBeTruthy();
    expect(a).toBe(b);
  });
});

describe('emitStructuredLog helpers', () => {
  it('builds records with Error first arg', () => {
    const record = buildStructuredLogRecord('error', [new Error('boom'), { code: 'X' }]);
    expect(record.msg).toBe('boom');
    expect(record.code).toBe('X');
    expect(record.level).toBe('error');
  });

  it('gates console emit by environment', () => {
    expect(shouldEmitToConsole('error')).toBe(true);
  });
});
