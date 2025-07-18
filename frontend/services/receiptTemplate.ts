// Bu fonksiyon, EPSON/Star yazıcıya uygun, çevrimdışı modda kullanılabilen plain text fiş şablonu üretir.
// OCRA-B fontu ve 42/48 karakter genişliği için optimize edilmiştir.

export interface ReceiptItem {
  name: string;
  quantity: number;
  price: number;
  total: number;
}

export interface ReceiptData {
  receiptNumber: string;
  date: string; // DD.MM.YYYY
  time: string; // HH:MM:SS
  tseSignature: string;
  kassenId: string;
  steuernummer: string;
  items: ReceiptItem[];
  total: number;
  paymentMethod: string;
  taxDetails: { standard: number; reduced: number; special: number };
}

// Satırı belirli karakter genişliğinde ortala
function center(text: string, width: number) {
  const pad = Math.max(0, Math.floor((width - text.length) / 2));
  return ' '.repeat(pad) + text;
}

// Satırı belirli karakter genişliğinde sağa hizala
function right(text: string, width: number) {
  return ' '.repeat(Math.max(0, width - text.length)) + text;
}

// Fiş şablonu oluşturucu
export function generateReceiptText(data: ReceiptData, width: number = 42): string {
  // Başlık
  let lines: string[] = [];
  lines.push(center('*** KASSENBELEG ***', width));
  lines.push(center('Registrierkasse GmbH', width));
  lines.push(center('www.registrierkasse.at', width));
  lines.push('-'.repeat(width));
  lines.push(`BelegNr: ${data.receiptNumber}`);
  lines.push(`Datum: ${data.date}   Uhrzeit: ${data.time}`);
  lines.push(`Kassen-ID: ${data.kassenId}`);
  lines.push(`Steuernummer: ${data.steuernummer}`);
  lines.push('-'.repeat(width));
  lines.push('Menge  Artikel                Einzel  Gesamt');
  lines.push('-'.repeat(width));
  // Ürünler
  data.items.forEach(item => {
    const name = item.name.length > 18 ? item.name.slice(0, 18) + '.' : item.name;
    const qty = right(item.quantity.toString(), 2);
    const price = right(item.price.toFixed(2), 6);
    const total = right(item.total.toFixed(2), 7);
    lines.push(`${qty}   ${name.padEnd(20)}${price} ${total}`);
  });
  lines.push('-'.repeat(width));
  lines.push(right(`Summe: ${data.total.toFixed(2)} EUR`, width));
  lines.push(right(`MwSt 20%: ${data.taxDetails.standard.toFixed(2)} EUR`, width));
  lines.push(right(`MwSt 10%: ${data.taxDetails.reduced.toFixed(2)} EUR`, width));
  lines.push(right(`MwSt 13%: ${data.taxDetails.special.toFixed(2)} EUR`, width));
  lines.push('-'.repeat(width));
  lines.push(`Zahlart: ${data.paymentMethod}`);
  lines.push('-'.repeat(width));
  lines.push('TSE-Signatur:');
  lines.push(data.tseSignature);
  lines.push('-'.repeat(width));
  lines.push(center('Vielen Dank für Ihren Einkauf!', width));
  return lines.join('\n');
} 