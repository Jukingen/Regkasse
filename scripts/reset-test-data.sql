-- scripts/reset-test-data.sql
-- Delete test data (but keep essential seed data)
DELETE FROM payment_details WHERE created_at > NOW() - INTERVAL '1 day' AND note LIKE '[TEST]%';
DELETE FROM audit_logs WHERE created_at > NOW() - INTERVAL '1 day' AND user_id = 'testsprite_user';
DELETE FROM activity_events WHERE created_at > NOW() - INTERVAL '1 day' AND actor_user_id = 'testsprite_user';

-- Reset sequences if needed
SELECT setval('receipt_sequence_seq', (SELECT COALESCE(MAX(sequence_number), 0) FROM receipt_sequence));