"""
Reduces the Poly Haven models (client/Assets/ThirdParty/PolyHaven) to a phone budget.

    blender -b --python tools/polyhaven_mobile.py

Poly Haven meshes are made for close-ups (20-50k triangles for a lamp or chair); in the room view a piece of
furniture is a few hundred pixels. Every mesh above MAX_TRIANGLES (and every model above MAX_MODEL_TRIANGLES) is collapse-decimated and the glTF rewritten in
place, keeping the original texture files. Run after tools/fetch_polyhaven.py; idempotent.
"""
import glob
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(__file__))
from fetch_polyhaven import VARIANTS  # noqa: E402 – files with several variants side by side: keep one

MAX_TRIANGLES = 3000         # per mesh
MAX_MODEL_TRIANGLES = 12000  # per model (chess sets and book rows are many small meshes)
# Items placed many times on a storey (the tower floors line the glass with plants) get a smaller budget.
MODEL_BUDGET = {"potted_plant_01": 4000, "potted_plant_02": 4000}
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "client", "Assets", "ThirdParty", "PolyHaven"))


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def decimate(obj, ratio):
    modifier = obj.modifiers.new("Decimate", "DECIMATE")
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    with bpy.context.temp_override(object=obj, active_object=obj):
        bpy.ops.object.modifier_apply(modifier="Decimate")


for path in sorted(glob.glob(os.path.join(ROOT, "*", "*.gltf"))):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    model = os.path.splitext(os.path.basename(path))[0]
    removed = False
    if model in VARIANTS:
        # Keep the chosen variant (its root objects end with the suffix), move it to the origin.
        for obj in [o for o in bpy.data.objects if o.parent is None]:
            if obj.name.split(".")[0].endswith(VARIANTS[model]):
                obj.location.x = 0.0
                obj.location.y = 0.0
            else:
                for child in list(obj.children_recursive) + [obj]:
                    bpy.data.objects.remove(child, do_unlink=True)
                removed = True
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    before = sum(triangles(o) for o in meshes)
    # Some models are modelled in cm with a node scale of 0.01; the room sets the root scale when placing the model,
    # so bake every scale into the meshes (metres, scale 1).
    scaled = [o for o in bpy.data.objects if any(abs(v - 1.0) > 1e-4 for v in o.scale)]
    if scaled:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in scaled:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = scaled[0]
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for obj in meshes:
        if triangles(obj) > MAX_TRIANGLES:
            decimate(obj, MAX_TRIANGLES / triangles(obj))
    total = sum(triangles(o) for o in meshes)
    budget = MODEL_BUDGET.get(model, MAX_MODEL_TRIANGLES)
    if total > budget:
        for obj in meshes:
            decimate(obj, budget / total)
    after = sum(triangles(o) for o in meshes)
    if after < before or scaled or removed:
        bpy.ops.export_scene.gltf(filepath=path, export_format="GLTF_SEPARATE", export_keep_originals=True,
                                  export_yup=True, export_apply=True)
    print(f"POLYHAVEN {os.path.basename(path)}: {before} -> {after} triangles")
