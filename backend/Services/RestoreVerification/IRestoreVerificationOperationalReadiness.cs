namespace KasseAPI_Final.Services.RestoreVerification;

public interface IRestoreVerificationOperationalReadiness
{
    RestoreVerificationConfigurationHealthSnapshot GetConfigurationHealth();
}
