# Leave Management System

Microservices assignment – .NET 10, PostgreSQL, RabbitMQ, Ocelot gateway, Eureka, Jaeger tracing.

## Documentation

| Document | Purpose |
|----------|---------|
| [README-ASSESSOR.md](README-ASSESSOR.md) | Quick evaluation guide |
| [docs/DESIGN.md](docs/DESIGN.md) | Microservices design + architecture diagram |
| [docs/API.md](docs/API.md) | API endpoints with request/response samples |
| [docs/INTER-SERVICE-COMMUNICATION.md](docs/INTER-SERVICE-COMMUNICATION.md) | Sync/async communication |
| [docs/SUBMISSION.md](docs/SUBMISSION.md) | Submission checklist |

## Repository

_(add your GitHub URL before submission)_

## Demo video

_(add 5–10 minute demo recording URL before submission)_

## Run it

```powershell
cd LeaveManagement
docker compose up --build
```

First run can take a few minutes (wait for Eureka and .NET services).

| URL | What |
|-----|------|
| http://localhost:5000 | API gateway (Postman base URL) |
| http://localhost:8761 | Eureka dashboard |
| http://localhost:15672 | RabbitMQ UI (guest/guest) |
| http://localhost:16686 | Jaeger |

```powershell
docker compose down      # stop
docker compose down -v   # wipe DB volumes (fresh seed)
```

## Docker images

Images are **built locally** with `docker compose up --build`. They are not published to Docker Hub.

## Environment variables

Set in `docker-compose.yml` (override there if needed):

| Variable | Used by | Example |
|----------|---------|---------|
| `Jwt__Key` | All services + gateway | `BalpreetNAGP2026` |
| `Jwt__Issuer` | All | `LeaveManagementSystem` |
| `Jwt__Audience` | All | `LeaveManagementClients` |
| `Jwt__ExpiryMinutes` | Auth | `60` |
| `ConnectionStrings__DefaultConnection` | Auth, Employee, Leave | Postgres host per service |
| `Eureka__Client__ServiceUrl` | All apps | `http://eureka:8761/eureka` |
| `Services__EmployeeService` | Leave | `http://employee-service:8080` |
| `RabbitMQ__Host` | Leave, Notification | `rabbitmq` |
| `RabbitMQ__Username` / `Password` | Leave, Notification | `guest` / `guest` |
| `OpenTelemetry__OtlpEndpoint` | All .NET services | `http://jaeger:4317` |
| `RunDbSeed` | Employee replica | `false` on `employee-service-replica` |
| `INSTANCE_ID` | Employee instances | `employee-1` / `employee-2` |

## API testing

1. Import `LeaveManagement.postman_collection.json`
2. Run **Auth → employee login** (saves `{{token}}`)
3. For manager flows: **manager login**, copy token to `{{token}}` if needed
4. See [docs/API.md](docs/API.md) for full endpoint list

## Users (seeded)

| User | Password | Role |
|------|----------|------|
| employee1 | Employee@123 | Employee |
| employee2 | Employee@123 | Employee |
| manager1 | Manager@123 | Manager |

Balances per year: Casual 12, Sick 10, Privilege 15.

## Two employee containers

- **employee-service** – main instance, DB seed
- **employee-service-replica** – same app, for gateway load-balancing demo

Both register in Eureka as **employee-service**. Check `X-Service-Instance` header (`employee-1` / `employee-2`) on repeated GETs via gateway.

## Notifications

```powershell
docker compose logs -f notification-service
```

## Troubleshooting

- **503 on apply leave** – employee-service down or circuit breaker open
- **401** – token expired; login again
- **Eureka empty** – wait ~2 min after `compose up`; check `docker compose ps` all Running
- **Circuit breaker demo** – `docker compose stop employee-service employee-service-replica`, apply leave, check `docker compose logs leave-service`

## Build locally

```powershell
dotnet build LeaveManagement.sln -c Release
```
