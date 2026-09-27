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
      Tests/PlayMode/          Startet Main.unity; rendert u. a. Stadt, Showcase-Räume, Tower-Stockwerke (tower-*.png,
                             tower-city-*.png) nach client/Logs; GoogleCityTests nur explizit (kostet eine Google-Sitzung)
```

### Module

| Modul    | Verantwortung | Schema | Darf nutzen |
|----------|---------------|--------|-------------|
| Identity | Konten, Login, JWT/Refresh, Rollen, Dev-Admin | `identity` | – |
| City     | Gebäude (PostGIS, Grundrisse), Kartensitzungen (Google/swisstopo) | `city` | – |
| Wallet   | CHF-Konten (Rappen, Kontobuch), Startkapital 10'000 CHF | `wallet` | – |
| Safety   | Blocks, Reports (Moderation) | `safety` | Identity |
| Profiles | Anzeigename, Alter, Bio, Verifizierung | `profiles` | Identity, Safety |
| Social   | Likes, Matches, Match-Chat (`/hubs/chat`) | `social` | Profiles, Safety |
| Rooms    | Räume, Layout (jsonb), Tower-Stockwerke mit Kapazität, Lift + Warteschlange, Präsenz + Minigames (`/hubs/room`, Redis), Showcase | `rooms` | Identity, Profiles, Safety, City |
| RealEstate | Büros kaufen/verkaufen (vorerst Prime Tower), legt den Raum des Käufers an | `realestate` | Wallet, Rooms, City |

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
- **Spielfiguren (realistisch, erwachsen):** 12 MakeHuman-Figuren (CC0) in `Assets/ThirdParty/MakeHuman`, erzeugt per
  `tools/avatars/build_avatars.py` (Blender + MPFB, siehe `tools/avatars/README.md`), Animationen aus der Quaternius
  Universal Animation Library (CC0, `Assets/ThirdParty/Quaternius`). Alles **Humanoid** (Rig `mixamo_unity`, Ruhepose
  T-Pose), `AvatarSetup` baut Prefabs (`_Project/Avatars`), URP-Materialien pro Mesh-Slot und den Animator: Basis
  (Idle/Walk/Talk/Sitzen/Springen/Schwimmen), Ebene `Hands` (entspannte Finger), `Arm Gestures`/`Head Gestures` (Winken,
  Nicken, Kopfschütteln als Muskel-Clips; `AvatarView` blendet die Ebene während der Geste ein). Massstab echte Meter.
  Das Animations-FBX muss das Mannequin-Mesh enthalten (Bind-Pose = Humanoid-Referenz, sonst läuft alles schief).
  Die Szene braucht genau einen `AudioListener` (an der Hauptkamera), sonst ist nichts zu hören.
  **Handy-Budget:** jede Figur = ein Skinned Mesh mit 2 Materialien (Atlas `opaque` 1024 / `cutout` 512, ASTC) in
  3 LODs (~8k / 3k / 1k Dreiecke, LODGroup), 2-Knochen-Skinning. Poly-Haven-Möbel werden nach dem Download mit
  `tools/polyhaven_mobile.py` (Blender) auf ≤ 3k Dreiecke pro Teil reduziert (oft platzierte Modelle wie Topfpflanzen:
  `MODEL_BUDGET`). **Draw Calls:** Setup Project backt jedes mehrteilige Katalogmodell zu einem Mesh mit einem Sub-Mesh pro
  Material (`ItemMeshMerger` → `_Project/Build/Merged/*.mesh|.prefab`, LFS); aus Primitiven gebaute Items (Bar, Kern,
  Fassade, Spielstationen) fasst `MeshBaker.MergeStill` zur Laufzeit pro Material zusammen (bewegte Teile mit Skript
  bleiben einzeln), `CustomItems` teilt Materialien gleicher Farbe; kleine Dinge werden ab 1,2 % Bildhöhe nicht gezeichnet
  (`CullWhenTiny`). Auf grossen Etagen lässt sich nur bis ~24 m Bildbreite herauszoomen (`MaxPlayViewWidth`).
  `CrowdPerformanceTests`: volle Lobby (150 Personen, ganz herausgezoomt über der Etagenmitte) auf „Normal“ ≤ 400 Draw
  Calls, ≤ 80 Set-Pass-Calls, ≤ 400k Dreiecke; „Hoch“ (SSAO zeichnet alles ein zweites Mal in einen Tiefen-Vorabdurchgang)
  das Doppelte bei Draw Calls/Dreiecken. Das Log listet die teuersten Items („Draws by item“).
- **Grafik Hoch/Normal** (`GraphicsQuality`, pro Gerät gemerkt, Stadt-Menü): Hoch = SSAO (Tiefenquelle, halbe Auflösung),
  Reflexionen, doppelt so viele Lampenlichter; Standard Hoch ausser auf Handys < 5,5 GB RAM.
- **Kamera im Raum:** drehbar (Knöpfe ‹ › in 90°-Schritten, zwei Finger drehen, Q/E am PC); Wände zwischen Kamera und
  Raum werden ausgeblendet (`RoomView.RotateView`, Habbo-Prinzip: man schaut immer in den Raum).
- **Sitzen:** Kein Emote-Knopf – Stuhl/Sofa/Liege antippen (auch knapp daneben, `RoomView.SeatAt`), die Figur läuft hin und
  setzt sich. Welche Möbel Sitze sind und wie viele Plätze sie haben, steht in `RoomSeats` (Contracts, Server und Client).
  Der Server merkt sich den Platz pro Person (`Sit(item, place)` → false wenn besetzt, `StandUp`, `MoveTo` steht auf;
  `PlayerSeated`, Snapshot enthält `Seat`), adressiert über den Index im Layout. Der Client misst die Sitzhöhe am Modell
  (`Seat`, Strahl von oben); die Blickrichtung ist die Vorderseite des Möbels laut Katalog (Kenney −Z, Poly Haven +Z bei
  Rotation 0). Hocker ohne Lehne drehen sich immer zur Theke/zum Tisch daneben. `SeatDirectionTests` prüft jedes
  Sitzmöbel in alle vier Richtungen und den Barfall. `AvatarView.SitOn` schiebt die Figur nach der Animation so, dass die Hüfte
  auf der Sitzfläche liegt. Antippen eines Stuhls/Sofas oder „Sitzen“ (nächster freier Platz). `SeatingTests` prüft alle Sitztypen.
- **Schwimmen:** Pools (`custom-pool` 6×3 m oder `custom-pool-<B>x<T>`) sind in den Boden versenkt (Boden und Sockel
  werden um sie herum aus Stücken gebaut, `CustomItems.PoolDepth`/`WaterLevel`); ihre Felder sind begehbares Wasser
  (`RoomView.IsWater`). Wer darauf steht, schwimmt (`Swimming`: Wassertreten/Kraulen aus der Quaternius-Bibliothek), die
  Figur wird so tief gesetzt, dass der Kopf knapp über dem Wasser bleibt. Der Server kennt kein Wasser (keine
  Tile-Prüfung) – jeder Client leitet es gleich aus dem Layout ab. Raum: „Seebad Utoquai“ (Theme `pool`), `PoolTests`.
- **Musik:** ein CC0-Titel pro Raum-Theme (OpenGameArt, `tools/fetch_music.py` prüft die Lizenz, `Music/CREDITS.md`),
  `RoomMusic` blendet ein/aus, „Ton“/„Stumm“ im Raum wird gemerkt; Import: Streaming + Vorbis.
- **3D-Modelle:** Kenney Furniture Kit + Mini Characters (CC0) in `Assets/ThirdParty/Kenney` (Low-Poly, Figuren nicht mehr genutzt), dazu
  realistische **Poly-Haven-Modelle** (CC0, kommerziell frei, keine Namensnennung nötig) in `Assets/ThirdParty/PolyHaven`
  als glTF (Import über `com.unity.cloud.gltfast`, 1k-Texturen für Mobile). Neue Poly-Haven-Modelle: Namen in
  `tools/fetch_polyhaven.py` eintragen, Skript ausführen, dann `tools/polyhaven_mobile.py` (Blender, Dreiecksbudget),
  dann Setup Project. ItemId = `ph-<name>` (echte Meter, Scale 1; schauen bei Rotation 0 nach +Z, Kenney nach -Z).
  Kenney-Möbel-ItemId = Modellname; `game-tictactoe`/`game-quiz` sind Spielstationen, `custom-*` baut der Client
  (Pool, Säule …). Massstab: Möbel ×0.2 ≈ echte Grösse (Tisch 65 cm, Tür 2 m) – Ausreisser des Kenney-Kits (Laptop, WC,
  Dusche …) bekommen in `ProjectSetup.KenneyRealHeights` ihre echte Höhe. Figuren 1:1 (1,60–1,85 m). 1 Laufeld = 1 m.
- **Baueditor & Bauregeln:** Baufeld = 25 cm (16 pro Laufeld, `BuildGrid`), Deko auf Tischen im 12,5-cm-Raster
  (`DecorStep`, Position = Mitte in Metern). Jedes Item hat im Katalog (`ItemDefinitions`, generiert von
  `BuildCatalogGenerator` beim Setup Project aus den echten Modellen → `Contracts/Rooms/ItemCatalogData.cs`, danach
  `dotnet build`) Grundfläche in Zellen (bis 5 cm Überstand), echte Grösse, Höhe, Art (Boden/Teppich/Deko/Wand/Decke),
  Kategorie, deutschen Namen und – bei Tischen/Regalen/Theken – die per Strahlen gemessene Tischplatte (Höhe + flache
  Fläche; runde Tische: Quadrat in der Platte). Dazu rendert er pro Item ein Katalogbild (`Build/Icons`, `BuildIcons.asset`).
  Poly-Haven-Dateien mit mehreren Varianten nebeneinander (Glückskastanie, Kerzenständer) werden in `fetch_polyhaven.py`
  (`VARIANTS`) auf eine reduziert. `RoomLayout.Validate` (Server bei jedem Speichern, Client
  im Editor): Raster + 90°-Schritte, im Raum, keine Überlappung pro Ebene, Deko nur ganz auf einer Tischfläche, Wandobjekte
  an einer Wand, Eingang und Lift-Ausstieg frei (`RoomZones`), jeder Sitz erreichbar. Begehbare/Wasser-Felder kommen aus
  `RoomLayout.BlockedTiles/WaterTiles` (Server, Client, Editor gleich). `PUT /rooms/{id}/layout`: Besitzer oder Admin,
  400 mit Fehler pro `items[i]`, danach `RoomLayoutChanged` an alle im Raum. Im Client: „Bauen“ → `BuildPanel` +
  `BuildEditor` (reines C#) + `BuildPreview` (Raster, Geist grün/rot). `RoomLayoutFixer` setzt alte Layouts aufs Raster.
  `RoomLayoutFixer` schiebt Stühle/Hocker an den Tisch vor ihnen (an kleinen Tischen mittig).
  Geseedete Räume stehen als `Cell(item, x, z, rotation)` (Zellen, linke untere Ecke der Grundfläche) bzw. Deko als
  `At(item, x, z, rotation)` (Mitte in Metern) im Code; neu aufs Raster: `Dump_seed_rooms` + `RECONNECT_EXPORT_FROM`;
  `SeedRoomLayoutTests` prüft jeden Raum; `SeedRoomExport` (mit `RECONNECT_EXPORT_ROOMS=<Ordner>`) gibt Karten aus.
- **Räume:** eigene Grösse (6–40 m), Themes mit prozeduralem Boden (FloorTextures) und einer `Enclosure`:
  `Walls` (Kenney-Wände mit Fenstern/Tür), `Railing` (Dachterrasse, Glasgeländer) oder `GlassFacade` (verglastes
  Obergeschoss mit LED-Kante). `Railing`/`GlassFacade` stehen auf dem echten Gebäudedach in der 3D-Stadt
  (`CityView.RoofAnchorAsync`). Die **Sky Lounge** (Theme `skylounge`, 35. OG) bildet das Restaurant/die Bar im obersten Stock des
  Prime Towers nach (Bar, offene Küche, Bistro, Restaurant, Lounge, Privé) mit futuristischen Custom-Items
  (Hologramm, schwebende Murano-Orbs, DJ-Pult mit Equalizer, Panorama-Fernrohre). Unbekannte ItemIds landen in
  `RoomView.MissingItems` – der Showcase-Test verlangt, dass die Liste leer ist.
- **Stadt-Darstellung pro Sitzung (Backend entscheidet, `POST /v1/maps/session`):** Premium (Rolle `Premium`
  oder `Admin`) bekommt immer **Google Photorealistic 3D Tiles**, Gratis-Nutzer `Maps:FreeGoogleSessionsPerMonth`
  Sitzungen pro Monat, danach swisstopo; `Maps:MonthlyFreeGoogleSessionBudget` begrenzt die Kosten aller
  Gratis-Nutzer. Ohne Key bekommt jeder swisstopo. **Schalter im Stadt-Menü** „Stadt: swisstopo / Google 3D“ (pro Gerät
  gemerkt, Standard swisstopo – dann wird das Backend gar nicht gefragt und nichts abgerechnet, auch nicht im Editor). Der Client (`MapService`) merkt sich die Antwort ~3 h
  (eine Google-Sitzung wird einmal abgerechnet). Im Google-Modus läuft swisstopo **unsichtbar** weiter
  (Layer `CityData`): Dachhöhen, Marker, Rooftop-Platzierung und Kamera-Kollisionen kommen nie aus Google-Daten
  (Google-Bedingungen verbieten das Extrahieren). Google-Höhen sind ellipsoidisch, swisstopo über Meer: der
  Google-Tileset wird um die Geoidhöhe gesenkt (`CitySettings.googleHeightOffset`, Zürich −47.75 m, EGM2008).
  Google-Logo und -Credits zeigt Cesium an (`showCreditsOnScreen`) – Pflicht laut Google.
  **Key:** `dotnet user-secrets set "Parameters:google-maps-api-key" "<key>" --project src/Reconnect.AppHost`
  (nie in Code/Repo). Im Google-Cloud-Konto Key auf Map Tiles API + App-IDs beschränken und Tageslimit setzen.
  Automatische Tests starten nie eine Google-Sitzung.
- **Prime Tower (Stockwerke):** öffentliche Stockwerke gehören dem Turm (`TowerOwners`, alle Umgebungen, `PrimeTowerFloors`):
  Lobby EG (150 Personen, Warteschlangen-Ort, muss die grösste Kapazität haben), Coworking 12. OG (100), Sky Office 24. OG
  (80, Theme `office`), Konferenzzentrum 34. OG (140), Sky Lounge 35. OG (120). Jede Etage ist der **echte Grundriss**
  (~63 × 36 m, 1580 m², schräge Fassaden): `TowerFloorPlan.FromFootprint` legt das kleinste Rechteck um den OSM-Grundriss,
  `RoomDto.Outline` (Polygon in Raum-Metern) + `Anchor` (Geo-Position/Drehung); Bauregeln und Laufkacheln folgen dem
  Polygon (`RoomLayoutContext.IsInside/TileWalkable`, 0,3 m Abstand zur Fassade), keine Wände/Tür. In der Mitte der fixe
  Gebäudekern `custom-core` mit Lifttüren auf beiden Längsseiten (nicht verschieb-/löschbar, Ausstieg bleibt frei, Spawn
  vor dem Lift). Eingerichtet werden die Etagen in `TowerFloorDesigns` mit `FloorDesigner` (Gruppen in Metern: Lounge,
  Pultinsel, Glasraum, Tafel, Bistro, Stehtisch; `FacadeGreenery` stellt Pflanzen der Fassade entlang, `TryAt` nur wo
  keine Regel bricht). Laufkarten prüfen: `Dump_tile_maps` mit `RECONNECT_TILEMAP=<Ordner>`. Spiele pro Etage: Vier gewinnt,
  Memory, Schach (`BoardGameJoin/Move/Reset`, `TwoPlayerGame`), Quiz-Show, Tic-Tac-Toe, Zürich-Quiz.
  Kapazität wird atomar in Redis geprüft (Lua), der Lift (`RideElevator`) fährt sofort oder stellt in eine FIFO-Warteschlange;
  wird ein Platz frei, fährt der Nächste automatisch (`ElevatorArrived`). Wer mit dem Lift kommt, steht vor der Liftbank
  (`custom-elevator`). Büros (RealEstate, 2.–33. OG) sind ganze Etagen (60 Personen, 5000 + Etage × 500 CHF, `StarterOffice`-Einrichtung) und kosten CHF, Verkauf zurück an den Turm zum Kaufpreis; in Towers kann man
  keine freien Räume anlegen. Geld: Wallet (Rappen als Ganzzahl, jede Buchung im Kontobuch; Transaktionen in der
  Execution Strategy, weil Aspire Retries aktiviert).
- **Stockwerke in echter Höhe (Schnittansicht):** `TowerInfo` = Grundriss aus `BuildingDto.Footprint` (OpenStreetMap,
  © OpenStreetMap-Mitwirkende, ODbL – Quellenangabe in Stadt- und Raum-UI; swisstopo modelliert den Prime Tower unvollständig),
  Boden-/Dachhöhe aus swisstopo. `TowerCutaway` schneidet den echten Turm aus Google-Tiles und Gelände (Cesium-Polygon-Clipping,
  `materialKey = "Clipping"` setzen!) und baut unser Turmmodell bis zum Stockwerk; der Raum steht an der Fassade
  (`RoomAnchor`, innerhalb des Grundrisses). swisstopo-Gebäude lassen sich nicht clippen (eigenes Material) und werden im
  swisstopo-Modus während der Schnittansicht ausgeblendet. Etage = 3,5 m.
- **Echte Gebäude & Namen (rechtlich):** Gebäude dürfen als Ort genannt und von aussen dargestellt werden, aber
  keine Marken/Namen von Betrieben (z. B. das Restaurant im Prime Tower heisst im Spiel „Sky Lounge“), keine Logos,
  Schriftzüge oder Kunstwerke nachbauen, und überall wo ein echtes Gebäude bespielt wird der Hinweis „Unabhängiges Spiel –
  nicht verbunden mit den Eigentümern …“ (Stadt-Panel, Büro-Markt, Tower-Stockwerke). Vor Echtgeld-Käufen von Büros:
  Erlaubnis des Eigentümers (Swiss Prime Site) einholen oder den Tower neutral benennen.
- **Gebäude in der Stadt:** swissBUILDINGS3D kommt ohne Textur mit Rohfarben (rote Dächer, gelbe Wände). Das Tileset
  nutzt deshalb `Materials/Buildings.mat` (helles „Architekturmodell“); Gelände behält Cesiums Material (Overlays).
- **swisstopo-Daten** (OGD, kommerziell nutzbar): Quellenangabe „© swisstopo“ muss sichtbar bleiben.
  Vor dem Launch Daten selbst hosten (Fair-Use der geo.admin.ch-Dienste) und Nutzungsbedingungen prüfen.
- **Handy-Layout (nur Hochformat):** USS-Grössen sind Punkte/dp. `ScreenNavigator.UpdateLayout` skaliert das Panel
  (Laufzeitkopie der PanelSettings, `ConstantPixelSize`) mit `DisplayMetrics.UiScale` = dpi/160 auf Mobilgeräten, Desktop 1:1,
  und legt die Safe Area (Notch, Dynamic Island, Home-Leiste) als Padding an. Regeln: Tippziele ≥ 44 px, alles muss auf
  360 px Breite passen (Galaxy/iPhone SE), lange Namen mit Klasse `ellipsis` kürzen, Kopfzeilen `flex-shrink: 0`,
  Scrollbalken sind auf Handys aus. `DeviceLayoutTests` (PlayMode) prüft alle Screens auf iPhone SE/14/15/15 Pro Max,
  Galaxy S23 und Pixel 8 (Safe Area, Überlappung, abgeschnittener Text, Tippgrösse) und legt Screenshots in
  `client/Logs/devices/`. Zum Ansehen im Editor: Window → General → Device Simulator. (Unity Remote 5 funktioniert mit
  iOS 27 nicht – die App öffnet ihren Port nicht.)
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
  (wird generiert und in den AppHost-User-Secrets gespeichert). Google-Maps-Key = optionaler Aspire-Parameter
  `google-maps-api-key` (User Secrets), der Client bekommt ihn nur über `POST /v1/maps/session`. Connection Strings kommen
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
(Café Limmat, Kunst-Atelier, Opern-Foyer, ETH Bibliothek, Seebad Utoquai) und aktualisiert sie bei jedem Start. Möbel sind
überwiegend realistische Poly-Haven-Modelle, platziert mit `Cell(...)` nach den Bauregeln (Deko liegt auf Tischflächen).

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
4. **Raum-Editor** – erledigt: Baueditor mit 25-cm-Raster, Deko-Feinraster, Bildkatalog, Bauregeln auf Server und Client
5. **Likes/Matches/Chat im Client** (SignalR)
6. **Stadtquartier aus swisstopo-Daten** – Grundlage steht (Cesium + swissBUILDINGS3D); offen: Gebäude-Eingänge, Self-Hosting, Performance auf Geräten
7. **Face-Tracking**
8. **Videocall (LiveKit)**
