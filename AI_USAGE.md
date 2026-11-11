# AI Usage Disclosure

## AI tool

ChatGPT (OpenAI) was used to assist with this take-home School Admission System assignment.

## How AI was used

- **Requirements and planning:** Explained the assignment brief, extracted the eight required API endpoints and the mandatory backend, UI, Docker, and testing rules, and proposed an implementation order.
- **Code development:** Assisted with substantial initial code for the ASP.NET Core (.NET 10) API, Entity Framework Core/PostgreSQL data access, transaction-based seat reservations, hold expiration, React/TypeScript interface, Docker Compose configuration, and xUnit API integration tests.
- **UI revisions:** Helped revise the application flow so a parent can browse classes first and enter an existing 17-digit birth registration number when selecting **Apply**. The top student ID field remains available for looking up applications. AI also assisted with frontend validation and error messages.
- **Debugging and testing:** Suggested diagnostic commands and a connection-check script, reviewed build/test output, and helped investigate why automatically generated test classes appeared in the ordinary admission portal.
- **Documentation:** Assisted with the README, assumptions, setup instructions, and this disclosure.

## Verification I performed

I ran the following commands on my Windows development computer on **11 November 2026** and examined their output:

```powershell
docker compose up --build -d
docker compose ps
Invoke-RestMethod http://localhost:8080/health/db
Invoke-RestMethod http://localhost:8080/classes
docker compose run --build --rm tests
git status
git log --oneline -8
git remote -v
```

The results showed:

- The Docker images for the API and frontend built successfully, and the `api`, `frontend`, and PostgreSQL `db` containers were running; PostgreSQL reported **healthy**.
- `GET /health/db` returned **Healthy / Connected**.
- `GET /classes` returned the saved admission classes and their seat counts.
- The xUnit integration test run completed with **11 passed, 0 failed, 0 skipped**. This included a test where 50 concurrent requests competed for three seats, along with student ID validation, duplicate-application protection, confirmation ownership and repeat handling, cancellation, and class-reference consistency tests.
- At the time of the Git check, the working tree was clean and the local `main` branch matched `origin/main`.

These are results from the recorded local run, not a claim that the project has passed the employer's private evaluation.

## Issues identified and changes made

1. **Admission UI flow:** The initial interface emphasized the student ID entry before browsing. After reviewing the workflow, I requested a revision: a user may browse first, then enter the existing registration number when applying. This was implemented while retaining the top-of-page lookup required by the brief.
2. **Student ID validation:** The UI was updated to request exactly 17 digits. Backend automated tests also cover shorter, longer, and nonnumeric IDs.
3. **Class-list discrepancy:** A previously observed difference between the direct backend class list and the visible application led to the addition of `CHECK_CONNECTION.ps1`. Its first execution attempt was blocked by Windows PowerShell's script execution policy; the script's comparison result has not yet been verified in the supplied logs.
4. **Automated test records in the portal:** The tests create uniquely named `Automated test-...` classes against the running development database. They appeared in the normal class browser. This is a limitation of the current test setup, not intended school data. They should be cleaned from the demonstration database after testing; for a larger project, the tests should use a separate test database.

## Scope and limitations

- The Docker build and 11 integration tests succeeded locally. A fresh-machine installation, a completed restart-persistence check, and a successful run of the connection-comparison script were **not shown in the verification output available when this disclosure was written**.
- The assignment intentionally uses a birth registration number without login and simulates successful payment. This is suitable for the specified demo but is not a secure production identity/payment design.
- I am responsible for reviewing the generated code, accurately reporting test results, and submitting the final repository. AI-generated suggestions are not treated as proof of correctness without testing.
