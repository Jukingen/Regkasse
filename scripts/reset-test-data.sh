#!/bin/bash
# scripts/reset-test-data.sh

echo "Resetting test database..."
docker exec -i postgres psql -U postgres -d kasse_db < scripts/reset-test-data.sql

echo "Seeding test data..."
docker exec -i postgres psql -U postgres -d kasse_db < scripts/seed-test-data.sql

echo "Clearing Redis cache..."
docker exec redis redis-cli FLUSHALL

echo "Test data reset complete!"