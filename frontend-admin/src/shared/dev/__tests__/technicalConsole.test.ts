import { afterEach, describe, expect, it, vi } from 'vitest';

import { logger } from '@/lib/logger';
import {
  redactTechnicalLogArg,
  registerTechnicalErrorReporter,
  technicalConsole,
} from '@/shared/dev/technicalConsole';

describe('redactTechnicalLogArg', () => {
  it('redacts sensitive object keys', () => {
    expect(
      redactTechnicalLogArg({
        code: 'VALIDATION_ERROR',
        password: 'secret',
        accessToken: 'abc',
        nested: { refreshToken: 'xyz', ok: true },
      }),
    ).toEqual({
      code: 'VALIDATION_ERROR',
      password: '[REDACTED]',
      accessToken: '[REDACTED]',
      nested: { refreshToken: '[REDACTED]', ok: true },
    });
  });

  it('redacts JWT-shaped strings', () => {
    const jwt = 'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk';
    expect(redactTechnicalLogArg(jwt)).toBe('[REDACTED_JWT]');
  });

  it('maps Error to name/message only', () => {
    expect(redactTechnicalLogArg(new Error('boom'))).toEqual({
      name: 'Error',
      message: 'boom',
    });
  });
});

describe('technicalConsole / logger environment gating', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.restoreAllMocks();
    registerTechnicalErrorReporter(null);
  });

  it('warn and log emit structured records in development with redaction', () => {
    vi.stubEnv('NODE_ENV', 'development');
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const infoSpy = vi.spyOn(console, 'info').mockImplementation(() => {});

    technicalConsole.warn('probe', { token: 'secret-value', code: 'X' });
    logger.log('hello', { password: 'x' });

    expect(warnSpy).toHaveBeenCalledWith(
      '[regkasse-admin]',
      expect.objectContaining({
        level: 'warn',
        msg: 'probe',
        token: '[REDACTED]',
        code: 'X',
        service: 'frontend-admin',
        time: expect.any(String),
      }),
    );
    expect(infoSpy).toHaveBeenCalledWith(
      '[regkasse-admin]',
      expect.objectContaining({
        level: 'info',
        msg: 'hello',
        password: '[REDACTED]',
      }),
    );
  });

  it('warn and log stay silent in production', () => {
    vi.stubEnv('NODE_ENV', 'production');
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const infoSpy = vi.spyOn(console, 'info').mockImplementation(() => {});

    logger.warn('should-not-appear');
    logger.log('should-not-appear');

    expect(warnSpy).not.toHaveBeenCalled();
    expect(infoSpy).not.toHaveBeenCalled();
  });

  it('error emits in production and notifies the optional reporter', () => {
    vi.stubEnv('NODE_ENV', 'production');
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    const reporter = vi.fn();
    registerTechnicalErrorReporter(reporter);

    const err = new Error('critical');
    logger.error(err, { code: 'X' });

    expect(errorSpy).toHaveBeenCalledWith(
      '[regkasse-admin]',
      expect.objectContaining({
        level: 'error',
        msg: 'critical',
        code: 'X',
        service: 'frontend-admin',
      }),
    );
    expect(reporter).toHaveBeenCalledWith(
      err,
      expect.objectContaining({ args: expect.any(Array) }),
    );
  });

  it('child logger binds component context', () => {
    vi.stubEnv('NODE_ENV', 'development');
    const infoSpy = vi.spyOn(console, 'info').mockImplementation(() => {});

    logger.child({ component: 'LoginForm' }).info('ready');

    expect(infoSpy).toHaveBeenCalledWith(
      '[regkasse-admin]',
      expect.objectContaining({
        msg: 'ready',
        component: 'LoginForm',
      }),
    );
  });
});
