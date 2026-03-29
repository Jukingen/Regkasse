/**
 * Committed OpenAPI (backend/swagger.json) patch: Admin backup + restore verification endpoints.
 * Run from repo root: node scripts/patch-swagger-backup-dr.cjs
 * Then: cd frontend-admin && npm run generate:api
 */
const fs = require('fs');
const path = require('path');

const swaggerPath = path.join(__dirname, '..', 'backend', 'swagger.json');
const doc = JSON.parse(fs.readFileSync(swaggerPath, 'utf8'));

const intEnum = (values) => ({ type: 'integer', format: 'int32', enum: values });

const newPaths = {
  '/api/admin/backup/status/latest': {
    get: {
      tags: ['AdminBackup'],
      responses: {
        '200': {
          description: 'OK',
          content: {
            'application/json': {
              schema: { $ref: '#/components/schemas/BackupLatestStatusResponseDto' },
            },
          },
        },
      },
    },
  },
  '/api/admin/backup/trigger': {
    post: {
      tags: ['AdminBackup'],
      requestBody: {
        content: {
          'application/json': {
            schema: { $ref: '#/components/schemas/BackupTriggerRequestDto' },
          },
        },
        required: false,
      },
      responses: {
        '200': {
          description: 'OK',
          content: {
            'application/json': {
              schema: { $ref: '#/components/schemas/BackupTriggerResponseDto' },
            },
          },
        },
        '202': {
          description: 'Accepted',
          content: {
            'application/json': {
              schema: { $ref: '#/components/schemas/BackupTriggerResponseDto' },
            },
          },
        },
      },
    },
  },
  '/api/admin/backup/runs': {
    get: {
      tags: ['AdminBackup'],
      parameters: [
        {
          name: 'page',
          in: 'query',
          schema: { type: 'integer', format: 'int32', default: 1 },
        },
        {
          name: 'pageSize',
          in: 'query',
          schema: { type: 'integer', format: 'int32', default: 20 },
        },
      ],
      responses: {
        '200': {
          description: 'OK',
          content: {
            'application/json': {
              schema: { $ref: '#/components/schemas/BackupHistoryResponseDto' },
            },
          },
        },
      },
    },
  },
  '/api/admin/backup/runs/{id}': {
    get: {
      tags: ['AdminBackup'],
      parameters: [
        {
          name: 'id',
          in: 'path',
          required: true,
          schema: { type: 'string', format: 'uuid' },
        },
      ],
      responses: {
        '200': {
          description: 'OK',
          content: {
            'application/json': {
              schema: { $ref: '#/components/schemas/BackupRunResponseDto' },
            },
          },
        },
        '404': { description: 'Not Found' },
      },
    },
  },
  '/api/admin/backup/verification/latest': {
    get: {
      tags: ['AdminBackup'],
      responses: {
        '200': {
          description: 'OK',
          content: {
            'application/json': {
              schema: {
                nullable: true,
                allOf: [{ $ref: '#/components/schemas/BackupVerificationResponseDto' }],
              },
            },
          },
        },
      },
    },
  },
  '/api/admin/restore-verification/trigger': {
    post: {
      tags: ['AdminRestoreVerification'],
      responses: {
        '202': {
          description: 'Accepted',
          content: {
            'application/json': {
              schema: { $ref: '#/components/schemas/RestoreVerificationRunResponseDto' },
            },
          },
        },
      },
    },
  },
  '/api/admin/restore-verification/runs/latest': {
    get: {
      tags: ['AdminRestoreVerification'],
      responses: {
        '200': {
          description: 'OK',
          content: {
            'application/json': {
              schema: {
                nullable: true,
                allOf: [{ $ref: '#/components/schemas/RestoreVerificationRunResponseDto' }],
              },
            },
          },
        },
      },
    },
  },
  '/api/admin/restore-verification/runs': {
    get: {
      tags: ['AdminRestoreVerification'],
      parameters: [
        {
          name: 'page',
          in: 'query',
          schema: { type: 'integer', format: 'int32', default: 1 },
        },
        {
          name: 'pageSize',
          in: 'query',
          schema: { type: 'integer', format: 'int32', default: 20 },
        },
      ],
      responses: {
        '200': {
          description: 'OK',
          content: {
            'application/json': {
              schema: { $ref: '#/components/schemas/RestoreVerificationHistoryResponseDto' },
            },
          },
        },
      },
    },
  },
  '/api/admin/restore-verification/runs/{id}': {
    get: {
      tags: ['AdminRestoreVerification'],
      parameters: [
        {
          name: 'id',
          in: 'path',
          required: true,
          schema: { type: 'string', format: 'uuid' },
        },
      ],
      responses: {
        '200': {
          description: 'OK',
          content: {
            'application/json': {
              schema: { $ref: '#/components/schemas/RestoreVerificationRunResponseDto' },
            },
          },
        },
        '404': { description: 'Not Found' },
      },
    },
  },
  '/api/admin/restore-verification/readiness': {
    get: {
      tags: ['AdminRestoreVerification'],
      responses: {
        '200': {
          description: 'OK',
          content: {
            'application/json': {
              schema: { $ref: '#/components/schemas/RestoreVerificationReadinessResponseDto' },
            },
          },
        },
      },
    },
  },
};

const newSchemas = {
  BackupRunResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      id: { type: 'string', format: 'uuid' },
      status: intEnum([0, 1, 2, 3, 4, 5, 6]),
      triggerSource: intEnum([0, 1, 2]),
      adapterKind: { type: 'string' },
      idempotencyKey: { type: 'string', nullable: true },
      requestedByUserId: { type: 'string', nullable: true },
      requestedAt: { type: 'string', format: 'date-time' },
      startedAt: { type: 'string', format: 'date-time', nullable: true },
      completedAt: { type: 'string', format: 'date-time', nullable: true },
      failureCode: { type: 'string', nullable: true },
      failureDetail: { type: 'string', nullable: true },
      correlationId: { type: 'string', nullable: true },
      duplicatePrevented: { type: 'boolean' },
      artifacts: {
        type: 'array',
        nullable: true,
        items: { $ref: '#/components/schemas/BackupArtifactResponseDto' },
      },
      verifications: {
        type: 'array',
        nullable: true,
        items: { $ref: '#/components/schemas/BackupVerificationResponseDto' },
      },
    },
  },
  BackupArtifactResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      id: { type: 'string', format: 'uuid' },
      artifactType: intEnum([0, 1, 2, 3, 4, 5]),
      storageLocator: { type: 'string' },
      byteSize: { type: 'integer', format: 'int64', nullable: true },
      contentHashSha256: { type: 'string', nullable: true },
      lifecycleState: intEnum([0, 1, 2, 3]),
      externalRedactedLocator: { type: 'string', nullable: true },
    },
  },
  BackupVerificationResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      id: { type: 'string', format: 'uuid' },
      backupRunId: { type: 'string', format: 'uuid' },
      status: intEnum([0, 1, 2]),
      startedAt: { type: 'string', format: 'date-time' },
      completedAt: { type: 'string', format: 'date-time', nullable: true },
      verifierSource: { type: 'string' },
      completenessFlag: { type: 'boolean' },
      failureReason: { type: 'string', nullable: true },
    },
  },
  BackupTriggerRequestDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      idempotencyKey: { type: 'string', nullable: true },
    },
  },
  BackupTriggerResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      run: { $ref: '#/components/schemas/BackupRunResponseDto' },
      duplicateExecutionPrevented: { type: 'boolean' },
      newQueuedRunCreated: { type: 'boolean' },
      orchestrationState: { type: 'string' },
    },
  },
  BackupHistoryResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      items: {
        type: 'array',
        items: { $ref: '#/components/schemas/BackupRunResponseDto' },
      },
      page: { type: 'integer', format: 'int32' },
      pageSize: { type: 'integer', format: 'int32' },
      totalCount: { type: 'integer', format: 'int32' },
    },
  },
  RestoreCapabilityDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      isAutomatedRestoreAvailable: { type: 'boolean' },
      notes: { type: 'string' },
    },
  },
  BackupConfigurationHealthResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      level: { type: 'string' },
      issues: { type: 'array', items: { type: 'string' } },
      effectiveAdapterKind: { type: 'string' },
      workerEnabled: { type: 'boolean' },
      artifactVerificationDisclaimer: { type: 'string' },
    },
  },
  BackupLatestStatusResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      latestRun: {
        nullable: true,
        allOf: [{ $ref: '#/components/schemas/BackupRunResponseDto' }],
      },
      restore: { $ref: '#/components/schemas/RestoreCapabilityDto' },
      configurationHealth: { $ref: '#/components/schemas/BackupConfigurationHealthResponseDto' },
      artifactPipelinePolicy: { $ref: '#/components/schemas/BackupArtifactPipelinePolicyResponseDto' },
    },
  },
  BackupArtifactPipelinePolicyResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      externalArchiveRequirement: { type: 'string' },
      externalArchiveRootConfigured: { type: 'boolean' },
      artifactStagingRootConfigured: { type: 'boolean' },
      willRunExternalArchiveAfterStagingVerificationWhenEligible: { type: 'boolean' },
      stagingOnDiskHashReverificationExpected: { type: 'boolean' },
      effectiveAdapterKind: { type: 'string' },
      operatorNotes: { type: 'array', items: { type: 'string' } },
    },
  },
  RestoreVerificationReadinessResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      level: { type: 'string' },
      issues: { type: 'array', items: { type: 'string' } },
      workerEnabled: { type: 'boolean' },
      orchestratorDistributedLockEnabled: { type: 'boolean' },
      scopeDisclaimer: { type: 'string' },
    },
  },
  RestoreVerificationRunResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      id: { type: 'string', format: 'uuid' },
      status: intEnum([0, 1, 2, 3]),
      triggerSource: intEnum([0, 1]),
      sourceBackupRunId: { type: 'string', format: 'uuid', nullable: true },
      dumpInspectionPassed: { type: 'boolean', nullable: true },
      pgRestoreListExitCode: { type: 'integer', format: 'int32', nullable: true },
      pgRestoreListLineCount: { type: 'integer', format: 'int32', nullable: true },
      restoreAttemptExecuted: { type: 'boolean' },
      restoreAttemptPassed: { type: 'boolean', nullable: true },
      restoreAttemptExitCode: { type: 'integer', format: 'int32', nullable: true },
      restoreAttemptSkipReason: { type: 'string', nullable: true },
      restoreTargetDbRedacted: { type: 'string', nullable: true },
      fiscalSqlSkipped: { type: 'boolean' },
      fiscalSqlSkipReason: { type: 'string', nullable: true },
      fiscalSqlPassed: { type: 'boolean', nullable: true },
      fiscalSqlFailCount: { type: 'integer', format: 'int32', nullable: true },
      fiscalSqlWarnCount: { type: 'integer', format: 'int32', nullable: true },
      integrityScope: { type: 'string', nullable: true },
      integrityChecksPassed: { type: 'boolean', nullable: true },
      requestedAt: { type: 'string', format: 'date-time' },
      startedAt: { type: 'string', format: 'date-time', nullable: true },
      completedAt: { type: 'string', format: 'date-time', nullable: true },
      failureCode: { type: 'string', nullable: true },
      failureDetail: { type: 'string', nullable: true },
      requestedByUserId: { type: 'string', nullable: true },
      correlationId: { type: 'string', nullable: true },
      detailsJson: { type: 'string', nullable: true },
    },
  },
  RestoreVerificationHistoryResponseDto: {
    type: 'object',
    additionalProperties: false,
    properties: {
      items: {
        type: 'array',
        items: { $ref: '#/components/schemas/RestoreVerificationRunResponseDto' },
      },
      page: { type: 'integer', format: 'int32' },
      pageSize: { type: 'integer', format: 'int32' },
      totalCount: { type: 'integer', format: 'int32' },
    },
  },
};

Object.assign(doc.paths, newPaths);
Object.assign(doc.components.schemas, newSchemas);

fs.writeFileSync(swaggerPath, JSON.stringify(doc, null, 2) + '\n', 'utf8');
console.log('Patched', swaggerPath, 'with AdminBackup + AdminRestoreVerification paths.');
