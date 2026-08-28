import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { injectSidebarFavoriteStars } from '@/features/menu/injectSidebarFavoriteStars';
import { I18nProvider } from '@/i18n';

describe('injectSidebarFavoriteStars', () => {
  it('wraps leaf labels with a hover star that toggles', async () => {
    const user = userEvent.setup();
    const onToggle = vi.fn();
    const items = injectSidebarFavoriteStars(
      [{ key: '/receipts', label: 'Belege' }],
      { isFavorite: (key) => key === '/receipts', onToggle, collapsed: false }
    );

    render(<I18nProvider>{items?.[0] && 'label' in items[0] ? items[0].label : null}</I18nProvider>);

    const star = screen.getByLabelText('Aus Favoriten entfernen');
    await user.click(star);
    expect(onToggle).toHaveBeenCalledWith('/receipts');
  });

  it('skips injection when collapsed', () => {
    const items = injectSidebarFavoriteStars([{ key: '/receipts', label: 'Belege' }], {
      isFavorite: () => false,
      onToggle: vi.fn(),
      collapsed: true,
    });
    expect(items?.[0]).toMatchObject({ key: '/receipts', label: 'Belege' });
  });
});
