/**
 * Regression: settings hub “Zu RKSV” must target the App Router RKSV hub (/rksv), not the legacy menu key (/rksv/operations).
 */
import '@testing-library/jest-dom';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { SettingsHubContextPanel } from '@/features/settings/components/SettingsHubContextPanel';
import { I18nProvider } from '@/i18n';
import { RKSV_HUB_PATH } from '@/shared/adminSidebarNavigation';

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('SettingsHubContextPanel — RKSV link', () => {
  it('links to RKSV hub path /rksv (not legacy /rksv/operations)', () => {
    render(
      <I18nProvider>
        <SettingsHubContextPanel />
      </I18nProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: /Bereiche & Verweise/i }));
    const rksvLink = screen.getByRole('link', { name: /Zu RKSV|Open RKSV|RKSV/i });
    expect(rksvLink).toHaveAttribute('href', RKSV_HUB_PATH);
    expect(rksvLink.getAttribute('href')).not.toBe('/rksv/operations');
  });
});
