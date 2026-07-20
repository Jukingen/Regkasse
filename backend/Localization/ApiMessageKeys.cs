namespace KasseAPI_Final.Localization;

/// <summary>Stable keys for user-facing API error and status messages.</summary>
public static class ApiMessageKeys
{
    // Auth
    public const string InvalidLoginCredentials = "auth.invalid_login_credentials";
    public const string AccountTemporarilyLocked = "auth.account_temporarily_locked";
    public const string ForgotPasswordGeneric = "auth.forgot_password_generic";
    public const string RegistrationFailed = "auth.registration_failed";
    public const string UserNotFound = "auth.user_not_found";
    public const string InvalidPassword = "auth.invalid_password";
    public const string PasswordChangeRequired = "auth.password_change_required";
    public const string LicenseExpired = "auth.license_expired";
    public const string AccountNotActive = "auth.account_not_active";
    public const string NotAuthorizedForApp = "auth.not_authorized_for_app";
    public const string LoginError = "auth.login_error";
    public const string TwoFactorRequired = "auth.two_factor_required";
    public const string TwoFactorInvalid = "auth.two_factor_invalid";
    public const string TwoFactorChallengeExpired = "auth.two_factor_challenge_expired";
    public const string UserCreatedSuccess = "auth.user_created_success";
    public const string TenantMembershipRequired = "auth.tenant_membership_required";
    public const string TenantDisabled = "auth.tenant_disabled";
    public const string TenantLicenseLockdown = "auth.tenant_license_lockdown";
    public const string LicenseStatusActive = "license.status.active";
    public const string LicenseStatusExpiringSoon = "license.status.expiring_soon";
    public const string LicenseStatusGrace = "license.status.grace";
    public const string LicenseStatusLocked = "license.status.locked";

    // Password change / Identity validation
    public const string PasswordChangeValidationFailed = "password.change_validation_failed";
    public const string PasswordChangeFailed = "password.change_failed";
    public const string PasswordChangeSuccess = "password.change_success";
    public const string PasswordCurrentIncorrect = "password.current_incorrect";
    public const string PasswordResetSuccess = "password.reset_success";
    public const string PasswordResetFailed = "password.reset_failed";
    public const string PasswordRequiresDigit = "identity.password_requires_digit";
    public const string PasswordRequiresLower = "identity.password_requires_lower";
    public const string PasswordRequiresUpper = "identity.password_requires_upper";
    public const string PasswordRequiresNonAlphanumeric = "identity.password_requires_non_alphanumeric";
    public const string PasswordTooShort = "identity.password_too_short";
    public const string PasswordRequiresUniqueChars = "identity.password_requires_unique_chars";
    public const string PasswordMismatch = "identity.password_mismatch";
    public const string PasswordValidationUnknown = "identity.password_validation_unknown";

    // Cash register
    public const string RegistersFetchError = "cash_register.registers_fetch_error";
    public const string RegistersFetchSuccess = "cash_register.registers_fetch_success";
    public const string RegisterNotFound = "cash_register.not_found";
    public const string RegisterFetchSuccess = "cash_register.fetch_success";
    public const string RegisterFetchError = "cash_register.fetch_error";
    public const string RegisterCreateSuccess = "cash_register.create_success";
    public const string RegisterCreateError = "cash_register.create_error";
    public const string RegisterUpdateError = "cash_register.update_error";
    public const string RegisterOpenSuccess = "cash_register.open_success";
    public const string RegisterAlreadyOpen = "cash_register.already_open";
    public const string RegisterOpenedByOtherUser = "cash_register.opened_by_other_user";
    public const string MonthlyReceiptRequired = "cash_register.monthly_receipt_required";
    public const string RegisterCannotOpenInState = "cash_register.cannot_open_in_state";
    public const string RegisterOpenError = "cash_register.open_error";
    public const string RegisterCloseSuccess = "cash_register.close_success";
    public const string RegisterAlreadyClosed = "cash_register.already_closed";
    public const string RegisterCloseError = "cash_register.close_error";
    public const string TransactionsFetchSuccess = "cash_register.transactions_fetch_success";
    public const string TransactionsFetchError = "cash_register.transactions_fetch_error";

    // FinanzOnline
    public const string CompanySettingsNotFound = "finanz_online.company_settings_not_found";
    public const string FinanzOnlineConfigFetchError = "finanz_online.config_fetch_error";
    public const string FinanzOnlineConfigUpdateError = "finanz_online.config_update_error";
    public const string InvoiceSubmissionFailed = "finanz_online.invoice_submission_failed";
    public const string FinanzOnlineErrorsFetchError = "finanz_online.errors_fetch_error";
    public const string FinanzOnlineNotEnabled = "finanz_online.not_enabled";
    public const string FinanzOnlineConnectionTestFailed = "finanz_online.connection_test_failed";
    public const string HistoryFetchError = "finanz_online.history_fetch_error";

    // Invoice
    public const string InvoiceNotFound = "invoice.not_found";
    public const string CompanyNameRequired = "invoice.company_name_required";
    public const string CompanyTaxNumberRequired = "invoice.company_tax_number_required";
    public const string CompanyTaxNumberInvalidFormat = "invoice.company_tax_number_invalid_format";
    public const string OriginalInvoiceNotFound = "invoice.original_not_found";
}
