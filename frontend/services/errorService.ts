export class APIError extends Error {
  constructor(
    message: string,
    public statusCode: number,
    public code?: string
  ) {
    super(message);
    this.name = 'APIError';
  }
}

export const ErrorCodes = {
  // Authentication Errors
  AUTH_INVALID_CREDENTIALS: 'AUTH_001',
  AUTH_TOKEN_EXPIRED: 'AUTH_002',
  AUTH_TOKEN_INVALID: 'AUTH_003',
  AUTH_UNAUTHORIZED: 'AUTH_004',

  // Network Errors
  NETWORK_OFFLINE: 'NET_001',
  NETWORK_TIMEOUT: 'NET_002',
  NETWORK_SERVER_ERROR: 'NET_003',

  // Validation Errors
  VALIDATION_REQUIRED: 'VAL_001',
  VALIDATION_INVALID_FORMAT: 'VAL_002',
  VALIDATION_INVALID_LENGTH: 'VAL_003',

  // Business Logic Errors
  BUSINESS_INSUFFICIENT_STOCK: 'BUS_001',
  BUSINESS_INVALID_OPERATION: 'BUS_002',
  BUSINESS_DUPLICATE_ENTRY: 'BUS_003',

  // System Errors
  SYSTEM_INTERNAL_ERROR: 'SYS_001',
  SYSTEM_DATABASE_ERROR: 'SYS_002',
  SYSTEM_CONFIGURATION_ERROR: 'SYS_003',
} as const;

export const ErrorMessages = {
  [ErrorCodes.AUTH_INVALID_CREDENTIALS]: 'Invalid email or password',
  [ErrorCodes.AUTH_TOKEN_EXPIRED]: 'Session expired. Please login again',
  [ErrorCodes.AUTH_TOKEN_INVALID]: 'Invalid session. Please login again',
  [ErrorCodes.AUTH_UNAUTHORIZED]: 'You are not authorized to perform this action',

  [ErrorCodes.NETWORK_OFFLINE]: 'No internet connection',
  [ErrorCodes.NETWORK_TIMEOUT]: 'Request timed out',
  [ErrorCodes.NETWORK_SERVER_ERROR]: 'Server error occurred',

  [ErrorCodes.VALIDATION_REQUIRED]: '{{field}} is required',
  [ErrorCodes.VALIDATION_INVALID_FORMAT]: 'Invalid {{field}} format',
  [ErrorCodes.VALIDATION_INVALID_LENGTH]: '{{field}} length must be between {{min}} and {{max}}',

  [ErrorCodes.BUSINESS_INSUFFICIENT_STOCK]: 'Insufficient stock for {{product}}',
  [ErrorCodes.BUSINESS_INVALID_OPERATION]: 'Invalid operation',
  [ErrorCodes.BUSINESS_DUPLICATE_ENTRY]: '{{field}} already exists',

  [ErrorCodes.SYSTEM_INTERNAL_ERROR]: 'Internal system error',
  [ErrorCodes.SYSTEM_DATABASE_ERROR]: 'Database error occurred',
  [ErrorCodes.SYSTEM_CONFIGURATION_ERROR]: 'System configuration error',
} as const;

export const handleAPIError = (error: any): APIError => {
  if (error instanceof APIError) {
    return error;
  }

  // Network errors
  if (!error.response) {
    if (!navigator.onLine) {
      return new APIError(
        ErrorMessages[ErrorCodes.NETWORK_OFFLINE],
        0,
        ErrorCodes.NETWORK_OFFLINE
      );
    }
    return new APIError(
      ErrorMessages[ErrorCodes.NETWORK_TIMEOUT],
      0,
      ErrorCodes.NETWORK_TIMEOUT
    );
  }

  const { status, data } = error.response;

  // Authentication errors
  if (status === 401) {
    if (data?.code === ErrorCodes.AUTH_TOKEN_EXPIRED) {
      return new APIError(
        ErrorMessages[ErrorCodes.AUTH_TOKEN_EXPIRED],
        status,
        ErrorCodes.AUTH_TOKEN_EXPIRED
      );
    }
    return new APIError(
      ErrorMessages[ErrorCodes.AUTH_UNAUTHORIZED],
      status,
      ErrorCodes.AUTH_UNAUTHORIZED
    );
  }

  // Validation errors
  if (status === 400) {
    const code = data?.code || ErrorCodes.VALIDATION_INVALID_FORMAT;
    const message = ErrorMessages[code] || data?.message || 'Invalid request';
    return new APIError(message, status, code);
  }

  // Server errors
  if (status >= 500) {
    return new APIError(
      ErrorMessages[ErrorCodes.SYSTEM_INTERNAL_ERROR],
      status,
      ErrorCodes.SYSTEM_INTERNAL_ERROR
    );
  }

  // Default error
  return new APIError(
    data?.message || 'An unexpected error occurred',
    status,
    data?.code
  );
};

export const formatErrorMessage = (
  message: string,
  params: Record<string, string | number> = {}
): string => {
  return Object.entries(params).reduce(
    (msg, [key, value]) => msg.replace(`{{${key}}}`, String(value)),
    message
  );
}; 