import '@testing-library/jest-dom';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { useTenantAssignmentModal } from '@/features/users/hooks/useTenantAssignmentModal';

function HookHarness({ onEditRequested }: { onEditRequested?: (userId: string) => void }) {
  const modal = useTenantAssignmentModal({ onEditRequested });

  return (
    <div>
      <button
        type="button"
        onClick={() =>
          modal.openModal({
            userId: 'user-new',
            userEmail: 'new@example.com',
            existingTenants: [],
          })
        }
      >
        open-new
      </button>
      <button
        type="button"
        onClick={() =>
          modal.openModal({
            userId: 'user-existing',
            userEmail: 'existing@example.com',
            existingTenants: [{ id: 'tenant-1', name: 'Cafe Central', slug: 'cafe-central' }],
          })
        }
      >
        open-existing
      </button>
      <button type="button" onClick={modal.closeModal}>
        close
      </button>

      <div data-testid="visible">{String(modal.visible)}</div>
      <div data-testid="user-id">{modal.userId ?? ''}</div>
      <div data-testid="user-email">{modal.userEmail}</div>
    </div>
  );
}

describe('useTenantAssignmentModal', () => {
  it('opens the modal for users without existing tenants', () => {
    render(<HookHarness />);

    fireEvent.click(screen.getByRole('button', { name: 'open-new' }));

    expect(screen.getByTestId('visible')).toHaveTextContent('true');
    expect(screen.getByTestId('user-id')).toHaveTextContent('user-new');
    expect(screen.getByTestId('user-email')).toHaveTextContent('new@example.com');
  });

  it('redirects existing-tenant users to edit flow instead of opening the modal', () => {
    const onEditRequested = vi.fn();
    render(<HookHarness onEditRequested={onEditRequested} />);

    fireEvent.click(screen.getByRole('button', { name: 'open-existing' }));

    expect(onEditRequested).toHaveBeenCalledWith('user-existing');
    expect(screen.getByTestId('visible')).toHaveTextContent('false');
    expect(screen.getByTestId('user-id')).toHaveTextContent('');
  });
});
