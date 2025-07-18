import { Alert } from 'react-native';

export interface APIError {
    status?: number;
    data?: any;
    message: string;
}

export const handleAPIError = (error: any): APIError => {
    console.error('API Error:', error);

    // Network errors
    if (!error.response) {
        return {
            message: 'Network error. Please check your connection and try again.'
        };
    }

    const { status, data } = error.response;

    // HTTP status code based errors
    switch (status) {
        case 400:
            return {
                status,
                data,
                message: data?.message || 'Invalid request. Please check your input and try again.'
            };
        case 401:
            return {
                status,
                data,
                message: 'Authentication failed. Please login again.'
            };
        case 403:
            return {
                status,
                data,
                message: 'Access denied. You do not have permission to perform this action.'
            };
        case 404:
            return {
                status,
                data,
                message: 'Resource not found. Please check the URL and try again.'
            };
        case 409:
            return {
                status,
                data,
                message: data?.message || 'Conflict occurred. The resource already exists.'
            };
        case 422:
            return {
                status,
                data,
                message: data?.message || 'Validation failed. Please check your input.'
            };
        case 500:
            return {
                status,
                data,
                message: 'Server error. Please try again later.'
            };
        case 502:
            return {
                status,
                data,
                message: 'Bad gateway. Please try again later.'
            };
        case 503:
            return {
                status,
                data,
                message: 'Service unavailable. Please try again later.'
            };
        default:
            return {
                status,
                data,
                message: data?.message || 'An unexpected error occurred. Please try again.'
            };
    }
};

export const showErrorAlert = (error: APIError | string, title: string = 'Error') => {
    const message = typeof error === 'string' ? error : error.message;
    Alert.alert(title, message, [{ text: 'OK' }]);
};

export const showSuccessAlert = (message: string, title: string = 'Success') => {
    Alert.alert(title, message, [{ text: 'OK' }]);
};

export const showConfirmationAlert = (
    message: string,
    title: string = 'Confirm',
    onConfirm: () => void,
    onCancel?: () => void
) => {
    Alert.alert(
        title,
        message,
        [
            { text: 'Cancel', style: 'cancel', onPress: onCancel },
            { text: 'Confirm', style: 'destructive', onPress: onConfirm }
        ]
    );
};

// Specific error messages for different operations
export const ErrorMessages = {
    // Authentication
    LOGIN_FAILED: 'Login failed. Please check your credentials and try again.',
    LOGOUT_FAILED: 'Logout failed. Please try again.',
    SESSION_EXPIRED: 'Your session has expired. Please login again.',
    
    // Products
    PRODUCTS_LOAD_FAILED: 'Failed to load products. Please check your connection and try again.',
    PRODUCT_CREATE_FAILED: 'Failed to create product. Please try again.',
    PRODUCT_UPDATE_FAILED: 'Failed to update product. Please try again.',
    PRODUCT_DELETE_FAILED: 'Failed to delete product. Please try again.',
    PRODUCT_NOT_FOUND: 'Product not found.',
    INSUFFICIENT_STOCK: 'Insufficient stock available.',
    
    // Customers
    CUSTOMERS_LOAD_FAILED: 'Failed to load customers. Please check your connection and try again.',
    CUSTOMER_CREATE_FAILED: 'Failed to create customer. Please try again.',
    CUSTOMER_UPDATE_FAILED: 'Failed to update customer. Please try again.',
    CUSTOMER_DELETE_FAILED: 'Failed to delete customer. Please try again.',
    CUSTOMER_NOT_FOUND: 'Customer not found.',
    
    // Orders
    ORDER_CREATE_FAILED: 'Failed to create order. Please try again.',
    ORDER_UPDATE_FAILED: 'Failed to update order. Please try again.',
    ORDER_CANCEL_FAILED: 'Failed to cancel order. Please try again.',
    ORDER_COMPLETE_FAILED: 'Failed to complete order. Please try again.',
    ORDER_NOT_FOUND: 'Order not found.',
    NO_ITEMS_IN_ORDER: 'No items added to order.',
    
    // Payments
    PAYMENT_FAILED: 'Payment failed. Please try again.',
    PAYMENT_PROCESSING_ERROR: 'Error occurred during payment processing.',
    INVALID_PAYMENT_AMOUNT: 'Invalid payment amount.',
    INSUFFICIENT_PAYMENT: 'Payment amount is insufficient.',
    
    // Reports
    REPORTS_LOAD_FAILED: 'Failed to load reports. Please try again.',
    REPORT_EXPORT_FAILED: 'Failed to export report. Please try again.',
    REPORT_PRINT_FAILED: 'Failed to print report. Please try again.',
    
    // Hardware
    PRINTER_CONNECTION_FAILED: 'Failed to connect to printer. Please check the connection.',
    TSE_CONNECTION_FAILED: 'Failed to connect to TSE device. Please check the connection.',
    SCANNER_ERROR: 'Scanner error. Please try again.',
    
    // Network
    NETWORK_ERROR: 'Network connection error. Please check your internet connection.',
    FINANZONLINE_UNAVAILABLE: 'FinanzOnline service is currently unavailable.',
    PENDING_INVOICES_ERROR: 'Error processing pending invoices.',
    INVOICE_SUBMISSION_FAILED: 'Invoice submission to FinanzOnline failed.',
    NETWORK_TIMEOUT: 'Network request timed out. Please try again.',
    CONNECTION_LOST: 'Connection lost. Please check your network settings.',
    SYNC_FAILED: 'Failed to sync data. Please try again.',
    
    // General
    UNKNOWN_ERROR: 'An unexpected error occurred. Please try again.',
    VALIDATION_ERROR: 'Please check your input and try again.',
    PERMISSION_DENIED: 'You do not have permission to perform this action.',
    RESOURCE_NOT_FOUND: 'Resource not found.',
    SERVER_ERROR: 'Server error. Please try again later.',
    
    // Success messages
    PRODUCT_CREATED: 'Product created successfully.',
    PRODUCT_UPDATED: 'Product updated successfully.',
    PRODUCT_DELETED: 'Product deleted successfully.',
    CUSTOMER_CREATED: 'Customer created successfully.',
    CUSTOMER_UPDATED: 'Customer updated successfully.',
    CUSTOMER_DELETED: 'Customer deleted successfully.',
    ORDER_CREATED: 'Order created successfully.',
    ORDER_UPDATED: 'Order updated successfully.',
    ORDER_CANCELLED: 'Order cancelled successfully.',
    ORDER_COMPLETED: 'Order completed successfully.',
    PAYMENT_SUCCESSFUL: 'Payment completed successfully.',
    SETTINGS_SAVED: 'Settings saved successfully.',
    DATA_SYNCED: 'Data synced successfully.',
};

// Helper function to get error message based on operation type
export const getErrorMessage = (operation: string, error?: any): string => {
    const errorKey = `${operation.toUpperCase()}_FAILED`;
    return ErrorMessages[errorKey as keyof typeof ErrorMessages] || ErrorMessages.UNKNOWN_ERROR;
};

// Helper function to get success message based on operation type
export const getSuccessMessage = (operation: string): string => {
    const successKey = `${operation.toUpperCase()}_SUCCESSFUL`;
    return ErrorMessages[successKey as keyof typeof ErrorMessages] || 'Operation completed successfully.';
}; 