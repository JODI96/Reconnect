"""
Converts the needed clips of Quaternius' Universal Animation Library (CC0) to one FBX for Unity (Humanoid).

    blender -b --python tools/avatars/build_animations.py -- <path to AnimationLibrary_Godot_Standard.gltf>

Source: https://github.com/J-Ponzo/gltf-universal-animation-library (glTF edition of quaternius.com's library).
Writes client/Assets/ThirdParty/Quaternius/AvatarAnimations.fbx. Wave, nod and head shake are not in the
library; ProjectSetup builds them as humanoid muscle clips.
"""
import os
import sys

import bpy

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(REPO, "client", "Assets", "ThirdParty", "Quaternius")

CLIPS = ["Idle_Loop", "Idle_Talking_Loop", "Walk_Loop", "Jump_Start", "Jump_Loop", "Jump_Land",
         "Sitting_Enter", "Sitting_Idle_Loop", "Sitting_Exit", "Dance_Loop"]

source = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)

rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
for action in list(bpy.data.actions):
    if action.name not in CLIPS:
        bpy.data.actions.remove(action)
missing = [c for c in CLIPS if c not in bpy.data.actions]
if missing:
    raise SystemExit(f"Clips missing in the library: {missing}")
for action in bpy.data.actions:
    action.use_fake_user = True
if rig.animation_data:
    rig.animation_data.action = None
    for track in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(track)

# The mannequin mesh is not needed; Unity builds the humanoid avatar from the skeleton.
for obj in list(bpy.data.objects):
    if obj.type == "MESH":
        bpy.data.objects.remove(obj, do_unlink=True)

os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "AvatarAnimations.fbx"), use_selection=True,
                         object_types={"ARMATURE"}, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
                         bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5)
print("ANIMATIONS", sorted(a.name for a in bpy.data.actions), "bones", len(rig.data.bones))
