"""
Builds the realistic avatar figures for Reconnect from MakeHuman (CC0) with Blender + MPFB.

    blender -b --python tools/avatars/build_avatars.py

Needs Blender 5.x with the MPFB extension and the CC0 asset packs installed in MPFB's user data
(see tools/avatars/README.md). Writes one folder per figure to client/Assets/ThirdParty/MakeHuman/<id>/:
<id>.fbx (mixamo_unity rig, meters, T-pose) and two texture atlases. Built for phones: every figure is ONE
skinned mesh with two materials ("opaque": skin, eyes, clothes; "cutout": hair, beard, brows, lashes) in three
LODs (<id>_LOD0/1/2, Unity makes a LODGroup of them) - two draw calls per visible figure.
Previews for review go to client/Logs/avatars/.
"""
import bpy
import math
import os
import re
import sys

from bl_ext.blender_org.mpfb.services import HumanService, LocationService

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(REPO, "client", "Assets", "ThirdParty", "MakeHuman")
PREVIEWS = os.path.join(REPO, "client", "Logs", "avatars")
DATA = LocationService.get_user_data()

# Triangle budget of the close-up LOD per part (the body proxy has ~3k); LOD1/LOD2 are reduced from LOD0.
PART_TRIS = {"hair": 1800, "beard": 600}
CLOTH_TRIS = 1500
LODS = [(1, 0.35), (2, 0.12)]
CUTOUT = {"hair", "beard", "eyebrows", "eyelashes"}
OPAQUE_ATLAS = 1024           # a figure is ~150-400 px tall on a phone; 512 px skin is plenty
CUTOUT_ATLAS = 512


def woman(race, **kw):
    phenotype = dict(gender=0.0, age=0.53, muscle=0.55, weight=0.42, proportions=0.95, height=0.58,
                     cupsize=0.55, firmness=0.7, race=race)
    phenotype.update(kw)
    return phenotype


def man(race, **kw):
    phenotype = dict(gender=1.0, age=0.55, muscle=0.68, weight=0.48, proportions=0.95, height=0.62,
                     cupsize=0.5, firmness=0.5, race=race)
    phenotype.update(kw)
    return phenotype


CAUCASIAN = dict(asian=0.0, caucasian=1.0, african=0.0)
AFRICAN = dict(asian=0.0, caucasian=0.1, african=0.9)
ASIAN = dict(asian=0.9, caucasian=0.1, african=0.0)
MIXED = dict(asian=0.2, caucasian=0.5, african=0.3)

# Adult figures (20–40), fully dressed, everyday to smart-casual looks. Assets are CC0 MakeHuman packs.
FIGURES = [
    dict(id="lena", phenotype=woman(CAUCASIAN), skin="toigo_light_skin_with_natural_makeup", eyes="brown",
         hair=("long01", None), brows="eyebrow010", clothes=["toigo_shift_dress", "toigo_ballet_flats"]),
    dict(id="amara", phenotype=woman(AFRICAN, height=0.62), skin="young_african_female", eyes="brown",
         hair=("afro01", None), brows="eyebrow011", clothes=["toigo_halter_dress_knee_length", "toigo_ballet_flats"]),
    dict(id="mei", phenotype=woman(ASIAN, height=0.5), skin="young_asian_female", eyes="brown",
         hair=("toigo_blunt_bob_with_bangs", None), brows="eyebrow012",
         clothes=["toigo_fisherman_sweater", "toigo_wool_pants", "shoes06"]),
    dict(id="sofia", phenotype=woman(MIXED), skin="callharvey3d_midtoned_female", eyes="brownlight",
         hair=("ponytail01", "dariush086_ponytail01_brown"), brows="eyebrow009",
         clothes=["toigo_female_suit", "toigo_ballet_flats"]),
    dict(id="nora", phenotype=woman(CAUCASIAN, height=0.55), skin="toigo_light_skin_female_freckles", eyes="green",
         hair=("toigo_curled_under_bob", None), brows="eyebrow010",
         clothes=["toigo_basic_tucked_t-shirt", "toigo_tiered_mini_skirt", "toigo_ballet_flats"]),
    dict(id="priya", phenotype=woman(dict(asian=0.5, caucasian=0.3, african=0.2)), skin="cutoff3d_indian_female_enhanced",
         eyes="brown", hair=("elvs_french_braid_variation", None), brows="eyebrow011",
         clothes=["toigo_halter_dress_midi", "toigo_ballet_flats"]),
    dict(id="luca", phenotype=man(CAUCASIAN), skin="young_caucasian_male", eyes="blue",
         hair=("short02", "bogdan666_short_02_brown"), brows="eyebrow001", clothes=["toigo_male_suit_3", "shoes03"]),
    dict(id="kofi", phenotype=man(AFRICAN, height=0.68), skin="young_african_male", eyes="brown",
         hair=("short04", None), brows="eyebrow001", clothes=["namuhekam_male_polo_shirt", "toigo_wool_pants", "shoes05"]),
    dict(id="jun", phenotype=man(ASIAN, height=0.55), skin="young_asian_male", eyes="brown",
         hair=("short01", None), brows="eyebrow002", clothes=["male_casualsuit05", "shoes06"]),
    dict(id="noah", phenotype=man(CAUCASIAN, muscle=0.72), skin="young_caucasian_male", eyes="bluegreen",
         hair=("short03", None), brows="eyebrow001", clothes=["toigo_fisherman_sweater", "cortu_cargo_pants", "shoes04"]),
    dict(id="elias", phenotype=man(CAUCASIAN, age=0.6), skin="young_caucasian_male2", eyes="grey",
         hair=("short04", "toigo_short_04_brown"), brows="eyebrow002",
         clothes=["male_casualsuit03", "wdg_scruffy_beard", "shoes01"]),
    dict(id="mateo", phenotype=man(MIXED), skin="toigo_light_skin_male_bronze", eyes="brownlight",
         hair=("culturalibre_hair_02", None), brows="eyebrow001", clothes=["toigo_male_suit_tie_and_jacket", "shoes03"]),
]


def asset_file(kind, folder, ext):
    """First file with the extension in data/<kind>/<folder>/ as MPFB-relative path."""
    directory = os.path.join(DATA, kind, folder)
    name = next(f for f in sorted(os.listdir(directory)) if f.endswith(ext))
    return folder + "/" + name


def mhclo_uuid(kind, relative):
    with open(os.path.join(DATA, kind, relative), encoding="utf-8", errors="replace") as f:
        for line in f:
            if line.startswith("uuid"):
                return line.split()[1].strip()
    return None


def textures_of(mhmat_path):
    """(diffuse, normal) absolute paths from a MakeHuman material."""
    diffuse = normal = None
    folder = os.path.dirname(mhmat_path)
    with open(mhmat_path, encoding="utf-8", errors="replace") as f:
        for line in f:
            parts = line.strip().split(None, 1)
            if len(parts) != 2:
                continue
            key, value = parts
            candidate = os.path.join(folder, value.strip())
            if not os.path.exists(candidate):
                candidate = os.path.join(folder, os.path.basename(value.strip()))
            if not os.path.exists(candidate):
                continue
            if key == "diffuseTexture":
                diffuse = candidate
            elif key in ("normalmapTexture",):
                normal = candidate
    return diffuse, normal


def default_mhmat(kind, folder):
    directory = os.path.join(DATA, kind, folder)
    mats = [f for f in sorted(os.listdir(directory)) if f.endswith(".mhmat")]
    return os.path.join(directory, mats[0])


def build_figure(fig):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    hair_folder, hair_color = fig["hair"]
    hair_rel = asset_file("hair", hair_folder, ".mhclo")
    info = HumanService._create_default_human_info_dict()
    proxy = "female1605/female1605.proxy" if fig["phenotype"]["gender"] < 0.5 else "male1591/male1591.proxy"
    info.update(dict(
        name=fig["id"], phenotype=fig["phenotype"], rig="mixamo_unity", proxy=proxy,
        eyes="low-poly/low-poly.mhclo", eyebrows=asset_file("eyebrows", fig["brows"], ".mhclo"),
        eyelashes="eyelashes01/eyelashes01.mhclo", hair=hair_rel,
        clothes=[asset_file("clothes", c, ".mhclo") for c in fig["clothes"]],
        skin_mhmat=asset_file("skins", fig["skin"], ".mhmat"), skin_material_type="MAKESKIN",
        eyes_material_type="MAKESKIN"))
    hair_mhmat = default_mhmat("hair", hair_folder)
    if hair_color:
        alt = hair_folder + "/" + hair_color + "/" + hair_color + ".mhmat"
        info["alternative_materials"] = {mhclo_uuid("hair", hair_rel): alt}
        hair_mhmat = os.path.join(DATA, "hair", alt)

    settings = HumanService.get_default_deserialization_settings()
    settings["subdiv_levels"] = 0
    basemesh = HumanService.deserialize_from_dict(info, settings)
    rig = basemesh.parent

    # Which texture belongs to which mesh (objects are named "<id>.<asset>").
    parts = {
        "skin": (os.path.basename(proxy).split(".")[0], os.path.join(DATA, "skins", info["skin_mhmat"])),
        "eyes": ("low-poly", os.path.join(DATA, "eyes", "materials", fig["eyes"] + ".mhmat")),
        "eyebrows": (fig["brows"], default_mhmat("eyebrows", fig["brows"])),
        "eyelashes": ("eyelashes01", default_mhmat("eyelashes", "eyelashes01")),
        "hair": (hair_folder, hair_mhmat),
    }
    for i, cloth in enumerate(fig["clothes"]):
        # Beards are hair cards (alpha cut-out), not fabric.
        parts["beard" if "beard" in cloth else f"cloth_{i}"] = (cloth, default_mhmat("clothes", cloth))

    out_dir = os.path.join(OUT, fig["id"])
    os.makedirs(out_dir, exist_ok=True)
    for f in os.listdir(out_dir):
        if f.endswith((".png", ".fbx", ".png.meta")):
            os.remove(os.path.join(out_dir, f))

    meshes = {}
    for obj in [o for o in bpy.data.objects if o.type == "MESH" and o != basemesh]:
        slot = next((name for name, (asset, _) in parts.items() if obj.name.endswith("." + asset) or obj.name.endswith(asset)), None)
        if slot is None:
            print("UNMAPPED", obj.name)
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        prepare_mesh(obj, slot)
        meshes[slot] = obj
    bpy.data.objects.remove(basemesh, do_unlink=True)

    # Two atlases, UVs moved into each part's cell, one mesh with two materials.
    textures = {slot: textures_of(parts[slot][1])[0] for slot in meshes}
    # The garment covering the most surface (dress, suit, shirt) gets as much texture as the skin.
    cloths = sorted((s for s in meshes if s.startswith("cloth_")), key=lambda s: -sum(p.area for p in meshes[s].data.polygons))
    opaque_sizes = [(s, 512 if s == "skin" or (cloths and s == cloths[0]) else 128 if s == "eyes" else 256)
                    for s in meshes if s not in CUTOUT]
    cutout_sizes = [(s, 256 if s == "hair" else 128) for s in meshes if s in CUTOUT]
    for name, sizes, atlas, opaque in (("opaque", opaque_sizes, OPAQUE_ATLAS, True), ("cutout", cutout_sizes, CUTOUT_ATLAS, False)):
        if not sizes:
            continue
        cells = pack(sizes, atlas)
        image = compose_atlas(cells, textures, atlas, os.path.join(out_dir, name + ".png"), opaque)
        material = atlas_material(name, image)
        for slot, cell in cells.items():
            remap_uvs(meshes[slot], cell, atlas)
            meshes[slot].data.materials.clear()
            meshes[slot].data.materials.append(material)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes.values():
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes["skin"]
    bpy.ops.object.join()
    lod0 = meshes["skin"]
    lod0.name = lod0.data.name = fig["id"] + "_LOD0"
    to_t_pose(rig, [lod0])

    lods = [lod0]
    for level, ratio in LODS:
        lod = lod0.copy()
        lod.data = lod0.data.copy()
        lod.name = lod.data.name = f"{fig['id']}_LOD{level}"
        bpy.context.scene.collection.objects.link(lod)
        decimate(lod, ratio)
        lod.hide_render = True
        lods.append(lod)

    # Export: armature + LOD meshes, meters, Y up (Unity).
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    for lod in lods:
        lod.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, fig["id"] + ".fbx"), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False,
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", path_mode="STRIP")
    print(f"FIGURE {fig['id']}: LOD triangles {[triangles(l) for l in lods]}, {len(rig.data.bones)} bones")
    render_preview(fig["id"])


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def decimate(obj, ratio):
    """Collapse-decimates the mesh (before the armature modifier) and applies it."""
    if ratio >= 1.0:
        return
    modifier = obj.modifiers.new("Decimate", "DECIMATE")
    modifier.ratio = ratio
    with bpy.context.temp_override(object=obj, active_object=obj):
        bpy.ops.object.modifier_move_to_index(modifier="Decimate", index=0)
        bpy.ops.object.modifier_apply(modifier="Decimate")


def pack(sizes, atlas):
    """Places power-of-two squares into the atlas (quadtree): slot -> (x, y, size) in pixels."""
    free = [(0, 0, atlas)]
    cells = {}
    for slot, size in sorted(sizes, key=lambda item: -item[1]):
        fitting = sorted((f for f in free if f[2] >= size), key=lambda f: f[2])
        if not fitting:
            raise RuntimeError(f"Atlas {atlas} full, cannot place {slot} ({size})")
        x, y, square = fitting[0]
        free.remove(fitting[0])
        while square > size:
            square //= 2
            free += [(x + square, y, square), (x, y + square, square), (x + square, y + square, square)]
        cells[slot] = (x, y, size)
    return cells


def compose_atlas(cells, textures, atlas, path, opaque):
    """Scales each part's texture into its cell and saves the atlas as PNG."""
    import numpy as np

    pixels = np.zeros((atlas, atlas, 4), np.float32)
    pixels[..., :3] = 0.5
    if opaque:
        pixels[..., 3] = 1.0
    for slot, (x, y, size) in cells.items():
        if not textures.get(slot):
            continue
        image = bpy.data.images.load(textures[slot], check_existing=False)
        image.scale(size, size)
        cell = np.empty(size * size * 4, np.float32)
        image.pixels.foreach_get(cell)
        cell = cell.reshape(size, size, 4)
        if opaque:
            cell[..., 3] = 1.0
        pixels[y:y + size, x:x + size] = cell
        bpy.data.images.remove(image)
    result = bpy.data.images.new(os.path.basename(path), atlas, atlas, alpha=True)
    result.pixels.foreach_set(pixels.ravel())
    result.filepath_raw = path
    result.file_format = "PNG"
    result.save()
    return result


def remap_uvs(obj, cell, atlas):
    """Moves the part's 0..1 UVs into its atlas cell (one pixel inset against bleeding)."""
    import numpy as np

    mesh = obj.data
    while len(mesh.uv_layers) > 1:
        mesh.uv_layers.remove(mesh.uv_layers[-1])
    mesh.uv_layers[0].name = "UVMap"
    data = mesh.uv_layers[0].data
    uv = np.empty(len(data) * 2, np.float32)
    data.foreach_get("uv", uv)
    uv = np.clip(uv.reshape(-1, 2), 0.0, 1.0)
    x, y, size = cell
    uv[:, 0] = (x + 1 + uv[:, 0] * (size - 2)) / atlas
    uv[:, 1] = (y + 1 + uv[:, 1] * (size - 2)) / atlas
    data.foreach_set("uv", uv.ravel())


def atlas_material(name, image):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    material.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    material.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.7
    return material


# Unity's Humanoid retargeting expects the rest pose to be a T-pose (the animation library is in T-pose);
# MakeHuman rigs rest in an A-pose (arms ~47° down, elbows bent), which would offset every animation.
T_POSE = [
    ("mixamorig:LeftArm", (1, 0, 0)), ("mixamorig:LeftForeArm", (1, 0, 0)), ("mixamorig:LeftHand", (1, 0, 0)),
    ("mixamorig:RightArm", (-1, 0, 0)), ("mixamorig:RightForeArm", (-1, 0, 0)), ("mixamorig:RightHand", (-1, 0, 0)),
    ("mixamorig:LeftUpLeg", (0, 0, -1)), ("mixamorig:LeftLeg", (0, 0, -1)),
    ("mixamorig:RightUpLeg", (0, 0, -1)), ("mixamorig:RightLeg", (0, 0, -1)),
]


def to_t_pose(rig, meshes):
    """Poses arms and legs straight (T-pose) and makes that the rest pose, meshes included."""
    from mathutils import Matrix, Vector

    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    def align(name, target):
        bone = rig.pose.bones.get(name)
        if bone is None:
            return
        bpy.context.view_layer.update()
        current = (bone.tail - bone.head).normalized()   # armature space, current pose
        rotation = current.rotation_difference(Vector(target).normalized()).to_matrix().to_4x4()
        pivot = Matrix.Translation(bone.head)
        bone.matrix = pivot @ rotation @ pivot.inverted() @ bone.matrix

    for name, target in T_POSE:
        align(name, target)
    # Fingers straight with their natural spread, thumb forward-down – as in the animation library's rest pose
    # (otherwise every relaxed hand in the clips becomes a fist).
    for side, sign in (("Left", 1), ("Right", -1)):
        for finger in ("Index", "Middle", "Ring", "Pinky"):
            first = rig.pose.bones.get(f"mixamorig:{side}Hand{finger}1")
            if first is None:
                continue
            bpy.context.view_layer.update()
            spread = (first.tail - first.head).normalized().y
            for segment in (1, 2, 3):
                align(f"mixamorig:{side}Hand{finger}{segment}", (sign, spread, 0))
        for segment in (1, 2, 3):
            align(f"mixamorig:{side}HandThumb{segment}", (sign * 0.79, -0.41, -0.45))
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")

    # Bake the pose into the meshes, make it the rest pose, then skin them again.
    for mesh in meshes:
        armature = next((m for m in mesh.modifiers if m.type == "ARMATURE"), None)
        if armature is None:
            continue
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.modifier_apply(modifier=armature.name)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for mesh in meshes:
        modifier = mesh.modifiers.new("Armature", "ARMATURE")
        modifier.object = rig
        with bpy.context.temp_override(object=mesh):
            bpy.ops.object.modifier_move_to_index(modifier="Armature", index=0)


def prepare_mesh(obj, slot):
    """Bakes shape keys, applies the masks (no skin under clothes) and trims parts to the mobile budget."""
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    if obj.data.shape_keys:
        bpy.ops.object.shape_key_remove(all=True, apply_mix=True)
    for modifier in list(obj.modifiers):
        if modifier.type in ("MASK", "SUBSURF"):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    budget = PART_TRIS.get(slot, CLOTH_TRIS if slot.startswith("cloth_") else None)
    tris = triangles(obj)
    if budget and tris > budget:
        decimate(obj, budget / tris)


def render_preview(name):
    """Front view with soft studio light for review (client/Logs/avatars/<id>.png)."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 1024
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.lens = 70
    scene.collection.objects.link(cam)
    cam.location = (0.9, -4.2, 1.2)
    cam.rotation_euler = (math.radians(88), 0, math.radians(12))
    scene.camera = cam
    for loc, energy, rot in [((2.5, -3, 3), 900, (50, 0, 40)), ((-3, -2, 2), 350, (60, 0, -50)), ((0, 3, 3), 500, (-50, 0, 180))]:
        light = bpy.data.objects.new("light", bpy.data.lights.new("light", "AREA"))
        light.data.energy = energy
        light.data.size = 3
        light.location = loc
        light.rotation_euler = tuple(math.radians(v) for v in rot)
        scene.collection.objects.link(light)
    world = bpy.data.worlds.new("world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.37, 0.42, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    scene.world = world
    os.makedirs(PREVIEWS, exist_ok=True)
    scene.render.filepath = os.path.join(PREVIEWS, name + ".png")
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    only = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for figure in FIGURES:
        if not only or figure["id"] in only:
            build_figure(figure)
    print("DONE")
