"""Blender-authored silicone mat, procedural PBR maps and functional layout anchors."""
import bpy
import math
import json
import numpy as np
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
OUT = REPO / "Assets/Art/SolderingTrial"
for path in (OUT / "Models", OUT / "Textures", REPO / "Logs/Mat"):
    path.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.scene.unit_settings.system = "METRIC"
W, D = 1.76, 1.22
SIZE = .28  # 49.28 x 34.16 cm; preserve the moulded silicone thickness.
root = bpy.data.objects.new("SolderingMat", None)
bpy.context.collection.objects.link(root)

def v(x, y, z):
    return (-x, -z, y)  # Same Blender/FBX handedness convention as the existing M2 props.

def rr(cx, cz, width, depth, radius, segments=4):
    points = []
    for x, z, start in ((cx+width/2-radius, cz+depth/2-radius, 0),
                        (cx-width/2+radius, cz+depth/2-radius, 90),
                        (cx-width/2+radius, cz-depth/2+radius, 180),
                        (cx+width/2-radius, cz-depth/2+radius, 270)):
        for i in range(segments+1):
            angle = math.radians(start+i*90/segments)
            points.append((x+math.cos(angle)*radius, z+math.sin(angle)*radius))
    return points

def mesh(name, loops, caps=False):
    # All loops have matching samples; rings have four loops including the inner wall.
    n = len(loops[0][0])
    vertices = [v(x, y, z) for points, y in loops for x, z in points]
    faces = []
    for level in range(len(loops)-1):
        for i in range(n):
            j = (i+1) % n
            faces.append((level*n+i, level*n+j, (level+1)*n+j, (level+1)*n+i))
    if caps:
        faces.extend((tuple(reversed(range(n))), tuple(range((len(loops)-1)*n, len(loops)*n))))
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.parent = root
    return obj

def ring(name, cx, cz, width, depth, radius=.025, thickness=.006, height=.010):
    outer = rr(cx, cz, width, depth, radius)
    inner = rr(cx, cz, width-2*thickness, depth-2*thickness, max(.002, radius-thickness))
    return mesh(name, [(outer,.003), (outer,height-.001), (inner,height), (inner,.004)])

outline = rr(0, 0, W, D, .045, 8)
mesh("Silicone rounded foundation", [(outline,0), (outline,.0035)], True)
ring("Soft outer retaining rim", 0, 0, W-.012, D-.012, .038, .009, .009)
ring("Central repair field", 0, -.015, .91, .51, .028, .004, .006)
ring("Wide lid and parts tray", 0, .425, .70, .34, .028, .008, .011)
ring("Upper left solder station", -.645, .30, .405, .54, .032, .006, .008)
ring("Right iron rest", .66, -.045, .385, .76, .03, .007, .009)
for i in range(3):
    ring("Small parts pocket", -.32+i*.32, -.475, .295, .15, .018, .005, .008)
# A restrained grid of real shallow screw wells, with open faces rather than painted dots.
for row in range(12):
    for col in range(5):
        ring("Screw sorting well", -.808+col*.043, -.423+row*.036, .034, .028, .0045, .0022, .007)
# Circular dishes are useful silhouettes at the sides of the work area.
for x,z,r in ((.69,-.405,.073), (.69,.455,.067)):
    ring("Round magnetic-style parts dish", x,z,r*2,r*2,r,.005,.009)

def text(label, point, size):
    curve = bpy.data.curves.new(label, "FONT")
    curve.body = label
    curve.size = size
    curve.align_x = "CENTER"
    curve.extrude = .00022
    obj = bpy.data.objects.new("Moulded " + label, curve)
    bpy.context.collection.objects.link(obj)
    obj.location = v(*point)
    obj.rotation_euler = (0, 0, math.pi)  # Readable from the player at negative Unity Z.
    obj.parent = root
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj.select_set(False)

text("SOLDER", (-.645,.0042,.535), .020)
text("PARTS", (0,.0042,.555), .018)
text("IRON", (.66,.0042,.27), .020)
text("SILICONE", (.05,.0042,-.365), .018)

# Maps are original procedural silicone: subtle granulation, slight mould variation,
# and debossed ruler ticks. No reference photograph or logo is baked into the asset.
N = 1024
rng = np.random.default_rng(221026)
zz,xx = np.mgrid[0:N,0:N].astype(np.float32)
ux,uz = xx/(N-1)*W-W/2, zz/(N-1)*D-D/2
grain = rng.normal(0,1,(N,N)).astype(np.float32)
variation = .008*np.sin(xx*.037)*np.cos(zz*.029) + .004*grain
base = np.ones((N,N,4),np.float32)
base[:,:,:3] = np.clip(np.array((.035,.43,.59))[None,None,:] + variation[:,:,None],0,1)
ticks = (np.abs(np.mod(ux+.46,.02)-.01)<.0007) & (uz>-.303) & (uz<-.286)
longticks = (np.abs(np.mod(ux+.46,.10)-.05)<.0010) & (uz>-.311) & (uz<-.286)
ticks = (ticks | longticks) & (ux>-.44) & (ux<.44)
base[ticks,:3] *= .78
height = .004*grain + .002*np.sin(xx*1.4)*np.sin(zz*.8)
height[ticks] -= .025
dz,dx = np.gradient(height)
normal = np.ones_like(base)
normal[:,:,0] = .5-dx*.8
normal[:,:,1] = .5-dz*.8
normal[:,:,2] = np.sqrt(np.maximum(0,1-(dx*1.6)**2-(dz*1.6)**2))*.5+.5
mask = np.zeros_like(base)
mask[:,:,3] = np.clip(.20+.02*np.sin(xx*.015)*np.cos(zz*.023),.14,.24)
roughness = np.ones_like(base)
roughness[:,:,:3] = 1-mask[:,:,3,None]

def save(name, data, srgb=False):
    image = bpy.data.images.new(name,width=N,height=N,alpha=True)
    image.colorspace_settings.name = "sRGB" if srgb else "Non-Color"
    image.pixels.foreach_set(data.ravel())
    image.filepath_raw = str(OUT / "Textures" / (name+".png"))
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image

base_image = save("SolderingMat_BaseColor",base,True)
normal_image = save("SolderingMat_Normal",normal)
save("SolderingMat_MetallicSmoothness",mask)
rough_image = save("SolderingMat_Roughness",roughness)
material = bpy.data.materials.new("Blue silicone - matte polymer")
material.use_nodes = True
nodes, links = material.node_tree.nodes, material.node_tree.links
shader = nodes.get("Principled BSDF")
for image, socket in ((base_image,"Base Color"),(rough_image,"Roughness")):
    node = nodes.new("ShaderNodeTexImage"); node.image = image
    links.new(node.outputs["Color"],shader.inputs[socket])
node = nodes.new("ShaderNodeTexImage"); node.image = normal_image
bump = nodes.new("ShaderNodeNormalMap")
links.new(node.outputs["Color"],bump.inputs["Color"])
links.new(bump.outputs["Normal"],shader.inputs["Normal"])

objects = list(root.children)
bpy.ops.object.select_all(action="DESELECT")
for obj in objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = objects[0]
bpy.ops.object.join()
model = bpy.context.object
model.name = "Moulded silicone mat"
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
for vertex in model.data.vertices:
    vertex.co.x *= SIZE
    vertex.co.y *= SIZE
model.data.update()
# Box projection keeps side UVs valid, with a continuous world-scale top surface.
uv = model.data.uv_layers.new(name="Silicone UV")
for face in model.data.polygons:
    for index in face.loop_indices:
        p = model.data.vertices[model.data.loops[index].vertex_index].co
        if abs(face.normal.z) > .5:
            coord = ((-p.x+W*SIZE/2)/(W*SIZE),(-p.y+D*SIZE/2)/(D*SIZE))
        elif abs(face.normal.x) > abs(face.normal.y):
            coord = ((-p.y+D*SIZE/2)/(D*SIZE),p.z/.03)
        else:
            coord = ((-p.x+W*SIZE/2)/(W*SIZE),p.z/.03)
        uv.data[index].uv = coord
model.data.materials.clear()
model.data.materials.append(material)
# Match M2's recalculated mesh normals.
import bmesh
bm=bmesh.new(); bm.from_mesh(model.data)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(model.data); bm.free()

def point(x,y,z): return dict(x=x,y=y,z=z)
a = Vector((.65,.38,-.66)).normalized()
b = Vector((0,0,-1))
cross = a.cross(b)
q = [cross.x,cross.y,cross.z,1+a.dot(b)]
# mathutils.Vector's homogeneous normalization does not normalize W as a quaternion.
length = math.sqrt(sum(component*component for component in q))
q = [component/length for component in q]
anchors = [
    dict(name="RepairOrigin",position=point(0,.0341,0)),
    dict(name="Supply",position=point(0,.0341,-.38*SIZE)),
    dict(name="SolderDock",position=point(-.56*SIZE,-.0406,.27*SIZE)),
    dict(name="IronDock",position=point(.54*SIZE,.022,.34*SIZE),rotation=dict(x=q[0],y=q[1],z=q[2],w=q[3])),
    dict(name="CoverTray",position=point(0,.0098,.425*SIZE))]
for anchor in anchors:
    marker=bpy.data.objects.new(anchor["name"],None)
    bpy.context.collection.objects.link(marker)
    marker.parent=root
    p=anchor["position"]; marker.location=v(p["x"],p["y"],p["z"])
    marker.empty_display_type="ARROWS"; marker.empty_display_size=.04
(OUT/"Models/SolderingMat.layout.json").write_text(json.dumps(dict(anchors=anchors),indent=2),encoding="utf-8")
bpy.ops.object.select_all(action="DESELECT")
root.select_set(True); model.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=str(OUT/"Models/SolderingMat.fbx"),use_selection=True,
    object_types={"MESH","EMPTY"},add_leaf_bones=False,axis_forward="-Z",axis_up="Y",
    apply_unit_scale=True,bake_space_transform=True,mesh_smooth_type="FACE",use_mesh_modifiers=True)
# Render the isolated asset; anchors are editor-only source aids and do not render.
bpy.ops.object.camera_add(location=v(1.0*SIZE,1.7*SIZE,-1.35*SIZE))
camera=bpy.context.object
target=Vector(v(0,0,0))
camera.rotation_euler=(target-camera.location).to_track_quat("-Z","Y").to_euler()
camera.data.type="ORTHO"; camera.data.ortho_scale=2.15*SIZE
scene=bpy.context.scene; scene.camera=camera
for location,power in (((-.7,2,-.6),220),((1,1.8,.7),130)):
    bpy.ops.object.light_add(type="AREA",location=v(*(coordinate*SIZE for coordinate in location)))
    light=bpy.context.object; light.data.energy=power*SIZE*SIZE; light.data.shape="DISK"; light.data.size=2*SIZE
    light.rotation_euler=(target-light.location).to_track_quat("-Z","Y").to_euler()
scene.world.color=(.18,.18,.18)
scene.render.engine="CYCLES"; scene.cycles.samples=24
scene.render.resolution_x=1200; scene.render.resolution_y=900; scene.render.resolution_percentage=100
scene.render.image_settings.file_format="PNG"
scene.render.filepath=str(REPO/"Logs/Mat/SolderingMat-Blender.png")
scene.view_settings.view_transform="AgX"
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/"SolderingMat.blend"))
bpy.ops.render.render(write_still=True)
print("MAT_EXPORTED",len(model.data.vertices),"vertices;",sum(len(p.vertices)-2 for p in model.data.polygons),"triangles; 5 anchors")
