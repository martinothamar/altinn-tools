CREATE DATABASE monitordb;
\c monitordb;
CREATE SCHEMA monitor;

ALTER SYSTEM SET max_connections TO '200';

CREATE ROLE platform_monitoring WITH LOGIN PASSWORD 'Password';

GRANT USAGE ON SCHEMA monitor TO platform_monitoring;

ALTER DEFAULT PRIVILEGES FOR USER platform_monitoring_admin IN SCHEMA monitor GRANT SELECT,INSERT,UPDATE,REFERENCES,DELETE,TRUNCATE,REFERENCES,TRIGGER ON TABLES TO platform_monitoring;
ALTER DEFAULT PRIVILEGES FOR USER platform_monitoring_admin IN SCHEMA monitor GRANT ALL ON SEQUENCES TO platform_monitoring;
