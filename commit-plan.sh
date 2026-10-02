#!/usr/bin/env bash
# commit-plan.sh
# Split the country-layer and vertical-profile working tree into reviewable commits.
# Commits 1–8 stay in order. Commit 4b carries migration chronology and the CI filter.
#
#   DRY_RUN=1 bash commit-plan.sh   # print git add / git commit only
#   DRY_RUN=0 bash commit-plan.sh   # execute
#
# Does not push.

set -euo pipefail

DRY_RUN="${DRY_RUN:-1}"
COMMITS_CREATED=0
BAK_PATH="backend/Migrations/AppDbContextModelSnapshot.cs.bak"

run_git() {
  if [[ "${DRY_RUN}" == "1" ]]; then
    printf 'git'
    local arg
    for arg in "$@"; do
      printf ' %q' "$arg"
    done
    printf '\n'
    return 0
  fi
  git "$@"
}

if [[ -z "$(git status --porcelain)" ]]; then
  echo "preflight: working tree has no uncommitted changes." >&2
  exit 1
fi

if ! git diff --check; then
  echo "preflight: git diff --check reported conflict markers or whitespace errors." >&2
  exit 1
fi

if ! git diff --cached --check; then
  echo "preflight: staged diff failed git diff --check." >&2
  exit 1
fi

if [[ -n "$(git ls-files -u)" ]]; then
  echo "preflight: unmerged paths are present." >&2
  exit 1
fi

echo "# unstage the index so each commit adds only its own paths"
run_git restore --staged :/

# Commit 1: feat(country): add Peppol, e-invoicing, and DE/CH fiscal adapters
run_git add -- \
  'backend/Configuration/KassenSicherheitHostOptionsValidator.cs' \
  'backend/Configuration/KassenSicherheitOptions.cs' \
  'backend/Configuration/PeppolOptions.cs' \
  'backend/Configuration/PeppolStorecoveOptionsValidator.cs' \
  'backend/Configuration/QrRechnungOptions.cs' \
  'backend/Controllers/AdminChQrGapAcceptanceController.cs' \
  'backend/Controllers/AdminChQrInvoiceOperatorController.cs' \
  'backend/Controllers/AdminFeatureFlagsController.cs' \
  'backend/Controllers/AdminMwstRatesController.cs' \
  'backend/Controllers/AdminPeppolParticipantsController.cs' \
  'backend/Controllers/AdminPeppolSubmissionsController.cs' \
  'backend/Fiscal/FiscalSignatureRouter.cs' \
  'backend/KasseAPI_Final.Tests/AdminChQrInvoiceOperatorControllerTests.cs' \
  'backend/KasseAPI_Final.Tests/AdminCountriesControllerTests.cs' \
  'backend/KasseAPI_Final.Tests/AdminPeppolSubmissionsControllerTests.cs' \
  'backend/KasseAPI_Final.Tests/AustriaStrategyDelegationTests.cs' \
  'backend/KasseAPI_Final.Tests/ChMwstEffectiveRatesTests.cs' \
  'backend/KasseAPI_Final.Tests/ChQrGapAcceptanceTests.cs' \
  'backend/KasseAPI_Final.Tests/ChQrKnownGapsTests.cs' \
  'backend/KasseAPI_Final.Tests/Countries/EInvoicing/PeppolCanaryEndToEndTests.cs' \
  'backend/KasseAPI_Final.Tests/CountryCallSiteMigrationTests.cs' \
  'backend/KasseAPI_Final.Tests/CountryFiscalLockEvaluatorTests.cs' \
  'backend/KasseAPI_Final.Tests/CountryLayerBypassGateTests.cs' \
  'backend/KasseAPI_Final.Tests/CountryLayerNewPackagesGateTests.cs' \
  'backend/KasseAPI_Final.Tests/CountryPaymentStrategyContractTests.cs' \
  'backend/KasseAPI_Final.Tests/CountryStrategyResolverTests.cs' \
  'backend/KasseAPI_Final.Tests/CreateTenantWizardScenarioTests.cs' \
  'backend/KasseAPI_Final.Tests/DeTaxSetMapperTests.cs' \
  'backend/KasseAPI_Final.Tests/DeTseSignaturePersistenceTests.cs' \
  'backend/KasseAPI_Final.Tests/En16931XmlBuilderTests.cs' \
  'backend/KasseAPI_Final.Tests/FeatureFlagNamesTests.cs' \
  'backend/KasseAPI_Final.Tests/FeatureFlagServiceTests.cs' \
  'backend/KasseAPI_Final.Tests/FiscalSignatureRouterAtDeTests.cs' \
  'backend/KasseAPI_Final.Tests/FiskalyDeKassenSicherheitHttpClientTests.cs' \
  'backend/KasseAPI_Final.Tests/FiskalyDeKassenSicherheitServiceTests.cs' \
  'backend/KasseAPI_Final.Tests/GermanyInvoiceStrategyTests.cs' \
  'backend/KasseAPI_Final.Tests/GermanyTaxStrategyTests.cs' \
  'backend/KasseAPI_Final.Tests/KassenSicherheitHostOptionsValidatorTests.cs' \
  'backend/KasseAPI_Final.Tests/OssVatRateIsolationTests.cs' \
  'backend/KasseAPI_Final.Tests/PeppolAckPollingServiceTests.cs' \
  'backend/KasseAPI_Final.Tests/PeppolAckRetryTests.cs' \
  'backend/KasseAPI_Final.Tests/PeppolCanarySubmissionTests.cs' \
  'backend/KasseAPI_Final.Tests/PeppolParticipantSchemaTests.cs' \
  'backend/KasseAPI_Final.Tests/PeppolReservedExitTests.cs' \
  'backend/KasseAPI_Final.Tests/PeppolReservedFlagGuardTests.cs' \
  'backend/KasseAPI_Final.Tests/PeppolStorecoveOptionsValidatorTests.cs' \
  'backend/KasseAPI_Final.Tests/PeppolSubmissionTests.cs' \
  'backend/KasseAPI_Final.Tests/QrRechnungBuilderTests.cs' \
  'backend/KasseAPI_Final.Tests/SoftKassenSicherheitServiceTests.cs' \
  'backend/KasseAPI_Final.Tests/StorecovePeppolAccessPointClientTests.cs' \
  'backend/KasseAPI_Final.Tests/SwitzerlandInvoiceStrategyTests.cs' \
  'backend/KasseAPI_Final.Tests/SwitzerlandTaxStrategyTests.cs' \
  'backend/KasseAPI_Final.Tests/VatIdPatternConsolidationTests.cs' \
  'backend/Migrations/20260929114500_AddDeTseSignaturePersistence.Designer.cs' \
  'backend/Migrations/20260929114500_AddDeTseSignaturePersistence.cs' \
  'backend/Migrations/20260929215000_AddPeppolParticipantAndSubmissionOutbox.Designer.cs' \
  'backend/Migrations/20260929215000_AddPeppolParticipantAndSubmissionOutbox.cs' \
  'backend/Migrations/20260930055757_AddPeppolProviderMessageId.Designer.cs' \
  'backend/Migrations/20260930055757_AddPeppolProviderMessageId.cs' \
  'backend/Migrations/20260930060830_AddPeppolParticipantIdentity.Designer.cs' \
  'backend/Migrations/20260930060830_AddPeppolParticipantIdentity.cs' \
  'backend/Migrations/20260930143000_AddPeppolProviderAttemptCount.Designer.cs' \
  'backend/Migrations/20260930143000_AddPeppolProviderAttemptCount.cs' \
  'backend/Models/Countries/CountryProfileSummaryDto.cs' \
  'backend/Models/DeTseSignature.cs' \
  'backend/Models/EinvoiceSubmission.cs' \
  'backend/Models/Iso3166CountryCode.cs' \
  'backend/Models/PeppolParticipant.cs' \
  'backend/Services/Countries/ChMwstEffectiveRates.cs' \
  'backend/Services/Countries/CountryFiscalLockEvaluator.cs' \
  'backend/Services/Countries/CountryStrategyContext.cs' \
  'backend/Services/Countries/EInvoicing/EInvoicingNotSupportedForCountryException.cs' \
  'backend/Services/Countries/EInvoicing/En16931InvoiceValidationService.cs' \
  'backend/Services/Countries/EInvoicing/En16931Schematron.cs' \
  'backend/Services/Countries/EInvoicing/En16931UblXmlBuilder.cs' \
  'backend/Services/Countries/EInvoicing/NotImplementedXrechnungXmlBuilder.cs' \
  'backend/Services/Countries/EInvoicing/NotImplementedZugferdXmlBuilder.cs' \
  'backend/Services/Countries/EInvoicing/PeppolAccessPoint.cs' \
  'backend/Services/Countries/EInvoicing/PeppolAckPollingService.cs' \
  'backend/Services/Countries/EInvoicing/PeppolSubmissionService.cs' \
  'backend/Services/Countries/EInvoicing/StorecovePeppolAccessPointClient.cs' \
  'backend/Services/Countries/FiscalDocumentCountryStamp.cs' \
  'backend/Services/Countries/ICountryStrategyContext.cs' \
  'backend/Services/Countries/KassenSicherheit/FiskalyDeKassenSicherheitHttpClient.cs' \
  'backend/Services/Countries/KassenSicherheit/FiskalyDeKassenSicherheitService.cs' \
  'backend/Services/Countries/KassenSicherheit/IKassenSicherheitHttpClient.cs' \
  'backend/Services/Countries/KassenSicherheit/IKassenSicherheitService.cs' \
  'backend/Services/Countries/KassenSicherheit/KassenSicherheitHttpException.cs' \
  'backend/Services/Countries/KassenSicherheit/NotImplementedKassenSicherheitService.cs' \
  'backend/Services/Countries/KassenSicherheit/SoftKassenSicherheitService.cs' \
  'backend/Services/Countries/QrRechnung/ChQrGapAcceptanceService.cs' \
  'backend/Services/Countries/QrRechnung/ChQrKnownGapCatalog.cs' \
  'backend/Services/Countries/QrRechnung/ChQrKnownGaps.json' \
  'backend/Services/Countries/QrRechnung/IChQrGapAcceptanceService.cs' \
  'backend/Services/Countries/QrRechnung/QrRechnungAudit.cs' \
  'backend/Services/Countries/QrRechnung/QrRechnungBuilder.cs' \
  'backend/Services/Countries/QrRechnung/QrRechnungModels.cs' \
  'backend/Services/Countries/QrRechnung/QrRechnungPdf.cs' \
  'backend/Services/Countries/Strategies/Germany/DeTaxSetMapper.cs' \
  'backend/Services/Countries/Strategies/Germany/GermanyStrategies.cs' \
  'backend/Services/Countries/Strategies/InvoiceStrategyContracts.cs' \
  'backend/Services/Countries/Strategies/Switzerland/ChTaxSetMapper.cs' \
  'backend/Services/Countries/Strategies/Switzerland/SwitzerlandStrategies.cs' \
  'backend/Services/Countries/Strategies/TaxStrategyContracts.cs' \
  'backend/Services/FeatureFlags/CountryFeatureFlagDefaults.cs' \
  'backend/Services/FeatureFlags/FeatureFlagLockedException.cs' \
  'backend/Services/FeatureFlags/FeatureFlagNames.cs' \
  'backend/Services/FeatureFlags/FeatureFlagService.cs' \
  'docs/CANARY_LAUNCH_RUNBOOK.md' \
  'docs/COUNTRIES.md' \
  'docs/EINVOICING_EU.md' \
  'docs/EINVOICING_EU_SUBMISSION_PLAN.md' \
  'docs/FEATURE_FLAGS.md' \
  'docs/FISCAL_GERMANY.md' \
  'docs/FISCAL_GERMANY_PROVIDER_DECISION.md' \
  'docs/FISCAL_ROUTER_PLAN.md' \
  'docs/FISCAL_SWITZERLAND.md' \
  'docs/FISCAL_SWITZERLAND_QR_PLAN.md' \
  'frontend-admin/src/api/generated/model/chQrBankUploadConfirmationRequest.ts' \
  'frontend-admin/src/api/generated/model/getApiAdminPeppolSubmissionsParams.ts' \
  'frontend-admin/src/api/generated/model/peppolParticipantDto.ts' \
  'frontend-admin/src/api/generated/model/peppolSubmissionDetailDto.ts' \
  'frontend-admin/src/api/generated/model/peppolSubmissionListResponse.ts' \
  'frontend-admin/src/api/generated/model/peppolSubmissionRowDto.ts' \
  'frontend-admin/src/api/generated/model/registerPeppolParticipantRequest.ts' \
  'frontend-admin/src/app/(protected)/admin/kassensicherheit/page.tsx' \
  'frontend-admin/src/app/(protected)/admin/mwst/page.tsx' \
  'frontend-admin/src/app/(protected)/admin/peppol/participants/page.tsx' \
  'frontend-admin/src/app/(protected)/admin/peppol/submissions/page.tsx' \
  'frontend-admin/src/features/invoices/api/chQrRechnungOperatorApi.ts' \
  'frontend-admin/src/features/invoices/components/ChQrRechnungGapWarningBanner.tsx' \
  'frontend-admin/src/features/invoices/components/ChQrRechnungOperatorActions.tsx' \
  'frontend-admin/src/features/invoices/components/__tests__/ChQrRechnungGapWarningBanner.test.tsx' \
  'frontend-admin/src/features/invoices/components/__tests__/ChQrRechnungOperatorActions.test.tsx' \
  'frontend-admin/src/features/kassenSicherheit/KassenSicherheitCanaryPage.tsx' \
  'frontend-admin/src/features/kassenSicherheit/KassenSicherheitPage.tsx' \
  'frontend-admin/src/features/kassenSicherheit/__tests__/kassenSicherheitCanaryPage.test.tsx' \
  'frontend-admin/src/features/kassenSicherheit/__tests__/kassenSicherheitPage.test.tsx' \
  'frontend-admin/src/features/kassenSicherheit/api.ts' \
  'frontend-admin/src/features/mwst-canary/api.ts' \
  'frontend-admin/src/features/mwst/MwstRatesPage.tsx' \
  'frontend-admin/src/features/mwst/__tests__/mwstRatesPage.test.tsx' \
  'frontend-admin/src/features/peppol/PeppolParticipantsPage.tsx' \
  'frontend-admin/src/features/peppol/PeppolSubmissionsPage.tsx' \
  'frontend-admin/src/features/peppol/__tests__/peppolParticipantsPage.test.tsx' \
  'frontend-admin/src/features/peppol/__tests__/peppolSubmissionsPage.test.tsx' \
  'frontend-admin/src/features/peppol/failureHint.ts' \
  'frontend-admin/src/features/settings/hooks/__tests__/useCountryFormatting.test.tsx' \
  'frontend-admin/src/features/settings/hooks/__tests__/useCountryVatIdValidation.test.tsx' \
  'frontend-admin/src/features/settings/hooks/useCountryFormatting.ts' \
  'frontend-admin/src/features/settings/hooks/useCountryVatIdValidation.ts' \
  'frontend-admin/src/features/super-admin/components/ChQrGapAcceptancePanel.tsx' \
  'frontend-admin/src/features/super-admin/components/CreateTenantCountryDrivenFields.tsx' \
  'frontend-admin/src/features/super-admin/components/CreateTenantCountryStep.tsx' \
  'frontend-admin/src/features/super-admin/components/CreateTenantWizard.tsx' \
  'frontend-admin/src/features/super-admin/components/TenantCountryFiscalRegimeCard.tsx' \
  'frontend-admin/src/features/super-admin/components/__tests__/ChQrGapAcceptancePanel.test.tsx' \
  'frontend-admin/src/features/super-admin/components/__tests__/CreateTenantFiscalStep.test.tsx' \
  'frontend-admin/src/features/super-admin/components/__tests__/CreateTenantWizard.countryStep.test.tsx' \
  'frontend-admin/src/features/super-admin/components/__tests__/TenantCountryFiscalRegimeCard.test.tsx' \
  'frontend-admin/src/features/super-admin/components/__tests__/initialCountryFromCatalog.test.ts' \
  'frontend-admin/src/features/super-admin/components/createTenantFormTypes.ts' \
  'frontend-admin/src/features/tenant-portal/components/TenantCountryProfilePage.tsx' \
  'frontend-admin/src/features/tenant-portal/components/__tests__/TenantCountryProfilePage.test.tsx' \
  'frontend-admin/src/features/tenants/components/__tests__/countrySelectOptionsFromCatalog.test.ts' \
  'frontend-admin/src/i18n/locales/de/kassenSicherheit.json' \
  'frontend-admin/src/i18n/locales/de/peppol.json' \
  'frontend-admin/src/i18n/locales/de/tenantCountry.json' \
  'frontend-admin/src/i18n/locales/en/kassenSicherheit.json' \
  'frontend-admin/src/i18n/locales/en/peppol.json' \
  'frontend-admin/src/i18n/locales/en/tenantCountry.json' \
  'frontend-admin/src/i18n/locales/tr/kassenSicherheit.json' \
  'frontend-admin/src/i18n/locales/tr/peppol.json' \
  'frontend-admin/src/i18n/locales/tr/tenantCountry.json' \
  'frontend-admin/src/lib/__tests__/countryFormatProfiles.test.ts' \
  'frontend-admin/src/lib/countryFormatProfiles.ts' \
  'frontend-admin/tests/e2e/tenant-country-card.spec.ts' \
  'frontend-admin/tests/e2e/tenant-create.spec.ts' \
  'scripts/smoke/peppol-canary-test-smoke.mjs'
run_git commit -m 'feat(country): add Peppol, e-invoicing, and DE/CH fiscal adapters'
COMMITS_CREATED=$((COMMITS_CREATED + 1))
# Commit 2: feat(vertical-profiles): add profile catalog, tenant assignment, and POS context
run_git add -- \
  'backend/Controllers/AdminCurrentVerticalProfileController.cs' \
  'backend/Controllers/AdminTenantVerticalProfileController.cs' \
  'backend/Controllers/AdminVerticalProfilesController.cs' \
  'backend/Controllers/PosVerticalProfileController.cs' \
  'backend/KasseAPI_Final.Tests/PosCustomerVerticalProfileTests.cs' \
  'backend/KasseAPI_Final.Tests/VerticalProfileApiTests.cs' \
  'backend/KasseAPI_Final.Tests/VerticalProfileCatalogTests.cs' \
  'backend/KasseAPI_Final.Tests/VerticalProfileDomainFieldsTests.cs' \
  'backend/Migrations/20260930083226_AddVerticalProfiles.Designer.cs' \
  'backend/Migrations/20260930083226_AddVerticalProfiles.cs' \
  'backend/Migrations/20261001031200_AddVerticalProfileOverrides.Designer.cs' \
  'backend/Migrations/20261001031200_AddVerticalProfileOverrides.cs' \
  'backend/Models/TenantVerticalOverride.cs' \
  'backend/Models/VerticalProfile.cs' \
  'backend/Models/VerticalProfileOverride.cs' \
  'backend/Services/VerticalProfiles/VerticalProfileCatalogService.cs' \
  'backend/Services/VerticalProfiles/VerticalProfileDtos.cs' \
  'backend/Services/VerticalProfiles/VerticalProfileJson.cs' \
  'backend/Services/VerticalProfiles/VerticalProfileRegistry.cs' \
  'backend/Services/VerticalProfiles/VerticalProfileService.cs' \
  'docs/VERTICAL_PROFILES.md' \
  'frontend-admin/src/api/admin/vertical-profile.ts' \
  'frontend-admin/src/api/generated/model/effectiveVerticalProfileDto.ts' \
  'frontend-admin/src/api/generated/model/updateTenantVerticalProfileRequest.ts' \
  'frontend-admin/src/api/generated/model/verticalProfileDto.ts' \
  'frontend-admin/src/app/(protected)/admin/tenants/[tenantId]/vertical-profile/page.tsx' \
  'frontend-admin/src/app/(protected)/admin/vertical-profiles/page.tsx' \
  'frontend-admin/src/features/super-admin/components/TenantVerticalProfileEditor.tsx' \
  'frontend-admin/src/features/super-admin/components/__tests__/TenantVerticalProfileEditor.test.tsx' \
  'frontend-admin/src/features/vertical-profiles/VerticalConfigHub.tsx' \
  'frontend-admin/src/features/vertical-profiles/__tests__/VerticalConfigHub.test.tsx' \
  'frontend-admin/src/features/vertical-profiles/api.ts' \
  'frontend-admin/src/features/vertical-profiles/profilePreview.ts' \
  'frontend-admin/src/i18n/locales/de/verticalProfiles.json' \
  'frontend-admin/src/i18n/locales/en/verticalProfiles.json' \
  'frontend-admin/src/i18n/locales/tr/verticalProfiles.json' \
  'frontend/__tests__/VerticalProfileContext.test.tsx' \
  'frontend/components/DynamicField.tsx' \
  'frontend/components/IfVerticalFeature.tsx' \
  'frontend/contexts/VerticalProfileContext.tsx' \
  'frontend/i18n/locales/de/verticalProfiles.json' \
  'frontend/i18n/locales/en/verticalProfiles.json' \
  'frontend/i18n/locales/tr/verticalProfiles.json'
run_git commit -m 'feat(vertical-profiles): add profile catalog, tenant assignment, and POS context'
COMMITS_CREATED=$((COMMITS_CREATED + 1))
# Commit 3: feat(vertical-profiles): add vet, hair-salon, mobile-services, taxi, handy-shop, and ticket-sales
run_git add -- \
  'backend/Controllers/AdminLodgingController.cs' \
  'backend/Controllers/AdminProductImeisController.cs' \
  'backend/Controllers/AdminStaffController.cs' \
  'backend/Controllers/AdminTicketRedemptionsController.cs' \
  'backend/Controllers/PosAppointmentsController.cs' \
  'backend/Controllers/PosProductImeisController.cs' \
  'backend/Controllers/PosRoomsController.cs' \
  'backend/Controllers/PosStaffController.cs' \
  'backend/Controllers/PosTicketsController.cs' \
  'backend/KasseAPI_Final.Tests/AdminProductsDurationStaffTests.cs' \
  'backend/KasseAPI_Final.Tests/AdminStaffControllerTests.cs' \
  'backend/KasseAPI_Final.Tests/AdminTicketRedemptionsControllerTests.cs' \
  'backend/KasseAPI_Final.Tests/LodgingApiTests.cs' \
  'backend/KasseAPI_Final.Tests/OrderLocationDataTests.cs' \
  'backend/KasseAPI_Final.Tests/PaymentImeiTests.cs' \
  'backend/KasseAPI_Final.Tests/PaymentPrescriptionTests.cs' \
  'backend/KasseAPI_Final.Tests/PaymentTaxiTests.cs' \
  'backend/KasseAPI_Final.Tests/PaymentTicketTests.cs' \
  'backend/KasseAPI_Final.Tests/PosAppointmentApiTests.cs' \
  'backend/KasseAPI_Final.Tests/PosStaffControllerTests.cs' \
  'backend/KasseAPI_Final.Tests/PosTicketsApiTests.cs' \
  'backend/KasseAPI_Final.Tests/ProductImeiApiTests.cs' \
  'backend/Migrations/20260930090543_AddVetAndHairSalonVerticalFields.Designer.cs' \
  'backend/Migrations/20260930090543_AddVetAndHairSalonVerticalFields.cs' \
  'backend/Migrations/20260930180000_AddAppointments.Designer.cs' \
  'backend/Migrations/20260930180000_AddAppointments.cs' \
  'backend/Migrations/20260930190000_AddPaymentDetailsPrescriptionReference.Designer.cs' \
  'backend/Migrations/20260930190000_AddPaymentDetailsPrescriptionReference.cs' \
  'backend/Migrations/20260930220000_AddImeiTracking.Designer.cs' \
  'backend/Migrations/20260930220000_AddImeiTracking.cs' \
  'backend/Migrations/20260930240000_AddTicketProductFlag.Designer.cs' \
  'backend/Migrations/20260930240000_AddTicketProductFlag.cs' \
  'backend/Migrations/20260930241000_AddTicketRedemptions.Designer.cs' \
  'backend/Migrations/20260930241000_AddTicketRedemptions.cs' \
  'backend/Migrations/20261001040000_AddMobileServiceAddressAndLocation.Designer.cs' \
  'backend/Migrations/20261001040000_AddMobileServiceAddressAndLocation.cs' \
  'backend/Migrations/20261001050000_AddBeherbergungRoomsAndFolios.Designer.cs' \
  'backend/Migrations/20261001050000_AddBeherbergungRoomsAndFolios.cs' \
  'backend/Migrations/20261001063000_AddRoomsAndGuestFolios.Designer.cs' \
  'backend/Migrations/20261001063000_AddRoomsAndGuestFolios.cs' \
  'backend/Models/Appointment.cs' \
  'backend/Models/AppointmentStatus.cs' \
  'backend/Models/GuestFolio.cs' \
  'backend/Models/GuestFolioItem.cs' \
  'backend/Models/GuestFolioStatus.cs' \
  'backend/Models/ProductImei.cs' \
  'backend/Models/ProductImeiStatus.cs' \
  'backend/Models/Room.cs' \
  'backend/Models/RoomStatus.cs' \
  'backend/Models/TicketRedemption.cs' \
  'backend/Services/Appointments/AppointmentDtos.cs' \
  'backend/Services/Appointments/AppointmentService.cs' \
  'backend/Services/Appointments/IAppointmentService.cs' \
  'backend/Services/Imei/IProductImeiService.cs' \
  'backend/Services/Imei/ProductImeiDtos.cs' \
  'backend/Services/Imei/ProductImeiService.cs' \
  'backend/Services/Lodging/LodgingDtos.cs' \
  'backend/Services/Lodging/LodgingService.cs' \
  'backend/Services/Tickets/TicketDtos.cs' \
  'backend/Services/Tickets/TicketRedemptionService.cs' \
  'docs/BEHERBERGUNG.md' \
  'docs/TICKETS.md' \
  'frontend-admin/src/api/admin/lodging.ts' \
  'frontend-admin/src/api/admin/product-imeis.ts' \
  'frontend-admin/src/api/admin/staff.ts' \
  'frontend-admin/src/api/admin/ticket-redemptions.ts' \
  'frontend-admin/src/api/generated/model/customerAddressData.ts' \
  'frontend-admin/src/api/generated/model/customerPetData.ts' \
  'frontend-admin/src/api/generated/model/posCustomerPetDataDto.ts' \
  'frontend-admin/src/app/(protected)/admin/rooms/page.tsx' \
  'frontend-admin/src/app/(protected)/admin/tickets/redemptions/page.tsx' \
  'frontend-admin/src/features/customers/components/CustomerForm.tsx' \
  'frontend-admin/src/features/customers/components/CustomerList.tsx' \
  'frontend-admin/src/features/customers/components/__tests__/CustomerForm.addressDisplay.test.tsx' \
  'frontend-admin/src/features/customers/components/__tests__/CustomerList.addressDisplay.test.tsx' \
  'frontend-admin/src/features/lodging/LodgingWorkspace.tsx' \
  'frontend-admin/src/features/lodging/__tests__/lodgingWorkspace.test.tsx' \
  'frontend-admin/src/features/products/components/ProductForm.tsx' \
  'frontend-admin/src/features/products/components/__tests__/ProductForm.verticalFields.test.tsx' \
  'frontend-admin/src/features/products/utils/__tests__/productVerticalFields.test.ts' \
  'frontend-admin/src/features/products/utils/productVerticalFields.ts' \
  'frontend-admin/src/features/tickets/TicketRedemptionsPage.tsx' \
  'frontend-admin/src/features/tickets/__tests__/ticketRedemptionsPage.test.tsx' \
  'frontend/__tests__/AppointmentDayCalendar.test.tsx' \
  'frontend/__tests__/ImeiPickerModal.test.tsx' \
  'frontend/__tests__/MobileServiceRoutePanel.test.tsx' \
  'frontend/__tests__/RoomPicker.test.tsx' \
  'frontend/__tests__/RoomsScreen.test.tsx' \
  'frontend/__tests__/StaffPicker.test.tsx' \
  'frontend/__tests__/TaxiSalePanel.test.tsx' \
  'frontend/__tests__/TicketValidateScreen.test.tsx' \
  'frontend/__tests__/VetHairSalonScreens.test.tsx' \
  'frontend/__tests__/appointmentNotifications.test.ts' \
  'frontend/__tests__/appointmentStorage.test.ts' \
  'frontend/__tests__/staffService.test.ts' \
  'frontend/__tests__/taxiFare.test.ts' \
  'frontend/app/(screens)/appointment.tsx' \
  'frontend/app/(screens)/patient-record.tsx' \
  'frontend/app/(screens)/rooms.tsx' \
  'frontend/app/(tabs)/appointments.tsx' \
  'frontend/app/(tabs)/patient-record.tsx' \
  'frontend/app/(tabs)/rooms.tsx' \
  'frontend/app/(tabs)/ticket-validate.tsx' \
  'frontend/components/AppointmentDayCalendar.tsx' \
  'frontend/components/FolioChargeBar.tsx' \
  'frontend/components/ImeiPickerModal.tsx' \
  'frontend/components/MobileServiceRoutePanel.tsx' \
  'frontend/components/RoomPicker.tsx' \
  'frontend/components/StaffPicker.tsx' \
  'frontend/components/TaxiSalePanel.tsx' \
  'frontend/contexts/ImeiSelectionContext.tsx' \
  'frontend/contexts/MobileServiceJobContext.tsx' \
  'frontend/contexts/TaxiTripContext.tsx' \
  'frontend/services/appointmentDay.ts' \
  'frontend/services/appointmentNotifications.ts' \
  'frontend/services/taxiFare.ts' \
  'frontend/services/ticketPrinter.ts' \
  'frontend/utils/staffInitials.ts'
run_git commit -m 'feat(vertical-profiles): add vet, hair-salon, mobile-services, taxi, handy-shop, and ticket-sales'
COMMITS_CREATED=$((COMMITS_CREATED + 1))
# Commit 4: fix(ci): keep migration and CI gates under test
run_git add -- \
  '.github/workflows/frontend-admin-ci.yml' \
  'backend/KasseAPI_Final.Tests/MigrationAttributeTests.cs'
run_git commit -m 'fix(ci): keep migration and CI gates under test'
COMMITS_CREATED=$((COMMITS_CREATED + 1))

# Commit 4b: fix(ci): enforce migration chronology and CI gates
# Inserted after Commit 4. Commits 1–3 and 5–8 are unchanged.
run_git add -- \
  '.github/workflows/backend-unit-tests.yml' \
  'backend/KasseAPI_Final.Tests/MigrationChronologyTests.cs' \
  'backend/Migrations/UnregisteredRecentMigrations.cs' \
  'backend/docs/MIGRATION_SQUASH.md'
run_git commit -m 'fix(ci): enforce migration chronology and CI gates'
COMMITS_CREATED=$((COMMITS_CREATED + 1))

# Commit 5: fix(migrations): align snapshot with kitchen and guest-folio indexes
run_git add -- \
  'backend/Migrations/20260930210000_AddTaxiFields.Designer.cs' \
  'backend/Migrations/20260930210000_AddTaxiFields.cs' \
  'backend/Migrations/20261002083418_SyncKitchenAndGuestFolioIndexes.Designer.cs' \
  'backend/Migrations/20261002083418_SyncKitchenAndGuestFolioIndexes.cs' \
  'backend/Migrations/AppDbContextModelSnapshot.cs'
run_git commit -m 'fix(migrations): align snapshot with kitchen and guest-folio indexes'
COMMITS_CREATED=$((COMMITS_CREATED + 1))
# Commit 6: fix(pos): resolve typecheck and lint errors from vertical-profile work
# Absent from this tree (not added):
#   frontend/services/api/kitchenOrderService.ts
#   frontend/components/TableSelector/**
#   frontend/components/OfflineQueueIndicatorView.test.tsx
#   frontend/hooks/useVerticalFeatures*
run_git add -- \
  'frontend/app/(tabs)/_layout.tsx' \
  'frontend/app/(tabs)/cash-register.tsx' \
  'frontend/services/api/appointmentService.ts' \
  'frontend/services/api/imeiService.ts' \
  'frontend/services/api/lodgingService.ts' \
  'frontend/services/api/staffService.ts' \
  'frontend/services/api/ticketService.ts' \
  'frontend/services/verticalProfiles/appointmentStorage.ts'
run_git commit -m 'fix(pos): resolve typecheck and lint errors from vertical-profile work'
COMMITS_CREATED=$((COMMITS_CREATED + 1))
# Commit 7: chore(infra): update DbContext, DI, and cross-cutting entities
run_git add -- \
  'backend/ApplicationHost.cs' \
  'backend/Authorization/AppPermissions.cs' \
  'backend/Controllers/AdminProductsController.cs' \
  'backend/Controllers/AdminTenantsController.cs' \
  'backend/Controllers/OfflineHealthController.cs' \
  'backend/Controllers/OrdersController.cs' \
  'backend/Controllers/PaymentController.cs' \
  'backend/Controllers/PosCustomerController.cs' \
  'backend/Controllers/ProductController.cs' \
  'backend/DTOs/AdminProductDto.cs' \
  'backend/DTOs/CatalogDTOs.cs' \
  'backend/DTOs/OfflineOrderDtos.cs' \
  'backend/DTOs/OperationLogDtos.cs' \
  'backend/DTOs/PaymentApiContractDtos.cs' \
  'backend/DTOs/PaymentApiContractMapper.cs' \
  'backend/DTOs/PaymentDTOs.cs' \
  'backend/DTOs/PosCustomerDtos.cs' \
  'backend/Data/AppDbContext.cs' \
  'backend/KasseAPI_Final.Tests/ActivityDtoTypeWireFormatTests.cs' \
  'backend/KasseAPI_Final.Tests/ActivityEventPublishBuilderTests.cs' \
  'backend/KasseAPI_Final.Tests/AdminTenantsControllerTests.cs' \
  'backend/KasseAPI_Final.Tests/AppDbContextTenantModelTests.cs' \
  'backend/KasseAPI_Final.Tests/EnumVarnamesOpenApiDocumentTests.cs' \
  'backend/KasseAPI_Final.Tests/EnumVarnamesSchemaFilterTests.cs' \
  'backend/KasseAPI_Final.Tests/OfflineHealthControllerTests.cs' \
  'backend/KasseAPI_Final.Tests/OfflineOrderBelegNrStrategyTests.cs' \
  'backend/KasseAPI_Final.Tests/PaymentServiceCoverageHarness.cs' \
  'backend/KasseAPI_Final.Tests/RksvDepExportServiceTests.cs' \
  'backend/KasseAPI_Final.Tests/TenantLimitAlertServiceTests.cs' \
  'backend/KasseAPI_Final.csproj' \
  'backend/Models/ActivityEventType.cs' \
  'backend/Models/AuditEventType.cs' \
  'backend/Models/CompanySettings.cs' \
  'backend/Models/Customer.cs' \
  'backend/Models/DTOs/ProductListDto.cs' \
  'backend/Models/Invoice.cs' \
  'backend/Models/Order.cs' \
  'backend/Models/PaymentDetails.cs' \
  'backend/Models/Product.cs' \
  'backend/Services/Activity/ActivityEventMapper.cs' \
  'backend/Services/Activity/ActivityEventPublishBuilder.cs' \
  'backend/Services/Activity/ActivityEventSeverityRules.cs' \
  'backend/Services/AdminProductListService.cs' \
  'backend/Services/AdminTenants/AdminTenantDtos.cs' \
  'backend/Services/AdminTenants/AdminTenantService.cs' \
  'backend/Services/AdminTenants/IAdminTenantService.cs' \
  'backend/Services/Caching/CacheKeys.cs' \
  'backend/Services/Operations/OperationSnapshots.cs' \
  'backend/Services/PaymentService.cs' \
  'backend/Services/PosCustomerQrLookupService.cs' \
  'backend/Services/PriceChangeService.cs' \
  'backend/Services/TenantLimits/LimitDashboardMapper.cs' \
  'backend/Services/TenantLimits/TenantLimitAlertService.cs' \
  'backend/Services/TenantLimits/TenantLimitDashboardService.cs' \
  'backend/appsettings.Development.example.json' \
  'backend/appsettings.Production.example.json' \
  'backend/appsettings.Staging.example.json' \
  'backend/swagger.json' \
  'frontend-admin/README.md' \
  'frontend-admin/src/api/__tests__/auditEventTypeNames.test.ts' \
  'frontend-admin/src/api/admin/products.ts' \
  'frontend-admin/src/api/generated/admin/admin.ts' \
  'frontend-admin/src/api/generated/model/activityEventType.ts' \
  'frontend-admin/src/api/generated/model/adminTenantListItemDto.ts' \
  'frontend-admin/src/api/generated/model/auditEventType.ts' \
  'frontend-admin/src/api/generated/model/companySettings.ts' \
  'frontend-admin/src/api/generated/model/createPosCustomerRequest.ts' \
  'frontend-admin/src/api/generated/model/customer.ts' \
  'frontend-admin/src/api/generated/model/index.ts' \
  'frontend-admin/src/api/generated/model/notificationConfigEnabledEvents.ts' \
  'frontend-admin/src/api/generated/model/notificationConfigSeverityThreshold.ts' \
  'frontend-admin/src/api/generated/model/posCustomerDto.ts' \
  'frontend-admin/src/api/generated/model/product.ts' \
  'frontend-admin/src/api/generated/model/productListDto.ts' \
  'frontend-admin/src/api/generated/pos/pos.ts' \
  'frontend-admin/src/app/(protected)/admin/tenants/[tenantId]/page.tsx' \
  'frontend-admin/src/app/(protected)/admin/tenants/__tests__/page.test.tsx' \
  'frontend-admin/src/app/(protected)/tenant/profile/page.tsx' \
  'frontend-admin/src/features/activity-notifications/activityTypes.ts' \
  'frontend-admin/src/features/activity-notifications/components/ActivityNotificationList.tsx' \
  'frontend-admin/src/features/activity-notifications/components/NotificationIcon.tsx' \
  'frontend-admin/src/features/activity-notifications/components/NotificationSettingsForm.tsx' \
  'frontend-admin/src/features/activity-notifications/components/__tests__/ActivityNotificationList.catalog.test.tsx' \
  'frontend-admin/src/features/activity-notifications/formatActivityTitle.ts' \
  'frontend-admin/src/features/alerts/hooks/__tests__/useAlerts.test.ts' \
  'frontend-admin/src/features/audit-logs/constants/__tests__/auditLogStatusParams.test.ts' \
  'frontend-admin/src/features/audit-logs/constants/auditLogFilters.ts' \
  'frontend-admin/src/features/backup-dr/components/BackupDrDashboard.tsx' \
  'frontend-admin/src/features/backup-dr/components/BackupManualActionsPanel.tsx' \
  'frontend-admin/src/features/backup-dr/components/BackupRecentRestoreDrillsTable.tsx' \
  'frontend-admin/src/features/backup-dr/components/BackupRestoreReadinessCard.tsx' \
  'frontend-admin/src/features/backup-dr/components/BackupStatusHeader.tsx' \
  'frontend-admin/src/features/backup-dr/components/RecoverabilitySummaryCard.tsx' \
  'frontend-admin/src/features/backup-dr/components/RestoreVerificationCard.tsx' \
  'frontend-admin/src/features/backup-dr/components/__tests__/BackupArtifactsDownloadCard.semantics.test.tsx' \
  'frontend-admin/src/features/backup-dr/components/__tests__/BackupConfidenceDashboard.semantics.test.tsx' \
  'frontend-admin/src/features/backup-dr/components/__tests__/BackupDrOperationalHonestyRegression.test.tsx' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/backupArtifactDownloadTruth.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/backupDrContradictoryState.integration.test.tsx' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/backupDrEvidenceLadder.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/backupDrMappers.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/backupDrOperatorTruthModel.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/backupDrOperatorTruthSemantics.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/backupMonitoringMetrics.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/backupPipelineDerived.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/backupRunDetailPollPolicy.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/drProofLevelPresentation.scenarios.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/drProofLevelPresentation.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/fixtures/backupDrContradictoryBundles.ts' \
  'frontend-admin/src/features/backup-dr/logic/__tests__/manualRestorePresentation.test.ts' \
  'frontend-admin/src/features/backup-dr/logic/backupArtifactDownloadTruth.ts' \
  'frontend-admin/src/features/backup-dr/logic/backupDrEvidenceLadder.ts' \
  'frontend-admin/src/features/backup-dr/logic/backupDrMappers.ts' \
  'frontend-admin/src/features/backup-dr/logic/backupDrOperatorTruthModel.ts' \
  'frontend-admin/src/features/backup-dr/logic/backupMonitoringMetrics.ts' \
  'frontend-admin/src/features/backup-dr/logic/backupPipelineDerived.ts' \
  'frontend-admin/src/features/backup-dr/logic/backupRunDetailPollPolicy.ts' \
  'frontend-admin/src/features/backup-dr/logic/drProofLevelPresentation.ts' \
  'frontend-admin/src/features/backup-dr/logic/manualRestorePresentation.ts' \
  'frontend-admin/src/features/backup-dr/logic/restoreReadinessPresentation.ts' \
  'frontend-admin/src/features/backup/components/BackupPerformanceDashboard.tsx' \
  'frontend-admin/src/features/backup/components/BackupRunsTable.tsx' \
  'frontend-admin/src/features/backup/components/__tests__/BackupList.test.tsx' \
  'frontend-admin/src/features/backup/hooks/__tests__/useBackupAttention.test.ts' \
  'frontend-admin/src/features/backup/hooks/useBackupProgress.ts' \
  'frontend-admin/src/features/backup/logic/__tests__/backupPerformancePresentation.test.ts' \
  'frontend-admin/src/features/backup/logic/__tests__/backupProgressPresentation.test.ts' \
  'frontend-admin/src/features/backup/logic/__tests__/backupRunTablePresentation.test.ts' \
  'frontend-admin/src/features/backup/logic/__tests__/backupRunTrafficLight.test.ts' \
  'frontend-admin/src/features/backup/logic/backupProgressPresentation.ts' \
  'frontend-admin/src/features/backup/logic/backupRunDetailPresentation.ts' \
  'frontend-admin/src/features/backup/logic/backupRunDisplay.ts' \
  'frontend-admin/src/features/backup/logic/backupRunTablePresentation.ts' \
  'frontend-admin/src/features/backup/logic/backupRunTrafficLight.ts' \
  'frontend-admin/src/features/backup/logic/backupScheduleCronCodec.ts' \
  'frontend-admin/src/features/backup/logic/backupVerificationPresentation.ts' \
  'frontend-admin/src/features/backup/pages/AdminBackupPage.tsx' \
  'frontend-admin/src/features/backup/pages/BackupDashboard.tsx' \
  'frontend-admin/src/features/dashboard/components/ActivitySummary.tsx' \
  'frontend-admin/src/features/invoices/components/InvoiceActions.tsx' \
  'frontend-admin/src/features/invoices/components/InvoiceList.tsx' \
  'frontend-admin/src/features/license/components/__tests__/LicenseStatusDashboard.test.tsx' \
  'frontend-admin/src/features/payments/components/PaymentsPageContent.tsx' \
  'frontend-admin/src/features/payments/stornoRefundAudit/StornoRefundAuditDetailModal.tsx' \
  'frontend-admin/src/features/products/utils/__tests__/productMapper.test.ts' \
  'frontend-admin/src/features/products/utils/productMapper.ts' \
  'frontend-admin/src/features/super-admin/api/adminTenants.ts' \
  'frontend-admin/src/features/super-admin/components/TenantDetailOverviewTab.tsx' \
  'frontend-admin/src/features/super-admin/components/TenantDetailTabs.tsx' \
  'frontend-admin/src/features/super-admin/components/TenantFormFields.tsx' \
  'frontend-admin/src/features/super-admin/components/TenantsTable.tsx' \
  'frontend-admin/src/features/tenancy/hooks/useSuperAdminTenantMode.ts' \
  'frontend-admin/src/features/tenants/components/TenantSettingsChangePanel.tsx' \
  'frontend-admin/src/i18n/config.ts' \
  'frontend-admin/src/i18n/generated/translationKeys.ts' \
  'frontend-admin/src/i18n/locales/de/activity.json' \
  'frontend-admin/src/i18n/locales/de/activityNotifications.json' \
  'frontend-admin/src/i18n/locales/de/admin.json' \
  'frontend-admin/src/i18n/locales/de/customers.json' \
  'frontend-admin/src/i18n/locales/de/invoices.json' \
  'frontend-admin/src/i18n/locales/de/nav.json' \
  'frontend-admin/src/i18n/locales/de/products.json' \
  'frontend-admin/src/i18n/locales/de/tenants.json' \
  'frontend-admin/src/i18n/locales/en/activity.json' \
  'frontend-admin/src/i18n/locales/en/activityNotifications.json' \
  'frontend-admin/src/i18n/locales/en/admin.json' \
  'frontend-admin/src/i18n/locales/en/customers.json' \
  'frontend-admin/src/i18n/locales/en/invoices.json' \
  'frontend-admin/src/i18n/locales/en/nav.json' \
  'frontend-admin/src/i18n/locales/en/products.json' \
  'frontend-admin/src/i18n/locales/en/tenants.json' \
  'frontend-admin/src/i18n/locales/tr/activity.json' \
  'frontend-admin/src/i18n/locales/tr/activityNotifications.json' \
  'frontend-admin/src/i18n/locales/tr/admin.json' \
  'frontend-admin/src/i18n/locales/tr/customers.json' \
  'frontend-admin/src/i18n/locales/tr/invoices.json' \
  'frontend-admin/src/i18n/locales/tr/nav.json' \
  'frontend-admin/src/i18n/locales/tr/products.json' \
  'frontend-admin/src/i18n/locales/tr/tenants.json' \
  'frontend-admin/src/lib/__tests__/validation.test.ts' \
  'frontend-admin/src/lib/validation.ts' \
  'frontend-admin/src/lib/validations/__tests__/formRules.test.ts' \
  'frontend-admin/src/lib/validations/common.ts' \
  'frontend-admin/src/lib/validations/formRules.ts' \
  'frontend-admin/src/lib/validations/index.ts' \
  'frontend-admin/src/shared/__tests__/fixtures/adminAppPermissionFixtures.ts' \
  'frontend-admin/src/shared/__tests__/sidebarRegistryCatalog.test.ts' \
  'frontend-admin/src/shared/adminShellLabels.ts' \
  'frontend-admin/src/shared/adminSidebarNavigation.ts' \
  'frontend-admin/src/shared/adminSidebarRegistry.ts' \
  'frontend-admin/src/shared/auth/routePermissions.ts' \
  'frontend-admin/src/shared/buildAdminSidebar.tsx' \
  'frontend-admin/src/shared/sidebarLicenseLockdown.ts' \
  'frontend-admin/tests/e2e/helpers/apiMocks.ts' \
  'frontend/README.md' \
  'frontend/__tests__/expoRouterStructure.contract.test.ts' \
  'frontend/app/(tabs)/orders.tsx' \
  'frontend/app/_layout.tsx' \
  'frontend/components/OrderConfirmationModal.tsx' \
  'frontend/components/PaymentModal.tsx' \
  'frontend/components/UserMenu.tsx' \
  'frontend/i18n/index.ts' \
  'frontend/i18n/locales/de/navigation.json' \
  'frontend/i18n/locales/de/settings.json' \
  'frontend/i18n/locales/en/navigation.json' \
  'frontend/i18n/locales/en/settings.json' \
  'frontend/i18n/locales/tr/navigation.json' \
  'frontend/i18n/locales/tr/settings.json' \
  'frontend/jest.config.js' \
  'frontend/package.json' \
  'frontend/services/api/apiPaths.ts' \
  'frontend/services/api/config.ts' \
  'frontend/services/api/customerService.ts' \
  'frontend/services/api/orderService.ts' \
  'frontend/services/api/paymentService.ts' \
  'frontend/services/api/productService.ts' \
  'frontend/types/order.ts' \
  'frontend/utils/paymentTaxType.ts' \
  'localization/dynamic-key-expansions.json' \
  'localization/namespace-manifest.json' \
  'package-lock.json' \
  'package.json'
run_git commit -m 'chore(infra): update DbContext, DI, and cross-cutting entities'
COMMITS_CREATED=$((COMMITS_CREATED + 1))
# Commit 8: docs: update agent rules and repo documentation
run_git add -- \
  'AGENTS.md' \
  'docs/CI_CD.md' \
  'docs/OFFLINE_SYSTEM_INDEX.md' \
  'docs/RKSV_CASH_REGISTER_OPERATIONS.md' \
  'docs/TENANT_LIMITS.md'
run_git commit -m 'docs: update agent rules and repo documentation'
COMMITS_CREATED=$((COMMITS_CREATED + 1))

# Snapshot backup is gitignored (*.bak). Delete it; do not commit it.
# Commit 9 exists only if the path was ever tracked.
if [[ -f "${BAK_PATH}" ]]; then
  echo "# remove gitignored ${BAK_PATH}"
  if [[ "${DRY_RUN}" == "1" ]]; then
    printf 'rm -f -- %q\n' "${BAK_PATH}"
  else
    rm -f -- "${BAK_PATH}"
  fi
  if git ls-files --error-unmatch -- "${BAK_PATH}" >/dev/null 2>&1; then
    echo "# Commit 9: chore: remove temporary migration snapshot backup"
    run_git add -A -- "${BAK_PATH}"
    run_git commit -m 'chore: remove temporary migration snapshot backup'
    COMMITS_CREATED=$((COMMITS_CREATED + 1))
  elif [[ "${DRY_RUN}" == "1" ]]; then
    echo "# ${BAK_PATH} is gitignored; removal leaves nothing to commit"
  else
    echo "${BAK_PATH} is gitignored and has been removed; no commit created."
  fi
else
  echo "No snapshot backup at ${BAK_PATH}; nothing to delete."
fi

echo
echo "Commits created: ${COMMITS_CREATED}"
echo
git status
echo
git log --oneline -10
