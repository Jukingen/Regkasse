CREATE TABLE IF NOT EXISTS "UserSettings" (
    "Id" uuid NOT NULL,
    "UserId" character varying(450) NOT NULL,
    "Language" character varying(10) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "DateFormat" character varying(20) NOT NULL,
    "TimeFormat" character varying(10) NOT NULL,
    "CashRegisterId" character varying(100) NULL,
    "DefaultTaxRate" integer NOT NULL,
    "EnableDiscounts" boolean NOT NULL,
    "EnableCoupons" boolean NOT NULL,
    "AutoPrintReceipts" boolean NOT NULL,
    "ReceiptHeader" character varying(200) NULL,
    "ReceiptFooter" character varying(200) NULL,
    "TseDeviceId" character varying(100) NULL,
    "FinanzOnlineEnabled" boolean NOT NULL,
    "FinanzOnlineUsername" character varying(100) NULL,
    "SessionTimeout" integer NOT NULL,
    "RequirePinForRefunds" boolean NOT NULL,
    "MaxDiscountPercentage" integer NOT NULL,
    "Theme" character varying(10) NOT NULL,
    "CompactMode" boolean NOT NULL,
    "ShowProductImages" boolean NOT NULL,
    "EnableNotifications" boolean NOT NULL,
    "LowStockAlert" boolean NOT NULL,
    "DailyReportEmail" character varying(255) NULL,
    "DefaultPaymentMethod" character varying(20) NOT NULL,
    "DefaultTableNumber" character varying(10) NULL,
    "DefaultWaiterName" character varying(100) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_UserSettings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_UserSettings_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_UserSettings_UserId" ON "UserSettings" ("UserId");
CREATE INDEX "IX_UserSettings_Language" ON "UserSettings" ("Language");
CREATE INDEX "IX_UserSettings_Currency" ON "UserSettings" ("Currency");

-- Migration kaydını ekle
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250814165300_AddUserSettingsManually', '7.0.11');
