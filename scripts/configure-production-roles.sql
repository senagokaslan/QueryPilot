\set ON_ERROR_STOP on

SELECT 'CREATE ROLE querypilot_app LOGIN'
WHERE NOT EXISTS (
  SELECT 1
  FROM pg_roles
  WHERE rolname = 'querypilot_app')
\gexec

\password querypilot_app

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT CONNECT ON DATABASE querypilot TO querypilot_app;
GRANT USAGE ON SCHEMA public TO querypilot_app;

ALTER DEFAULT PRIVILEGES FOR ROLE querypilot_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO querypilot_app;
ALTER DEFAULT PRIVILEGES FOR ROLE querypilot_migrator IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO querypilot_app;

GRANT SELECT, INSERT, UPDATE, DELETE
  ON ALL TABLES IN SCHEMA public TO querypilot_app;
GRANT USAGE, SELECT, UPDATE
  ON ALL SEQUENCES IN SCHEMA public TO querypilot_app;
