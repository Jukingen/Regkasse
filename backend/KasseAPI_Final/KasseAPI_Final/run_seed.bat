set PGPASSWORD=asdasd#
psql -U postgres -d kasse_db -h localhost -p 5432 -f demo_seed_products.sql
