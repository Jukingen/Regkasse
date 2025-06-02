import { apiClient } from './config';

export interface SalesReport {
    period: string;
    totalSales: number;
    totalTax: number;
    totalItems: number;
    salesByTaxType: {
        standard: number;
        reduced: number;
        special: number;
    };
    salesByCategory: {
        [category: string]: number;
    };
}

export interface CashReport {
    period: string;
    openingBalance: number;
    closingBalance: number;
    totalCashIn: number;
    totalCashOut: number;
    transactions: {
        type: 'cash_in' | 'cash_out';
        amount: number;
        description: string;
        timestamp: string;
    }[];
}

export type ReportPeriod = 'daily' | 'weekly' | 'monthly' | 'yearly';

export const reportService = {
    getSalesReport: async (period: ReportPeriod): Promise<SalesReport> => {
        return apiClient.get<SalesReport>(`/reports/sales/${period}`);
    },

    getCashReport: async (period: ReportPeriod): Promise<CashReport> => {
        return apiClient.get<CashReport>(`/reports/cash/${period}`);
    },

    getDailyReport: async (date: string): Promise<SalesReport & CashReport> => {
        return apiClient.get<SalesReport & CashReport>(`/reports/daily/${date}`);
    }
}; 