"""
Body and face shapes for the character creator (weight, muscles, nose, eyes, jaw …), from MakeHuman's targets (CC0).

Each morph becomes one or two shape keys ("<id>+", "<id>-") on every exported mesh it moves: the body, eyes, lashes and every
garment, hairstyle, brow, beard and hat. Clothes follow the body through their .mhclo fitting (each clothes vertex hangs on
three base-mesh vertices), so a heavier body gets wider clothes. The game bakes the chosen values into the figure when it
assembles it (no blend shapes at runtime).

Pipeline: base_deltas (rest pose, on the MakeHuman base mesh) → asset_deltas (per fitted asset) → posed (through the T-pose
bake, arms turned) → transfer (onto the decimated LODs, by nearest vertex).
"""
import os

import bpy
import numpy as np
from mathutils.kdtree import KDTree

from bl_ext.blender_org.mpfb.services import LocationService, TargetService
from bl_ext.blender_org.mpfb.entities.objectproperties import HumanObjectProperties

F, M, U = ("female",), ("male",), ("female", "male")


def T(*names):
    return [("target", n) for n in names]


def LR(region, name):
    """Both sides of a paired target (eyes, ears, cheeks): one slider moves them together."""
    return T(f"{region}/l-{name}", f"{region}/r-{name}")


def MACRO(name, value):
    return [("macro", name, value)]


# (id, group, bodies, minus, plus). minus None = one-sided (0 … 1). Labels live in Contracts (WardrobeMorphs.cs).
MORPHS = [
    # Body.
    ("weight", "body", U, MACRO("weight", 0.0), MACRO("weight", 1.0)),
    ("muscle", "body", U, MACRO("muscle", 0.0), MACRO("muscle", 1.0)),
    ("bust", "body", F, MACRO("cupsize", 0.0), MACRO("cupsize", 1.0)),
    ("chest", "body", M, T("torso/torso-muscle-pectoral-decr"), T("torso/torso-muscle-pectoral-incr")),
    ("shoulders", "body", U, T("torso/measure-shoulder-dist-decr"), T("torso/measure-shoulder-dist-incr")),
    ("waist", "body", U, T("torso/measure-waist-circ-decr"), T("torso/measure-waist-circ-incr")),
    ("hips", "body", U, T("torso/measure-hips-circ-decr"), T("torso/measure-hips-circ-incr")),
    ("buttocks", "body", U, T("buttocks/buttocks-volume-decr"), T("buttocks/buttocks-volume-incr")),
    ("neck", "body", U, T("neck/measure-neck-circ-decr"), T("neck/measure-neck-circ-incr")),
    # Face shape (one-sided: how much of each shape) and proportions.
    ("head-oval", "shape", U, None, T("head/head-oval")),
    ("head-round", "shape", U, None, T("head/head-round")),
    ("head-square", "shape", U, None, T("head/head-square")),
    ("head-rectangular", "shape", U, None, T("head/head-rectangular")),
    ("head-triangular", "shape", U, None, T("head/head-triangular")),
    ("head-heart", "shape", U, None, T("head/head-invertedtriangular")),
    ("head-diamond", "shape", U, None, T("head/head-diamond")),
    ("face-width", "face", U, T("head/head-scale-horiz-decr"), T("head/head-scale-horiz-incr")),
    ("face-length", "face", U, T("head/head-scale-vert-decr"), T("head/head-scale-vert-incr")),
    ("face-fullness", "face", U, T("head/head-fat-decr"), T("head/head-fat-incr")),
    ("jaw", "face", U, T("chin/chin-width-decr"), T("chin/chin-width-incr")),
    ("chin", "face", U, T("chin/chin-prominent-decr"), T("chin/chin-prominent-incr")),
    ("chin-height", "face", U, T("chin/chin-height-decr"), T("chin/chin-height-incr")),
    ("cheekbones", "face", U, LR("cheek", "cheek-bones-decr"), LR("cheek", "cheek-bones-incr")),
    ("cheeks", "face", U, LR("cheek", "cheek-volume-decr"), LR("cheek", "cheek-volume-incr")),
    ("forehead", "face", U, T("forehead/forehead-scale-vert-decr"), T("forehead/forehead-scale-vert-incr")),
    # Eyes.
    ("eye-size", "eyes", U, LR("eyes", "eye-scale-decr"), LR("eyes", "eye-scale-incr")),
    ("eye-distance", "eyes", U, LR("eyes", "eye-trans-in"), LR("eyes", "eye-trans-out")),
    ("eye-height", "eyes", U, LR("eyes", "eye-trans-down"), LR("eyes", "eye-trans-up")),
    ("eye-tilt", "eyes", U, LR("eyes", "eye-eyefold-angle-down"), LR("eyes", "eye-eyefold-angle-up")),
    ("eye-open", "eyes", U, LR("eyes", "eye-height2-decr"), LR("eyes", "eye-height2-incr")),
    ("eye-lid", "eyes", U, LR("eyes", "eye-epicanthus-in"), LR("eyes", "eye-epicanthus-out")),
    ("eye-depth", "eyes", U, LR("eyes", "eye-push1-in"), LR("eyes", "eye-push1-out")),
    ("eye-bags", "eyes", U, LR("eyes", "eye-bag-decr"), LR("eyes", "eye-bag-incr")),
    # Nose.
    ("nose-width", "nose", U, T("nose/nose-scale-horiz-decr"), T("nose/nose-scale-horiz-incr")),
    ("nose-length", "nose", U, T("nose/nose-scale-vert-decr"), T("nose/nose-scale-vert-incr")),
    ("nose-size", "nose", U, T("nose/nose-volume-decr"), T("nose/nose-volume-incr")),
    ("nose-bridge", "nose", U, T("nose/nose-hump-decr"), T("nose/nose-hump-incr")),
    ("nose-tip", "nose", U, T("nose/nose-point-down"), T("nose/nose-point-up")),
    ("nostrils", "nose", U, T("nose/nose-nostrils-width-decr"), T("nose/nose-nostrils-width-incr")),
    ("nose-depth", "nose", U, T("nose/nose-scale-depth-decr"), T("nose/nose-scale-depth-incr")),
    # Mouth.
    ("mouth-width", "mouth", U, T("mouth/mouth-scale-horiz-decr"), T("mouth/mouth-scale-horiz-incr")),
    ("upper-lip", "mouth", U, T("mouth/mouth-upperlip-volume-decr"), T("mouth/mouth-upperlip-volume-incr")),
    ("lower-lip", "mouth", U, T("mouth/mouth-lowerlip-volume-decr"), T("mouth/mouth-lowerlip-volume-incr")),
    ("cupids-bow", "mouth", U, T("mouth/mouth-cupidsbow-decr"), T("mouth/mouth-cupidsbow-incr")),
    ("mouth-corners", "mouth", U, T("mouth/mouth-angles-down"), T("mouth/mouth-angles-up")),
    ("mouth-height", "mouth", U, T("mouth/mouth-trans-down"), T("mouth/mouth-trans-up")),
    # Ears and brows.
    ("ear-size", "ears", U, LR("ears", "ear-scale-decr"), LR("ears", "ear-scale-incr")),
    ("ear-angle", "ears", U, LR("ears", "ear-wing-decr"), LR("ears", "ear-wing-incr")),
    ("ear-lobe", "ears", U, LR("ears", "ear-lobe-decr"), LR("ears", "ear-lobe-incr")),
    ("brow-height", "brows", U, T("eyebrows/eyebrows-trans-down"), T("eyebrows/eyebrows-trans-up")),
    ("brow-angle", "brows", U, T("eyebrows/eyebrows-angle-down"), T("eyebrows/eyebrows-angle-up")),
]

EPSILON = 1e-5   # metres: smaller moves are no change


def keys_for(body):
    """Shape key name → sources, for the morphs of this body."""
    keys = {}
    for morph_id, _group, bodies, minus, plus in MORPHS:
        if body not in bodies:
            continue
        keys[morph_id + "+"] = plus
        if minus is not None:
            keys[morph_id + "-"] = minus
    return keys


def _coords(obj):
    array = np.empty(len(obj.data.vertices) * 3, dtype=np.float64)
    obj.data.vertices.foreach_get("co", array)
    return array.reshape(-1, 3)


def _mix(basemesh):
    """The base mesh as its shape keys mix it (rest pose, object space)."""
    keys = basemesh.data.shape_keys
    if not keys:
        return _coords(basemesh)
    def co(block):
        array = np.empty(len(block.data) * 3, dtype=np.float64)
        block.data.foreach_get("co", array)
        return array.reshape(-1, 3)
    basis = co(keys.reference_key)
    out = basis.copy()
    for block in keys.key_blocks:
        if block == keys.reference_key or block.mute or abs(block.value) < 1e-6:
            continue
        out += block.value * (co(block) - co(block.relative_key))
    return out


def base_deltas(basemesh, body):
    """Shape key name → movement of every base-mesh vertex (object space, rest pose)."""
    targets = LocationService.get_mpfb_data("targets")
    base = _mix(basemesh)
    deltas = {}
    for key, sources in keys_for(body).items():
        total = np.zeros_like(base)
        for source in sources:
            if source[0] == "macro":
                _, name, value = source
                before = HumanObjectProperties.get_value(name, entity_reference=basemesh)
                HumanObjectProperties.set_value(name, value, entity_reference=basemesh)
                TargetService.reapply_macro_details(basemesh)
                total += _mix(basemesh) - base
                HumanObjectProperties.set_value(name, before, entity_reference=basemesh)
                TargetService.reapply_macro_details(basemesh)
            else:
                path = os.path.join(targets, source[1] + ".target.gz")
                block = TargetService.load_target(basemesh, path, weight=1.0, name="morph_tmp")
                total += _mix(basemesh) - base
                basemesh.shape_key_remove(block)
        deltas[key] = total
        print(f"MORPH {body}/{key}: max {np.abs(total).max() * 100:.2f} cm")
    return deltas


def asset_deltas(obj, mhclo, basemesh, deltas):
    """Movement of a fitted asset's vertices: each hangs on three base-mesh vertices with weights (.mhclo)."""
    count = len(obj.data.vertices)
    if len(mhclo.verts) != count:
        print(f"MORPH skip {obj.name}: {count} vertices, mhclo {len(mhclo.verts)}")
        return {}
    refs = np.array([mhclo.verts[i]["verts"] for i in range(count)], dtype=np.int64)
    weights = np.array([mhclo.verts[i]["weights"] for i in range(count)], dtype=np.float64)
    to_local = np.array(obj.matrix_world.inverted().to_3x3() @ basemesh.matrix_world.to_3x3())
    result = {}
    for key, delta in deltas.items():
        moved = (delta[refs] * weights[:, :, None]).sum(axis=1) @ to_local.T
        if np.abs(moved).max() > EPSILON:
            result[key] = moved
    return result


def posed(meshes, rest):
    """
    The rest-pose movements through the T-pose (called while the rig is posed, before the pose is baked): each is
    added to the mesh, the armature deforms it, the difference to the deformed mesh without it is the posed movement.
    """
    depsgraph = bpy.context.evaluated_depsgraph_get()
    result = {}
    for obj in meshes:
        deltas = rest.get(obj.name)
        if not deltas:
            continue
        original = _coords(obj)

        def evaluate():
            obj.data.update()
            depsgraph.update()
            evaluated = obj.evaluated_get(depsgraph)
            array = np.empty(len(evaluated.data.vertices) * 3, dtype=np.float64)
            evaluated.data.vertices.foreach_get("co", array)
            return array.reshape(-1, 3)

        base = evaluate()
        if len(base) != len(original):
            print(f"MORPH skip {obj.name}: modifiers change the vertex count")
            continue
        result[obj.name] = {}
        for key, delta in deltas.items():
            obj.data.vertices.foreach_set("co", (original + delta).ravel())
            result[obj.name][key] = evaluate() - base
        obj.data.vertices.foreach_set("co", original.ravel())
        obj.data.update()
    return result


def snapshot(obj):
    return _coords(obj)


def _nearest(source_positions, obj):
    tree = KDTree(len(source_positions))
    for index, co in enumerate(source_positions):
        tree.insert(co, index)
    tree.balance()
    return np.array([tree.find(v.co)[1] for v in obj.data.vertices], dtype=np.int64)


def transfer(source_positions, deltas, obj, vertex_index_layer=None):
    """
    Puts the movements on a (decimated) copy as shape keys, each vertex taking its nearest original's. With
    vertex_index_layer, the copy's UV channel of original vertex numbers (the game hides covered skin by them) is set to
    the nearest original too – decimation had blended the numbers.
    """
    nearest = _nearest(source_positions, obj)
    if vertex_index_layer and vertex_index_layer in obj.data.uv_layers:
        layer = obj.data.uv_layers[vertex_index_layer]
        for loop in obj.data.loops:
            layer.data[loop.index].uv = (float(nearest[loop.vertex_index]), 0.0)
    if not deltas:
        return 0
    base = _coords(obj)
    obj.shape_key_add(name="Basis", from_mix=False)
    for key, delta in deltas.items():
        moved = delta[nearest]
        if np.abs(moved).max() <= EPSILON:
            continue
        block = obj.shape_key_add(name=key, from_mix=False)
        block.data.foreach_set("co", (base + moved).ravel())
    return len(obj.data.shape_keys.key_blocks) - 1
