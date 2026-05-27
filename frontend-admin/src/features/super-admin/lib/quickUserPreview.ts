/** Matches backend UniqueUsernameGenerator role prefixes for UI preview. */
export function getQuickUsernamePattern(role: string): string {
    switch (role?.trim()) {
        case 'Manager':
            return 'manager1 … manager999';
        case 'Cashier':
            return 'cashier1 … cashier999';
        case 'Accountant':
        case 'SuperAdmin':
            return role === 'SuperAdmin' ? 'admin1 … admin999' : 'user1 … user999';
        default:
            return 'user1 … user999';
    }
}
