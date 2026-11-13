import { describe, expect, it } from '@jest/globals';

import { applyPosWebDocument, POS_WEB_DOCUMENT } from '../utils/posWebDocument';

describe('POS web document', () => {
  it('sets a German title, language, and meta description', () => {
    const meta = {
      name: '',
      content: '',
      setAttribute(name: string, value: string) {
        if (name === 'name') this.name = value;
        if (name === 'content') this.content = value;
      },
    };
    const doc = {
      title: '',
      documentElement: { lang: 'en' },
      head: { appendChild: () => undefined },
      querySelector: () => null,
      createElement: () => meta,
    };

    applyPosWebDocument(doc);

    expect(doc.title).toBe(POS_WEB_DOCUMENT.title);
    expect(doc.documentElement.lang).toBe('de');
    expect(meta.name).toBe('description');
    expect(meta.content).toBe('Regkasse Kassensystem für den Verkauf an der Kasse.');
  });
});
