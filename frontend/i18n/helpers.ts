import { useTranslation } from 'react-i18next';

/**
 * i18n kullanımını kolaylaştırmak için yardımcı hook
 */
export const useI18n = () => {
  const { t, i18n } = useTranslation();
  
  /**
   * Mevcut dili döndürür
   */
  const getCurrentLanguage = () => i18n.language;
  
  /**
   * Desteklenen dilleri döndürür
   */
  const getSupportedLanguages = () => ['de', 'en', 'tr'];
  
  /**
   * Dil değiştirir
   */
  const changeLanguage = async (language: string) => {
    try {
      await i18n.changeLanguage(language);
      return true;
    } catch (error) {
      console.error('Dil değiştirilirken hata:', error);
      return false;
    }
  };
  
  /**
   * Çeviri anahtarının mevcut olup olmadığını kontrol eder
   */
  const hasTranslation = (key: string) => {
    try {
      const translation = t(key);
      return translation !== key;
    } catch {
      return false;
    }
  };
  
  /**
   * Çeviri anahtarının mevcut olup olmadığını kontrol eder ve fallback değer döndürür
   */
  const getTranslation = (key: string, fallback: string = '') => {
    try {
      const translation = t(key);
      return translation !== key ? translation : fallback;
    } catch {
      return fallback;
    }
  };
  
  return {
    t,
    i18n,
    getCurrentLanguage,
    getSupportedLanguages,
    changeLanguage,
    hasTranslation,
    getTranslation
  };
};

/**
 * Çeviri anahtarları için sabitler
 */
export const I18N_KEYS = {
  // Common
  COMMON: {
    APP_NAME: 'common.appName',
    LOADING: 'common.loading',
    ERROR: 'common.error',
    SUCCESS: 'common.success',
    SAVE: 'common.save',
    CANCEL: 'common.cancel',
    DELETE: 'common.delete',
    EDIT: 'common.edit',
    SEARCH: 'common.search',
    BACK: 'common.back',
    CONTINUE: 'common.continue',
    CONFIRM: 'common.confirm',
    CLOSE: 'common.close',
    YES: 'common.yes',
    NO: 'common.no',
    STEP: 'common.step'
  },
  
  // Auth
  AUTH: {
    LOGIN: 'auth.login',
    LOGOUT: 'auth.logout',
    EMAIL: 'auth.email',
    PASSWORD: 'auth.password',
    FORGOT_PASSWORD: 'auth.forgotPassword',
    LOGIN_ERROR: 'auth.loginError',
    INVALID_EMAIL: 'auth.invalidEmail',
    INVALID_PASSWORD: 'auth.invalidPassword',
    REQUIRED: 'auth.required'
  },
  
  // Cash Register
  CASH_REGISTER: {
    TITLE: 'cashRegister.title',
    CART: 'cashRegister.cart',
    TOTAL: 'cashRegister.total',
    CHECKOUT: 'cashRegister.checkout',
    ADD_TO_CART: 'cashRegister.addToCart',
    REMOVE_FROM_CART: 'cashRegister.removeFromCart',
    QUANTITY: 'cashRegister.quantity',
    PRICE: 'cashRegister.price',
    PRODUCT: 'cashRegister.product',
    PRODUCTS: 'cashRegister.products',
    STOCK: 'cashRegister.stock',
    OUT_OF_STOCK: 'cashRegister.outOfStock',
    SUBTOTAL: 'cashRegister.subtotal',
    TAX: 'cashRegister.tax',
    DISCOUNT: 'cashRegister.discount'
  },
  
  // Payment
  PAYMENT: {
    TITLE: 'payment.title',
    CUSTOMER_SELECTION: 'payment.customerSelection',
    PAYMENT_METHOD: 'payment.paymentMethod',
    PAYMENT_AMOUNT: 'payment.paymentAmount',
    TSE_VERIFICATION: 'payment.tseVerification',
    CONFIRMATION: 'payment.confirmation',
    RECEIPT: 'payment.receipt',
    
    STEP_TITLES: {
      CUSTOMER_SELECTION: 'payment.stepTitles.customerSelection',
      PAYMENT_METHOD: 'payment.stepTitles.paymentMethod',
      PAYMENT_AMOUNT: 'payment.stepTitles.paymentAmount',
      TSE_VERIFICATION: 'payment.stepTitles.tseVerification',
      CONFIRMATION: 'payment.stepTitles.confirmation',
      RECEIPT: 'payment.stepTitles.receipt'
    },
    
    CUSTOMER: {
      ID: 'payment.customer.id',
      ID_PLACEHOLDER: 'payment.customer.idPlaceholder',
      ID_REQUIRED: 'payment.customer.idRequired',
      ID_VALID: 'payment.customer.idValid',
      ID_INVALID: 'payment.customer.idInvalid'
    },
    
    METHODS: {
      CASH: 'payment.methods.cash',
      CARD: 'payment.methods.card',
      VOUCHER: 'payment.methods.voucher',
      CONTACTLESS: 'payment.methods.contactless'
    },
    
    AMOUNT: {
      TOTAL: 'payment.amount.total',
      PLACEHOLDER: 'payment.amount.placeholder',
      HINT: 'payment.amount.hint',
      ERROR: 'payment.amount.error'
    },
    
    TSE: {
      REQUIRED: 'payment.tse.required',
      NOT_REQUIRED: 'payment.tse.notRequired',
      SIGNATURE: 'payment.tse.signature',
      SIGNATURE_PLACEHOLDER: 'payment.tse.signaturePlaceholder',
      VERIFICATION_ERROR: 'payment.tse.verificationError'
    },
    
    CONFIRMATION: {
      TITLE: 'payment.confirmation.title',
      CUSTOMER_ID: 'payment.confirmation.customerId',
      PAYMENT_METHOD: 'payment.confirmation.paymentMethod',
      PAYMENT_AMOUNT: 'payment.confirmation.paymentAmount',
      TOTAL_AMOUNT: 'payment.confirmation.totalAmount',
      CHANGE: 'payment.confirmation.change'
    },
    
    RECEIPT: {
      TITLE: 'payment.receipt.title',
      DESCRIPTION: 'payment.receipt.description',
      VIEW: 'payment.receipt.view',
      ERROR: 'payment.receipt.error'
    },
    
    BUTTONS: {
      CONTINUE: 'payment.buttons.continue',
      BACK: 'payment.buttons.back',
      CONFIRM: 'payment.buttons.confirm',
      CANCEL: 'payment.buttons.cancel',
      VERIFY: 'payment.buttons.verify',
      CONFIRM_PAYMENT: 'payment.buttons.confirmPayment'
    },
    
    ERRORS: {
      PAYMENT_FAILED: 'payment.errors.paymentFailed',
      PAYMENT_ERROR: 'payment.errors.paymentError',
      GENERAL_ERROR: 'payment.errors.generalError'
    },
    
    CANCELLATION: {
      TITLE: 'payment.cancellation.title',
      MESSAGE: 'payment.cancellation.message',
      CONFIRM: 'payment.cancellation.confirm',
      DENY: 'payment.cancellation.deny',
      SUCCESS: 'payment.cancellation.success',
      REASON: 'payment.cancellation.reason',
      CANCELLED_BY: 'payment.cancellation.cancelledBy',
      CANCELLED_AT: 'payment.cancellation.cancelledAt'
    }
  },
  
  // Settings
  SETTINGS: {
    TITLE: 'settings.title',
    LANGUAGE: 'settings.language',
    THEME: 'settings.theme',
    NOTIFICATIONS: 'settings.notifications',
    DARK_MODE: 'settings.darkMode',
    LIGHT_MODE: 'settings.lightMode',
    SYSTEM_THEME: 'settings.systemTheme',
    GERMAN: 'settings.german',
    ENGLISH: 'settings.english',
    TURKISH: 'settings.turkish'
  }
};

/**
 * Çeviri anahtarlarını kullanarak çeviri yapar
 */
export const translate = (key: string, options?: any) => {
  const { t } = useTranslation();
  return t(key, options);
};

/**
 * Çeviri anahtarlarını kullanarak çeviri yapar (hook olmadan)
 */
export const translateKey = (key: string, options?: any) => {
  // Bu fonksiyon sadece string döndürür, gerçek çeviri yapmaz
  // Gerçek çeviri için useI18n hook'unu kullanın
  return key;
};
