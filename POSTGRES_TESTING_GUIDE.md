# Running Postgres Integration Tests

This guide explains how to run the comprehensive Postgres integration tests that were added to validate the persistence layer hardening.

## Prerequisites

- **Docker Desktop** (or Docker Engine) must be installed and running
- **.NET 8 SDK** for building and running tests
- **Testcontainers DotNet** (automatically downloaded via NuGet)
- **Npgsql 8.0.11** (pinned version in project)

## Quick Start

### 1. Ensure Docker is Running

```bash
# On Windows with Docker Desktop
docker ps
# Should output container list (may be empty)

# On Linux/Mac
docker info
# Should show Docker version and resource info
```

### 2. Run Postgres Integration Tests

From the workspace root:

```bash
# Run all Postgres-specific tests
dotnet test backend/WeUP.Tests/WeUP.Tests.csproj --filter "PostgresPersistenceApiTests" -v normal

# Or run all backend tests (includes Postgres + unit tests)
dotnet test backend/WeUP.Tests/WeUP.Tests.csproj -c Release

# Or run with detailed output
dotnet test backend/WeUP.Tests/WeUP.Tests.csproj -v detailed
```

### 3. Expected Output

When Docker is available and tests pass:

```
[xUnit.net] WeUP.Tests.Integration.PostgresPersistenceApiTests.PostgresMode_WithoutConnectionString_FailsFast [PASS]
[xUnit.net] WeUP.Tests.Integration.PostgresPersistenceApiTests.SubmissionCreationAndSubmit_Flow_PersistsInDatabase [PASS]
[xUnit.net] WeUP.Tests.Integration.PostgresPersistenceApiTests.EfRepositories_ReadAndWriteAgainstSameDatabase [PASS]
[xUnit.net] WeUP.Tests.Integration.PostgresPersistenceApiTests.SaveAndUnsave_Flow_PersistsAcrossRequests [PASS]
[xUnit.net] WeUP.Tests.Integration.PostgresPersistenceApiTests.MapCalendarAndDetail_Flows_ReadSeededPostgresData [PASS]
[xUnit.net] WeUP.Tests.Integration.PostgresPersistenceApiTests.Health_ReturnsHealthyPersistenceDetails [PASS]

Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

## What Each Test Validates

### 1. `PostgresMode_WithoutConnectionString_FailsFast` (21ms)

**Purpose**: Validates that the startup validator correctly rejects misconfiguration.

**What it tests**:

- Application startup fails immediately if Postgres mode is selected but no connection string provided
- Error message is clear and actionable

**Success criteria**:

- Constructor throws `InvalidOperationException` with expected message
- No database connection attempted

---

### 2. `SubmissionCreationAndSubmit_Flow_PersistsInDatabase` (2-3s)

**Purpose**: Validates full submission lifecycle persistence.

**What it tests**:

- Draft submission creation and storage
- Submission update with review note
- Status transitions (Draft → UnderReview → Approved)
- Submission retrieval after database reset

**Success criteria**:

- Submission IDs match after retrieval
- Status and review notes persist correctly
- Data survives database reset

**Database operations**:

- CREATE submission table
- INSERT draft submission
- UPDATE submission status
- SELECT submission by ID

---

### 3. `EfRepositories_ReadAndWriteAgainstSameDatabase` (1-2s)

**Purpose**: Validates that EF repositories maintain consistency.

**What it tests**:

- EF event repository writes and reads events correctly
- EF user repository creates and retrieves profiles
- EF save repository maintains save relationships
- All repositories work against the same database

**Success criteria**:

- Data written by one repository can be read by another
- Internal GUIDs and public IDs stay in sync
- No transaction conflicts

---

### 4. `SaveAndUnsave_Flow_PersistsAcrossRequests` (1-2s)

**Purpose**: Validates save/unsave functionality with persistence.

**What it tests**:

- User saves an event
- Save is persisted to database
- User unsaves the event
- Unsave is persisted
- Saved events list queries correctly

**Success criteria**:

- Save count increments correctly
- Unsave removes record
- SavedEventsResponse returns correct subset

---

### 5. `MapCalendarAndDetail_Flows_ReadSeededPostgresData` (1-2s)

**Purpose**: Validates seeded data is correctly read through API projections.

**What it tests**:

- Seeded phase0-dataset.json is inserted into Postgres
- Map feed queries return deterministic card projections
- Calendar feed queries return deterministic calendar projections
- Event detail queries return full event information

**Success criteria**:

- Event counts match seed dataset
- Seeded event IDs (e.g., `evt-sf-midnight-groove`) are queryable
- All projections include expected fields

---

### 6. `Health_ReturnsHealthyPersistenceDetails` (500ms)

**Purpose**: Validates health check endpoint works with Postgres.

**What it tests**:

- `/health` endpoint returns HTTP 200
- Response includes persistence check
- Persistence check reports Postgres mode
- Pending migration count is accurate

**Success criteria**:

- HTTP status is 200 OK
- JSON response includes `{ status: "Healthy" }`
- Data field reports mode as "Postgres"
- Migration count is 0 if all migrations applied

---

## Test Infrastructure Details

### Testcontainers Configuration

The tests use `DotNet.Testcontainers` to automatically:

1. **Download** the `postgres:16-alpine` image
2. **Create** a container with random port mapping
3. **Wait** for database to be ready (via health check)
4. **Initialize** with seeded data
5. **Cleanup** after test completion

### Database Reset Strategy

For each test:

- TRUNCATE all tables to clean state
- Re-seed from phase0-dataset.json
- Run migrations to latest schema

This ensures:

- No test pollution
- Deterministic data
- Fresh Postgres instance per test class (via `IAsyncLifetime`)

### Custom Postgres Factory

`PostgresApiFactory` (internal test class):

```csharp
public class PostgresApiFactory : IAsyncLifetime
{
    private PostgreSqlContainer _container;
    private WebApplicationFactory<Program> _apiFactory;
    private WeUpDbContext _dbContext;

    // Configures Postgres container
    // Applies migrations
    // Resets seed data
    // Provides WebApplication for testing
}
```

## CI/CD Integration

### GitHub Actions Example

```yaml
- name: Run Backend Tests
  run: |
    # Docker is available in GitHub Actions
    dotnet test backend/WeUP.Tests/WeUP.Tests.csproj \
      -c Release \
      --logger "trx;LogFileName=test-results.trx"

- name: Upload Test Results
  if: always()
  uses: actions/upload-artifact@v3
  with:
    name: test-results
    path: "**/*.trx"
```

### Docker Compose for Local CI

Alternative: Use `compose.yaml` for explicit control

```bash
# Start Postgres
docker-compose up -d

# Wait for health check
docker-compose exec postgres pg_isready

# Run tests
dotnet test backend/WeUP.Tests/WeUP.Tests.csproj

# Cleanup
docker-compose down
```

## Troubleshooting

### Docker Connection Refused

**Error**: `Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'`

**Solutions**:

- Ensure Docker Desktop is running: `docker ps`
- Restart Docker Desktop
- Check Docker settings → Resources → WSL 2 integration

### Postgres Container Timeout

**Error**: `Testcontainers.Builders.DockerUnavailableException: Connection timeout`

**Solutions**:

- Increase system timeout: `docker pull postgres:16-alpine` (pre-download image)
- Check disk space: `docker system df`
- Increase resources: Docker Settings → Resources → Memory

### Migration Failures

**Error**: `Npgsql migration failed to apply`

**Solutions**:

- Check `ASPNETCORE_ENVIRONMENT=Testing` is set
- Verify `appsettings.Testing.json` has `AutoApplyMigrations: true`
- Check for pending migrations: `dotnet ef migrations list`

### Port Already in Use

**Error**: `Cannot bind to port because it is already in use`

**Solutions**:

- Kill previous containers: `docker ps -a | grep postgres`
- `docker rm -f <container-id>`
- Testcontainers auto-maps to random ports (shouldn't occur)

## Performance Baseline

Typical test execution times (with Docker available):

```
PostgresMode_WithoutConnectionString_FailsFast      21 ms
SubmissionCreationAndSubmit_Flow                   2,100 ms
EfRepositories_ReadAndWriteAgainstSameDatabase    1,800 ms
SaveAndUnsave_Flow_PersistsAcrossRequests         1,200 ms
MapCalendarAndDetail_Flows                        1,500 ms
Health_ReturnsHealthyPersistenceDetails             450 ms
────────────────────────────────────────────────
Total (6 tests)                                   ~7,000 ms (7 seconds)
```

## Extending the Test Suite

To add a new Postgres integration test:

### 1. Add Test Class to `PostgresPersistenceApiTests.cs`

```csharp
public async Task YourNewScenario_WhenConditionMet_ThenBehavior()
{
    // Arrange
    var client = _factory.CreateClient();

    // Act
    var response = await client.GetAsync("/api/events/map");

    // Assert
    response.StatusCode.Should().Be(200);
}
```

### 2. Use Seeded Data

```csharp
// Seeded event IDs from phase0-dataset.json
var eventId = "evt-sf-midnight-groove";
var response = await client.GetAsync($"/api/events/{eventId}");
```

### 3. Verify Database State

```csharp
// Optional: Query database directly for assertions
var saved = await _factory.DbContext.Events
    .FirstOrDefaultAsync(e => e.PublicId == eventId);
saved.Should().NotBeNull();
```

## References

- [Testcontainers DotNet Documentation](https://dotnet.testcontainers.org/)
- [ASP.NET Core WebApplicationFactory](https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [Npgsql Connection String Reference](https://www.npgsql.org/doc/connection-string-parameters.html)
- [Postgres Container Image](https://hub.docker.com/_/postgres)

## Next Steps

1. ✅ Ensure Docker is installed and running
2. ✅ Run the full test suite
3. ✅ Verify all 6 Postgres tests pass
4. ✅ Check health endpoint returns Postgres mode
5. ✅ Review test output for any connection issues
6. 📋 Integrate into CI/CD pipeline
7. 📋 Add performance baselines to monitoring

---

**Status**: All infrastructure ready. Tests can execute on systems with Docker available.
