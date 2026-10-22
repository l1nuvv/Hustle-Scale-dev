"""Adapt the existing Blender flashlight; export only the M3 variant, preserving M2."""
import ast
import bpy
import bmesh
import math
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
bpy.ops.wm.open_mainfile(filepath=str(HERE / "M2_SolderingProps.blend"))
bpy.context.preferences.filepaths.save_version = 0
atlas_material = bpy.data.materials["M2_SurfaceAtlas"]
# Reuse the existing coordinate conversion, bevel and atlas UV authoring procedure.
source = ast.parse((HERE / "build_soldering_props.py").read_text(encoding="utf-8"))
helpers = [node for node in source.body if isinstance(node, ast.FunctionDef)
           and node.name in ("v", "finish", "box", "lathe")]
exec(compile(ast.Module(body=helpers, type_ignores=[]), "M2 authoring helpers", "exec"))

original = bpy.data.objects["M2_OpenFlashlight"]
root = bpy.data.objects.new("M3_Flashlight", None)
bpy.context.collection.objects.link(root)
flashlight_root = root
for obj in list(original.children):
    copy = obj.copy()
    copy.data = obj.data.copy()
    copy.name = "M3Body_" + obj.name
    bpy.context.collection.objects.link(copy)
    copy.parent = root
    copy.matrix_parent_inverse.identity()
    copy.matrix_basis = obj.matrix_basis.copy()

def group(name, parent, point=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = v(point)
    return obj

cover = group("M3_Cover", root)
root = cover
box("M3 shaped removable blue lid", (-.025, .036, 0), (.565, .038, .28), 1, .009)
box("M3 front lid skirt", (-.025, .004, -.140), (.565, .05, .016), 1, .004)
box("M3 rear lid skirt", (-.025, .004, .140), (.565, .05, .016), 1, .004)
box("M3 end lid skirt", (-.300, .004, 0), (.016, .05, .28), 1, .004)
box("M3 lid inset panel", (-.025, .057, .025), (.31, .006, .12), 0, .004)
for index, x in enumerate((-.24, .17)):
    root = group("M3_Fastener" + str(index), cover, (x, .067, -.088))
    lathe("M3 captive quarter-turn head", (0, 0, 0), (0, 1, 0),
          [(-.010, .012), (0, .020), (.008, .020), (.012, .016)], 2, 20)
    box("M3 thumb wing", (0, .015, 0), (.058, .014, .015), 2, .003)
root = group("M3_PowerSwitch", cover, (-.10, .070, -.080))
box("M3 power rocker", (0, 0, 0), (.052, .019, .035), 0, .004)
box("M3 power mark", (-.01, .010, 0), (.004, .001, .016), 5, .0003)

objects = [flashlight_root] + list(flashlight_root.children_recursive)
# Keep the adapted item in its own editable file, without duplicated M2 preview props.
for obj in list(bpy.data.objects):
    if obj not in objects:
        bpy.data.objects.remove(obj, do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE / "M3_Flashlight.blend"))
meshes = [obj for obj in objects if obj.type == "MESH"]
# Match M2's flat mesh export: nested FBX empties plus bake_space_transform
# introduce centimetre scaling. Unity creates normalized movable groups around
# these unchanged, correctly converted mesh transforms.
bpy.context.view_layer.update()
for obj in meshes:
    world = obj.matrix_world.copy()
    obj.parent = flashlight_root
    obj.matrix_world = world
bpy.ops.object.select_all(action="DESELECT")
flashlight_root.select_set(True)
for obj in meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = flashlight_root
bpy.ops.export_scene.fbx(filepath=str(REPO / "Assets/Art/SolderingTrial/Models/M3_Flashlight.fbx"),
    use_selection=True, object_types={"MESH", "EMPTY"}, add_leaf_bones=False,
    axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=True,
    mesh_smooth_type="FACE", path_mode="RELATIVE", use_mesh_modifiers=True)
print("M3_EXPORTED", len(meshes), "meshes;", sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in meshes), "triangles")
