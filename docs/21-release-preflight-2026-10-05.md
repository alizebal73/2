# Release Pre-flight — 2026-10-05

## Scope

این ممیزی فقط برای آماده‌سازی Setup/Installer و نصب واقعی Stage 14.6 انجام شد. ممیزی مجدد کل Stage 1 تا 14 نیست.

## Baseline

- Branch: `main`
- HEAD checked: `f848c792c8bb628b9c2bebefa2caf66a323089cd`
- Latest CI on HEAD at audit time: Run #1532 — success
- Dashboard production build was successfully produced locally from this baseline during the physical-test preparation.

## Passed

- .NET SDK/solution restore path is valid.
- `dotnet tool restore` succeeds with `dotnet-ef 10.0.12`.
- Dashboard dependency installation succeeds from the committed lockfile.
- Dashboard production build succeeds and writes the compiled SPA to `src/Server/wwwroot`.
- ASP.NET Core Server serves `wwwroot` and has `/api/health`.
- Production configuration uses a separate SQLite database file from Development.
- Production startup refuses the demo/default admin password unless `GAMENET_ADMIN_PASSWORD` is explicitly provided.
- Agent registration refuses to work without `Agent:RegistrationToken`.
- DataProtection keys, backup and database paths are persistent rather than in-memory.
- Client update path has package SHA-256 and size verification, version markers, staged versions and rollback state.
- Agent state is persisted under ProgramData by default.
- Current CI already covers Build, Tests, EF validation, Server smoke, Dashboard build/lint/preview and Browser Smoke.

## Release blockers before Installer

### 1. Production fresh-install provisioning has no real Station creation path

`DatabaseSeeder.SeedAsync(..., includeDemoData)` creates demo Stations only when the environment is Development. A fresh Production database therefore does not receive PC/PS/Table Stations.

The current Dashboard has no server-backed Station create/edit endpoint. The physical gate requires real Station IDs for PC-01/PC-02.

**Required:** provide an explicit production provisioning path for Stations before physical validation. This should support real shop configuration rather than silently enabling demo data.

### 2. Server LAN binding must be installed/configured explicitly

The checked Production config does not define a LAN listener. Development launch settings use localhost:5080 only.

The Installer must configure the installed Server to listen on the chosen LAN address/port (target default: TCP 5080) and verify the binding after installation.

### 3. Installer must provision secrets safely

Production requires:
- `GAMENET_ADMIN_PASSWORD`
- `Agent:RegistrationToken`

Neither may be hard-coded into Git, an installer source file, or a public config template.

The Installer/first-run setup must generate or collect these values and persist them securely.

### 4. Server writable-data location must be separated from the binary install location

The current Server stores SQLite, DataProtection keys and backups under `ContentRootPath/App_Data`. Installing binaries under a protected location such as Program Files can cause write/ACL problems for a Windows service account.

**Required:** define a release-safe data root (preferably ProgramData or an explicitly ACL'd installation data folder) before final Installer packaging.

### 5. Server service hosting must be defined

The current Server project is an ASP.NET Core application but the checked project does not yet include explicit Windows Service hosting support.

**Required:** decide and implement the release service model so Server starts automatically and is recoverable after reboot. A native Windows Service is preferred for the Server.

### 6. Client runtime strategy must be fixed for Installer

The CI Client release package is currently framework-dependent `dotnet publish`. A clean Client PC therefore needs a compatible .NET 10 runtime unless the Installer installs it.

**Required:** either bundle/install the .NET 10 runtime as a prerequisite or publish the Client self-contained. For the simplest shop deployment, self-contained Client packaging is preferred.

## Non-blocking observations

- Vite reports a >500 kB minified JS chunk. This is an optimization warning, not a Release blocker.
- The current CI production smoke is actually run with `ASPNETCORE_ENVIRONMENT=Development`, so it does not prove fresh Production provisioning/hosting behavior.
- Client update/rollback logic exists and is hardened, but Installer integration still has to respect the versioned client data layout.

## Release decision

**PRE-FLIGHT: NOT READY FOR INSTALLER**

Reason: Installer packaging should not begin until the five operational blockers above are resolved, especially Station provisioning and production writable-data/service configuration.

## Correct next sequence

1. Add/fix Production provisioning for real Stations.
2. Define Server data root + Windows Service hosting.
3. Define safe Admin/Agent secret provisioning.
4. Choose self-contained vs runtime prerequisite for Client.
5. Add a true Production smoke gate to CI.
6. Re-run Release Pre-flight.
7. Build Server Setup + Client Setup.
8. Install on Server + PC-01 + PC-02.
9. Execute Stage 14.6 physical validation.

