// Form validasyon yardımcı fonksiyonları
export interface ValidationResult {
  isValid: boolean;
  message?: string;
}

// Kullanıcı adı validasyonu (email veya normal kullanıcı adı)
export const validateUsername = (username: string): ValidationResult => {
  if (!username.trim()) {
    return {
      isValid: false,
      message: 'Kullanıcı adı gereklidir'
    };
  }
  
  if (username.length < 3) {
    return {
      isValid: false,
      message: 'Kullanıcı adı en az 3 karakter olmalıdır'
    };
  }
  
  if (username.length > 55) {
    return {
      isValid: false,
      message: 'Kullanıcı adı en fazla 55 karakter olabilir'
    };
  }
  
  // Email formatı kontrolü
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (emailRegex.test(username)) {
    return { isValid: true }; // Email formatı geçerli
  }
  
  // Normal kullanıcı adı için: harf, rakam, alt çizgi, tire ve nokta kabul et
  const usernameRegex = /^[a-zA-Z0-9._-]+$/;
  if (!usernameRegex.test(username)) {
    return {
      isValid: false,
      message: 'Kullanıcı adı sadece harf, rakam, nokta, alt çizgi ve tire içerebilir'
    };
  }
  
  return { isValid: true };
};

// Şifre validasyonu
export const validatePassword = (password: string): ValidationResult => {
  if (!password) {
    return {
      isValid: false,
      message: 'Şifre gereklidir'
    };
  }
  
  if (password.length < 6) {
    return {
      isValid: false,
      message: 'Şifre en az 6 karakter olmalıdır'
    };
  }
  
  if (password.length > 128) {
    return {
      isValid: false,
      message: 'Şifre en fazla 128 karakter olabilir'
    };
  }
  
  return { isValid: true };
};

// Email validasyonu (opsiyonel)
export const validateEmail = (email: string): ValidationResult => {
  if (!email) {
    return { isValid: true }; // Email opsiyonel
  }
  
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!emailRegex.test(email)) {
    return {
      isValid: false,
      message: 'Geçerli bir email adresi giriniz'
    };
  }
  
  return { isValid: true };
};

// Form validasyonu
export const validateForm = (values: Record<string, string>): Record<string, string> => {
  const errors: Record<string, string> = {};
  
  // Kullanıcı adı validasyonu
  if (values.username) {
    const usernameValidation = validateUsername(values.username);
    if (!usernameValidation.isValid) {
      errors.username = usernameValidation.message!;
    }
  }
  
  // Şifre validasyonu
  if (values.password) {
    const passwordValidation = validatePassword(values.password);
    if (!passwordValidation.isValid) {
      errors.password = passwordValidation.message!;
    }
  }
  
  // Email validasyonu
  if (values.email) {
    const emailValidation = validateEmail(values.email);
    if (!emailValidation.isValid) {
      errors.email = emailValidation.message!;
    }
  }
  
  return errors;
};

// Real-time validasyon için
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
 * @param steuernummer - Vergi numarası
 * @returns Validasyon sonucu
 */
export const validateSteuernummer = (steuernummer: string): boolean => {
  const steuernummerRegex = /^ATU\d{8}$/;
  return steuernummerRegex.test(steuernummer);
};

/**
 * KassenId validasyonu (3-50 karakter)
 * @param kassenId - Kasa ID
 * @returns Validasyon sonucu
 */
export const validateKassenId = (kassenId: string): boolean => {
  return kassenId.length >= 3 && kassenId.length <= 50;
};

/**
 * Avusturya vergi oranları validasyonu
 * @param taxType - Vergi tipi
 * @returns Validasyon sonucu
 */
export const validateTaxType = (taxType: string): boolean => {
  const validTaxTypes = ['standard', 'reduced', 'special'];
  return validTaxTypes.includes(taxType);
};

/**
 * TSE gerekli mi kontrolü
 * @param paymentMethod - Ödeme yöntemi
 * @returns TSE gerekli mi
 */
export const isTseRequired = (paymentMethod: string): boolean => {
  // Avusturya yasalarına göre tüm ödemelerde TSE gerekli
  return true;
};

/**
 * Ödeme tutarı validasyonu (0.01'den büyük olmalı)
 * @param amount - Tutar
 * @returns Validasyon sonucu
 */
export const validateAmount = (amount: number): boolean => {
  return amount > 0.01;
};
