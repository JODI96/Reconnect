# Reconnect

Habbo-ähnliches 3D-Social-Dating-Spiel. Die Aussenwelt ist ein Nachbau von Zürich aus
swisstopo-Geodaten; Gebäude sind Eingänge zu Räumen, die Nutzer selbst einrichten. In den
Räumen treffen sich Leute als 3D-Avatare, chatten, liken sich und matchen.
Zielplattformen: iOS, Android, später Desktop.

Der Entwickler ist erfahren in .NET/C#, aber neu in Unity. Unity-spezifische Schritte daher
ausführlicher erklären.

## Repository

Monorepo in `C:\Users\Joys9\Reconnect` (Git + Git LFS für Binär-Assets):
Backend (`src/`, `tests/`) und Unity-Client (`client/`). Das frühere Plastic-Projekt ist
als Backup unter `C:\Users\Joys9\Reconnect-Unity-Backup-2026-09-25.zip` gesichert.

## Gesamtstack

- **Client:** Unity 6.3 **LTS** (6000.3.x), URP, UI Toolkit, Newtonsoft.Json, neues Input System.
  Nur LTS-Versionen verwenden.
- **Echtzeit in Räumen:** Photon Fusion 2 (ab Phase 3)
- **Backend:** ASP.NET Core Minimal APIs auf .NET 10
- **Datenbank:** PostgreSQL + PostGIS, EF Core mit Npgsql + NetTopologySuite
- **Cache/Präsenz:** Redis
- **Chat/Benachrichtigungen:** SignalR (`/hubs/chat`)
- **Auth:** ASP.NET Core Identity + JWT (Access Token + rotierender Refresh Token)
- **Dateien:** Azure Blob Storage (lokal: Azurite)
- **Lokale Orchestrierung:** Aspire 13
- **Tests:** xUnit, Integrationstests mit Testcontainers (postgis/postgis)

## Architektur

```
src/
  Reconnect.AppHost          Aspire: startet API, Postgres+PostGIS, Redis, Azurite
  Reconnect.ServiceDefaults  OpenTelemetry, Health Checks, Resilience (Aspire-Standard)
  Reconnect.Api              Minimal APIs, nach Features geschnitten (Vertical Slices)
    Common/                  Querschnitt: Endpoint-Discovery, Auth-Helfer, Validierung, Fehler
    Features/<Feature>/      Endpoints + Mapping + feature-spezifische Logik
    Hubs/                    SignalR-Hubs
  Reconnect.Domain           Entities + Geschäftsregeln. Keine Abhängigkeit auf EF/ASP.NET
                             (einzige Ausnahme: NetTopologySuite für Geometrien)
  Reconnect.Infrastructure   DbContext, EF-Konfigurationen, Migrations, Seed, Identity-User
  Reconnect.Contracts        DTOs für API + Unity. netstandard2.1, C# 9, KEINE Pakete
tests/
  Reconnect.Api.Tests        Integrationstests (WebApplicationFactory + Testcontainers)
client/                      Unity-Projekt
  Assets/Plugins/Reconnect.Contracts/   Contracts-DLL (wird von dotnet build hierher kopiert)
  Assets/_Project/           Alles Eigene (Template-/Store-Assets bleiben ausserhalb)
    Scripts/                 Assembly Reconnect.Client
      Core/                  AppBootstrap (Composition Root), ApiSettings
      Networking/            ApiClient, IHttpTransport, Json, ApiResult
      Auth/ Rooms/ …         Ein Ordner pro Feature: Services, die die API aufrufen
      UI/                    ScreenNavigator, ScreenBase, UiCatalog
      UI/Screens/            Ein Screen = Klasse + UXML-Template
    UI/                      UXML-Layouts + Theme.uss
    Settings/                ScriptableObjects (ApiSettings, UiCatalog, PanelSettings)
    Scenes/Main.unity        Einzige Szene (vorerst)
    Editor/ProjectSetup.cs   Menü "Reconnect → Setup Project": Szene/Assets/PlayerSettings anlegen
    Tests/EditMode/          Unity-Tests (NUnit)
```

Abhängigkeiten: `Api → Infrastructure → Domain`, `Api → Contracts`, `AppHost → Api`.
Domain und Contracts kennen sich nicht; das Mapping passiert in der Api (`*Mappings.cs`).
Der Unity-Client kennt nur Contracts (DLL), nie Domain/Infrastructure.

### Unity-Client: Konventionen

- **Composition Root statt Singletons:** Services werden in `AppBootstrap` erzeugt und per
  Konstruktor an Screens übergeben. Keine `static Instance`-Singletons, kein `FindObjectOfType`.
- **Services sind plain C#** (keine MonoBehaviours) und dadurch in EditMode-Tests testbar;
  HTTP läuft über `IHttpTransport` (`FakeTransport` in Tests).
- **Neuer Screen:** UXML in `_Project/UI/`, Feld in `UiCatalog` + Zuweisung in `ProjectSetup`,
  Klasse `XyzScreen : ScreenBase` in `UI/Screens/`, Navigation in `AppBootstrap`.
- **Async:** UI-Aktionen über `ScreenBase.RunAsync(...)`; API-Aufrufe erhalten `Lifetime`
  (wird beim Verlassen des Screens abgebrochen).
- Szene/Assets möglichst per `ProjectSetup` erzeugen statt von Hand – reproduzierbar im Git-Diff.
- `.meta`-Dateien immer mit committen.
- Backend-URL: `Settings/ApiSettings.asset` (Editor: localhost, Android-Emulator: 10.0.2.2,
  echtes Gerät: LAN-IP des PCs).

### Neues Feature hinzufügen

1. Entity in `Reconnect.Domain/<Bereich>/` anlegen (Factory-Methode `Create(...)`, Regeln im Entity).
2. `DbSet` in `ReconnectDbContext` + `IEntityTypeConfiguration<T>` in
   `Reconnect.Infrastructure/Persistence/Configurations/`.
3. Migration erzeugen (siehe unten).
4. DTOs in `Reconnect.Contracts/<Bereich>/`, Routen in `ApiRoutes`.
5. `Reconnect.Api/Features/<Bereich>/<Bereich>Endpoints.cs` mit einer Klasse, die
   `IEndpointModule` implementiert – sie wird automatisch gefunden und gemappt.
6. Integrationstest in `tests/Reconnect.Api.Tests/<Bereich>/`.

## Konventionen

- Sprache im Code: Englisch (Namen, Kommentare nur wo nötig). Doku/Commits dürfen Deutsch sein.
- IDs sind `Guid` (v7, zeitlich sortierbar: `Guid.CreateVersion7()`).
- Zeit immer über `TimeProvider` (testbar), gespeichert als UTC `DateTimeOffset`.
- Geodaten: SRID 4326 (WGS84), Spalten als `geography` → Distanzen in Metern.
  In Contracts nur `Latitude`/`Longitude` als `double`, nie NTS-Typen.
- Endpoints geben `TypedResults` zurück; Fehler als ProblemDetails / ValidationProblem.
- Kein Repository-Layer: Endpoints nutzen `ReconnectDbContext` direkt; wiederverwendete
  Abfragen als Extension-Methoden (z.B. `BlockQueries`).
- Sicherheitsregeln (Blocks!) nie im Client verlassen – jede Abfrage auf Nutzer/Räume muss
  Blocks berücksichtigen.
- Contracts: nur C# 9, keine Pakete, keine `System.Text.Json`-Attribute (Unity nutzt
  Newtonsoft.Json via `com.unity.nuget.newtonsoft-json`).
- Paketversionen nur in `Directory.Packages.props` (Central Package Management).
- **Keine Secrets im Code.** JWT-Signaturschlüssel = Aspire-Parameter `jwt-signing-key`
  (wird generiert und in den AppHost-User-Secrets gespeichert). Connection Strings kommen
  von Aspire.
- Kleine Commits mit klaren Messages. Vor jedem Commit: `dotnet build` und `dotnet test`.

## Befehle

```powershell
dotnet build                                   # alles bauen
dotnet test                                    # Tests (Docker muss laufen)
dotnet run --project src/Reconnect.AppHost     # alles lokal starten (Docker muss laufen)

# Neue Migration
dotnet ef migrations add <Name> --project src/Reconnect.Infrastructure --output-dir Persistence/Migrations
```

Migrationen werden in der Entwicklung beim Start der API automatisch angewendet.

```powershell
# Unity (Editor geschlossen) – Pfad zur installierten LTS-Version anpassen
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe"
& $unity -batchmode -quit -projectPath client -executeMethod Reconnect.Client.Editor.ProjectSetup.Run -logFile -
& $unity -batchmode -projectPath client -runTests -testPlatform EditMode -testResults client/Logs/editmode.xml -logFile -
```

## Roadmap

1. **Backend-Grundgerüst** – Auth, Profile, Räume, Gebäude (PostGIS), Likes/Matches, Blocks/Reports
2. **Unity-Client mit Login und Raumliste** – Upgrade auf Unity 6 LTS, Contracts-DLL einbinden
3. **Multiplayer-Raum mit Photon Fusion 2**
4. **Raum-Editor** (Layout speichern über `PUT /rooms/{id}/layout`)
5. **Likes/Matches/Chat im Client** (SignalR)
6. **Stadtquartier aus swisstopo-Daten**
7. **Face-Tracking**
8. **Videocall (LiveKit)**
