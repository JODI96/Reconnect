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
- **Echtzeit in Räumen:** eigener SignalR-Hub `/hubs/room` + Redis (Anwesenheit, Spielstände), lokal ohne
  Account. Client spricht nur mit `IRoomSession` → später austauschbar (z. B. Photon Fusion 2).
- **Backend:** ASP.NET Core Minimal APIs auf .NET 10
- **Datenbank:** PostgreSQL + PostGIS, EF Core mit Npgsql + NetTopologySuite
- **Cache/Präsenz:** Redis
- **Chat/Benachrichtigungen:** SignalR (`/hubs/chat` Match-Chat, `/hubs/room` Raum-Chat)
- **Auth:** ASP.NET Core Identity + JWT (Access Token + rotierender Refresh Token)
- **Dateien:** Azure Blob Storage (lokal: Azurite)
- **Lokale Orchestrierung:** Aspire 13
- **Tests:** xUnit – Unit-Tests, Architekturtests (NetArchTest), Integrationstests mit Testcontainers
- **CI:** GitHub Actions (`.github/workflows/backend.yml`): Build, Migrations-Check, alle Tests

## Architektur

Das Backend ist ein **modularer Monolith**: ein deploybarer Prozess, intern in fachliche Module mit
harten Grenzen geschnitten. So bleibt es heute einfach und lässt sich später modulweise skalieren oder
in eigene Services auslagern, ohne alles umzubauen.

```
src/
  Reconnect.AppHost          Aspire: startet API, Postgres+PostGIS, Redis, Azurite
  Reconnect.ServiceDefaults  OpenTelemetry, Health Checks, Resilience (Aspire-Standard)
  Reconnect.Api              Host – komponiert NUR: Module, Redis/Blobs, ProblemDetails, OpenAPI,
                             Rate Limiting (HostSetup.cs). Keine Fachlogik, keine DbContexts/Hubs.
  Reconnect.SharedKernel     Modul-Infrastruktur: IModule, Event-Bus, Modul-DbContext, DomainException,
                             AgePolicy, Web-Helfer (GetUserId, Paging, RateLimitPolicies). Kennt keine Module.
  Reconnect.Contracts        DTOs + Routen für API und Unity. netstandard2.1, C# 9, KEINE Pakete
  Modules/<Name>/Reconnect.Modules.<Name>/
    <Name>Module.cs          Einstieg: Register (DI), MapEndpoints, MigrateAsync, SeedAsync
    Public/                  EINZIGE Schnittstelle für andere Module (Interfaces, Events, Ids)
    Domain/                  Entities + Regeln (internal). Kein EF/ASP.NET/Redis
    Features/                Endpoints, Event-Handler, Abfragen (internal)
    Infrastructure/          <Name>DbContext, Migrations/, Stores, Seeding (internal)
    Hubs/                    SignalR-Hubs (internal) + Client-Interface (public, SignalR braucht es)
tests/
  Reconnect.UnitTests          Domänenregeln, Minigames, Event-Bus (schnell, ohne Docker)
  Reconnect.ArchitectureTests  Erzwingt die Modulgrenzen (siehe unten)
  Reconnect.Api.Tests          Integrationstests (WebApplicationFactory + Testcontainers)
client/                      Unity-Projekt
  Assets/Plugins/Reconnect.Contracts/   Contracts-DLL (wird von dotnet build hierher kopiert)
  Assets/_Project/           Alles Eigene (Template-/Store-Assets bleiben ausserhalb)
    Scripts/                 Assembly Reconnect.Client
      Core/                  AppBootstrap (Composition Root), ApiSettings
      Networking/            ApiClient, IHttpTransport, Json, ApiResult
      Auth/ Rooms/ …         Ein Ordner pro Feature: Services, die die API aufrufen
      Rooms/                 Raum: IRoomSession/SignalRRoomSession, RoomView, AvatarView, RoomPathfinder,
                             ItemCatalog/AvatarCatalog (Kenney-Modelle), RoomTheme, GameStation
      Networking/Realtime/   Schlanker SignalR-Client (JSON-Protokoll über WebSocket)
      UI/Games/              TicTacToePanel, QuizPanel
      City/                  3D-Stadt: CityView (Cesium-Tilesets, Marker, Koordinaten ToUnity/ToGeo),
                             CityCameraController (Orbit: Pan/Zoom/Drehen/Neigen/Tap), CityMarker
      UI/                    ScreenNavigator, ScreenBase, UiCatalog
      UI/Screens/            Ein Screen = Klasse + UXML-Template
    UI/                      UXML-Layouts + Theme.uss
    Settings/                ScriptableObjects (ApiSettings, UiCatalog, PanelSettings)
    Scenes/Main.unity        Einzige Szene (vorerst)
    Editor/ProjectSetup.cs   Menü "Reconnect → Setup Project": Szene/Assets/PlayerSettings anlegen
    Tests/EditMode/          Unity-Tests (NUnit), schnell, ohne Szene
    Tests/PlayMode/          Startet Main.unity; City-Test rendert client/Logs/city-preview.png
```

### Module

| Modul    | Verantwortung | Schema | Darf nutzen |
|----------|---------------|--------|-------------|
| Identity | Konten, Login, JWT/Refresh, Rollen, Dev-Admin | `identity` | – |
| City     | Gebäude (PostGIS), später swisstopo-Import | `city` | – |
| Safety   | Blocks, Reports (Moderation) | `safety` | Identity |
| Profiles | Anzeigename, Alter, Bio, Verifizierung | `profiles` | Identity, Safety |
| Social   | Likes, Matches, Match-Chat (`/hubs/chat`) | `social` | Profiles, Safety |
| Rooms    | Räume, Layout (jsonb), Präsenz + Minigames (`/hubs/room`, Redis), Showcase | `rooms` | Identity, Profiles, Safety, City |

**Regeln (durch `Reconnect.ArchitectureTests` erzwungen):**
- Andere Module nur über `*.Public` ansprechen: `IUserDirectory`, `IProfileDirectory`, `IBlockQueries`,
  `ICityDirectory`, `ZurichBuildings`. Alles andere ist `internal`.
- Abhängigkeiten nur gemäss Tabelle, zyklenfrei. Neue Pfeile = bewusste Architekturentscheidung
  (Tabelle hier + `AllowedDependencies` im Test anpassen).
- Reagieren statt aufrufen: **Integration Events** (`IEventBus`). `UserRegistered` (Identity → Profiles
  legt Profil an; wirft der Handler eine DomainException, wird die Registrierung zurückgerollt),
  `UserBlocked` (Safety → Social löscht Likes/Match). Handler müssen idempotent sein.
  Der Bus ist heute in-process/synchron; bei Auslagerung eines Moduls wird er durch Broker + Outbox ersetzt.
- Jedes Modul hat **eigenen DbContext, eigenes Postgres-Schema, eigene Migrationen** (History-Tabelle im
  Schema). **Keine Fremdschlüssel und keine Joins über Modulgrenzen** – IDs werden als Guid referenziert,
  Daten anderer Module über deren Public-Interfaces geholt (z. B. Besitzernamen gebündelt pro Seite).
- Start: erst migrieren alle Module, dann seeden alle Module (Reihenfolge = `AddModules(...)` in Program.cs).
- Alle Routen liegen unter **`/v1`** (`ApiRoutes.Version`). Breaking Changes → `/v2` parallel betreiben.
- **Rate Limiting:** `RateLimitPolicies.Auth` (pro IP) für Auth-Endpoints, `RateLimitPolicies.Writes`
  (pro User) für spambare Schreibaktionen. Grenzen in `RateLimiting:*` konfigurierbar.

Der Unity-Client kennt nur Contracts (DLL), nie Module.

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
- **3D-Stadt = Cesium for Unity** (Apache 2.0) mit swisstopo-Daten, direkt von geo.admin.ch gestreamt:
  Gelände (`3d.geo.admin.ch/ch.swisstopo.terrain.3d`), swissBUILDINGS3D (`…swissbuildings3d.3d`),
  SWISSIMAGE/Landeskarte als Raster-Overlay (WMTS 3857, `{reverseY}`!). Szene wird von `ProjectSetup` gebaut.
- **Welt-Koordinaten:** Unity-Ursprung = Zürich HB auf Bodenhöhe (`CitySettings`, Höhe 408 m – swisstopo-3D
  nutzt Höhen über Meer). X = Ost, Y = oben, Z = Nord, 1 Einheit = 1 m. Nur über `CityView.ToUnity/ToGeo`
  umrechnen. Höhen von Dächern/Gelände: `Cesium3DTileset.SampleHeightMostDetailed` oder `CityView.SurfaceHeightAt`.
- **3D-Modelle:** Kenney Furniture Kit + Mini Characters (CC0) in `Assets/ThirdParty/Kenney` (Low-Poly), dazu
  realistische **Poly-Haven-Modelle** (CC0, kommerziell frei, keine Namensnennung nötig) in `Assets/ThirdParty/PolyHaven`
  als glTF (Import über `com.unity.cloud.gltfast`, 1k-Texturen für Mobile). Neue Poly-Haven-Modelle: Namen in
  `tools/fetch_polyhaven.py` eintragen, Skript ausführen, dann Setup Project. ItemId = `ph-<name>` (echte Meter, Scale 1;
  im Backend über `Ph(...)` platzieren – die Modelle schauen nach +Z, `Ph` dreht sie passend). Kenney-Möbel-ItemId =
  Modellname; `game-tictactoe`/`game-quiz` sind Spielstationen, `custom-*` baut der Client (Pool, Säule …),
  Kleinteile (Laptop, Lampe, Kaffeemaschine) werden automatisch auf Möbel gestapelt.
  Massstab: Möbel ×0.2 ≈ echte Grösse (Tisch 65 cm, Tür 2 m), Figuren ×1.8 (≈ 1.4 m). 1 Rasterfeld = 1 m.
- **Räume:** eigene Grösse (6–40 m), Themes mit prozeduralem Boden (FloorTextures) und einer `Enclosure`:
  `Walls` (Kenney-Wände mit Fenstern/Tür), `Railing` (Dachterrasse, Glasgeländer) oder `GlassFacade` (verglastes
  Obergeschoss mit LED-Kante). `Railing`/`GlassFacade` stehen auf dem echten Gebäudedach in der 3D-Stadt
  (`CityView.RoofAnchorAsync`). Die **Rooftop Lounge** (Theme `skylounge`) bildet das echte „Clouds“ im 35. Stock des
  Prime Towers nach (Bar, offene Küche, Bistro, Restaurant, Lounge, Privé) mit futuristischen Custom-Items
  (Hologramm, schwebende Murano-Orbs, DJ-Pult mit Equalizer, Panorama-Fernrohre). Unbekannte ItemIds landen in
  `RoomView.MissingItems` – der Showcase-Test verlangt, dass die Liste leer ist.
- **Gebäude in der Stadt:** swissBUILDINGS3D kommt ohne Textur mit Rohfarben (rote Dächer, gelbe Wände). Das Tileset
  nutzt deshalb `Materials/Buildings.mat` (helles „Architekturmodell“); Gelände behält Cesiums Material (Overlays).
- **swisstopo-Daten** (OGD, kommerziell nutzbar): Quellenangabe „© swisstopo“ muss sichtbar bleiben.
  Vor dem Launch Daten selbst hosten (Fair-Use der geo.admin.ch-Dienste) und Nutzungsbedingungen prüfen.
- **Screens über der 3D-Welt** sind transparent (`screen--transparent`); Container, die Touches
  zur Karte durchlassen sollen, bekommen die Klasse `pass-through`.

### Neues Feature hinzufügen

1. Passendes Modul wählen (oder neues Modul, siehe unten).
2. Entity in `Domain/` (internal, Factory `Create(...)`, Regeln im Entity) + Unit-Test in `tests/Reconnect.UnitTests`.
3. `DbSet` + Konfiguration im `<Name>DbContext` des Moduls, dann Migration erzeugen (siehe Befehle).
4. DTOs in `Reconnect.Contracts/<Bereich>/`, Routen in `ApiRoutes` (`Path` relativ, `Group` inkl. `/v1`).
5. Endpoints in `Features/` (statische `Map(api)` mit `api.MapGroup(ApiRoutes.X.Path)`), in
   `<Name>Module.MapEndpoints` aufrufen. Braucht es Daten eines anderen Moduls → dessen `Public`-Interface.
6. Integrationstest in `tests/Reconnect.Api.Tests/<Bereich>/`.

### Neues Modul hinzufügen

1. `src/Modules/<Name>/Reconnect.Modules.<Name>/` – csproj wie die anderen Module (SharedKernel, Contracts,
   erlaubte Module; `InternalsVisibleTo Reconnect.UnitTests`).
2. `<Name>Module : IModule`, `<Name>DbContext` mit `HasDefaultSchema("<name>")` + `ModuleDesignTimeFactory`.
3. In `Program.cs` bei `AddModules(...)` eintragen, Projekt in `Reconnect.slnx`, Api und Testprojekten referenzieren.
4. In `ModuleBoundaryTests` (`Modules` + `AllowedDependencies`) und in der Modultabelle oben eintragen.

## Konventionen

- Sprache im Code: Englisch (Namen, Kommentare nur wo nötig). Doku/Commits dürfen Deutsch sein.
- IDs sind `Guid` (v7, zeitlich sortierbar: `Guid.CreateVersion7()`).
- Zeit immer über `TimeProvider` (testbar), gespeichert als UTC `DateTimeOffset`.
- Geodaten: SRID 4326 (WGS84), Spalten als `geography` → Distanzen in Metern.
  In Contracts nur `Latitude`/`Longitude` als `double`, nie NTS-Typen.
- Endpoints geben `TypedResults` zurück; Fehler als ProblemDetails / ValidationProblem.
- Kein Repository-Layer: Endpoints nutzen den DbContext ihres Moduls direkt; wiederverwendete
  Abfragen als kleine Klassen im Modul (z. B. `RoomReader`).
- Sicherheitsregeln (Blocks!) nie im Client verlassen – jede Abfrage auf Nutzer/Räume muss
  Blocks über `IBlockQueries` berücksichtigen.
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
dotnet test                                    # alle Tests (Docker muss laufen)
dotnet run --project src/Reconnect.AppHost     # alles lokal starten (Docker muss laufen)
dotnet tool restore                            # dotnet-ef aus dotnet-tools.json

# Neue Migration in einem Modul (Beispiel Rooms)
$p = "src/Modules/Rooms/Reconnect.Modules.Rooms/Reconnect.Modules.Rooms.csproj"
dotnet ef migrations add <Name> --project $p --startup-project $p --context RoomsDbContext --output-dir Infrastructure/Migrations
dotnet ef migrations has-pending-model-changes --project $p --startup-project $p   # prüft auch die CI
```

Migrationen werden in der Entwicklung beim Start der API automatisch angewendet (pro Modul).
Lokale Dev-DB zurücksetzen: AppHost stoppen, Container entfernen, Volume `reconnect.apphost-*-postgres-data` löschen.

**Showcase-Räume:** In Development legt `Modules/Rooms/.../Infrastructure/Seeding/ShowcaseRooms.cs` 5 eingerichtete Räume an
(Rooftop Lounge, Café Limmat, Kunst-Atelier, Opern-Foyer, ETH Bibliothek) und aktualisiert sie bei jedem Start.

**Dev-Login:** In Development legt die API das Konto **Admin / Admin** an (Rolle `Admin`,
`DevAdmin` in `appsettings.Development.json`, Code: `Modules/Identity/.../Features/DevAdminSeeder.cs`).
Es umgeht die Passwortregeln und existiert nur, wenn die Umgebung Development ist –
nie in anderen Umgebungen aktivieren (ein Test prüft das).

```powershell
# Unity (Editor geschlossen) – Pfad zur installierten LTS-Version anpassen
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe"
& $unity -batchmode -quit -projectPath client -executeMethod Reconnect.Client.Editor.ProjectSetup.Run -logFile -
& $unity -batchmode -projectPath client -runTests -testPlatform EditMode -testResults client/Logs/editmode.xml -logFile -
```

## Roadmap

1. **Backend-Grundgerüst** – Auth, Profile, Räume, Gebäude (PostGIS), Likes/Matches, Blocks/Reports
2. **Unity-Client mit Login und Raumliste** – Upgrade auf Unity 6 LTS, Contracts-DLL einbinden
3. **Multiplayer-Raum** – erledigt lokal über SignalR: Avatare, Laufen (Wegfindung), Sprechblasen, Emotes,
   Minigames (Tic-Tac-Toe, Zürich-Quiz), 5 Showcase-Räume. Offen: Photon/Custom Auth bei Bedarf
4. **Raum-Editor** (Layout speichern über `PUT /rooms/{id}/layout`)
5. **Likes/Matches/Chat im Client** (SignalR)
6. **Stadtquartier aus swisstopo-Daten** – Grundlage steht (Cesium + swissBUILDINGS3D); offen: Gebäude-Eingänge, Self-Hosting, Performance auf Geräten
7. **Face-Tracking**
8. **Videocall (LiveKit)**
