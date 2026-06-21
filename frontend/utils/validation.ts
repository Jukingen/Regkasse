import i18n from '../i18n';

// Form validasyon yardımcı fonksiyonları
export interface ValidationResult {
  isValid: boolean;
  message?: string;
}

function tAuth(key: string): string {
  return i18n.t(key, { ns: 'auth' });
}

// Login identifier validation (email or username)
export const validateUsername = (username: string): ValidationResult => {
  if (!username.trim()) {
    return {
      isValid: false,
      message: tAuth('validation.loginIdentifierRequired'),
    };
  }

  if (username.length < 3) {
    return {
      isValid: false,
      message: tAuth('validation.loginIdentifierMin'),
    };
  }

  if (username.length > 50) {
    return {
      isValid: false,
      message: tAuth('validation.loginIdentifierMax'),
    };
  }

  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (emailRegex.test(username)) {
    return { isValid: true };
  }

  const usernameRegex = /^[a-zA-Z0-9_-]+$/;
  if (!usernameRegex.test(username)) {
    return {
      isValid: false,
      message: tAuth('validation.loginIdentifierPattern'),
    };
  }

  return { isValid: true };
};

// Password validation (client-side UX; backend Identity is authoritative)
export const validatePassword = (password: string): ValidationResult => {
  if (!password) {
    return {
      isValid: false,
      message: tAuth('validation.passwordRequired'),
    };
  }

  if (password.length < 8) {
    return {
      isValid: false,
      message: tAuth('validation.passwordMin'),
    };
  }

  if (password.length > 128) {
    return {
      isValid: false,
      message: tAuth('validation.passwordMax'),
    };
  }

  return { isValid: true };
};

// Email validasyonu (opsiyonel)
export const validateEmail = (email: string): ValidationResult => {
  if (!email) {
    return { isValid: true };
  }

  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!emailRegex.test(email)) {
    return {
      isValid: false,
      message: tAuth('invalidEmail'),
    };
  }

  return { isValid: true };
};

export const validateForm = (values: Record<string, string>): Record<string, string> => {
  const errors: Record<string, string> = {};

  if ('username' in values) {
    const usernameValidation = validateUsername(values.username);
    if (!usernameValidation.isValid) {
      errors.username = usernameValidation.message!;
    }
  }

  if ('password' in values) {
    const passwordValidation = validatePassword(values.password);
    if (!passwordValidation.isValid) {
      errors.password = passwordValidation.message!;
    }
  }

  if ('email' in values) {
    const emailValidation = validateEmail(values.email);
    if (!emailValidation.isValid) {
      errors.email = emailValidation.message!;
    }
  }

  return errors;
};

export const validateField = (fieldName: string, value: string): string | undefined => {
  switch (fieldName) {
    case 'username':
      return validateUsername(value).message;
    case 'password':
      return validatePassword(value).message;
    case 'email':
      return validateEmail(value).message;
    default:
      return undefined;
  }
};

// Avusturya yasal gereksinimleri için validasyon fonksiyonları

/**
 * Steuernummer validasyonu (ATU12345678 format)
 */
export const validateSteuernummer = (steuernummer: string): boolean => {
  const steuernummerRegex = /^ATU\d{8}$/;
  return steuernummerRegex.test(steuernummer);
};

/**
 * KassenId validasyonu (3-50 karakter)
 */
export const validateKassenId = (kassenId: string): boolean => {
  return kassenId.length >= 3 && kassenId.length <= 50;
};

/**
 * Avusturya vergi oranları validasyonu
 */
export const validateTaxType = (taxType: string): boolean => {
  const validTaxTypes = ['standard', 'reduced', 'special'];
  return validTaxTypes.includes(taxType);
};

/**
 * TSE gerekli mi kontrolü
 */
export const isTseRequired = (_paymentMethod: string): boolean => {
  return true;
};

/**
 * Ödeme tutarı validasyonu (0.01'den büyük olmalı)
 */
export const validateAmount = (amount: number): boolean => {
  return amount > 0.01;
};
