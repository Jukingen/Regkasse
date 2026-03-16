import { apiClient } from './config';
import type { Customer } from './customerService';

/** Minimal employee entry for POS list (from GET /api/Employee/list). */
export interface EmployeeSummary {
  employeeNumber: string;
  name: string;
  userId: string;
  customerId: string;
}

/**
 * POS employee identification: lookup by EmployeeNumber and list employees.
 * Returns the benefit-bearing Customer record for that employee (same shape as Customer)
 * so payment flow can continue to use customerId without change.
 */
class EmployeeService {
  private baseUrl = '/Employee';

  /**
   * Look up employee by EmployeeNumber and return the linked Customer used for benefit application.
   * Returns null if not found or employee has no linked benefit identity (404).
   */
  async getByEmployeeNumber(employeeNumber: string): Promise<Customer | null> {
    const trimmed = String(employeeNumber ?? '').trim();
    if (!trimmed) return null;
    try {
      const response = await apiClient.get<{ data?: Customer }>(
        `${this.baseUrl}/by-number/${encodeURIComponent(trimmed)}`
      );
      const customer = (response as any)?.data ?? response;
      return customer as Customer;
    } catch (e: any) {
      if (e?.response?.status === 404 || e?.status === 404) return null;
      throw e;
    }
  }

  /**
   * List active employees with linked benefit identity (for POS "Aus Liste wählen").
   */
  async getAllEmployees(): Promise<EmployeeSummary[]> {
    const response = await apiClient.get<{ data?: EmployeeSummary[] }>(`${this.baseUrl}/list`);
    const list = (response as any)?.data ?? response;
    return Array.isArray(list) ? list : [];
  }
}

export const employeeService = new EmployeeService();
export default employeeService;
