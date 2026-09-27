"""Downloads the floor textures (Poly Haven, CC0) into client/Assets/ThirdParty/PolyHaven/Textures.

Usage: python tools/fetch_textures.py
For each texture: colour (Diffuse), normal map (OpenGL) and a smoothness map for URP (metallic 0 in RGB, smoothness =
1 - roughness in alpha), all 1k for phones. Idempotent: existing files are skipped.
"""
import io
import json
import os
import urllib.request

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "client", "Assets", "ThirdParty", "PolyHaven", "Textures")
RESOLUTION = "1k"
HEADERS = {"User-Agent": "Reconnect/1.0 (texture fetch)"}

# Texture id → what it is for (room theme; see FloorMaterialCatalog in ProjectSetup).
TEXTURES = {
    "marble_01": "lobby",
    "rectangular_parquet": "coworking",
    "poly_wool_herringbone": "conference",
    "granite_tile": "skylounge",
    "herringbone_parquet": "office",
}


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS)) as response:
        return response.read()


def main():
    for texture in TEXTURES:
        folder = os.path.join(ROOT, texture)
        os.makedirs(folder, exist_ok=True)
        files = json.loads(get(f"https://api.polyhaven.com/files/{texture}"))
        maps = {"Diffuse": "color", "nor_gl": "normal", "Rough": "rough"}
        for source, name in maps.items():
            path = os.path.join(folder, f"{texture}_{name}.jpg")
            if os.path.exists(path):
                continue
            url = files[source][RESOLUTION]["jpg"]["url"]
            with open(path, "wb") as file:
                file.write(get(url))

        smooth = os.path.join(folder, f"{texture}_smooth.png")
        rough = os.path.join(folder, f"{texture}_rough.jpg")
        if not os.path.exists(smooth):
            roughness = Image.open(rough).convert("L")
            smoothness = roughness.point(lambda v: 255 - v)
            black = Image.new("L", roughness.size, 0)
            Image.merge("RGBA", (black, black, black, smoothness)).save(smooth)
        os.remove(rough)
        print("ok", texture)

    with open(os.path.join(ROOT, "License.txt"), "w", encoding="utf-8") as file:
        file.write("All textures in this folder are from Poly Haven (https://polyhaven.com) and licensed CC0 1.0.\n"
                   "Free for commercial use, no attribution required. Fetched by tools/fetch_textures.py.\n")


if __name__ == "__main__":
    main()
