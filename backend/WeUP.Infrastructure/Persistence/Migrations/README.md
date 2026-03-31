# EF Core Migrations

Migrations are generated via:

```bash
cd backend
dotnet ef migrations add InitialSchema \
  --project WeUP.Infrastructure \
  --startup-project WeUP.Api

dotnet ef database update \
  --project WeUP.Infrastructure \
  --startup-project WeUP.Api
```

## Required packages (add to WeUP.Infrastructure.csproj before running)

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.*" />
```

## Required package in WeUP.Api.csproj (for EF design-time discovery)

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.*" />
```

## Connection string

Set in environment or `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "WeUpDb": "Host=localhost;Database=weup_phase0;Username=weup;Password=YOUR_PASSWORD"
  }
}
```

The P09 implementation step registers `WeUpDbContext` and replaces stub repositories.
