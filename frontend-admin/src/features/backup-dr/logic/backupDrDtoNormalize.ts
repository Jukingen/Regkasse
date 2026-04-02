/**
 * Orval’ın `| null` döndürdüğü uç noktaları UI katmanında `undefined` ile temsil eder;
 * böylece `buildBackupOperatorTruthModel` ve türevleri tek `undefined` sözleşmesi kullanır.
 */
export function apiNullableToUndefined<T>(value: T | null | undefined): T | undefined {
  return value === null ? undefined : value;
}
