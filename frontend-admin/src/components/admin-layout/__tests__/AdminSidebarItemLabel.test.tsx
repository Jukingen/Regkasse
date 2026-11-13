import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AdminSidebarItemLabel } from '@/components/admin-layout/AdminSidebarItemLabel';

describe('AdminSidebarItemLabel', () => {
  it('renders title and muted subtitle', () => {
    render(<AdminSidebarItemLabel title="RKSV & FinanzOnline" subtitle="TSE, Sonderbelege, DEP-Export" />);
    expect(screen.getByText('RKSV & FinanzOnline')).toBeInTheDocument();
    expect(screen.getByText('TSE, Sonderbelege, DEP-Export')).toBeInTheDocument();
  });

  it('exposes a combined aria-label for screen readers', () => {
    const { container } = render(
      <AdminSidebarItemLabel title="Dashboard" subtitle="Übersicht" />
    );
    const label = container.querySelector('.admin-sidebar-item-label');
    expect(label).toHaveAttribute('aria-label', 'Dashboard. Übersicht');
    expect(container.querySelector('.admin-sidebar-item-subtitle')).toHaveAttribute(
      'aria-hidden',
      'true'
    );
  });

  it('omits subtitle node when not provided', () => {
    const { container } = render(<AdminSidebarItemLabel title="Only title" />);
    expect(container.querySelector('.admin-sidebar-item-subtitle')).toBeNull();
  });
});
