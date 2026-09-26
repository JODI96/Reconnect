# Avatar-Figuren (MakeHuman → Unity)

Die realistischen Spielfiguren werden per Skript aus **MakeHuman** (CC0) erzeugt, die Animationen kommen aus der
**Universal Animation Library** von Quaternius (CC0). Beides ist kommerziell nutzbar, ohne Namensnennung.

## Einmalig einrichten

1. Blender 5.x installieren: `winget install BlenderFoundation.Blender`
2. MPFB (MakeHuman für Blender) installieren:
   `blender --online-mode --command extension sync` und
   `blender --online-mode --command extension install -s --enable mpfb`
3. Die CC0-Asset-Packs von <http://static.makehumancommunity.org/assets/assetpacks.html> laden und in MPFBs
   Benutzerdaten entpacken (`%APPDATA%\Blender Foundation\Blender\5.2\extensions\.user\blender_org\mpfb\data`):
   `makehuman_system_assets`, `skins01`, `skins02`, `eyebrows01`, `eyelashes01`, `hair01`, `pants01`, `shirts01`,
   `shoes01`, `skirts01`, `dress01`, `suits01`, `bodyparts05`, `system_clothes_materials01`, `system_hair_materials01`.
   Nur die `*_cc0.zip`-Pakete verwenden (die CC-BY-Pakete bräuchten eine Namensnennung).
4. Animationen: `git clone https://github.com/J-Ponzo/gltf-universal-animation-library`

## Bauen

```powershell
$blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
& $blender -b --python tools/avatars/build_avatars.py                  # alle Figuren (einzelne: -- lena luca)
& $blender -b --python tools/avatars/build_animations.py -- <pfad>\glTF\AnimationLibrary_Godot_Standard.gltf
```

Danach in Unity **Reconnect → Setup Project** (oder batch `ProjectSetup.Run`): baut Prefabs, Materialien und den
Animator. `AvatarGalleryTests` rendert `client/Logs/avatars*.png` zur Kontrolle, Vorschauen aus Blender liegen in
`client/Logs/avatars/`.

## Wichtig

- **T-Pose:** MakeHuman-Rigs ruhen in einer A-Pose; `build_avatars.py` stellt Arme, Beine und Finger gerade und macht
  das zur Ruhepose. Sonst verschiebt Unitys Humanoid-Retargeting jede Animation (Arme abgespreizt, Fäuste).
- **Mesh-Namen = Material-Slots** (`skin`, `eyes`, `eyebrows`, `eyelashes`, `hair`, `beard`, `cloth_<n>`):
  `AvatarSetup` findet darüber Textur und Material.
- Figuren: 13–25k Dreiecke, 1k-Texturen. Für volle Räume (Lobby 80 Personen) fehlt noch ein LOD.
- Keine Haut-Assets mit Genitalien oder Tattoos; Figuren sind erwachsen (20–40) und angezogen.
