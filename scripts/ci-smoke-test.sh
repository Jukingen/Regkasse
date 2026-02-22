#!/bin/bash
set -e

echo "🚀 Starting CI Migration & Recovery Smoke Test..."

cd backend/KasseAPI_Final/KasseAPI_Final

# 1. Build backend
echo "📦 Building backend..."
dotnet build

# 2. Apply ALL migrations to a fresh test DB
echo "🗄️ Applying migrations to test database..."
# Fails pipeline if any EF migration is structurally corrupt
dotnet ef database update --connection "Host=localhost;Database=test_regkasse;Username=postgres;Password=postgres"

# 3. Start isolated backend in background
echo "🏃 Starting isolated backend server..."
dotnet run --no-build & 
BACKEND_PID=$!

# Wait for healthy port
echo "⏳ Waiting for server to initialize (10s)..."
sleep 10

# 4. Execute 200 OK Smoke Test
echo "🔍 Validating /api/Cart/table-orders-recovery endpoint..."
# Fails pipeline if endpoint returns 500
STATUS_CODE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5183/api/Cart/table-orders-recovery)

if [ "$STATUS_CODE" -ne 200 ] && [ "$STATUS_CODE" -ne 401 ]; then
  echo "🚨 Smoke test failed! Expected 200 or 401, got $STATUS_CODE"
  kill $BACKEND_PID
  exit 1
fi

echo "✅ Migration & Recovery Smoke Test Passed."
kill $BACKEND_PID
exit 0
