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

# Furniture surfaces (see SurfaceMaterials in ProjectSetup): wood veneers, leathers, stone as they are; fabrics as
# grey-scale (tinted per piece of furniture: sage velvet, sand bouclé …).
SURFACES = {
    "natural_walnut_veneer": False,   # raw veneer: the material tints it to oiled walnut
    "white_oak_veneer": False,
    "oak_veneer_01": False,
    "fabric_leather_02": False,       # cognac leather
    "leather_white": False,
    "velour_velvet": True,
    "terry_cloth": True,              # looped weave: bouclé upholstery
    "rough_linen": True,              # table cloths
}


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS)) as response:
        return response.read()


# ambientCG (https://ambientcg.com, CC0): stone slabs Poly Haven doesn't have (its marble_01 is a tiled floor).
AMBIENTCG = {
    "Marble012": "carrara",
    "Marble016": "black marble",
    "Travertine009": "travertine",
    "Tiles078": "large cream marble tiles (residence living/kitchen floor)",
    "Tiles075": "large dark green-black marble tiles (bath/spa floor)",
    "WoodFloor015": "light herringbone parquet",
    "WoodFloor014": "dark herringbone parquet",
    "WoodFloor064": "teak-like planks (pool deck)",
    "Rubber001": "gym rubber floor",
}


def fetch_ambientcg():
    import zipfile
    for asset in AMBIENTCG:
        folder = os.path.join(ROOT, asset)
        colour = os.path.join(folder, f"{asset}_color.jpg")
        if os.path.exists(colour):
            continue
        os.makedirs(folder, exist_ok=True)
        data = get(f"https://ambientcg.com/get?file={asset}_1K-JPG.zip")
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            names = archive.namelist()

            def read(suffix):
                return archive.read(next(n for n in names if n.endswith(suffix)))

            with open(colour, "wb") as file:
                file.write(read("_Color.jpg"))
            with open(os.path.join(folder, f"{asset}_normal.jpg"), "wb") as file:
                file.write(read("_NormalGL.jpg"))
            roughness = Image.open(io.BytesIO(read("_Roughness.jpg"))).convert("L")
        smoothness = roughness.point(lambda v: 255 - v)
        black = Image.new("L", roughness.size, 0)
        Image.merge("RGBA", (black, black, black, smoothness)).save(os.path.join(folder, f"{asset}_smooth.png"))
        print("ok", asset)


def main():
    fetch_ambientcg()
    for texture in list(TEXTURES) + list(SURFACES):
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
        if os.path.exists(rough):
            os.remove(rough)
        colour = os.path.join(folder, f"{texture}_color.jpg")
        grey = os.path.join(folder, f"{texture}_grey.png")
        if SURFACES.get(texture) and not os.path.exists(grey):
            # Tintable: the weave's light and shade only, around 80 % brightness (the tint gives the colour).
            image = Image.open(colour).convert("L")
            mean = sum(image.getdata()) / (image.width * image.height)
            image.point(lambda v: max(0, min(255, int(v * 205 / max(mean, 1))))).save(grey)
        print("ok", texture)

    with open(os.path.join(ROOT, "License.txt"), "w", encoding="utf-8") as file:
        file.write("All textures in this folder are from Poly Haven (https://polyhaven.com) or ambientCG (https://ambientcg.com: "
                   + ", ".join(AMBIENTCG) + ") and licensed CC0 1.0.\n"
                   "Free for commercial use, no attribution required. Fetched by tools/fetch_textures.py.\n")


if __name__ == "__main__":
    main()
