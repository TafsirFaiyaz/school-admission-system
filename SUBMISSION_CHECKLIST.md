# Deadline checklist

- [ ] `docker compose up --build -d` succeeds (no extra installed runtimes)
- [ ] `docker compose ps` shows database, API, frontend running
- [ ] `http://localhost:8080/health/db` shows database Connected
- [ ] `http://localhost:5173` loads and admin class creation works
- [ ] Browsing works before entering a student ID; Apply now opens the 17-digit ID prompt
- [ ] Input and backend reject 16-digit, 18-digit, and non-numeric student IDs
- [ ] `CHECK_CONNECTION.ps1` confirms direct and proxied class lists match
- [ ] Confirm / cancel / expiration / refresh work in browser
- [ ] `docker compose run --build --rm tests` passes, including concurrency test
- [ ] Data persists across `docker compose down` and `docker compose up -d`
- [ ] `README.md`, `ASSUMPTIONS.md`, `AI_USAGE.md` are accurate
- [ ] `git status` clean, meaningful commits made
- [ ] GitHub repository is **public** and contains all source files (not the assignment PDF)
- [ ] GitHub repository URL submitted through the official form before 11:59:59 PM (timezone not specified by the brief)
