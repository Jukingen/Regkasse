import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { CreateTicketModal } from '@/features/support-tickets/components/CreateTicketModal';
import { I18nProvider } from '@/i18n';

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
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('CreateTicketModal', () => {
  it('submits subject, category, priority, and message', async () => {
    const onSubmit = vi.fn();

    render(
      <I18nProvider>
        <CreateTicketModal open loading={false} onCancel={() => undefined} onSubmit={onSubmit} />
      </I18nProvider>
    );

    await userEvent.type(await screen.findByLabelText('Betreff'), 'Bondrucker defekt');
    await userEvent.type(
      screen.getByLabelText('Nachricht'),
      'Der Bondrucker reagiert nicht mehr.'
    );
    await userEvent.click(screen.getByRole('button', { name: 'Neues Ticket' }));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({
        category: 'Technical',
        priority: 'Medium',
        title: 'Bondrucker defekt',
        message: 'Der Bondrucker reagiert nicht mehr.',
      })
    );
  });
});
