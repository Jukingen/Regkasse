/**
 * Client-side password generator aligned with TenantProvisioningService.GenerateCompliantPassword.
 * Used when the operator chooses "auto-generate" and wants a preview before submit.
 */
export function generateCompliantPassword(length = 16): string {
  const lowers = 'abcdefghjkmnpqrstuvwxyz';
  const uppers = 'ABCDEFGHJKMNPQRSTUVWXYZ';
  const digits = '23456789';
  const symbols = '!@#$%&*';
  const all = lowers + uppers + digits + symbols;

  const pick = (alphabet: string) => alphabet[secureInt(alphabet.length)]!;
  const buffer: string[] = [pick(lowers), pick(uppers), pick(digits), pick(symbols)];
  for (let i = buffer.length; i < length; i++) {
    buffer.push(pick(all));
  }

  for (let i = buffer.length - 1; i > 0; i--) {
    const j = secureInt(i + 1);
    [buffer[i], buffer[j]] = [buffer[j]!, buffer[i]!];
  }

  return buffer.join('');
}

function secureInt(maxExclusive: number): number {
  if (maxExclusive <= 0) {
    return 0;
  }
  const array = new Uint32Array(1);
  crypto.getRandomValues(array);
  return array[0]! % maxExclusive;
}
