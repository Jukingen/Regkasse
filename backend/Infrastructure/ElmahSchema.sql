-- ElmahCore.Postgresql schema (PgsqlErrorLog).
-- Table is auto-created when Elmah starts with CreateTablesIfNotExist (default for PgsqlErrorLog).
-- Run manually only when you need explicit DDL before first deploy.

CREATE SEQUENCE IF NOT EXISTS elmah_error_sequence;

CREATE TABLE IF NOT EXISTS elmah_error
(
    errorid UUID NOT NULL,
    application VARCHAR(60) NOT NULL,
    host VARCHAR(50) NOT NULL,
    type VARCHAR(100) NOT NULL,
    source VARCHAR(60) NOT NULL,
    message VARCHAR(500) NOT NULL,
    "user" VARCHAR(50) NOT NULL,
    statuscode INT NOT NULL,
    timeutc TIMESTAMP NOT NULL,
    sequence INT NOT NULL DEFAULT NEXTVAL('elmah_error_sequence'),
    allxml TEXT NOT NULL,
    CONSTRAINT pk_elmah_error PRIMARY KEY (errorid)
);

CREATE INDEX IF NOT EXISTS ix_elmah_error_app_time_seq ON elmah_error USING BTREE
(
    application ASC,
    timeutc DESC,
    sequence DESC
);
