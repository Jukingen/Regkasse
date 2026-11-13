/** Canonical walk-in / guest customer id — matches backend WalkInCustomerConstants.GuestCustomerId. */
export const WALK_IN_CUSTOMER_ID = '00000000-0000-0000-0000-000000000001';

export function isSystemCustomer(customer: {
  id?: string | null;
  isSystem?: boolean | null;
}): boolean {
  return Boolean(customer.isSystem) || customer.id === WALK_IN_CUSTOMER_ID;
}
