import { describe, expect, it } from 'vitest';

import {
  supportCategoryLabelKey,
  supportPriorityLabelKey,
  supportStatusColor,
  supportStatusLabelKey,
} from '@/features/support-tickets/utils/supportTicketDisplay';

describe('supportTicketDisplay', () => {
  it('maps statuses to labels and colors', () => {
    expect(supportStatusLabelKey('Open')).toBe('support.tickets.statusOpen');
    expect(supportStatusLabelKey('InProgress')).toBe('support.tickets.statusInProgress');
    expect(supportStatusLabelKey('Resolved')).toBe('support.tickets.statusResolved');
    expect(supportStatusLabelKey('Closed')).toBe('support.tickets.statusClosed');
    expect(supportStatusColor('Open')).toBe('blue');
    expect(supportStatusColor('InProgress')).toBe('gold');
    expect(supportStatusColor('Resolved')).toBe('green');
  });

  it('maps categories and priorities', () => {
    expect(supportCategoryLabelKey('General')).toBe('support.tickets.categoryGeneral');
    expect(supportCategoryLabelKey('FeatureRequest')).toBe('support.tickets.categoryFeature');
    expect(supportPriorityLabelKey('High')).toBe('support.tickets.priorityHigh');
    expect(supportPriorityLabelKey('Urgent')).toBe('support.tickets.priorityUrgent');
  });
});
