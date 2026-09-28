"""
Builds the modular avatar wardrobe for the character creator from MakeHuman (CC0) with Blender + MPFB.

    blender -b --python tools/avatars/build_wardrobe.py            # both bodies, everything
    blender -b --python tools/avatars/build_wardrobe.py -- male     # one body

Per body (female, male) it writes to client/Assets/ThirdParty/MakeHuman/Wardrobe/<body>/:
  body.fbx         the rig (mixamo_unity, T-pose, metres) with the body proxy, eyes and eyelashes; the body carries its
                   own vertex index in UV channel 2, so the game can hide exactly what a garment covers
  <part>.fbx       every garment, hairstyle, beard, eyebrow and hat on the same rig (LOD0 + LOD1 + LOD2)
  textures/        colour (+ normal) per part and colour variant, skins and eye colours
  wardrobe.json    the manifest: parts with kind, variants, triangles and the body vertices each part hides
The generated C# list of valid ids goes to src/Reconnect.Contracts/Avatars/WardrobeData.cs (server validates looks).

Why hiding: MakeHuman garments come with a delete group (body vertices under the cloth). MPFB puts it on the full base
mesh only – the game uses the light proxy body, so it is mapped onto the proxy here (a proxy vertex follows three base
vertices; covered when they are). Without it skin shows through clothes when the figure moves.
"""
import bpy
import json
import math
import os
import shutil
import sys

from bl_ext.blender_org.mpfb.services import HumanService, LocationService
from bl_ext.blender_org.mpfb.entities.clothes.mhclo import Mhclo

sys.path.insert(0, os.path.dirname(__file__))
from build_avatars import to_t_pose, decimate, triangles, woman, man, MIXED  # noqa: E402
import morphs  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(REPO, "client", "Assets", "ThirdParty", "MakeHuman", "Resources", "Wardrobe")   # loaded on demand
CSHARP = os.path.join(REPO, "src", "Reconnect.Contracts", "Avatars", "WardrobeData.cs")
DATA = LocationService.get_user_data()

# Triangle budgets of LOD0 per kind (a crowd of 150 must stay cheap; the atlas makes one draw per figure).
# Generous: decimating cloth frays its hems and hair cards fall apart; far away LOD1 (40 %) takes over.
BUDGET = {"top": 6000, "bottom": 5000, "dress": 7000, "outfit": 9000, "shoes": 2000, "hat": 2000, "hair": 8000,
          "beard": 3000, "brows": 800}
LOD1 = 0.2   # the room view: a fifth of the triangles
# Far away (crowds, zoomed out): triangles per part, textures shrunk into one atlas per figure at runtime. Plain collapse
# (hair cards are all open edges – keeping them would keep everything); at a few pixels nobody sees frayed hems.
LOD2 = {"hair": 160, "beard": 50, "brows": 20, "hat": 60, "top": 120, "bottom": 110, "dress": 160, "outfit": 240, "shoes": 50}
LOD2_BODY = 360
# The body: MakeHuman's detailed proxy (~13.8k quads) so faces can be shaped, trimmed for close-ups; the room view and
# crowds get far fewer.
BODY_TRIS = (16000, 2600)
TEXTURE = {"skin": 1024, "outfit": 1024, "dress": 1024, "hair": 512}   # others 512, eyes 128

# "rig" names the armature like the ready-made figure whose humanoid avatar the body shares (Unity binds bones by path).
BODIES = {
    "female": dict(phenotype=woman(MIXED), proxy="female_generic/female_generic.proxy", rig="lena"),
    "male": dict(phenotype=man(MIXED), proxy="male_generic/male_generic.proxy", rig="luca"),
}

F, M, U = ("female",), ("male",), ("female", "male")

# (folder, kind, bodies). Kinds: top, bottom, dress (top + bottom), outfit (top + bottom, suits), shoes, hat, beard.
CLOTHES = [
    # Tops.
    ("joepal_crude_t-shirt_female", "top", F), ("skalldyrssuppe_tube_top_funky_colors", "top", F),
    ("toigo_basic_tucked_t-shirt", "top", U), ("toigo_bodice-style_top", "top", F), ("toigo_camisole_top", "top", F),
    ("toigo_fisherman_sweater", "top", U), ("toigo_keyhole_tank_top", "top", F), ("toigo_turtleneck_halter_top", "top", F),
    ("elvs_crude_t-shirt_male", "top", M), ("namuhekam_male_polo_shirt", "top", M),
    # Bottoms.
    ("cortu_cargo_pants", "bottom", U), ("cortu_jeans_shorts", "bottom", U), ("toigo_harem_pants", "bottom", U),
    ("toigo_wool_pants", "bottom", U), ("frankyaye_mini_skirt_01", "bottom", F), ("frankyaye_mini_skirt_02", "bottom", F),
    ("toigo_long_full_skirt", "bottom", F), ("toigo_skirt_with_lace_ruffle", "bottom", F),
    ("toigo_tiered_mini_skirt", "bottom", F), ("toigo_tiered_skirt", "bottom", F),
    # Dresses.
    ("aethelraed_flapper_dress", "dress", F), ("toigo_bodice_dress_with_lace_ruffle_skirt", "dress", F),
    ("toigo_camisole_dress_with_full_skirt", "dress", F), ("toigo_cut_out_dress", "dress", F),
    ("toigo_dress_with_tiered_skirt", "dress", F), ("toigo_halter_dress_knee_length", "dress", F),
    ("toigo_halter_dress_midi", "dress", F), ("toigo_halter_dress_with_fluted_skirt", "dress", F),
    ("toigo_keyhole_neck_dress", "dress", F), ("toigo_shift_dress", "dress", F),
    ("toigo_strapless_ruffle_top_dress", "dress", F), ("mindfront_kimono", "dress", F),
    # Outfits and suits.
    ("female_casualsuit01", "outfit", F), ("female_casualsuit02", "outfit", F), ("female_elegantsuit01", "outfit", F),
    ("female_sportsuit01", "outfit", F), ("toigo_female_double-breasted_suit", "outfit", F),
    ("toigo_female_suit", "outfit", F), ("toigo_female_suit_2", "outfit", F),
    ("male_casualsuit01", "outfit", M), ("male_casualsuit02", "outfit", M), ("male_casualsuit03", "outfit", M),
    ("male_casualsuit04", "outfit", M), ("male_casualsuit05", "outfit", M), ("male_casualsuit06", "outfit", M),
    ("male_elegantsuit01", "outfit", M), ("male_worksuit01", "outfit", M),
    ("toigo_male_double-breasted_suit", "outfit", M), ("toigo_male_suit_3", "outfit", M),
    ("toigo_male_suit_tie_and_jacket", "outfit", M), ("toigo_suit_with_dinner_jacket", "outfit", M),
    ("toigo_suit_with_jacket_and_bowtie", "outfit", M),
    # Shoes.
    ("shoes01", "shoes", U), ("shoes02", "shoes", U), ("shoes03", "shoes", U), ("shoes04", "shoes", U),
    ("shoes05", "shoes", U), ("shoes06", "shoes", U), ("toigo_mj_cloth_shoes", "shoes", U),
    ("toigo_ballet_flats", "shoes", F), ("toigo_ballet_flats_with_bows", "shoes", F),
    ("toigo_ballet_flats_with_flowers", "shoes", F), ("toigo_flats", "shoes", F), ("toigo_stiletto_booties", "shoes", F),
    ("toigo_ankle_boots_female", "shoes", F), ("cortu_t-bar", "shoes", F), ("scailman_gogo_platform_boots", "shoes", F),
    ("culturalibre_heroine_boots_1", "shoes", F), ("culturalibre_heroine_boots_2", "shoes", F),
    ("grinsegold_female_pirate_boots", "shoes", F), ("cortu_floppy_overknee_shoes", "shoes", F),
    ("toigo_ankle_boots_male", "shoes", M), ("culturalibre_male_boots", "shoes", M),
    ("culturalibre_hero_boots_1", "shoes", M), ("culturalibre_hero_boots_2", "shoes", M),
    ("culturalibre_hero_boots_3", "shoes", M),
    # Hats and beards.
    ("fedora01", "hat", U), ("fedora_cocked", "hat", U),
    ("wdg_scruffy_beard", "beard", M), ("culturalibre_faun_beard", "beard", M), ("grinsegold_beard_sigmund_wip", "beard", M),
    ("rehmanpolanski_beard_viking", "beard", M), ("rehmanpolanski_moustache_viking", "beard", M),
]

HAIR = [
    "afro01", "bob01", "bob02", "braid01", "cortu_short_messy_hair", "cortu_straight_bangs", "culturalibre_hair_01",
    "culturalibre_hair_02", "culturalibre_hair_05", "culturalibre_hair_06", "elvs_double_mh_braid",
    "elvs_french_braid_variation", "elvs_reverse_french_braid_bun", "elvs_unkempt_french_braid", "faydaen_hair_1",
    "littleright_bobcut_hair", "long01", "o4saken_long01", "ponytail01", "rehmanpolanski_hair_bun_brown", "short01",
    "short02", "short03", "short04", "sonntag78_blond_with_headband", "toigo_blunt_bob", "toigo_blunt_bob_with_bangs",
    "toigo_curled_under_bob", "toigo_curled_under_bob_with_bangs", "toigo_inverted_bob", "toigo_inverted_bob_with_bangs",
    "cortu_shaggy_green_hair", "cortu_strawberry_cloud_hair",
]
# Texture brows only (the "mindfront" ones are strand geometry with up to 30k triangles).
BROWS = ["eyebrow001", "eyebrow002", "eyebrow003", "eyebrow005", "eyebrow008", "eyebrow009", "eyebrow010", "eyebrow011",
         "eyebrow012"]

# Skin tones per body (light → dark), no tattoos, no nudity textures.
SKINS = {
    "female": ["toigo_light_skin_female_freckles", "young_caucasian_female", "toigo_light_skin_with_natural_makeup",
               "darthfurby_caucasian_female", "toigo_light_skin_female_ginger", "young_caucasian_female2",
               "skalldyrssuppe_creamy_female", "toigo_light_skin_female_bronze", "young_asian_female",
               "onlytheghosts_young_eurasian_female", "callharvey3d_midtoned_female", "cutoff3d_indian_female_enhanced",
               "middleage_african_female", "young_african_female"],
    "male": ["toigo_light_skin_male_freckles", "young_caucasian_male", "young_caucasian_male2", "toigo_light_skin_male_ginger",
             "mindfront_aksel_skin", "toigo_light_skin_male_bronze", "middleage_caucasian_male", "young_asian_male",
             "middleage_asian_male", "young_african_male", "mindfront_skin_male_african_middleage", "middleage_african_male"],
}
EYES = ["brown", "brownlight", "green", "bluegreen", "blue", "lightblue", "deepblue", "grey", "ice"]


def asset_path(kind, folder, ext=".mhclo"):
    directory = os.path.join(DATA, kind, folder)
    return os.path.join(directory, next(f for f in sorted(os.listdir(directory)) if f.endswith(ext)))


def mhmat_colour(mhmat):
    """The material's plain diffuse colour as hex (for parts without a texture)."""
    with open(mhmat, encoding="utf-8", errors="replace") as f:
        for line in f:
            parts = line.split()
            if len(parts) == 4 and parts[0] == "diffuseColor":
                return "".join(f"{max(0, min(255, round(float(v) * 255))):02X}" for v in parts[1:])
    return "808080"


def mhmat_textures(mhmat):
    """diffuse, normal from an .mhmat (absolute paths or None)."""
    diffuse = normal = None
    folder = os.path.dirname(mhmat)
    with open(mhmat, encoding="utf-8", errors="replace") as f:
        for line in f:
            parts = line.strip().split(None, 1)
            if len(parts) != 2:
                continue
            key, value = parts
            for candidate in (os.path.join(folder, value.strip()), os.path.join(folder, os.path.basename(value.strip()))):
                if os.path.exists(candidate):
                    if key == "diffuseTexture":
                        diffuse = candidate
                    elif key == "normalmapTexture":
                        normal = candidate
                    break
    return diffuse, normal


def variants_of(kind, folder):
    """The asset's own material first, then its colour variants (sub folders with an .mhmat)."""
    directory = os.path.join(DATA, kind, folder)
    found = [("default", asset_path(kind, folder, ".mhmat"))]
    for sub in sorted(os.listdir(directory)):
        sub_dir = os.path.join(directory, sub)
        if os.path.isdir(sub_dir):
            mats = [f for f in sorted(os.listdir(sub_dir)) if f.endswith(".mhmat")]
            if mats:
                found.append((sub, os.path.join(sub_dir, mats[0])))
    return found


def save_texture(source, target, size, keep_alpha):
    """Copies the image (scaled down to size) into a new PNG – saving a loaded image under a new path loses its data."""
    import numpy as np

    image = bpy.data.images.load(source, check_existing=False)
    if image.size[0] > size or image.size[1] > size:
        image.scale(min(size, image.size[0]), min(size, image.size[1]))
    width, height = image.size
    pixels = np.empty(width * height * 4, np.float32)
    image.pixels.foreach_get(pixels)
    if keep_alpha is None:
        # Garments cut hems, lace and edges out with alpha: keep it where the texture really uses it.
        keep_alpha = bool(np.mean(pixels.reshape(-1, 4)[:, 3] < 0.5) > 0.003)
    if not keep_alpha:
        pixels = pixels.reshape(-1, 4)
        pixels[:, 3] = 1.0
        pixels = pixels.ravel()
    if not keep_alpha:
        target = os.path.splitext(target)[0] + ".jpg"   # opaque: JPEG keeps the repository small (Unity recompresses)
    copy = bpy.data.images.new(os.path.basename(target), width, height, alpha=keep_alpha)
    copy.pixels.foreach_set(pixels)
    copy.filepath_raw = target
    copy.file_format = "PNG" if keep_alpha else "JPEG"
    if keep_alpha:
        copy.save()
    else:
        copy.save(quality=90)
    bpy.data.images.remove(image)
    bpy.data.images.remove(copy)
    return os.path.basename(target), keep_alpha


def hidden_proxy_vertices(proxy_mhclo, delverts):
    """Proxy vertices whose base-mesh anchors are all covered by the garment."""
    covered = set(delverts)
    hidden = []
    for index, vdef in proxy_mhclo.verts.items():
        anchors = [v for v, w in zip(vdef["verts"], vdef["weights"]) if w > 0.05]
        if anchors and all(v in covered for v in anchors):
            hidden.append(index)
    return sorted(hidden)


COVERING = {"top", "bottom", "dress", "outfit", "shoes"}
COVER_REACH = 0.1   # a body vertex is under the cloth when the garment is this close along its normal (metres)


def covered_by_geometry(proxy, garment):
    """Body vertices lying under the garment: a ray outwards along the vertex normal hits the cloth within reach."""
    from mathutils.bvhtree import BVHTree

    depsgraph = bpy.context.evaluated_depsgraph_get()
    tree = BVHTree.FromObject(garment, depsgraph)
    to_garment = garment.matrix_world.inverted() @ proxy.matrix_world
    normal_matrix = to_garment.to_3x3().inverted().transposed()
    hidden = []
    for vertex in proxy.data.vertices:
        origin = to_garment @ vertex.co
        direction = (normal_matrix @ vertex.normal).normalized()
        hit = tree.ray_cast(origin, direction, COVER_REACH)
        if hit[0] is not None:
            hidden.append(vertex.index)
    # One ring less: along the garment's edge (collar, cuffs, hem) the skin stays, or one would look through a gap.
    neighbours = {}
    for edge in proxy.data.edges:
        a, b = edge.vertices
        neighbours.setdefault(a, set()).add(b)
        neighbours.setdefault(b, set()).add(a)
    covered = set(hidden)
    return [v for v in hidden if neighbours.get(v, set()) <= covered], hidden


def export(rig, meshes, path):
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    for mesh in meshes:
        mesh.hide_set(False)
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE", path_mode="STRIP")   # keeps the shape keys


def decimate_keeping_edges(obj, ratio):
    """Collapse-decimates like build_avatars.decimate, but keeps the open edges (hems, cuffs, collars) – they would fray."""
    import bmesh

    if ratio >= 1.0:
        return
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    edge = {v.index for e in bm.edges if e.is_boundary for v in e.verts}
    bm.free()
    group = obj.vertex_groups.new(name="Keep")
    group.add([v.index for v in obj.data.vertices if v.index not in edge], 1.0, "REPLACE")
    group.add(sorted(edge), 0.0, "REPLACE")   # low weight = expensive to collapse
    modifier = obj.modifiers.new("Decimate", "DECIMATE")
    modifier.ratio = ratio
    modifier.vertex_group = group.name
    modifier.vertex_group_factor = 20.0
    with bpy.context.temp_override(object=obj, active_object=obj):
        bpy.ops.object.modifier_move_to_index(modifier="Decimate", index=0)
        bpy.ops.object.modifier_apply(modifier="Decimate")
    obj.vertex_groups.remove(obj.vertex_groups["Keep"])   # by name: the handle is stale after applying


def with_lod(obj, budget, name, kind):
    """Trims the part to its budget (LOD0) and adds a reduced copy (LOD1)."""
    if obj.data.shape_keys:
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.shape_key_remove(all=True, apply_mix=True)
    tris = triangles(obj)
    if budget and tris > budget:
        decimate_keeping_edges(obj, budget / tris)
    obj.name = obj.data.name = name + "_LOD0"
    lod = obj.copy()
    lod.data = obj.data.copy()
    lod.name = lod.data.name = name + "_LOD1"
    bpy.context.scene.collection.objects.link(lod)
    decimate_keeping_edges(lod, LOD1)
    far = obj.copy()
    far.data = obj.data.copy()
    far.name = far.data.name = name + "_LOD2"
    bpy.context.scene.collection.objects.link(far)
    decimate(far, LOD2.get(kind, 120) / max(1, triangles(far)))
    return [obj, lod, far]


def build_body(body):
    spec = BODIES[body]
    out = os.path.join(OUT, body)
    tex = os.path.join(out, "textures")
    if os.path.isdir(out):
        shutil.rmtree(out)
    os.makedirs(tex)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    info = HumanService._create_default_human_info_dict()
    info.update(dict(name=spec["rig"], phenotype=spec["phenotype"], rig="mixamo_unity", proxy=spec["proxy"],
                     eyes="low-poly/low-poly.mhclo", eyelashes="eyelashes01/eyelashes01.mhclo",
                     skin_mhmat=os.path.relpath(asset_path("skins", SKINS[body][0], ".mhmat"), os.path.join(DATA, "skins")).replace("\\", "/"),
                     skin_material_type="MAKESKIN", eyes_material_type="MAKESKIN"))
    settings = HumanService.get_default_deserialization_settings()
    settings["subdiv_levels"] = 0
    basemesh = HumanService.deserialize_from_dict(info, settings)
    rig = basemesh.parent
    proxy_mhclo = Mhclo()
    proxy_mhclo.load(os.path.join(DATA, "proxymeshes", spec["proxy"]))

    body_meshes = [o for o in bpy.data.objects if o.type == "MESH" and o != basemesh]
    proxy = next(o for o in body_meshes if spec["proxy"].split("/")[0] in o.name)

    parts = []   # (id, kind, mesh object, variants[(name, mhmat)], hides)
    fits = {}    # mesh name -> its .mhclo (how it hangs on the base mesh: the morphs move it with the body)
    for folder, kind, bodies in CLOTHES:
        if body not in bodies:
            continue
        path = asset_path("clothes", folder)
        obj = HumanService.add_mhclo_asset(path, basemesh, asset_type="Clothes", subdiv_levels=0, material_type="MAKESKIN")
        garment = Mhclo()
        garment.load(path)
        fits[obj.name] = garment
        hides = hidden_proxy_vertices(proxy_mhclo, garment.delverts) if garment.delete else []
        parts.append((folder, kind, obj, variants_of("clothes", folder), hides))
    for folder, kind, asset_type in [(h, "hair", "Hair") for h in HAIR] + [(b, "brows", "Eyebrows") for b in BROWS]:
        library = "hair" if kind == "hair" else "eyebrows"
        path = asset_path(library, folder)
        obj = HumanService.add_mhclo_asset(path, basemesh, asset_type=asset_type, subdiv_levels=0, material_type="MAKESKIN")
        fits[obj.name] = Mhclo()
        fits[obj.name].load(path)
        parts.append((folder, kind, obj, variants_of(library, folder), []))

    # Every loaded garment put a mask on the body (MPFB): the whole body is exported, the game hides what the worn
    # parts cover.
    for obj in body_meshes:
        for modifier in [m for m in obj.modifiers if m.type == "MASK"]:
            obj.modifiers.remove(modifier)

    # The proxy remembers its own vertex numbers (UV channel 2) – the game hides by them.
    uv = proxy.data.uv_layers.new(name="VertexIndex")
    for loop in proxy.data.loops:
        uv.data[loop.index].uv = (float(loop.vertex_index), 0.0)
    all_meshes = body_meshes + [p[2] for p in parts]
    for obj in all_meshes:
        if obj.data.shape_keys:
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.shape_key_remove(all=True, apply_mix=True)

    # Morphs (weight, face ...): how each base-mesh vertex moves, then every fitted mesh along with it.
    eyes = next(o for o in body_meshes if "low-poly" in o.name)
    lashes = next(o for o in body_meshes if "eyelashes" in o.name)
    fits[proxy.name] = proxy_mhclo
    for obj, library, folder in ((eyes, "eyes", "low-poly"), (lashes, "eyelashes", "eyelashes01")):
        fits[obj.name] = Mhclo()
        fits[obj.name].load(asset_path(library, folder))
    base = morphs.base_deltas(basemesh, body)
    rest = {name: morphs.asset_deltas(bpy.data.objects[name], mhclo, basemesh, base) for name, mhclo in fits.items()}
    posed = {}
    bpy.data.objects.remove(basemesh, do_unlink=True)
    to_t_pose(rig, all_meshes, before_bake=lambda: posed.update(morphs.posed(all_meshes, rest)))
    shapes = {obj.name: (morphs.snapshot(obj), posed.get(obj.name, {})) for obj in all_meshes}

    manifest = {"body": body, "proxyVertices": len(proxy.data.vertices), "skins": [], "eyes": [], "parts": [],
                "morphs": sorted(morphs.keys_for(body))}
    assert len(proxy.data.vertices) == len(proxy_mhclo.verts), "the body is complete (no masks applied)"
    # Coverage is measured on the whole body (the exported one is trimmed; its vertex numbers point back to this).
    coverage = proxy.copy()
    coverage.data = proxy.data.copy()
    bpy.context.scene.collection.objects.link(coverage)
    coverage.hide_set(True)

    # Body: proxy + eyes + eyelashes, three LODs of the proxy.
    proxy_source = shapes[proxy.name]
    eyes_source, lashes_source = shapes[eyes.name], shapes[lashes.name]
    proxy.name = proxy.data.name = "Body_LOD0"
    eyes.name = eyes.data.name = "Eyes"
    lashes.name = lashes.data.name = "Lashes"
    body_lod = proxy.copy()
    body_lod.data = proxy.data.copy()
    body_lod.name = body_lod.data.name = "Body_LOD1"
    bpy.context.scene.collection.objects.link(body_lod)
    decimate(body_lod, BODY_TRIS[1] / max(1, triangles(body_lod)))
    body_far = proxy.copy()
    body_far.data = proxy.data.copy()
    body_far.name = body_far.data.name = "Body_LOD2"
    bpy.context.scene.collection.objects.link(body_far)
    decimate(body_far, LOD2_BODY / max(1, triangles(body_far)))
    decimate(proxy, BODY_TRIS[0] / max(1, triangles(proxy)))
    for mesh in (proxy, body_lod, body_far):
        count = morphs.transfer(*proxy_source, mesh, vertex_index_layer="VertexIndex")
        print(f"BODY {body}/{mesh.name}: {triangles(mesh)} triangles, {count} shape keys")
    morphs.transfer(*eyes_source, eyes)
    morphs.transfer(*lashes_source, lashes)
    for p in parts:
        p[2].hide_set(True)
    export(rig, [proxy, body_lod, body_far, eyes, lashes], os.path.join(out, "body.fbx"))
    for skin in SKINS[body]:
        diffuse, normal = mhmat_textures(asset_path("skins", skin, ".mhmat"))
        file, _ = save_texture(diffuse, os.path.join(tex, f"skin_{skin}.png"), TEXTURE["skin"], False)
        manifest["skins"].append({"id": skin, "texture": file})
    for eye in EYES:
        diffuse, _ = mhmat_textures(os.path.join(DATA, "eyes", "materials", eye + ".mhmat"))
        file, _ = save_texture(diffuse, os.path.join(tex, f"eyes_{eye}.png"), 128, False)
        manifest["eyes"].append({"id": eye, "texture": file})
    lash_diffuse, _ = mhmat_textures(asset_path("eyelashes", "eyelashes01", ".mhmat"))
    save_texture(lash_diffuse, os.path.join(tex, "lashes.png"), 256, True)

    for folder, kind, obj, variants, hides in parts:
        under = []
        if kind in COVERING:
            # Delete group (where the author set one) plus everything the cloth lies on; "under" also keeps the ring
            # along the garment's edge, which the game pulls in a little so it never shows through when moving.
            inside, lying_under = covered_by_geometry(coverage, obj)
            under = sorted(set(hides) | set(lying_under))
            hides = sorted(set(hides) | set(inside))
        source = shapes[obj.name]
        meshes = with_lod(obj, BUDGET[kind], folder, kind)
        for mesh in meshes:
            morphs.transfer(*source, mesh)
        export(rig, meshes, os.path.join(out, folder + ".fbx"))
        size = TEXTURE.get(kind, 512)
        cutout = kind in ("hair", "brows", "beard")
        exported = []
        for name, mhmat in variants:
            diffuse, normal = mhmat_textures(mhmat)
            if not diffuse:
                exported.append({"id": name, "colour": mhmat_colour(mhmat)})   # plain colour (e.g. solid brows)
                continue
            file, alpha = save_texture(diffuse, os.path.join(tex, f"{folder}__{name}.png"), size, True if cutout else None)
            entry = {"id": name, "texture": file, "alpha": alpha}
            if normal and not cutout:
                entry["normal"], _ = save_texture(normal, os.path.join(tex, f"{folder}__{name}_n.png"), size // 2, False)
            exported.append(entry)
        manifest["parts"].append({"id": folder, "kind": kind, "file": folder + ".fbx", "variants": exported,
                                  "triangles": triangles(meshes[0]), "hides": hides, "under": under})
        for mesh in meshes:
            bpy.data.objects.remove(mesh, do_unlink=True)
        print(f"PART {body}/{folder}: {kind}, {triangles(meshes[0]) if False else ''}{len(exported)} variants, hides {len(hides)}")

    with open(os.path.join(out, "wardrobe.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=1)
    return manifest


def write_csharp(manifests):
    """Valid ids per body, for the server to check looks (generated – don't edit)."""
    lines = [
        "// Generated by tools/avatars/build_wardrobe.py – do not edit.",
        "using System.Collections.Generic;",
        "",
        "namespace Reconnect.Contracts.Avatars",
        "{",
        "    public static partial class Wardrobe",
        "    {",
        "        /// <summary>Per body: part id → (kind, colour variants).</summary>",
        "        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, (string Kind, string[] Variants)>> Parts =",
        "            new Dictionary<string, IReadOnlyDictionary<string, (string Kind, string[] Variants)>>",
        "            {",
    ]
    for manifest in manifests:
        lines.append(f'                ["{manifest["body"]}"] = new Dictionary<string, (string, string[])>')
        lines.append("                {")
        for part in manifest["parts"]:
            variants = ", ".join(f'"{v["id"]}"' for v in part["variants"])
            lines.append(f'                    ["{part["id"]}"] = ("{part["kind"]}", new string[] {{ {variants} }}),')
        lines.append("                },")
    lines += ["            };", ""]
    lines.append("        /// <summary>Per body: skin ids (light → dark).</summary>")
    lines.append("        public static readonly IReadOnlyDictionary<string, string[]> Skins = new Dictionary<string, string[]>")
    lines.append("        {")
    for manifest in manifests:
        skins = ", ".join(f'"{s["id"]}"' for s in manifest["skins"])
        lines.append(f'            ["{manifest["body"]}"] = new[] {{ {skins} }},')
    lines += ["        };", ""]
    lines.append("        /// <summary>Body and face shapes (id, group, both directions or only up, bodies) – see tools/avatars/morphs.py.</summary>")
    lines.append("        public static readonly (string Id, string Group, bool TwoSided, string[] Bodies)[] Morphs =")
    lines.append("        {")
    for morph_id, group, bodies, minus, _plus in morphs.MORPHS:
        names = ", ".join(f'"{b}"' for b in bodies)
        lines.append(f'            ("{morph_id}", "{group}", {"true" if minus is not None else "false"}, new[] {{ {names} }}),')
    lines += ["        };", ""]
    eyes = ", ".join(f'"{e}"' for e in EYES)
    lines.append(f"        public static readonly string[] Eyes = {{ {eyes} }};")
    lines += ["    }", "}", ""]
    os.makedirs(os.path.dirname(CSHARP), exist_ok=True)
    with open(CSHARP, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))


if __name__ == "__main__":
    only = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    manifests = []
    for name in BODIES:
        path = os.path.join(OUT, name, "wardrobe.json")
        if not only or name in only:
            manifests.append(build_body(name))
        elif os.path.exists(path):
            with open(path, encoding="utf-8") as f:
                manifests.append(json.load(f))
    write_csharp(manifests)
    print("DONE")
