import { describe, expect, it } from 'vitest';

import { buildTemplateChangeRequestNote } from '@/features/website-generator/components/WebsiteTemplatePreviewPanel';

describe('buildTemplateChangeRequestNote', () => {
  it('prefixes template id for Super Admin review', () => {
    expect(buildTemplateChangeRequestNote('modern')).toBe('template-change:modern');
    expect(buildTemplateChangeRequestNote('  classic  ')).toBe('template-change:classic');
  });
});
