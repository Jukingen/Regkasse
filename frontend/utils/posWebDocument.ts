/** Static web document fields for the POS shell. German UI, so the document language is de. */
export const POS_WEB_DOCUMENT = {
  title: 'Regkasse POS',
  description: 'Regkasse Kassensystem für den Verkauf an der Kasse.',
  lang: 'de',
} as const;

type WebDocumentTarget = {
  title: string;
  documentElement: { lang: string };
  head: { appendChild: (node: unknown) => void };
  querySelector: (selector: string) => { setAttribute: (name: string, value: string) => void } | null;
  createElement: (tag: string) => { setAttribute: (name: string, value: string) => void };
};

/** Sets title, language, and meta description. Safe to call after the web document exists. */
export function applyPosWebDocument(doc: WebDocumentTarget = document as unknown as WebDocumentTarget): void {
  doc.title = POS_WEB_DOCUMENT.title;
  doc.documentElement.lang = POS_WEB_DOCUMENT.lang;
  let meta = doc.querySelector('meta[name="description"]');
  if (!meta) {
    meta = doc.createElement('meta');
    meta.setAttribute('name', 'description');
    doc.head.appendChild(meta);
  }
  meta.setAttribute('content', POS_WEB_DOCUMENT.description);
}
