import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  LICENSE_SUPPORT_EMAIL,
  LICENSE_SUPPORT_PHONE_DEFAULT,
  buildLicenseSupportMailto,
  buildLicenseSupportTelHref,
  getConfiguredLicenseSupportChatUrl,
  getConfiguredLicenseSupportPhone,
  resolveLicenseSupportLiveChatTarget,
  resolveLicenseSupportPhoneTarget,
  resolveLicenseSupportTicketTarget,
} from '../licenseSupportOptions';

describe('licenseSupportOptions', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('builds mailto and tel hrefs', () => {
    const mail = buildLicenseSupportMailto({ subject: 'Test', body: 'Hello' });
    expect(mail).toContain(`mailto:${LICENSE_SUPPORT_EMAIL}`);
    expect(mail).toContain('subject=Test');
    expect(buildLicenseSupportTelHref('+43 1 234 5678')).toBe('tel:+4312345678');
  });

  it('uses chat env when set and falls back to mailto', () => {
    vi.stubEnv('NEXT_PUBLIC_LICENSE_SUPPORT_CHAT_URL', 'https://chat.example/live');
    expect(getConfiguredLicenseSupportChatUrl()).toBe('https://chat.example/live');
    expect(resolveLicenseSupportLiveChatTarget()).toEqual({
      href: 'https://chat.example/live',
      kind: 'external',
    });

    vi.stubEnv('NEXT_PUBLIC_LICENSE_SUPPORT_CHAT_URL', '');
    expect(resolveLicenseSupportLiveChatTarget().kind).toBe('mailto');
  });

  it('resolves ticket mailto and phone tel target', () => {
    expect(resolveLicenseSupportTicketTarget().href).toContain('Ticket');
    expect(getConfiguredLicenseSupportPhone()).toBe(LICENSE_SUPPORT_PHONE_DEFAULT);
    vi.stubEnv('NEXT_PUBLIC_LICENSE_SUPPORT_PHONE', '+43 99 888');
    expect(resolveLicenseSupportPhoneTarget().href).toBe('tel:+4399888');
  });
});
