// Runs once, the first time MongoDB starts with an empty data volume (docker-entrypoint-initdb.d).
// Creates one database and one user per service, and each user can only reach its own database
// (docs/CSHARP_SETUP_TODO.md section 1: one bounded context = one service = one database).
//
// SERVICE_DATABASES lists the databases, e.g. "booking membership". The password for each comes from
// <NAME>_DB_PASSWORD, e.g. BOOKING_DB_PASSWORD. The user is <name>-svc, e.g. booking-svc.
const services = (process.env.SERVICE_DATABASES || "").split(/\s+/).filter(Boolean);

for (const service of services) {
  const passwordVariable = `${service.toUpperCase()}_DB_PASSWORD`;
  const password = process.env[passwordVariable];
  if (!password) {
    throw new Error(`${passwordVariable} is not set`);
  }

  db.getSiblingDB(service).createUser({
    user: `${service}-svc`,
    pwd: password,
    roles: [{ role: "readWrite", db: service }],
  });
  print(`Created user ${service}-svc with access to database ${service} only`);
}
