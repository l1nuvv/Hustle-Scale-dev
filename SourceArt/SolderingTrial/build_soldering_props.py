"""Blender-only source for the three M2 props. No downloads or third-party Python packages.

Run from the repo: blender --background --python SourceArt/SolderingTrial/build_soldering_props.py
Unity coordinates below are metres; working ends stay at the origin for the M2 contact check.
"""
import bpy
import bmesh
import math
import json
from pathlib import Path
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
OUT = REPO / "Assets/Art/SolderingTrial"
for folder in (OUT / "Models", OUT / "Textures", REPO / "Logs"):
    folder.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.scene.unit_settings.system = "METRIC"


def v(point):
    x, y, z = point
    # FBX's handedness conversion flips X on Unity import.
    return Vector((-x, -z, y))


# A compact painted atlas with large, restrained wear marks, rather than photographic noise.
palette = [
    ((.12, .15, .17), .0, .14),  # rubber
    ((.16, .32, .43), .0, .27),  # painted blue housing
    ((.64, .69, .72), .85, .48),  # steel
    ((.72, .38, .16), .8, .40),  # copper
    ((.10, .24, .17), .0, .23),  # board
    ((.80, .84, .79), .0, .32),  # opaque lens
    ((.54, .12, .09), .0, .24),  # red insulation
    ((.76, .70, .54), .0, .16),  # spool paper label
]
size = 512
base = np.ones((size, size, 4), dtype=np.float32)
mask = np.ones_like(base)
for tile, (color, metallic, smoothness) in enumerate(palette):
    col, row = tile % 4, tile // 4
    x0, y0 = col * 128, row * 256
    yy, xx = np.mgrid[0:256, 0:128]
    variation = .018 * np.sin(xx * .09 + yy * .025) + .012 * np.cos(yy * .055)
    if tile == 2:
        variation += .025 * np.sin(yy * .9)  # broad brushed steel
    if tile == 4:
        variation *= .35
    patch = np.clip(np.array(color)[None, None, :] + variation[:, :, None], 0, 1)
    # A few broad, directional scuffs on contact surfaces, kept clear of silhouettes.
    if tile in (0, 1, 7):
        scuff = ((xx > 20) & (xx < 27) & (yy > 55) & (yy < 160)) | \
                ((yy > 180) & (yy < 184) & (xx > 60) & (xx < 104))
        patch[scuff] *= .76
    if tile == 7:
        # Printed label border and three clear identification strokes, no tiny technical prose.
        ink = ((xx > 13) & (xx < 115) & (((yy > 36) & (yy < 42)) | ((yy > 214) & (yy < 220))))
        ink |= (xx > 24) & (xx < 33) & (yy > 78) & (yy < 166)
        ink |= (xx > 40) & (xx < 87) & (((yy > 78) & (yy < 90)) | ((yy > 119) & (yy < 131)) | ((yy > 154) & (yy < 166)))
        patch[ink] = (.11, .18, .22)
    base[y0:y0 + 256, x0:x0 + 128, :3] = patch
    mask[y0:y0 + 256, x0:x0 + 128, :3] = metallic
    mask[y0:y0 + 256, x0:x0 + 128, 3] = smoothness


def save_image(name, pixels, color_space):
    image = bpy.data.images.new(name, width=pixels.shape[1], height=pixels.shape[0], alpha=True)
    image.colorspace_settings.name = color_space
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = str(OUT / "Textures" / (name + ".png"))
    image.file_format = "PNG"
    image.save()
    return image


base_image = save_image("M2_BaseColor", base, "sRGB")
mask_image = save_image("M2_MetallicSmoothness", mask, "Non-Color")
yy, xx = np.mgrid[-1:1:64j, -1:1:64j]
soft = np.ones((64, 64, 4), dtype=np.float32)
soft[:, :, 3] = np.maximum(0, 1 - np.sqrt(xx * xx + yy * yy)) ** 2
save_image("M2_SoftSmoke", soft, "sRGB")

atlas_material = bpy.data.materials.new("M2_SurfaceAtlas")
atlas_material.use_nodes = True
nodes = atlas_material.node_tree.nodes
links = atlas_material.node_tree.links
bsdf = nodes.get("Principled BSDF")
tex = nodes.new("ShaderNodeTexImage")
tex.image = base_image
links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
surface = nodes.new("ShaderNodeTexImage")
surface.image = mask_image
split = nodes.new("ShaderNodeSeparateColor")
links.new(surface.outputs["Color"], split.inputs[0])
links.new(split.outputs[0], bsdf.inputs["Metallic"])
rough = nodes.new("ShaderNodeMath")
rough.operation = "SUBTRACT"
rough.inputs[0].default_value = 1
links.new(surface.outputs["Alpha"], rough.inputs[1])
links.new(rough.outputs[0], bsdf.inputs["Roughness"])


def finish(obj, tile, bevel=0):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    if bevel:
        mod = obj.modifiers.new("Authored edge bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.clear()
    obj.data.materials.append(atlas_material)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(65), island_margin=.035)
    bpy.ops.object.mode_set(mode="OBJECT")
    for loop in obj.data.uv_layers.active.data:
        loop.uv.x = ((tile % 4) + .04 + loop.uv.x * .92) / 4
        loop.uv.y = ((tile // 4) + .025 + loop.uv.y * .95) / 2
    obj.select_set(False)
    obj.parent = root
    return obj


def box(name, center, scale, tile, bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1, location=v(center))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (scale[0], scale[2], scale[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, tile, bevel)


def lathe(name, origin, axis, profile, tile, sides=24):
    axis = Vector(axis).normalized()
    side = axis.cross(Vector((0, 1, 0)))
    if side.length < .01:
        side = axis.cross(Vector((1, 0, 0)))
    side.normalize()
    other = axis.cross(side).normalized()
    vertices = []
    for distance, radius in profile:
        for i in range(sides):
            angle = i * math.tau / sides
            vertices.append(v(Vector(origin) + axis * distance + radius * (side * math.cos(angle) + other * math.sin(angle))))
    faces = []
    for j in range(len(profile) - 1):
        for i in range(sides):
            a, b = j * sides + i, j * sides + (i + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces += [tuple(reversed(range(sides))), tuple(range((len(profile) - 1) * sides, len(profile) * sides))]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for poly in mesh.polygons:
        poly.use_smooth = len(poly.vertices) == 4
    return finish(obj, tile)


def tube(name, points, radius, tile):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = 2
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, position in zip(spline.bezier_points, points):
        point.co = v(position)
        point.handle_left_type = point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    return finish(obj, tile)


roots = []


def asset(name):
    global root
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    roots.append(root)


asset("M2_SolderingIron")
axis = Vector((.65, .38, -.66)).normalized()
lathe("HotTip", (0, 0, 0), axis, [(0, .001), (.009, .003), (.024, .0055), (.032, .0055)], 2)
lathe("Tinned steel barrel", (0, 0, 0), axis,
      [(.028, .0055), (.036, .008), (.121, .008), (.126, .012), (.142, .012), (.15, .009)], 2)
lathe("Blue heat guard", (0, 0, 0), axis,
      [(.142, .015), (.149, .031), (.158, .033), (.168, .027), (.18, .025)], 1)
grip = [(.164, .023)]
for i in range(10):
    t = .176 + i * .010
    radius = .0235 - i * .00045
    grip.extend([(t, radius), (t + .003, radius + .0012), (t + .006, radius + .0012)])
grip.extend([(.281, .022), (.300, .020), (.310, .016)])
lathe("Ribbed rubber hand grip", (0, 0, 0), axis, grip, 0, 32)
lathe("Painted end cap", (0, 0, 0), axis, [(.303, .016), (.313, .017), (.322, .012), (.327, .007)], 1)
tail = axis * .326
tube("Flexible power lead", [tail, tail + Vector((.04, .014, -.045)), tail + Vector((.11, -.005, -.085)),
                            tail + Vector((.16, -.045, -.11))], .004, 0)

asset("M2_SolderSpool")
center = Vector((-.08, .14, -.07))
lathe("Moulded spool lower flange", center, (0, 1, 0), [(-.042, .026), (-.041, .067), (-.035, .071), (-.030, .064)], 1, 32)
lathe("Moulded spool upper flange", center, (0, 1, 0), [(.030, .064), (.035, .071), (.041, .067), (.042, .026)], 1, 32)
winding = [(-.030, .051)]
for i in range(12):
    h = -.029 + i * .0048
    winding.extend([(h, .053), (h + .002, .055), (h + .0045, .053)])
lathe("Visible wound solder wire", center, (0, 1, 0), winding, 2, 40)
lathe("Spool label", center, (0, 1, 0), [(.0424, .025), (.0424, .058), (.0429, .058), (.0429, .025)], 7, 32)
lathe("Recessed hub", center, (0, 1, 0), [(.042, .022), (.044, .022), (.044, .011), (.025, .011)], 0, 24)
tube("Solder feed wire", [(0, 0, 0), (-.013, .015, -.009), (-.040, .055, -.035), (-.06, .105, -.06)], .0035, 2)

asset("M2_OpenFlashlight")
box("Bevelled blue lower housing", (0, -.082, 0), (.65, .040, .31), 1, .012)
box("Front-facing shell rim", (0, -.038, -.142), (.63, .067, .023), 1, .007)
box("Back shell rim", (0, -.038, .142), (.63, .067, .023), 1, .007)
box("Battery end wall", (-.308, -.038, 0), (.027, .067, .28), 1, .006)
box("Exposed PCB", (-.035, -.035, -.015), (.48, .025, .205), 4, .003)
for x in (-.08, .08):
    box("Broken copper contact", (x, -.007, 0), (.085, .025, .075), 3, .002)
for x in (-.235, .17):
    for z in (-.09, .060):
        lathe("Board screw head", (x, -.015, z), (0, 1, 0), [(0, .009), (.004, .009), (.006, .006)], 2, 12)
lathe("Battery sleeve", (-.06, -.010, .088), (1, 0, 0), [(-.10, .020), (-.095, .022), (.095, .022), (.10, .018)], 0)
lathe("Battery positive terminal", (.045, -.010, .088), (1, 0, 0), [(0, .018), (.005, .018), (.005, .010), (.010, .010)], 2)
tube("Red cable to contact", [(-.20, -.014, .055), (-.185, .014, -.055), (-.11, .009, -.042), (-.08, .009, -.025)], .008, 6)
tube("Black cable to contact", [(.14, -.018, .050), (.19, .005, -.055), (.11, .009, -.048), (.08, .009, -.025)], .007, 0)
for x in (-.20, .14):
    box("Printed circuit trace", (x, -.020, -.040), (.032, .0015, .008), 3, .0005)
lathe("Reflector rim", (.29, -.010, 0), (1, 0, 0),
      [(-.035, .093), (-.025, .112), (.033, .123), (.043, .120), (.043, .101), (.030, .099)], 1, 40)
lathe("Inset reflector", (.29, -.010, 0), (1, 0, 0), [(.030, .099), (.013, .085), (.006, .032)], 2, 40)
lathe("Opaque lens diffuser", (.29, -.010, 0), (1, 0, 0), [(.031, .092), (.034, .092)], 5, 40)
tube("Carry handle", [(-.22, -.030, .15), (-.21, .052, .175), (-.15, .071, .18),
                      (.14, .071, .18), (.19, .04, .175), (.20, -.030, .15)], .014, 0)

stats = {}
for root in roots:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    objects = list(root.children)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=str(OUT / "Models" / (root.name + ".fbx")),
        use_selection=True, object_types={"MESH", "EMPTY"}, add_leaf_bones=False,
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=True,
        mesh_smooth_type="FACE", path_mode="RELATIVE", use_mesh_modifiers=True)
    stats[root.name] = {"meshes": len(objects), "triangles": sum(sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in objects)}

# Stage the editable source and a preview, keeping each exported working-end origin intact.
roots[0].location = v((.51, 1.12, -.05))
roots[1].location = v((-.49, 1.12, -.05))
roots[2].location = v((0, 1.075, 0))
bpy.ops.mesh.primitive_cube_add(size=1, location=v((0, .94, 0)))
table = bpy.context.object
table.name = "Preview table - not exported"
table.dimensions = (1.9, 1.0, .1)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
table_mat = bpy.data.materials.new("Preview worktop")
table_mat.diffuse_color = (.28, .22, .16, 1)
table.data.materials.append(table_mat)
bpy.ops.object.camera_add(location=v((1.0, 2.0, -1.45)))
camera = bpy.context.object
target = v((0, 1.05, 0))
camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.05
bpy.context.scene.camera = camera
for position, power, diameter in [((-.8, 3, -.7), 160, 2), ((1.2, 2.3, .5), 100, 1.5)]:
    bpy.ops.object.light_add(type="AREA", location=v(position))
    light = bpy.context.object
    light.data.energy = power
    light.data.shape = "DISK"
    light.data.size = diameter
    light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()
scene = bpy.context.scene
scene.world.color = (.20, .20, .20)
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.render.resolution_x = 1200
scene.render.resolution_y = 850
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "AgX"
scene.render.filepath = str(REPO / "Logs/M2-Blender-props.png")
for image in (base_image, mask_image):
    image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(HERE / "M2_SolderingProps.blend"))
bpy.ops.render.render(write_still=True)
(REPO / "Logs/M2-Blender-assets.json").write_text(json.dumps(stats, indent=2), encoding="utf-8")
print("M2_ART_EXPORTED", json.dumps(stats))
