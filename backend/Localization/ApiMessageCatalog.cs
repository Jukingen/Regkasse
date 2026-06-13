using KasseAPI_Final.Middleware;

namespace KasseAPI_Final.Localization;

public static class ApiMessageCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Messages =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            [ApiMessageKeys.InvalidLoginCredentials] = Msg(
                de: "Ungültiger Benutzername oder Passwort",
                en: "Invalid username or password",
                tr: "Kullanıcı adı veya şifre geçersiz"),
            [ApiMessageKeys.ForgotPasswordGeneric] = Msg(
                de: "Wenn ein Konto existiert, wird ein Link zum Zurücksetzen des Passworts gesendet.",
                en: "If an account exists, a password reset link will be sent.",
                tr: "Bir hesap varsa şifre sıfırlama bağlantısı gönderilecektir."),
            [ApiMessageKeys.RegistrationFailed] = Msg(
                de: "Registrierung fehlgeschlagen. Bitte überprüfen Sie Ihre Angaben.",
                en: "Registration failed. Please check your information.",
                tr: "Kayıt başarısız. Lütfen bilgilerinizi kontrol edin."),
            [ApiMessageKeys.UserNotFound] = Msg(
                de: "Benutzer nicht gefunden",
                en: "User not found",
                tr: "Kullanıcı bulunamadı"),
            [ApiMessageKeys.InvalidPassword] = Msg(
                de: "Ungültiges Passwort",
                en: "Invalid password",
                tr: "Geçersiz şifre"),
            [ApiMessageKeys.AccountNotActive] = Msg(
                de: "Konto ist nicht aktiv",
                en: "Account is not active",
                tr: "Hesap aktif değil"),
            [ApiMessageKeys.NotAuthorizedForApp] = Msg(
                de: "Dieser Benutzer ist für diese Anwendung nicht berechtigt.",
                en: "This user is not authorized for this application.",
                tr: "Bu kullanıcı bu uygulama için yetkili değil."),
            [ApiMessageKeys.LoginError] = Msg(
                de: "Bei der Anmeldung ist ein Fehler aufgetreten",
                en: "An error occurred during login",
                tr: "Giriş işlemi sırasında hata oluştu"),
            [ApiMessageKeys.UserCreatedSuccess] = Msg(
                de: "Benutzer erfolgreich erstellt",
                en: "User created successfully",
                tr: "Kullanıcı başarıyla oluşturuldu"),
            [ApiMessageKeys.TenantMembershipRequired] = Msg(
                de: "Kein Zugriff auf diesen Mandanten",
                en: "No access to this tenant",
                tr: "Bu kiracıya erişim yok"),
            [ApiMessageKeys.TenantDisabled] = Msg(
                de: "Dieser Mandant wurde deaktiviert.",
                en: "This tenant has been deactivated.",
                tr: "Bu kiracı devre dışı bırakıldı."),
            [ApiMessageKeys.TenantLicenseLockdown] = Msg(
                de: "Dieser Mandant ist wegen abgelaufener Lizenz gesperrt.",
                en: "This tenant is locked due to an expired license.",
                tr: "Bu kiracı süresi dolmuş lisans nedeniyle kilitlendi."),

            [ApiMessageKeys.PasswordChangeValidationFailed] = Msg(
                de: "Das neue Passwort erfüllt nicht alle Anforderungen.",
                en: "The new password does not meet all requirements.",
                tr: "Yeni şifre tüm gereksinimleri karşılamıyor."),
            [ApiMessageKeys.PasswordChangeFailed] = Msg(
                de: "Passwort konnte nicht geändert werden.",
                en: "Failed to change password.",
                tr: "Şifre değiştirilemedi."),
            [ApiMessageKeys.PasswordChangeSuccess] = Msg(
                de: "Passwort wurde erfolgreich geändert. Bitte melden Sie sich erneut an.",
                en: "Password changed successfully. Please sign in again.",
                tr: "Şifre başarıyla değiştirildi. Lütfen tekrar giriş yapın."),
            [ApiMessageKeys.PasswordCurrentIncorrect] = Msg(
                de: "Das aktuelle Passwort ist falsch.",
                en: "The current password is incorrect.",
                tr: "Mevcut şifre yanlış."),
            [ApiMessageKeys.PasswordResetSuccess] = Msg(
                de: "Passwort wurde erfolgreich zurückgesetzt.",
                en: "Password reset successfully.",
                tr: "Şifre başarıyla sıfırlandı."),
            [ApiMessageKeys.PasswordResetFailed] = Msg(
                de: "Passwort konnte nicht zurückgesetzt werden.",
                en: "Password reset failed.",
                tr: "Şifre sıfırlanamadı."),
            [ApiMessageKeys.PasswordRequiresDigit] = Msg(
                de: "Das Passwort muss mindestens eine Zahl (0–9) enthalten.",
                en: "The password must contain at least one digit (0–9).",
                tr: "Şifre en az bir rakam (0–9) içermelidir."),
            [ApiMessageKeys.PasswordRequiresLower] = Msg(
                de: "Das Passwort muss mindestens einen Kleinbuchstaben (a–z) enthalten.",
                en: "The password must contain at least one lowercase letter (a–z).",
                tr: "Şifre en az bir küçük harf (a–z) içermelidir."),
            [ApiMessageKeys.PasswordRequiresUpper] = Msg(
                de: "Das Passwort muss mindestens einen Großbuchstaben (A–Z) enthalten.",
                en: "The password must contain at least one uppercase letter (A–Z).",
                tr: "Şifre en az bir büyük harf (A–Z) içermelidir."),
            [ApiMessageKeys.PasswordRequiresNonAlphanumeric] = Msg(
                de: "Das Passwort muss mindestens ein Sonderzeichen enthalten (z. B. !, ?, #).",
                en: "The password must contain at least one special character (e.g. !, ?, #).",
                tr: "Şifre en az bir özel karakter içermelidir (ör. !, ?, #)."),
            [ApiMessageKeys.PasswordTooShort] = Msg(
                de: "Das Passwort muss mindestens 8 Zeichen lang sein.",
                en: "The password must be at least 8 characters long.",
                tr: "Şifre en az 8 karakter uzunluğunda olmalıdır."),
            [ApiMessageKeys.PasswordRequiresUniqueChars] = Msg(
                de: "Das Passwort muss mindestens eine eindeutige Zeichenkombination verwenden.",
                en: "The password must use a more unique combination of characters.",
                tr: "Şifre daha benzersiz bir karakter kombinasyonu kullanmalıdır."),
            [ApiMessageKeys.PasswordMismatch] = Msg(
                de: "Das Passwort stimmt nicht überein.",
                en: "The password does not match.",
                tr: "Şifre eşleşmiyor."),
            [ApiMessageKeys.PasswordValidationUnknown] = Msg(
                de: "Das Passwort erfüllt eine Anforderung nicht.",
                en: "The password does not meet a requirement.",
                tr: "Şifre bir gereksinimi karşılamıyor."),

            [ApiMessageKeys.RegistersFetchError] = Msg(
                de: "Fehler beim Abrufen der Kassen",
                en: "Error fetching cash registers",
                tr: "Kasalar getirilirken bir hata oluştu"),
            [ApiMessageKeys.RegistersFetchSuccess] = Msg(
                de: "Kassen erfolgreich abgerufen",
                en: "Cash registers fetched successfully",
                tr: "Kasalar başarıyla getirildi"),
            [ApiMessageKeys.RegisterNotFound] = Msg(
                de: "Kasse nicht gefunden",
                en: "Cash register not found",
                tr: "Kasa bulunamadı"),
            [ApiMessageKeys.RegisterFetchSuccess] = Msg(
                de: "Kasse erfolgreich abgerufen",
                en: "Cash register fetched successfully",
                tr: "Kasa başarıyla getirildi"),
            [ApiMessageKeys.RegisterFetchError] = Msg(
                de: "Fehler beim Abrufen der Kasse",
                en: "Error fetching cash register",
                tr: "Kasa getirilirken bir hata oluştu"),
            [ApiMessageKeys.RegisterCreateSuccess] = Msg(
                de: "Kasse erfolgreich erstellt",
                en: "Cash register created successfully",
                tr: "Kasa başarıyla oluşturuldu"),
            [ApiMessageKeys.RegisterCreateError] = Msg(
                de: "Fehler beim Erstellen der Kasse",
                en: "Error creating cash register",
                tr: "Kasa oluşturulurken bir hata oluştu"),
            [ApiMessageKeys.RegisterUpdateError] = Msg(
                de: "Fehler beim Aktualisieren der Kasse",
                en: "Error updating cash register",
                tr: "Kasa güncellenirken bir hata oluştu"),
            [ApiMessageKeys.RegisterOpenSuccess] = Msg(
                de: "Kasse erfolgreich geöffnet",
                en: "Cash register opened successfully",
                tr: "Kasa başarıyla açıldı"),
            [ApiMessageKeys.RegisterAlreadyOpen] = Msg(
                de: "Kasse ist bereits geöffnet",
                en: "Cash register is already open",
                tr: "Kasa zaten açık"),
            [ApiMessageKeys.RegisterOpenedByOtherUser] = Msg(
                de: "Kasse ist von einem anderen Benutzer geöffnet.",
                en: "Cash register is opened by another user.",
                tr: "Kasa başka bir kullanıcı tarafından açıldı."),
            [ApiMessageKeys.MonthlyReceiptRequired] = Msg(
                de: "Monatsbeleg muss für den aktuellen Monat erstellt werden.",
                en: "Monthly receipt must be created for the current month.",
                tr: "Cari ay için aylık makbuz oluşturulmalıdır."),
            [ApiMessageKeys.RegisterCannotOpenInState] = Msg(
                de: "Kasse kann in diesem Zustand nicht geöffnet werden.",
                en: "Cash register cannot be opened in this state.",
                tr: "Kasa bu durumda açılamaz."),
            [ApiMessageKeys.RegisterOpenError] = Msg(
                de: "Fehler beim Öffnen der Kasse",
                en: "Error opening cash register",
                tr: "Kasa açılırken bir hata oluştu"),
            [ApiMessageKeys.RegisterCloseSuccess] = Msg(
                de: "Kasse erfolgreich geschlossen",
                en: "Cash register closed successfully",
                tr: "Kasa başarıyla kapatıldı"),
            [ApiMessageKeys.RegisterAlreadyClosed] = Msg(
                de: "Kasse ist bereits geschlossen",
                en: "Cash register is already closed",
                tr: "Kasa zaten kapalı"),
            [ApiMessageKeys.RegisterCloseError] = Msg(
                de: "Fehler beim Schließen der Kasse",
                en: "Error closing cash register",
                tr: "Kasa kapatılırken bir hata oluştu"),
            [ApiMessageKeys.TransactionsFetchSuccess] = Msg(
                de: "Transaktionen erfolgreich abgerufen",
                en: "Transactions fetched successfully",
                tr: "İşlemler başarıyla getirildi"),
            [ApiMessageKeys.TransactionsFetchError] = Msg(
                de: "Fehler beim Abrufen der Transaktionen",
                en: "Error fetching transactions",
                tr: "İşlemler getirilirken bir hata oluştu"),

            [ApiMessageKeys.CompanySettingsNotFound] = Msg(
                de: "Firmeneinstellungen nicht gefunden",
                en: "Company settings not found",
                tr: "Firma ayarları bulunamadı"),
            [ApiMessageKeys.FinanzOnlineConfigFetchError] = Msg(
                de: "FinanzOnline-Konfiguration konnte nicht abgerufen werden",
                en: "Could not retrieve FinanzOnline configuration",
                tr: "FinanzOnline konfigürasyonu alınamadı"),
            [ApiMessageKeys.FinanzOnlineConfigUpdateError] = Msg(
                de: "FinanzOnline-Konfiguration konnte nicht aktualisiert werden",
                en: "Could not update FinanzOnline configuration",
                tr: "FinanzOnline konfigürasyonu güncellenemedi"),
            [ApiMessageKeys.InvoiceSubmissionFailed] = Msg(
                de: "Rechnungsübermittlung fehlgeschlagen",
                en: "Invoice submission failed",
                tr: "Fatura gönderimi başarısız"),
            [ApiMessageKeys.FinanzOnlineErrorsFetchError] = Msg(
                de: "FinanzOnline-Fehler konnten nicht abgerufen werden",
                en: "Could not retrieve FinanzOnline errors",
                tr: "FinanzOnline hataları alınamadı"),
            [ApiMessageKeys.FinanzOnlineNotEnabled] = Msg(
                de: "FinanzOnline ist nicht aktiviert",
                en: "FinanzOnline is not enabled",
                tr: "FinanzOnline etkin değil"),
            [ApiMessageKeys.FinanzOnlineConnectionTestFailed] = Msg(
                de: "FinanzOnline-Verbindungstest fehlgeschlagen",
                en: "FinanzOnline connection test failed",
                tr: "FinanzOnline bağlantı testi başarısız"),
            [ApiMessageKeys.HistoryFetchError] = Msg(
                de: "Verlauf konnte nicht abgerufen werden",
                en: "Could not retrieve history",
                tr: "Geçmiş alınamadı"),

            [ApiMessageKeys.InvoiceNotFound] = Msg(
                de: "Rechnung nicht gefunden",
                en: "Invoice not found",
                tr: "Fatura bulunamadı"),
            [ApiMessageKeys.CompanyNameRequired] = Msg(
                de: "Firmenname ist erforderlich",
                en: "Company name is required",
                tr: "Firma adı gerekli"),
            [ApiMessageKeys.CompanyTaxNumberRequired] = Msg(
                de: "Firmensteuernummer ist erforderlich",
                en: "Company tax number is required",
                tr: "Firma vergi numarası gerekli"),
            [ApiMessageKeys.CompanyTaxNumberInvalidFormat] = Msg(
                de: "Firmensteuernummer muss im ATU-Format sein",
                en: "Company tax number must be in ATU format",
                tr: "Firma vergi numarası ATU formatında olmalı"),
            [ApiMessageKeys.OriginalInvoiceNotFound] = Msg(
                de: "Originalrechnung nicht gefunden",
                en: "Original invoice not found",
                tr: "Orijinal fatura bulunamadı"),
        };

    public static string Get(string key, string? language)
    {
        var normalized = LanguageMiddleware.NormalizeLanguage(language);
        if (!Messages.TryGetValue(key, out var translations))
            return key;

        if (translations.TryGetValue(normalized, out var text))
            return text;

        if (translations.TryGetValue(LanguageMiddleware.DefaultLanguage, out text))
            return text;

        return translations.Values.First();
    }

    private static IReadOnlyDictionary<string, string> Msg(string de, string en, string tr) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["de"] = de,
            ["en"] = en,
            ["tr"] = tr,
        };
}
