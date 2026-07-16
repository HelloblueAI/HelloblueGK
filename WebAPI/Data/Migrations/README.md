# EF Core migrations

`DatabaseInitializer` calls `Database.Migrate()` automatically when migration
classes exist in this folder.

## Generate the first migration (local)

From the repo root (requires .NET 9 SDK):

```bash
dotnet tool install -g dotnet-ef
dotnet ef migrations add InitialCreate \
  --project WebAPI/HelloblueGK.WebAPI.csproj \
  --startup-project WebAPI/HelloblueGK.WebAPI.csproj \
  --output-dir Data/Migrations \
  --context HelloblueGKDbContext
```

Until migrations are checked in, startup creates missing tables from the current
EF model (`IRelationalDatabaseCreator.CreateTables`).
