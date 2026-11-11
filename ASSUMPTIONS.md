# Assumptions and reasons

1. **IDs:** GUID strings are used for class and hold IDs. The contract provides sample IDs but explicitly leaves their format open.
2. **Class uniqueness:** Names are unique after trimming leading/trailing spaces, to prevent identical options from confusing parents.
3. **Seat capacity:** Total seats must be a positive integer. There is no class editing or deletion endpoint because the brief only specifies creation and retrieval.
4. **Identity:** A student is identified by a 17-digit `X-Student-Id` value. No authentication is implemented, per the simplified assignment.
5. **Multiple shifts:** Different class/shift options are independent, so a student may apply to different options, but can have at most one HELD or CONFIRMED application in the same option.
6. **Reapplication:** An EXPIRED or CANCELLED hold is not active; its student may submit a new application for the same option if seats remain.
7. **Payment:** Clicking Pay Admission Fee marks a valid held reservation CONFIRMED; no external gateway is used.
8. **Confirmation repeats:** Reconfirming an already CONFIRMED hold belonging to that same student returns CONFIRMED without incrementing occupancy.
9. **Cancellation:** A HELD reservation can be cancelled; confirmed admissions cannot be cancelled through the given API. Repeated cancellation of one's own already CANCELLED hold returns 204.
10. **Expiry:** Hold duration is `HOLD_DURATION_SECONDS`, default 600. UTC timestamps are used; expired reservations free capacity without a user API call. A background process marks them expired, and availability calculations use timestamp checks.
11. **Concurrency:** PostgreSQL row-level locks serialize allocations for a class, including across multiple backend processes. Strict global HTTP-arrival ordering is not guaranteed by row-lock acquisition.
12. **UI refresh:** Seat counts and applications are polled every four seconds; countdowns update every second. The server controls expiration validity.
13. **Persistence:** A Docker named volume retains data across `docker compose down` and `up`. Removing the volume with `-v` is outside the persistence guarantee.
14. **Application errors:** Invalid IDs/inputs return 400, missing resources 404, seat shortages/duplicates/invalid state 409, and operations on another student's hold 403.
15. **Admin security:** Class creation has no admin login because the instructions explicitly say no login is needed. The resulting application is a demo, not suitable for real admissions with sensitive data.
16. **Automatic schema:** EF Core `EnsureCreatedAsync()` is used for Docker-first take-home setup; production schema changes would require versioned migrations.
