export const HERO = {
  headline: 'Die Kasse für jede Branche',
  subheadline: 'RKSV-konform, mandantenfähig, in Minuten startklar.',
  primaryCta: 'Demo anfordern',
  secondaryCta: 'Funktionen ansehen',
} as const;

export const FEATURES = [
  {
    title: 'RKSV & TSE',
    text: 'Belege werden signiert. Sonderbelege, DEP-Export und FinanzOnline bleiben im Produkt.',
  },
  {
    title: 'Mandantenfähig',
    text: 'Ein System, getrennte Mandanten. Jeder Betrieb sieht nur seine eigenen Daten.',
  },
  {
    title: 'Backup & DSGVO',
    text: 'Sicherungen und Datenexporte. Fiskaldaten bleiben gesetzlich aufbewahrt.',
  },
] as const;

export const INDUSTRIES = [
  { id: 'gastronomy', name: 'Gastronomie', description: 'Speisen und Getränke an der Theke.', icon: 'G' },
  { id: 'gastronomy-tables', name: 'Gastronomie mit Tischen', description: 'Bestellung und Service am Tisch.', icon: 'T' },
  { id: 'vet', name: 'Tierarzt', description: 'Patientenakte und Behandlung.', icon: 'V' },
  { id: 'doctor', name: 'Arztpraxis', description: 'Termine und Behandlung.', icon: 'A' },
  { id: 'hair-salon', name: 'Friseursalon', description: 'Termine und Dienstleistungen.', icon: 'F' },
  { id: 'mobile-services', name: 'Mobile Dienste', description: 'Einsatz und Adresse vor Ort.', icon: 'M' },
  { id: 'handy-shop', name: 'Handyshop', description: 'Geräte, Zubehör und IMEI.', icon: 'H' },
  { id: 'taxi', name: 'Taxi', description: 'Fahrten und Tarife.', icon: 'X' },
  { id: 'ticket-sales', name: 'Ticketverkauf', description: 'Events, Plätze und Kategorien.', icon: 'K' },
  { id: 'beherbergung', name: 'Beherbergung', description: 'Zimmer und Folios.', icon: 'B' },
  { id: 'retail', name: 'Einzelhandel', description: 'Kasse, Waren und Barcode.', icon: 'E' },
] as const;

export const INDUSTRY_IDS = INDUSTRIES.map((industry) => industry.id);

export const PRICING = [
  {
    name: 'Starter',
    price: '€29/Monat',
    detail: '1 Kasse, bis 3 Nutzer',
  },
  {
    name: 'Professional',
    price: '€79/Monat',
    detail: '3 Kassen, bis 10 Nutzer, Backup inklusive',
  },
  {
    name: 'Enterprise',
    price: 'Individuell',
    detail: 'unbegrenzt, eigene Infrastruktur, SLA',
  },
] as const;

export const FAQ = [
  {
    question: 'RKSV-konform?',
    answer: 'Ja. Die österreichische Kasse signiert Belege über die TSE und bleibt im RKSV-Pfad.',
  },
  {
    question: 'Wie lange dauert die Einrichtung?',
    answer: 'Ein Demo-Mandant ist in Minuten startklar. Die produktive Einrichtung hängt von Kasse, TSE und Stammdaten ab.',
  },
  {
    question: 'Kann ich meine Daten exportieren?',
    answer: 'Ja. Exporte und die DSGVO-Datenrechte bleiben verfügbar. Fiskaldaten werden gesetzlich aufbewahrt.',
  },
  {
    question: 'Welche Branchen?',
    answer: 'Elf Branchenprofile, von Gastronomie und Arztpraxis bis Taxi, Beherbergung und Einzelhandel.',
  },
  {
    question: 'Was kostet die Kündigung?',
    answer: '14 Tage können kostenlos getestet werden. Danach endet ein Abo zum Laufzeitende, ohne zusätzliche Kündigungsgebühr.',
  },
] as const;
