# Reconnect

Habbo-ähnliches 3D-Social-Dating-Spiel in Zürich. Monorepo mit Backend und Unity-Client.
Architektur, Konventionen und Roadmap: siehe [CLAUDE.md](CLAUDE.md).

| Ordner    | Inhalt |
|-----------|--------|
| `src/`    | Backend: ASP.NET Core (.NET 10), PostgreSQL/PostGIS, Redis, Azurite, Aspire |
| `tests/`  | Backend-Integrationstests |
| `client/` | Unity 6.3 LTS Client (iOS/Android) |

## Voraussetzungen

- .NET SDK 10, Docker Desktop (läuft)
- Unity Hub + Unity **6000.3.x LTS** (Module: Android Build Support, iOS Build Support)
- Git LFS (`git lfs install`)

## Backend starten

```powershell
dotnet run --project src/Reconnect.AppHost
```

Das Aspire-Dashboard öffnet sich (Link in der Konsole). Dort findest du die API (`/scalar` = interaktive
API-Doku), Postgres/pgAdmin, Redis und Azurite. Beim ersten Start werden die Container geladen, der
JWT-Schlüssel generiert (User Secrets) und die Migrationen inkl. Zürcher Beispielgebäude angewendet.

## Client starten

1. Backend starten (siehe oben) – die API läuft auf `http://localhost:5191`.
2. Unity Hub → *Add project from disk* → Ordner `client` wählen, mit 6000.3.x LTS öffnen.
3. Szene `Assets/_Project/Scenes/Main.unity` öffnen → Play.
4. Anmelden mit **Admin / Admin** (nur lokal in Development vorhanden) oder registrieren (ab 18).

Nach Änderungen an `src/Reconnect.Contracts` einmal `dotnet build` ausführen: die DLL wird
automatisch nach `client/Assets/Plugins/Reconnect.Contracts/` kopiert.

## Testen

- Backend: `dotnet test` (startet einen eigenen PostGIS-Container)
- Client: Unity → *Window → General → Test Runner → EditMode → Run All*
- Manuell: `src/Reconnect.Api/Reconnect.Api.http` oder Scalar
