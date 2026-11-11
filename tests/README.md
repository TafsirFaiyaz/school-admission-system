# Backend API integration tests

Run after `docker compose up --build -d`:

```bash
docker compose run --build --rm tests
```

The tests use the real ASP.NET Core API and PostgreSQL, not mocks. Each test creates an isolated class with a unique name. The concurrency test sends 50 simultaneous requests to a class with 3 seats.
