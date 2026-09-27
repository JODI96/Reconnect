"""Downloads the Poly Haven models used by the client (CC0) into client/Assets/ThirdParty/PolyHaven.

Usage: python tools/fetch_polyhaven.py
Idempotent: files that already exist are skipped. Resolution 1k keeps textures mobile-friendly.
"""
import json
import os
import urllib.request

MODELS = [
    # Seating
    "mid_century_lounge_chair", "modern_arm_chair_01", "sofa_02", "sofa_03", "Ottoman_01",
    "bar_chair_round_01", "dining_chair_02",
    # Tables
    "modern_coffee_table_01", "coffee_table_round_01", "side_table_01", "side_table_tall_01",
    "round_wooden_table_01",
    # Light, plants, decoration
    "modern_ceiling_lamp_01", "potted_plant_02", "potted_plant_04", "pachira_aquatica_01",
    "throw_pillows_01", "tea_set_01", "ceramic_vase_01", "marble_bust_01",
    # Offices and conference centre (Prime Tower)
    "metal_office_desk", "desk_lamp_arm_01", "Shelf_01", "GreenChair_01", "ArmChair_01", "modern_wooden_cabinet",
    "dining_table",
    # Showcase rooms (replace the low-poly Kenney pieces): café, library, opera foyer, art studio
    "gallinera_chair", "gallinera_table", "painted_wooden_bench", "WoodenChair_01", "WoodenTable_01", "Sofa_01",
    "CoffeeTable_01", "CoffeeCart_01", "standing_chalkboard_01", "wooden_display_shelves_01", "metal_stool_02",
    "wooden_bookshelf_worn", "book_encyclopedia_set_01", "decorative_book_set_01", "chess_set",
    "vintage_grandfather_clock_01", "mantel_clock_01", "Chandelier_01", "brass_candleholders", "antique_ceramic_vase_01",
    "hanging_picture_frame_01", "hanging_picture_frame_02", "hanging_picture_frame_03", "standing_picture_frame_01",
    "bronze_ray_statue", "potted_plant_01", "ceramic_vase_03",
    # Lake bath (Seebad): outdoor furniture, planters, deck lights
    "outdoor_table_chair_set_01", "planter_box_01", "planter_box_02", "street_lamp_02",
    # Prime Tower storeys: indoor plants, modern shelves, bar lights and stools, bar/office/lounge decoration
    "anthurium_botany_01", "calathea_orbifolia_01", "fern_02", "planter_pot_clay",
    "steel_frame_shelves_01", "steel_frame_shelves_02", "caged_hanging_light", "hanging_industrial_lamp", "Chandelier_02",
    "metal_stool_01", "metal_stool_03", "modern_coffee_table_02", "industrial_coffee_table", "round_wooden_table_02",
    "wine_bottles_01", "brass_goblets", "brass_vase_01", "brass_vase_02", "ceramic_vase_02", "bronze_shark_statue",
    "wall_clock", "fancy_picture_frame_01", "fancy_picture_frame_02", "projector_screen", "stationery_supplies",
    "classic_laptop",
]
RESOLUTION = "1k"
ROOT = os.path.join(os.path.dirname(__file__), "..", "client", "Assets", "ThirdParty", "PolyHaven")
HEADERS = {"User-Agent": "Reconnect asset fetch (CC0 Poly Haven)"}


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS)) as response:
        return response.read()


def save(url, path):
    if os.path.exists(path):
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as file:
        file.write(get(url))


# Some Poly Haven files hold several variants side by side; keep one (node-name suffix) and move it to the origin.
VARIANTS = {
    "pachira_aquatica_01": "_d", "brass_candleholders": "_02", "anthurium_botany_01": "01_a", "calathea_orbifolia_01": "01_a",
    "fern_02": "02_b", "wine_bottles_01": "_bordeaux",
}


def keep_variant(path, suffix):
    with open(path, encoding="utf-8") as file:
        gltf = json.load(file)
    scene = gltf["scenes"][gltf.get("scene", 0)]
    keep = [i for i in scene["nodes"] if gltf["nodes"][i].get("name", "").endswith(suffix)]
    if not keep or keep == scene["nodes"]:
        return
    scene["nodes"] = keep
    for i in keep:
        gltf["nodes"][i]["translation"] = [0, gltf["nodes"][i].get("translation", [0, 0, 0])[1], 0]
    with open(path, "w", encoding="utf-8") as file:
        json.dump(gltf, file, indent=1)


def main():
    for model in MODELS:
        files = json.loads(get(f"https://api.polyhaven.com/files/{model}"))
        gltf = files.get("gltf", {}).get(RESOLUTION, {}).get("gltf")
        if gltf is None:
            print("skipped (no glTF at", RESOLUTION + "):", model)
            continue
        folder = os.path.join(ROOT, model)
        save(gltf["url"], os.path.join(folder, f"{model}.gltf"))
        for relative, include in gltf.get("include", {}).items():
            save(include["url"], os.path.join(folder, *relative.split("/")))
        if model in VARIANTS:
            keep_variant(os.path.join(folder, f"{model}.gltf"), VARIANTS[model])
        print("ok", model)

    with open(os.path.join(ROOT, "License.txt"), "w", encoding="utf-8") as file:
        file.write("All models in this folder are from Poly Haven (https://polyhaven.com) and licensed CC0 1.0.\n"
                   "Free for commercial use, no attribution required. Fetched by tools/fetch_polyhaven.py.\n")


if __name__ == "__main__":
    main()
