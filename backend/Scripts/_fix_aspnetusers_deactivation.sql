ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS deactivated_at timestamptz NULL;
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS deactivated_by varchar(450) NULL;
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS deactivation_reason varchar(500) NULL;
