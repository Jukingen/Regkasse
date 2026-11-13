import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { InitialTableSkeleton } from '../InitialTableSkeleton';

describe('InitialTableSkeleton', () => {
  it('shows skeleton rows while a product, receipt, or user list is empty', () => {
    const { container } = render(
      <InitialTableSkeleton loading rowCount={0} rows={3} cols={2}>
        <div>Liste</div>
      </InitialTableSkeleton>
    );

    expect(screen.queryByText('Liste')).toBeNull();
    expect(container.querySelector('.ant-skeleton')).toBeTruthy();
  });

  it('shows the list after the first load', () => {
    render(
      <InitialTableSkeleton loading={false} rowCount={2}>
        <div>Liste</div>
      </InitialTableSkeleton>
    );

    expect(screen.getByText('Liste')).toBeTruthy();
  });
});
