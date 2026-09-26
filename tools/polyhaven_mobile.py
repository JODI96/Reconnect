"""
Reduces the Poly Haven models (client/Assets/ThirdParty/PolyHaven) to a phone budget.

    blender -b --python tools/polyhaven_mobile.py

Poly Haven meshes are made for close-ups (20-50k triangles for a lamp or chair); in the room view a piece of
furniture is a few hundred pixels. Every mesh above MAX_TRIANGLES is collapse-decimated and the glTF rewritten in
place, keeping the original texture files. Run after tools/fetch_polyhaven.py; idempotent.
"""
import glob
import os

import bpy

MAX_TRIANGLES = 3000
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "client", "Assets", "ThirdParty", "PolyHaven"))


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


for path in sorted(glob.glob(os.path.join(ROOT, "*", "*.gltf"))):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    before = after = 0
    for obj in [o for o in bpy.data.objects if o.type == "MESH"]:
        tris = triangles(obj)
        before += tris
        if tris > MAX_TRIANGLES:
            modifier = obj.modifiers.new("Decimate", "DECIMATE")
            modifier.ratio = MAX_TRIANGLES / tris
            modifier.use_collapse_triangulate = True
            with bpy.context.temp_override(object=obj, active_object=obj):
                bpy.ops.object.modifier_apply(modifier="Decimate")
        after += triangles(obj)
    if after < before:
        bpy.ops.export_scene.gltf(filepath=path, export_format="GLTF_SEPARATE", export_keep_originals=True,
                                  export_yup=True, export_apply=True)
    print(f"POLYHAVEN {os.path.basename(path)}: {before} -> {after} triangles")
