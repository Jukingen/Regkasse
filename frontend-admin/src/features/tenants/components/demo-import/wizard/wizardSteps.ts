export const WIZARD_STEPS = [
  {
    key: 'categories',
    title: 'Kategorien',
    description: 'Welche Kategorien möchten Sie importieren?',
  },
  { key: 'products', title: 'Produkte', description: 'Welche Produkte pro Kategorie?' },
  { key: 'prices', title: 'Preise', description: 'Preise prüfen und anpassen' },
  { key: 'taxes', title: 'Steuern', description: 'Steuersätze bestätigen (10%, 13%, 20%)' },
  { key: 'preview', title: 'Vorschau', description: 'Alle Änderungen im Überblick' },
  { key: 'import', title: 'Import', description: 'Bestätigen und importieren' },
] as const;
