/**
 * Ham metin + fallback — `extractRawApiErrorMessage` üzerinden (axios/ProblemDetails).
 */
import { extractRawApiErrorMessage } from './extractRawApiErrorMessage';

export function extractApiErrorMessage(error: unknown, fallback: string): string {
  return extractRawApiErrorMessage(error) ?? fallback;
}
