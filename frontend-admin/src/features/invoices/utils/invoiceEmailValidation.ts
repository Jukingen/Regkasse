const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function isValidInvoiceRecipientEmail(value: string): boolean {
  return EMAIL_PATTERN.test(value.trim());
}

export function resolveInvoiceRecipientEmail(input: string, fallback?: string | null): string {
  const trimmed = input.trim();
  if (trimmed) return trimmed;
  return fallback?.trim() ?? '';
}
