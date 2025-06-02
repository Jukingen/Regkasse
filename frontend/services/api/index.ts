import { authService } from './authService';
import { productService } from './productService';
import { customerService } from './customerService';
import { reportService } from './reportService';
import { settingsService } from './settingsService';
import { apiClient, API_BASE_URL } from './config';

export const api = {
    auth: authService,
    products: productService,
    customers: customerService,
    reports: reportService,
    settings: settingsService,
    client: apiClient,
    baseUrl: API_BASE_URL
};

export * from './authService';
export * from './productService';
export * from './customerService';
export * from './reportService';
export * from './settingsService';
export * from './config'; 