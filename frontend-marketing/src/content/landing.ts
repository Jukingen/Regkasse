export const INDUSTRIES = [
  { id: 'gastronomy', icon: 'G' },
  { id: 'gastronomy-tables', icon: 'T' },
  { id: 'vet', icon: 'V' },
  { id: 'doctor', icon: 'A' },
  { id: 'hair-salon', icon: 'F' },
  { id: 'mobile-services', icon: 'M' },
  { id: 'handy-shop', icon: 'H' },
  { id: 'taxi', icon: 'X' },
  { id: 'ticket-sales', icon: 'K' },
  { id: 'beherbergung', icon: 'B' },
  { id: 'retail', icon: 'E' },
] as const;

export const INDUSTRY_IDS = INDUSTRIES.map((industry) => industry.id);
