# Reconnect – Backend

ASP.NET Core (.NET 10) + PostgreSQL/PostGIS + Redis + Azurite, orchestriert mit Aspire.
Architektur, Konventionen und Roadmap: siehe [CLAUDE.md](CLAUDE.md).

## Voraussetzungen

- .NET SDK 10
- Docker Desktop (läuft) – für Aspire-Container und Integrationstests
- optional: `dotnet tool install -g dotnet-ef`

## Starten

```powershell
dotnet run --project src/Reconnect.AppHost
```

Das Aspire-Dashboard öffnet sich (Link in der Konsole). Dort siehst du:

- **api** – Links zur API; `/scalar` öffnet die interaktive API-Doku
- **postgres / pgadmin** – Datenbank inkl. Admin-Oberfläche
- **redis**, **storage** (Azurite)

Beim ersten Start werden die Container geladen (dauert ein paar Minuten), der JWT-Schlüssel
wird generiert und in den User Secrets des AppHost gespeichert, und die API wendet die
Migrationen inkl. Zürcher Beispielgebäude an.

## Testen

- **Automatisch:** `dotnet test` (startet einen eigenen PostGIS-Container)
- **Manuell:** `src/Reconnect.Api/Reconnect.Api.http` (Visual Studio / Rider) oder Scalar:
  1. `POST /auth/register` → `accessToken` kopieren
  2. In Scalar oben „Authentication“ → Bearer-Token einfügen
  3. z.B. `GET /buildings/nearby?lat=47.3779&lng=8.5402&radiusMeters=500`
