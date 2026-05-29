#!/bin/bash
# Run TestSprite tests with proper environment setup

set -e

echo "🚀 Starting Regkasse Test Suite"

# Load environment
source .env.test

# Reset test data
echo "🔄 Resetting test data..."
./scripts/reset-test-data.sh

# Wait for services
echo "⏳ Waiting for services..."
sleep 5

# Run tests based on argument
case "$1" in
  smoke)
    echo "🔥 Running smoke tests..."
    npx testsprite run --tag smoke --fail-fast
    ;;
  regression)
    echo "📊 Running regression tests..."
    npx testsprite run --tag regression --parallel 4
    ;;
  tenant)
    echo "🏢 Running tenant isolation tests..."
    npx testsprite run --suite tenant-isolation
    ;;
  all)
    echo "🔍 Running all tests..."
    npx testsprite run --all --parallel 4
    ;;
  *)
    echo "Usage: ./run-tests.sh [smoke|regression|tenant|all]"
    exit 1
    ;;
esac

echo "✅ Tests completed!"
