export interface LoginRequest {
  /** Email or username (preferred). */
  loginIdentifier: string;
  password: string;
  /** Legacy field; backend falls back when loginIdentifier is empty. */
  email?: string;
  /** Future-proof: backend policy (e.g. strict mode) may use this; POS sends 'pos'. */
  clientApp?: 'pos' | 'admin';
}

export function buildLoginPayload(
  loginIdentifier: string,
  password: string,
  clientApp: 'pos' | 'admin' = 'pos'
): LoginRequest {
  const trimmed = loginIdentifier.trim();
  return {
    loginIdentifier: trimmed,
    email: trimmed,
    password,
    clientApp,
  };
}
