# School Admission System

A take-home full-stack admission system with a first-come-first-served seat hold workflow.

- **Backend:** C# ASP.NET Core (.NET 10), Entity Framework Core, Npgsql
- **Database:** PostgreSQL 17
- **Frontend:** React 18, TypeScript, Vite, Nginx
- **Deployment:** Docker Compose
- **Backend tests:** xUnit integration tests against the running API (including 50 concurrent requests)

## Start the complete application

**Only Docker Desktop / Docker Compose is required** for the evaluator. From the repository root:

```bash
docker compose up --build -d
```

Then open:

| Service | URL |
|---|---|
| React application | http://localhost:5173 |
| Backend API | http://localhost:8080 |
| Basic health | http://localhost:8080/health |
| Database health | http://localhost:8080/health/db |

The backend initializes the schema automatically on the first start. You do **not** need to run `dotnet ef` or create tables manually. The frontend proxy forwards `/backend/*` to the API container, so the browser requires no Docker-internal hostnames.

Startup: `docker compose ps` and `docker compose logs -f api`.
Stop without deleting data: `docker compose down`.
To rebuild after editing code: `docker compose up --build -d`.

**Do not use `docker compose down -v` unless you deliberately want to delete admission data.** The PostgreSQL named volume persists across normal `down` / `up` cycles.

### Environment settings

Edit a root `.env` file (optional, do not commit real secrets), based on `.env.example`:

| Variable | Default | Notes |
|---|---|---|
| `SCHOOL_DB_PASSWORD` | `school_local_only_2026` | Local/demo-only PostgreSQL password |
| `HOLD_DURATION_SECONDS` | `600` | Reservation duration; positive integer |
| `VITE_API_BASE_URL` | `/backend` | Frontend backend base URL; baked in at build time |

The backend is exposed on **port 8080**. The database accepts host connections on **127.0.0.1:5433**; containers use `db:5432`. Frontend on host **5173**. PostgreSQL settings are injected using `ConnectionStrings__DefaultConnection`, so the database connection is configurable without code edits.

Changing `VITE_API_BASE_URL` requires a frontend rebuild (`docker compose up --build -d`). Changing the PostgreSQL password after initialization also requires changing the existing database account password or using a fresh local database volume.

## How to use the UI

1. Start at http://localhost:5173. The top birth registration field is a compact **optional lookup** while browsing, as required by the assignment.
2. Go to **School admin**, then create `Class 6 - Morning Shift` with `120` seats (or a smaller class for testing).
3. Return to **Browse classes**. The **Apply now** button works even when the top student field is empty.
4. Click **Apply now**. A dialog asks for the student's **existing 17-digit birth registration number**. IDs shorter/longer than 17 digits or containing non-numeric characters must not reach the backend as valid IDs.
5. Click **Reserve seat & continue**. The API creates a hold and the UI opens **My applications** with the countdown and payment/cancellation actions.
6. Choose **Pay admission fee (simulate)** to confirm, or **Cancel hold** to release the seat.
7. Refresh or reopen the page; the selected student ID is kept locally and application records reload from the database. The user can also change the top student ID for another student's history.

There is **no student registration form, login, or real payment gateway**. The assignment requires identity through `X-Student-Id` and simulated payment.

### Diagnose differing class lists

The React app uses `/backend/classes` through its Nginx proxy; directly browsing `http://localhost:8080/classes` accesses the API host port. They should both return the same classes when started by this Docker Compose project.

Run this from the repository root on Windows:

```powershell
.\CHECK_CONNECTION.ps1
```

The script reads both endpoints, compares class IDs, and checks database row counts without changing records. Optionally enter a student's 17-digit ID to check whether every application refers to a listed class. If the class lists differ, check for another process using port 8080, an overridden `VITE_API_BASE_URL`, or an outdated frontend/API container; rebuild both services:

```powershell
docker compose up --build -d --force-recreate api frontend
```

Then hard-refresh the browser with Ctrl+Shift+R. **Do not delete the PostgreSQL volume** merely to fix a display discrepancy.

## API contract (exact mandatory routes)

| Method | Path | Success |
|---|---|---|
| POST | `/classes` | `201` `id, name, totalSeats` |
| GET | `/classes` | `200` seat count array |
| GET | `/classes/{classId}` | `200` one seat count object |
| POST | `/classes/{classId}/holds` | `201` hold ID, student ID, status, expiresAt |
| GET | `/holds/{holdId}` | `200` hold status |
| POST | `/holds/{holdId}/confirm` | `200` confirmed hold |
| DELETE | `/holds/{holdId}` | `204` no body |
| GET | `/me/holds` | `200` student's applications |

The `X-Student-Id` header is **required** for creating/confirming/cancelling holds and for `/me/holds`. Student IDs must consist of exactly 17 ASCII digits. Hold statuses in API responses are `HELD`, `CONFIRMED`, `EXPIRED`, and `CANCELLED`.

Example request using PowerShell:

```powershell
Invoke-RestMethod -Method POST -Uri http://localhost:8080/classes -ContentType application/json -Body '{"name":"Class 6 - Morning Shift","totalSeats":120}'
```

Example hold (replace the GUID):

```powershell
Invoke-RestMethod -Method POST -Uri 'http://localhost:8080/classes/REPLACE_WITH_CLASS_GUID/holds' -Headers @{'X-Student-Id'='20152691234567890'}
```

### Concurrency and seat safety

New holds acquire a PostgreSQL `SELECT ... FOR UPDATE` row lock on the specific class **inside a transaction** before checking availability. The system then expires stale holds, enforces per-student duplicate rules, counts active/confirmed holds, and inserts no more than the available capacity. The lock is shared across **different backend processes** using the same PostgreSQL database, unlike a process-local C# lock. A database partial unique index adds duplicate-active-application protection.

Confirm/cancel operations lock the target hold row before modifying its status. Repeated confirmation returns the already confirmed result and does not allocate another seat. Expired holds cannot be confirmed. A hosted background service marks expired holds EXPIRED independently of user requests, while API views also treat overdue holds as expired if the job has not yet run.

For strict ordering of arrival timestamps, a dedicated queue would be required; this solution guarantees serialized, capacity-safe allocation rather than a global cross-server timestamp ordering.

### Automated backend tests

Run tests **against the running API** with one Docker command:

```bash
docker compose run --build --rm tests
```

The tests create isolated classes with random names, and check validation, duplicate applications, confirm idempotency and ownership, cancellation/release, and 50 concurrent applicants racing for three seats (exactly three successful holds, 47 conflicts).

The tests use the API directly and require a working `api` service and PostgreSQL. They do not require manual Postman or database access.

### Local development (optional)

If developing outside Docker: use .NET 10 SDK, Node.js 24, and Docker Desktop. Configure the backend's `ConnectionStrings__DefaultConnection` environment variable to the host PostgreSQL connection on port 5433. Run `dotnet run --project backend/SchoolAdmission.Api --urls http://localhost:8080`. In another terminal, run `cd frontend; npm install; npm run dev`. Vite forwards `/backend` requests to the local API. To run tests locally, use `dotnet test tests/SchoolAdmission.Tests/SchoolAdmission.Tests.csproj` with API on port 8080.

### Data persistence and schema note

This compact take-home project calls EF Core `EnsureCreatedAsync()` at startup so a clean Docker installation runs without a manual migration. This is **not** a production migration strategy, and should not be mixed with real EF migrations for future schema upgrades. For production, use controlled versioned migrations. An existing database made with the prior tutorial's compatible `admission_classes`/`admission_holds` schema may also be reused; the application creates the active-application index if missing. If prior experimental schema changes differ, use a clean project-only database after backing up data.

### Security limitations

The assignment intentionally omits authentication and permits student ID as identity. That is unsafe for genuine student records: possession of another student's birth registration ID would permit impersonation. Do not deploy this demo to a real school without proper authentication, authorization, data handling controls, rate limiting, and security review. The database password default is for local demos only.
