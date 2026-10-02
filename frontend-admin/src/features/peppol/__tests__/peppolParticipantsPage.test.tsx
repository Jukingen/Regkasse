import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { PeppolParticipantsPage } from '@/features/peppol/PeppolParticipantsPage';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

const tenantId = '11111111-1111-1111-1111-111111111111';
const post = vi.fn();

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ success: vi.fn(), error: vi.fn(), apiError: vi.fn() }),
}));

vi.mock('@/features/super-admin/api/adminTenants', () => ({
  listAdminTenants: vi.fn(async () => [{ id: tenantId, name: 'Cafe Muster', slug: 'cafe-muster' }]),
}));

vi.mock('@/api/generated/admin/admin', () => ({
  useGetApiAdminPeppolParticipantsTenantId: (id: string) => ({
    data: id
      ? [
          {
            id: 'row-1',
            tenantId: id,
            participantId: '0088:123456789',
            apEnvironment: 'TEST',
            legalEntityId: 'le-1',
            eIdentifierScheme: 'iso6523-actorid-upis',
            eIdentifierValue: '0088:123456789',
            createdAtUtc: '2026-09-30T08:00:00Z',
          },
        ]
      : [],
    isLoading: false,
    isError: false,
    queryKey: ['peppol-participants', id],
  }),
  usePostApiAdminPeppolParticipants: () => ({
    mutate: post,
    isPending: false,
  }),
}));

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider>
      <QueryClientProvider client={client}>
        <App>
          <PeppolParticipantsPage />
        </App>
      </QueryClientProvider>
    </I18nProvider>,
  );
}

describe('Peppol participants page', () => {
  beforeEach(() => {
    post.mockReset();
  });

  it('requires system.critical', () => {
    expect(getRequiredPermissionForPath('/admin/peppol/participants')).toEqual([
      PERMISSIONS.SYSTEM_CRITICAL,
    ]);
  });

  it('renders participant rows and create calls POST', async () => {
    renderPage();
    expect(await screen.findAllByText('0088:123456789')).toHaveLength(2);
    expect(screen.getByText('iso6523-actorid-upis')).toBeInTheDocument();
    expect(screen.getByText('le-1')).toBeInTheDocument();
    expect(screen.getByText('Cafe Muster')).toBeInTheDocument();
    expect(screen.queryByText(/api key/i)).not.toBeInTheDocument();

    const user = userEvent.setup({ delay: null });
    await user.click(screen.getByRole('button', { name: 'Teilnehmer anlegen' }));
    const dialog = await screen.findByRole('dialog');
    const participant = within(dialog).getByLabelText('Teilnehmer-ID');
    await user.click(participant);
    await user.paste('0088:999');
    await user.click(within(dialog).getByRole('button', { name: 'Speichern' }));

    await waitFor(() => {
      expect(post).toHaveBeenCalledWith({
        data: expect.objectContaining({
          tenantId,
          participantId: '0088:999',
          apEnvironment: 'TEST',
        }),
      });
    });
  });
});
