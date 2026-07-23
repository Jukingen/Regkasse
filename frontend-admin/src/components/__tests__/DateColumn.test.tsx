import { describe, expect, it } from 'vitest';

import { DateColumn, dateColumnRender } from '@/components/DateColumn';
import { EMPTY_DATE_DISPLAY } from '@/lib/dateUtils';

describe('dateColumnRender', () => {
  it('returns a render function that produces a DateColumn element', () => {
    const render = dateColumnRender('datetime');
    const element = render('2026-07-15T10:30:00Z') as React.ReactElement;
    expect(element.type).toBe(DateColumn);
    expect(element.props).toMatchObject({
      date: '2026-07-15T10:30:00Z',
      format: 'datetime',
    });
  });

  it('passes utc option through', () => {
    const render = dateColumnRender('datetimeSeconds', { utc: true });
    const element = render(null) as React.ReactElement;
    expect(element.type).toBe(DateColumn);
    expect(element.props).toMatchObject({ utc: true, date: null });
  });

  it('exposes empty display constant for callers', () => {
    expect(EMPTY_DATE_DISPLAY).toBe('—');
  });
});
