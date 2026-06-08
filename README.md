Balpreet Singh - 3201228 - balpreet.singh@nagarro.com

Stack

- .NET 10 microservices, Ocelot gateway, Steeltoe Eureka client
- Eureka server: `steeltoeoss/eureka-server` Docker image (port 8761)
- PostgreSQL per service, RabbitMQ , Jaeger, OpenTelemetry
- Polly HTTP retries + circuit breaker on LeaveService → EmployeeService

Startup steps:
1. docker compose up --build -d (you should use docker compose down -v to delete volumes in order to purge all data)
2. Wait for Eureka health (~45s first time).
3. GET http://localhost:5000/health → 200
4. Perform postman tests: employee login → apply casual → manager login → approve
5. `docker compose logs notification-service` → NOTIFICATION lines

employee-service vs employee-service-replica

Not two different microservices – same codebase, two containers for gateway round-robin demo. Only the main container seeds the DB.

Postman tests are included in repo root.

Failure cases (Postman)

- bad password
- not enough balance / overlap dates
- manager-only routes as employee → 403
- circuit breaker: stop employee-service + employee-service-replica, apply leave → 503, check leave-service logs for "Circuit OPEN"

Services used:
1. api-gateway              | 5000 |     -       | Ocelot + JWT, Eureka lookup 
2. auth-service             | 5001 |   auth-db   | login, JWT 
3. employee-service         | 5002 | employee-db | employees, balances 
4. employee-service-replica | 5006 |   same DB   | replica for LB demo only 
5. leave-service            | 5003 |  leave-db   | apply, approve, history 
6. notification-service     | 5005 |     -       | RabbitMQ consumer, logs notifications 
7. eureka                   | 8761 |     -       | steeltoeoss/eureka-server image 
8. rabbitmq                 | 15672 |    -       | 'leave.events' status
9.  jaeger                  | 16686 |    -       | tracing
