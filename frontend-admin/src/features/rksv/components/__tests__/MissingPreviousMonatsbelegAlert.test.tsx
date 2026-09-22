import '@testing-library/jest-dom';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { MissingPreviousMonatsbelegAlert } from '@/features/rksv/components/MissingPreviousMonatsbelegAlert';
import { I18nProvider } from '@/i18n';

describe('MissingPreviousMonatsbelegAlert', () => {
  it('renders the force-create action when the user can create', () => {
    const onCreateNow = vi.fn();
    render(
      <I18nProvider>
        <MissingPreviousMonatsbelegAlert visible canCreate onCreateNow={onCreateNow} />
      </I18nProvider>
    );
    expect(screen.getByTestId('missing-previous-monatsbeleg-alert')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('missing-previous-monatsbeleg-create'));
    expect(onCreateNow).toHaveBeenCalledTimes(1);
  });

  it('hides the create button without permission', () => {
    render(
      <I18nProvider>
        <MissingPreviousMonatsbelegAlert visible canCreate={false} onCreateNow={vi.fn()} />
      </I18nProvider>
    );
    expect(screen.getByTestId('missing-previous-monatsbeleg-alert')).toBeInTheDocument();
    expect(screen.queryByTestId('missing-previous-monatsbeleg-create')).toBeNull();
  });

  it('renders nothing when not visible', () => {
    const { container } = render(
      <I18nProvider>
        <MissingPreviousMonatsbelegAlert visible={false} canCreate onCreateNow={vi.fn()} />
      </I18nProvider>
    );
    expect(container).toBeEmptyDOMElement();
  });
});
