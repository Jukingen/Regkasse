export interface ValidationRule {
  type: 'required' | 'minLength' | 'maxLength' | 'pattern' | 'email' | 'phone' | 'number' | 'decimal' | 'range' | 'custom';
  value?: any;
  message: string;
  customValidator?: (value: any) => boolean;
}

export interface ValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

export interface FieldValidation {
  field: string;
  value: any;
  rules: ValidationRule[];
}

export class ValidationService {
  private static instance: ValidationService;

  // Common patterns
  private static patterns = {
    email: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
    phone: /^[\+]?[1-9][\d]{0,15}$/,
    austrianPhone: /^(\+43|0)[1-9][0-9]{3,14}$/,
    austrianTaxNumber: /^ATU\d{8}$/,
    austrianVAT: /^ATU\d{8}$/,
    postalCode: /^\d{4,5}$/,
    iban: /^[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}$/,
    bic: /^[A-Z]{6}[A-Z2-9][A-NP-Z0-9]([A-Z0-9]{3})?$/,
    barcode: /^[0-9]{8,13}$/,
    uuid: /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
    date: /^\d{4}-\d{2}-\d{2}$/,
    time: /^([01]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$/,
    // RKSV compliant Austrian date/time formats
    austrianDate: /^(0[1-9]|[12][0-9]|3[01])\.(0[1-9]|1[0-2])\.\d{4}$/, // DD.MM.YYYY
    austrianTime: /^([01]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$/, // HH:MM:SS
    decimal: /^\d+(\.\d{1,2})?$/,
    integer: /^\d+$/,
    alphanumeric: /^[a-zA-Z0-9]+$/,
    alphabetic: /^[a-zA-Z\s]+$/,
    numeric: /^[0-9]+$/
  };

  public static getInstance(): ValidationService {
    if (!ValidationService.instance) {
      ValidationService.instance = new ValidationService();
    }
    return ValidationService.instance;
  }

  public validateField(field: string, value: any, rules: ValidationRule[]): ValidationResult {
    const errors: string[] = [];
    const warnings: string[] = [];

    for (const rule of rules) {
      const validation = this.validateRule(value, rule);
      
      if (!validation.isValid) {
        errors.push(validation.message);
      } else if (validation.warning) {
        warnings.push(validation.message);
      }
    }

    return {
      isValid: errors.length === 0,
      errors,
      warnings
    };
  }

  public validateForm(fields: FieldValidation[]): {
    isValid: boolean;
    fieldErrors: Map<string, string[]>;
    fieldWarnings: Map<string, string[]>;
    allErrors: string[];
    allWarnings: string[];
  } {
    const fieldErrors = new Map<string, string[]>();
    const fieldWarnings = new Map<string, string[]>();
    const allErrors: string[] = [];
    const allWarnings: string[] = [];

    for (const field of fields) {
      const result = this.validateField(field.field, field.value, field.rules);
      
      if (result.errors.length > 0) {
        fieldErrors.set(field.field, result.errors);
        allErrors.push(...result.errors);
      }
      
      if (result.warnings.length > 0) {
        fieldWarnings.set(field.field, result.warnings);
        allWarnings.push(...result.warnings);
      }
    }

    return {
      isValid: allErrors.length === 0,
      fieldErrors,
      fieldWarnings,
      allErrors,
      allWarnings
    };
  }

  private validateRule(value: any, rule: ValidationRule): { isValid: boolean; message: string; warning?: boolean } {
    switch (rule.type) {
      case 'required':
        return this.validateRequired(value, rule.message);
      
      case 'minLength':
        return this.validateMinLength(value, rule.value, rule.message);
      
      case 'maxLength':
        return this.validateMaxLength(value, rule.value, rule.message);
      
      case 'pattern':
        return this.validatePattern(value, rule.value, rule.message);
      
      case 'email':
        return this.validateEmail(value, rule.message);
      
      case 'phone':
        return this.validatePhone(value, rule.message);
      
      case 'number':
        return this.validateNumber(value, rule.message);
      
      case 'decimal':
        return this.validateDecimal(value, rule.message);
      
      case 'range':
        return this.validateRange(value, rule.value, rule.message);
      
      case 'custom':
        return this.validateCustom(value, rule.customValidator!, rule.message);
      
      default:
        return { isValid: true, message: '' };
    }
  }

  private validateRequired(value: any, message: string): { isValid: boolean; message: string } {
    const isValid = value !== null && value !== undefined && value !== '';
    return { isValid, message: isValid ? '' : message };
  }

  private validateMinLength(value: any, minLength: number, message: string): { isValid: boolean; message: string } {
    if (!value) return { isValid: true, message: '' };
    const isValid = String(value).length >= minLength;
    return { isValid, message: isValid ? '' : message };
  }

  private validateMaxLength(value: any, maxLength: number, message: string): { isValid: boolean; message: string } {
    if (!value) return { isValid: true, message: '' };
    const isValid = String(value).length <= maxLength;
    return { isValid, message: isValid ? '' : message };
  }

  private validatePattern(value: any, pattern: RegExp, message: string): { isValid: boolean; message: string } {
    if (!value) return { isValid: true, message: '' };
    const isValid = pattern.test(String(value));
    return { isValid, message: isValid ? '' : message };
  }

  private validateEmail(value: any, message: string): { isValid: boolean; message: string } {
    if (!value) return { isValid: true, message: '' };
    const isValid = ValidationService.patterns.email.test(String(value));
    return { isValid, message: isValid ? '' : message };
  }

  private validatePhone(value: any, message: string): { isValid: boolean; message: string } {
    if (!value) return { isValid: true, message: '' };
    const isValid = ValidationService.patterns.phone.test(String(value));
    return { isValid, message: isValid ? '' : message };
  }

  private validateNumber(value: any, message: string): { isValid: boolean; message: string } {
    if (!value) return { isValid: true, message: '' };
    const isValid = !isNaN(Number(value)) && isFinite(Number(value));
    return { isValid, message: isValid ? '' : message };
  }

  private validateDecimal(value: any, message: string): { isValid: boolean; message: string } {
    if (!value) return { isValid: true, message: '' };
    const isValid = ValidationService.patterns.decimal.test(String(value));
    return { isValid, message: isValid ? '' : message };
  }

  private validateRange(value: any, range: { min?: number; max?: number }, message: string): { isValid: boolean; message: string } {
    if (!value) return { isValid: true, message: '' };
    const numValue = Number(value);
    const isValid = !isNaN(numValue) && 
      (range.min === undefined || numValue >= range.min) && 
      (range.max === undefined || numValue <= range.max);
    return { isValid, message: isValid ? '' : message };
  }

  private validateCustom(value: any, validator: (value: any) => boolean, message: string): { isValid: boolean; message: string } {
    const isValid = validator(value);
    return { isValid, message: isValid ? '' : message };
  }

  // Predefined validation rules for common use cases
  public static getCommonRules() {
    return {
      required: (message: string = 'This field is required'): ValidationRule => ({
        type: 'required',
        message
      }),

      email: (message: string = 'Please enter a valid email address'): ValidationRule => ({
        type: 'email',
        message
      }),

      phone: (message: string = 'Please enter a valid phone number'): ValidationRule => ({
        type: 'phone',
        message
      }),

      austrianPhone: (message: string = 'Please enter a valid Austrian phone number'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.austrianPhone,
        message
      }),

      austrianTaxNumber: (message: string = 'Please enter a valid Austrian tax number (ATU12345678)'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.austrianTaxNumber,
        message
      }),

      austrianDate: (message: string = 'Please enter a valid Austrian date (DD.MM.YYYY)'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.austrianDate,
        message
      }),

      austrianTime: (message: string = 'Please enter a valid Austrian time (HH:MM:SS)'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.austrianTime,
        message
      }),

      postalCode: (message: string = 'Please enter a valid postal code'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.postalCode,
        message
      }),

      iban: (message: string = 'Please enter a valid IBAN'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.iban,
        message
      }),

      barcode: (message: string = 'Please enter a valid barcode'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.barcode,
        message
      }),

      uuid: (message: string = 'Please enter a valid UUID'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.uuid,
        message
      }),

      date: (message: string = 'Please enter a valid date (YYYY-MM-DD)'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.date,
        message
      }),

      time: (message: string = 'Please enter a valid time (HH:MM:SS)'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.time,
        message
      }),

      decimal: (message: string = 'Please enter a valid decimal number'): ValidationRule => ({
        type: 'decimal',
        message
      }),

      integer: (message: string = 'Please enter a valid integer'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.integer,
        message
      }),

      alphanumeric: (message: string = 'Only alphanumeric characters are allowed'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.alphanumeric,
        message
      }),

      alphabetic: (message: string = 'Only alphabetic characters are allowed'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.alphabetic,
        message
      }),

      numeric: (message: string = 'Only numeric characters are allowed'): ValidationRule => ({
        type: 'pattern',
        value: ValidationService.patterns.numeric,
        message
      }),

      minLength: (min: number, message?: string): ValidationRule => ({
        type: 'minLength',
        value: min,
        message: message || `Minimum length is ${min} characters`
      }),

      maxLength: (max: number, message?: string): ValidationRule => ({
        type: 'maxLength',
        value: max,
        message: message || `Maximum length is ${max} characters`
      }),

      range: (min: number, max: number, message?: string): ValidationRule => ({
        type: 'range',
        value: { min, max },
        message: message || `Value must be between ${min} and ${max}`
      }),

      positiveNumber: (message: string = 'Please enter a positive number'): ValidationRule => ({
        type: 'range',
        value: { min: 0 },
        message
      }),

      percentage: (message: string = 'Please enter a valid percentage (0-100)'): ValidationRule => ({
        type: 'range',
        value: { min: 0, max: 100 },
        message
      }),

      custom: (validator: (value: any) => boolean, message: string): ValidationRule => ({
        type: 'custom',
        customValidator: validator,
        message
      })
    };
  }

  // Specific validation methods for business logic
  public validateInvoiceAmount(amount: number): ValidationResult {
    return this.validateField('amount', amount, [
      ValidationService.getCommonRules().required('Invoice amount is required'),
      ValidationService.getCommonRules().positiveNumber('Invoice amount must be positive'),
      ValidationService.getCommonRules().range(0.01, 999999.99, 'Invoice amount must be between €0.01 and €999,999.99')
    ]);
  }

  public validateProductBarcode(barcode: string): ValidationResult {
    return this.validateField('barcode', barcode, [
      ValidationService.getCommonRules().required('Product barcode is required'),
      ValidationService.getCommonRules().barcode('Please enter a valid barcode (8-13 digits)')
    ]);
  }

  public validateCustomerEmail(email: string): ValidationResult {
    return this.validateField('email', email, [
      ValidationService.getCommonRules().required('Customer email is required'),
      ValidationService.getCommonRules().email('Please enter a valid email address'),
      ValidationService.getCommonRules().maxLength(100, 'Email address is too long')
    ]);
  }

  public validateAustrianTaxNumber(taxNumber: string): ValidationResult {
    return this.validateField('taxNumber', taxNumber, [
      ValidationService.getCommonRules().required('Tax number is required'),
      ValidationService.getCommonRules().austrianTaxNumber('Please enter a valid Austrian tax number (ATU12345678)')
    ]);
  }

  public validatePaymentAmount(amount: number, invoiceAmount: number): ValidationResult {
    const rules: ValidationRule[] = [
      ValidationService.getCommonRules().required('Payment amount is required'),
      ValidationService.getCommonRules().positiveNumber('Payment amount must be positive'),
      ValidationService.getCommonRules().range(0.01, 999999.99, 'Payment amount must be between €0.01 and €999,999.99')
    ];

    // Add custom validation for payment amount
    if (amount < invoiceAmount) {
      rules.push({
        type: 'custom',
        customValidator: () => false,
        message: `Payment amount (€${amount.toFixed(2)}) is less than invoice amount (€${invoiceAmount.toFixed(2)})`
      });
    }

    return this.validateField('paymentAmount', amount, rules);
  }

  public validateQuantity(quantity: number): ValidationResult {
    return this.validateField('quantity', quantity, [
      ValidationService.getCommonRules().required('Quantity is required'),
      ValidationService.getCommonRules().integer('Quantity must be a whole number'),
      ValidationService.getCommonRules().range(1, 9999, 'Quantity must be between 1 and 9999')
    ]);
  }

  public validatePrice(price: number): ValidationResult {
    return this.validateField('price', price, [
      ValidationService.getCommonRules().required('Price is required'),
      ValidationService.getCommonRules().positiveNumber('Price must be positive'),
      ValidationService.getCommonRules().range(0.01, 99999.99, 'Price must be between €0.01 and €99,999.99')
    ]);
  }

  public sanitizeInput(input: string): string {
    if (!input) return '';
    
    // Remove potentially dangerous characters
    return input
      .replace(/[<>]/g, '') // Remove < and >
      .replace(/javascript:/gi, '') // Remove javascript: protocol
      .replace(/on\w+=/gi, '') // Remove event handlers
      .trim();
  }

  public sanitizeNumber(input: string): number {
    if (!input) return 0;
    
    // Remove non-numeric characters except decimal point
    const sanitized = input.replace(/[^\d.]/g, '');
    const number = parseFloat(sanitized);
    
    return isNaN(number) ? 0 : number;
  }

  public formatCurrency(amount: number): string {
    return new Intl.NumberFormat('de-DE', {
      style: 'currency',
      currency: 'EUR'
    }).format(amount);
  }

  public formatPercentage(value: number): string {
    return `${value.toFixed(2)}%`;
  }

  public formatDate(date: Date | string): string {
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    // RKSV compliant Austrian date format: DD.MM.YYYY
    return dateObj.toLocaleDateString('de-DE', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    });
  }

  public formatDateTime(date: Date | string): string {
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    // RKSV compliant Austrian date/time format: DD.MM.YYYY HH:MM:SS
    return dateObj.toLocaleString('de-DE', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
  }

  public formatTime(date: Date | string): string {
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    // RKSV compliant Austrian time format: HH:MM:SS
    return dateObj.toLocaleTimeString('de-DE', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
  }
}

// Export singleton instance
export const validationService = ValidationService.getInstance(); 