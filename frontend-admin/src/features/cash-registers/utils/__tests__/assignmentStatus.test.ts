import { describe, expect, it } from 'vitest';

import {
  assignmentTagColor,
  resolveAssignmentState,
} from '@/features/cash-registers/utils/assignmentStatus';

describe('resolveAssignmentState', () => {
  it('treats null, undefined and blank ids as unassigned', () => {
    expect(resolveAssignmentState(null, 'user-1')).toBe('unassigned');
    expect(resolveAssignmentState(undefined, 'user-1')).toBe('unassigned');
    expect(resolveAssignmentState('   ', 'user-1')).toBe('unassigned');
  });

  it('detects the signed-in admin as assignee', () => {
    expect(resolveAssignmentState('user-1', 'user-1')).toBe('assignedToMe');
    expect(resolveAssignmentState(' user-1 ', 'user-1')).toBe('assignedToMe');
  });

  it('reports a foreign assignee', () => {
    expect(resolveAssignmentState('user-2', 'user-1')).toBe('assignedToOther');
  });

  it('reports a foreign assignee when the current user is unknown', () => {
    expect(resolveAssignmentState('user-2', undefined)).toBe('assignedToOther');
    expect(resolveAssignmentState('user-2', '')).toBe('assignedToOther');
  });

  it('compares ordinally, like the backend visibility rule', () => {
    expect(resolveAssignmentState('User-1', 'user-1')).toBe('assignedToOther');
  });
});

describe('assignmentTagColor', () => {
  it('maps each state to a distinct tag tone', () => {
    expect(assignmentTagColor('unassigned')).toBe('default');
    expect(assignmentTagColor('assignedToMe')).toBe('success');
    expect(assignmentTagColor('assignedToOther')).toBe('processing');
  });
});
