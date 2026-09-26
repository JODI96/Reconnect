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


def main():
    for model in MODELS:
        files = json.loads(get(f"https://api.polyhaven.com/files/{model}"))
        gltf = files["gltf"][RESOLUTION]["gltf"]
        folder = os.path.join(ROOT, model)
        save(gltf["url"], os.path.join(folder, f"{model}.gltf"))
        for relative, include in gltf.get("include", {}).items():
            save(include["url"], os.path.join(folder, *relative.split("/")))
        print("ok", model)

    with open(os.path.join(ROOT, "License.txt"), "w", encoding="utf-8") as file:
        file.write("All models in this folder are from Poly Haven (https://polyhaven.com) and licensed CC0 1.0.\n"
                   "Free for commercial use, no attribution required. Fetched by tools/fetch_polyhaven.py.\n")


if __name__ == "__main__":
    main()
