# TestSprite Test Suite for Regkasse

## Quick Start

### Prerequisites
```bash
# Install TestSprite CLI
npm install -g testsprite-cli

# Start all services
cd backend && dotnet run &
cd frontend-admin && npm run dev &
cd frontend && npm start &
```

### Run Tests
```bash
# Run all API tests
testsprite run --suite api

# Run specific suite
testsprite run --suite api --filter tenant-isolation

# Run E2E tests
testsprite run --suite e2e

# Run smoke tests only
testsprite run --tag smoke

# Run with specific environment
testsprite run --env staging --suite critical-regression
```

## Test Tags

| Tag | Açıklama |
|---|---|
| `@smoke` | Critical path, runs on every PR |
| `@regression` | Full regression suite |
| `@tenant` | Multi-tenant isolation tests |
| `@fiscal` | RKSV compliance tests |
| `@slow` | Long-running tests (nightly only) |

## CI Integration

Tests run automatically:

- PR: `@smoke + @tenant`
- Nightly: `@regression`
- Release: Full suite

## Test Results

Results are stored in:

- `./test-results/api/` - API test reports
- `./test-results/e2e/` - E2E test reports
- `./test-screenshots/` - Failure screenshots
- `./test-videos/` - Test recordings

## Troubleshooting

### Common Issues

Backend not reachable:

```bash
curl http://localhost:5184/api/health
```

Test data conflicts:

```bash
./scripts/reset-test-data.sh
```

Flaky tests:

- Check tenant isolation (use unique test IDs)
- Verify async operations have proper waits

## Using TestSprite via MCP

Once configured, use these prompts in Cursor chat:

```markdown
# Generate tests for authentication flow
"Help me test the login flow with TestSprite.
Test cases: email login, username login, invalid credentials."

# Generate tests for tenant isolation
"Generate TestSprite tests for multi-tenant isolation.
Verify that tenant A cannot access tenant B's data."

# Run existing test suite
"Run the test suite for user management with TestSprite."

# Get test coverage report
"Show me the test coverage report from TestSprite."
```

## TestSprite MCP Tools (Detected)

Based on the installed `@testsprite/testsprite-mcp` package descriptors, these tools are available:

| Tool | Purpose |
|---|---|
| `testsprite_generate_code_and_execute` | Generate and execute tests from project context/instructions |
| `testsprite_generate_frontend_test_plan` | Create frontend-focused test plan |
| `testsprite_generate_backend_test_plan` | Create backend-focused test plan |
| `testsprite_generate_code_summary` | Analyze and summarize repository codebase |
| `testsprite_generate_standardized_prd` | Generate standardized PRD from project |
| `testsprite_open_test_result_dashboard` | Open dashboard to review/modify completed tests |
| `testsprite_check_account_info` | Show account plan/credits/profile info |
| `testsprite_bootstrap` | First-time project initialization only |

Note: Generic tool names like `analyzeTestCoverage`, `suggestTests`, and `runTests` are not exposed with those exact names in the current MCP server descriptors.

## Example Interaction

In Cursor Chat:

```text
User: "TestSprite: Generate tests for user creation in Regkasse.
Requirements:
- Super Admin can create user with auto-generated username
- Username must be unique (case-insensitive)
- Audit log must record username change
- User must change password on first login"
```

Expected response: TestSprite generates test cases and executes them.

## Verification Steps

```bash
# 1. Check MCP server is running
# In Cursor: View → MCP Servers → Should see "testsprite" as connected

# 2. Test basic interaction
# In Cursor chat: "TestSprite: Check if you can access the API health endpoint"

# 3. Run a simple test
# In Cursor chat: "TestSprite: Run a health check test for Regkasse backend"
```

## Troubleshooting

### MCP Server not connecting

```json
// Check if npx can find the package
npx -y @testsprite/testsprite-mcp --help

// If not found, install globally
npm install -g @testsprite/testsprite-mcp

// Then use command directly
{
  "command": "testsprite-mcp",
  "args": []
}
```

### API Key issues

```bash
# Verify API key is set
echo $TESTSPRITE_API_KEY

# Test API key (if TestSprite has a test endpoint)
curl -H "Authorization: Bearer $TESTSPRITE_API_KEY" https://api.testsprite.com/health
```

## Alternative: Keep Existing Test Files as Documentation

Even though the YAML files may not run directly, they serve as:

- Test documentation - Clear specifications of what to test
- Manual test guide - QA can follow step by step
- Future migration - If TestSprite adds CLI support

File structure to keep:

```text
testsprite/
├── ../testsprite.config.json  # Configuration reference (repo root)
├── api/
│   ├── health.yml        # Auth test specs
│   ├── users.yml         # User management specs
│   ├── pos.yml           # POS test specs
│   └── backup.yml        # Backup test specs
├── e2e/
│   ├── admin-users.yml   # Admin UI test specs
│   └── admin-backup.yml  # Backup UI test specs
└── README.md             # Test documentation
```
