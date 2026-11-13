import { describe, expect, it, vi, afterEach } from 'vitest';

import { redactTechnicalLogArg } from '@/lib/logging/redact';
import { LOG_LEVEL_NUM } from '@/lib/logging/types';
import {
  buildStructuredLogRecord,
  getBrowserLogExtras,
  writeStructuredToConsole,
} from '@/lib/logging/emitStructuredLog';

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

  it('merges Axios-like Error fields when Error is the first arg', () => {
    const err = Object.assign(new Error('Request failed with status code 500'), {
      isAxiosError: true,
      config: { url: '/api/admin/products', method: 'get' },
      response: { status: 500, data: { error: 'boom' } },
    });
    const record = buildStructuredLogRecord('error', [err, { component: 'ProductsPage' }]);
    expect(record.msg).toBe('Request failed with status code 500');
    expect(record.status).toBe(500);
    expect(record.endpoint).toBe('/api/admin/products');
    expect(record.method).toBe('GET');
    expect(record.component).toBe('ProductsPage');
    expect(record.data).toEqual({ error: 'boom' });
  });
});

describe('writeStructuredToConsole', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('never passes undefined as the console payload', () => {
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    writeStructuredToConsole('error', undefined);
    expect(errorSpy).toHaveBeenCalledWith(
      '[regkasse-admin]',
      expect.objectContaining({
        level: 'error',
        msg: '(missing log record)',
        service: 'frontend-admin',
      })
    );
  });
});

describe('getBrowserLogExtras', () => {
  it('reads pageUrl and optional dev tenant in jsdom', () => {
    window.localStorage.setItem('dev_tenant_id', 'dev');
    const extras = getBrowserLogExtras();
    expect(extras.pageUrl).toMatch(/^https?:\/\//);
    expect(extras.devTenant).toBe('dev');
    expect(typeof extras.userAgent).toBe('string');
  });
});

describe('redactTechnicalLogArg (logging package)', () => {
  it('redacts authorization header values by key', () => {
    expect(redactTechnicalLogArg({ authorization: 'Bearer abc' })).toEqual({
      authorization: '[REDACTED]',
    });
  });
});
