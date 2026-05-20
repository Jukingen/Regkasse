export type PasswordStrengthLevel = 0 | 1 | 2 | 3 | 4;

export type PasswordStrengthResult = {
    level: PasswordStrengthLevel;
    percent: number;
    labelKey: string;
};

/**
 * Scores auto-generated provisioning passwords for operator feedback.
 */
export function evaluatePasswordStrength(password: string): PasswordStrengthResult {
    if (!password) {
        return { level: 0, percent: 0, labelKey: 'tenants.provisioning.passwordStrength.empty' };
    }

    let score = 0;
    if (password.length >= 8) score++;
    if (password.length >= 12) score++;
    if (/[a-z]/.test(password) && /[A-Z]/.test(password)) score++;
    if (/\d/.test(password)) score++;
    if (/[^a-zA-Z0-9]/.test(password)) score++;

    const level = Math.min(4, Math.max(1, score - 1)) as PasswordStrengthLevel;
    const percent = level * 25;

    const labelKey =
        level >= 4
            ? 'tenants.provisioning.passwordStrength.strong'
            : level >= 3
              ? 'tenants.provisioning.passwordStrength.good'
              : level >= 2
                ? 'tenants.provisioning.passwordStrength.fair'
                : 'tenants.provisioning.passwordStrength.weak';

    return { level, percent, labelKey };
}
