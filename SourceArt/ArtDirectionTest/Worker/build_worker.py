"""Adapt the CC0 Quaternius base in Blender; run with Blender --background --python.

The downloaded skeleton, forearms, trouser topology and hair are reused.
The face, jacket and shoes are authored here for the isolated style sample.
"""
import bpy
import bmesh
import math
from pathlib import Path
from mathutils import Vector, Matrix

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
OUT = PROJECT / 'Assets/ArtDirectionTest/Worker'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(HERE / 'Original/Quaternius_SuperheroMale.fbx'))
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = bpy.data.objects['SuperHero_Male']

def material(name, color, roughness=.76):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*color, 1)
    node.inputs['Roughness'].default_value = roughness
    return mat

skin = material('Worker_Skin', (.49, .285, .16))
shirt = material('Worker_NavyCotton', (.035, .075, .12))
pants = material('Worker_Trousers', (.035, .05, .07))
boots = material('Worker_Leather', (.045, .033, .025), .62)
hair_mat = material('Worker_Hair', (.035, .023, .014))
eye_white = material('Worker_EyeWhite', (.78, .76, .69), .45)
iris = material('Worker_Iris', (.11, .07, .035), .4)
seam = material('Worker_Stitch', (.075, .105, .135))
tee = material('Worker_Undershirt', (.61,.60,.55))
pupil = material('Worker_Pupil', (.008,.009,.008))

# Remove unavailable imported texture paths; use restrained, ordinary game materials.
for obj in [o for o in bpy.data.objects if o.type == 'MESH']:
    obj.data.materials.clear()
    obj.data.materials.append(skin if obj == body else hair_mat if obj.name == 'Eyebrows' else eye_white)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = True

# The base pack's free male is athletic. Reduce the upper-body breadth without
# scaling the head into a narrow caricature. Match the rest rig to the new mesh.
def slender(point):
    x=abs(point.x)
    point.x=math.copysign(min(x,.22)*.72+max(0,x-.22)*.95,point.x)
    if x>.22:
        point.z=1.456+(point.z-1.456)*.72
        point.y=.065+(point.y-.065)*.72
    elif point.z<1.50:point.y*=.86

for obj in [o for o in bpy.data.objects if o.type == 'MESH']:
    for v in obj.data.vertices:
        slender(v.co)
    obj.data.update()
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for bone in arm.data.edit_bones:
    for point in (bone.head, bone.tail):
        slender(point)
bpy.ops.object.mode_set(mode='OBJECT')

def shell(name, bounds, mat, thickness, relax=0):
    obj = body.copy()
    obj.data = body.data.copy()
    bpy.context.collection.objects.link(obj)
    obj.name = name
    bm = bmesh.new(); bm.from_mesh(obj.data)
    for axis, low, high in bounds:
        normal=Vector((0,0,0));normal[axis]=1
        for value, below in [(low,True),(high,False)]:
            if value is None: continue
            point=Vector((0,0,0));point[axis]=value
            bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                plane_co=point,plane_no=normal,clear_inner=below,clear_outer=not below)
    bm.normal_update()
    bm.to_mesh(obj.data); bm.free()
    obj.data.materials.clear(); obj.data.materials.append(mat)
    if relax:
        bm=bmesh.new();bm.from_mesh(obj.data)
        indices=[v.index for v in bm.verts if not v.is_boundary]
        group=obj.vertex_groups.new(name='Relax interior cloth');group.add(indices,1,'REPLACE');bm.free()
        smooth = obj.modifiers.new('Relax cloth over anatomy', 'SMOOTH')
        smooth.factor = .85; smooth.iterations = relax
        smooth.vertex_group=group.name
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_move_up(modifier=smooth.name)
        bpy.ops.object.modifier_apply(modifier=smooth.name)
    obj.data.update()
    if name=='Open work jacket':
        bm=bmesh.new();bm.from_mesh(obj.data)
        def opening_width(z):return .028+max(0,min(1,(z-1.35)/.15))*.047
        bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.calc_center_median().y<-.02 and
            abs(f.calc_center_median().x)<opening_width(f.calc_center_median().z)],context='FACES')
        for v in bm.verts:
            if v.is_boundary and v.co.y<-.02 and abs(v.co.x)<.115 and .99<v.co.z<1.55:
                v.co.x=math.copysign(opening_width(v.co.z),v.co.x)
        bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
        bm.normal_update();bm.to_mesh(obj.data);bm.free();obj.data.update()
    for vertex in obj.data.vertices:
        vertex.co += vertex.normal * thickness
        for axis,low,high in bounds:
            if low is not None:vertex.co[axis]=max(low,vertex.co[axis])
            if high is not None:vertex.co[axis]=min(high,vertex.co[axis])
    solid = obj.modifiers.new('Cloth thickness', 'SOLIDIFY')
    solid.thickness = .0025; solid.offset = 0
    bpy.context.view_layer.objects.active = obj
    # Apply thickness before armature deformation, preserving editable weighted meshes.
    bpy.ops.object.modifier_move_up(modifier=solid.name)
    bpy.ops.object.modifier_apply(modifier=solid.name)
    for p in obj.data.polygons: p.use_smooth = True
    return obj

undershirt=shell('Offwhite undershirt',[(2,.975,1.51),(0,-.09,.09)],tee,.010,16)
bm=bmesh.new();bm.from_mesh(undershirt.data)
bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.calc_center_median().y>0],context='FACES')
bm.to_mesh(undershirt.data);bm.free()
shirt_obj = shell('Open work jacket', [(2,.98,1.555),(0,-.43,.43)], shirt, .032, 20)
pants_obj = shell('Work trousers', [(2,.115,.99)], pants, .023, 16)
for vertex in pants_obj.data.vertices:
    vertex.co.z=min(vertex.co.z,.985)
    if vertex.co.z < .85:
        centre=.082 if vertex.co.x>0 else -.082
        blend=max(0,min(1,(.90-vertex.co.z)/.15))
        angle=math.atan2((vertex.co.y-.025)/.07,(vertex.co.x-centre)/.06)
        vertex.co.x=(1-blend)*vertex.co.x+blend*(centre+math.cos(angle)*.052)
        vertex.co.y=(1-blend)*vertex.co.y+blend*(.025+math.sin(angle)*.060)

# Keep the underlying skin only where it is visible (face, neck, arms and hands).
bm = bmesh.new(); bm.from_mesh(body.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if (lambda p: p.z>1.535 or p.z<1.02 or (1.02<=p.z<1.535 and abs(p.x)<.425))(f.calc_center_median())], context='FACES')
bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
bm.to_mesh(body.data); bm.free()
body.name = 'Weighted hands and forearms'

# The eyes are real curved mesh from the base. Use their UVs to delimit small irises
# rather than adding oversize spheres on the face.
for name in ['Eyes','Eyebrows']:
    bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)

def weighted_mesh(name,vertices,faces,mat,bone='Head',smooth=True,native=False):
    # The source armature is rotated 180 degrees around Z; its local forward is -Y.
    if not native:
        vertices=[(x,-y,z) for x,y,z in vertices]
        faces=[tuple(reversed(face)) for face in faces]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat);obj.parent=arm
    group=obj.vertex_groups.new(name=bone);group.add(list(range(len(vertices))),1,'REPLACE')
    mod=obj.modifiers.new('Humanoid skin','ARMATURE');mod.object=arm
    for polygon in mesh.polygons:polygon.use_smooth=smooth
    return obj

# Own elongated, deliberately simple face. The realistic downloaded head/eyes are
# discarded, rather than merely decimated or recoloured and called stylized.
head_parts=[]
rings=[(1.475,.043,.039),(1.53,.043,.039),(1.545,.038,.035),
       (1.565,.054,.047),(1.605,.065,.055),(1.66,.075,.060),
       (1.715,.075,.060),(1.755,.068,.055),(1.780,.035,.029)]
vertices=[];faces=[];segments=20
for z,rx,ry in rings:
    for i in range(segments):
        angle=i*math.tau/segments
        vertices.append((math.cos(angle)*rx,.006+math.sin(angle)*ry,z))
for j in range(len(rings)-1):
    for i in range(segments):
        n=(i+1)%segments
        faces.append((j*segments+i,j*segments+n,(j+1)*segments+n,(j+1)*segments+i))
faces.extend([tuple(reversed(range(segments))),tuple((len(rings)-1)*segments+i for i in range(segments))])
head_parts.append(weighted_mesh('Simple adult head and neck',vertices,faces,skin))

def oval_face(name,centre,radius,mat):
    vertices=[];faces=[];rows=8;cols=16
    for j in range(rows+1):
        v=math.pi*j/rows
        for i in range(cols):
            u=math.tau*i/cols
            vertices.append((centre[0]+math.sin(v)*math.cos(u)*radius[0],
                             centre[1]+math.sin(v)*math.sin(u)*radius[1],centre[2]+math.cos(v)*radius[2]))
    for j in range(rows):
        for i in range(cols):
            n=(i+1)%cols;faces.append((j*cols+i,(j+1)*cols+i,(j+1)*cols+n,j*cols+n))
    obj=weighted_mesh(name,vertices,faces,mat);head_parts.append(obj);return obj

for side in [-1,1]:
    oval_face('Simple ear', (side*.076,.005,1.655),(.014,.019,.027),skin)
    oval_face('Eye white', (side*.034,.058,1.672),(.021,.008,.016),eye_white)
    oval_face('Small round pupil', (side*.034,.065,1.671),(.0065,.002,.008),pupil)
    # A straight brow, neutral rather than the original sculpt's angry V.
    x=side*.033
    brow=[(x-.020,.067,1.696),(x+.020,.067,1.697),(x+.020,.067,1.702),(x-.020,.067,1.701)]
    head_parts.append(weighted_mesh('Simple brow',brow,[(3,2,1,0)],hair_mat,smooth=False))
    oval_face('Upper eyelid', (side*.034,.058,1.686),(.022,.007,.004),skin)
nose=[(-.009,.058,1.667),(.009,.058,1.667),(-.011,.059,1.636),(.011,.059,1.636),
      (-.008,.084,1.638),(.008,.084,1.638)]
head_parts.append(weighted_mesh('Simple wedge nose',nose,[(0,1,5,4),(0,4,2),(1,3,5),(2,4,5,3)],skin,smooth=False))
head_parts.append(weighted_mesh('Short neutral mouth',[(-.014,.066,1.603),(.014,.066,1.603),(.014,.066,1.605),(-.014,.066,1.605)],[(3,2,1,0)],hair_mat,smooth=False))
# Combine the facial surfaces into one weighted mesh with material zones.
bpy.ops.object.select_all(action='DESELECT')
for obj in head_parts:obj.select_set(True)
bpy.context.view_layer.objects.active=head_parts[0];bpy.ops.object.join()
head_parts[0].name='Stylized head'

# Reuse the author's actual hairstyle mesh, fit to this head, bind it to Head.
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=str(HERE / 'Original/Quaternius_HairSimpleParted.fbx'))
hair_objects = [o for o in bpy.data.objects if o not in before and o.type == 'MESH']
for obj in hair_objects:
    obj.name = 'Short parted hair'
    obj.data.materials.clear(); obj.data.materials.append(hair_mat)
    relative=arm.matrix_world.inverted() @ obj.matrix_world
    obj.data.transform(relative)
    low=Vector([min(v.co[i] for v in obj.data.vertices) for i in range(3)])
    high=Vector([max(v.co[i] for v in obj.data.vertices) for i in range(3)])
    centre=(low+high)*.5
    for v in obj.data.vertices:
        v.co.x=(v.co.x-centre.x)*.15/(high.x-low.x)
        v.co.y=(v.co.y-centre.y)*.125/(high.y-low.y)-.005
        v.co.z=1.715+(v.co.z-low.z)*.09/(high.z-low.z)
    obj.vertex_groups.clear()
    vg = obj.vertex_groups.new(name='Head'); vg.add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
    obj.parent = arm
    obj.matrix_parent_inverse=Matrix.Identity(4);obj.matrix_basis=Matrix.Identity(4)
    modifier = obj.modifiers.new('Humanoid skin', 'ARMATURE'); modifier.object = arm
    for p in obj.data.polygons: p.use_smooth = True

# Use clean, broad garment surfaces. The rejected muscle-following cloth shell
# is not suitable for this style, even with a different face.
bpy.data.objects.remove(shirt_obj,do_unlink=True)
bpy.data.objects.remove(undershirt,do_unlink=True)
verts=[];faces=[];cols=24
sections=[(.98,.14,.090),(1.08,.14,.091),(1.25,.145,.098),
          (1.39,.16,.10),(1.47,.185,.086),(1.51,.17,.075),(1.535,.070,.045),(1.545,.052,.040)]
for z,rx,ry in sections:
    gap=math.asin(min(.80,(.029 if z<1.39 else .045)/rx))
    for i in range(cols+1):
        angle=-math.pi/2+gap+i*(math.tau-2*gap)/cols
        verts.append((math.cos(angle)*rx,math.sin(angle)*ry+.005,z))
for j in range(len(sections)-1):
    for i in range(cols):
        a=j*(cols+1)+i;faces.append((a,a+1,a+cols+2,a+cols+1))
shirt_obj=weighted_mesh('Open work jacket',verts,faces,shirt,'spine_03',True,True)
for v in shirt_obj.data.vertices:
    if v.co.z<1.30:
        shirt_obj.vertex_groups['spine_03'].remove([v.index])
        name='pelvis' if v.co.z<1.05 else 'spine_01' if v.co.z<1.20 else 'spine_02'
        group=shirt_obj.vertex_groups.get(name) or shirt_obj.vertex_groups.new(name=name)
        group.add([v.index],1,'REPLACE')
cloth_parts=[shirt_obj]
for side in [-1,1]:
    verts=[];faces=[];sleeve_sections=[(.11,.020),(.13,.055),(.17,.075),(.23,.071),(.34,.060),(.44,.054)]
    for x,r in sleeve_sections:
        for i in range(16):
            angle=i*math.tau/16
            verts.append((side*x,.065+math.cos(angle)*r,1.456+math.sin(angle)*r))
    for j in range(len(sleeve_sections)-1):
        for i in range(16):
            n=(i+1)%16;face=(j*16+i,j*16+n,(j+1)*16+n,(j+1)*16+i)
            faces.append(face if side>0 else tuple(reversed(face)))
    faces.append(tuple(reversed(range(16))) if side>0 else tuple(range(16)))
    suffix='l' if side>0 else 'r'
    sleeve=weighted_mesh('Continuous jacket sleeve',verts,faces,shirt,'upperarm_'+suffix,True,True)
    group=sleeve.vertex_groups.new(name='lowerarm_'+suffix)
    for v in sleeve.data.vertices:
        blend=max(0,min(1,(abs(v.co.x)-.34)/.10))
        if blend>0:
            sleeve.vertex_groups['upperarm_'+suffix].add([v.index],1-blend,'REPLACE')
            group.add([v.index],blend,'REPLACE')
    cloth_parts.append(sleeve)
verts=[];faces=[]
for z,half,depth in [(.98,.033,.075),(1.25,.033,.080),(1.46,.052,.066),(1.515,.050,.045)]:
    for x in [-half,0,half]:verts.append((x,-depth,z))
for j in range(3):
    for i in range(2):
        a=j*3+i;faces.append((a,a+1,a+4,a+3))
undershirt=weighted_mesh('Offwhite undershirt',verts,faces,tee,'spine_03',True,True)

# Open jacket lapels and functional chest pockets are cloth surfaces, skinned to
# the same torso. Their large shapes match the reference's ordinary workwear.
for side in [-1,1]:
    verts=[(side*.045,.068,1.53),(side*.095,.078,1.49),
           (side*.078,.110,1.405),(side*.035,.116,1.45)]
    faces=[(0,1,2,3)] if side>0 else [(3,2,1,0)]
    cloth_parts.append(weighted_mesh('Folded work collar',verts,faces,shirt,'spine_03',False))
    x=side*.094
    pocket=[]
    for px,pz in [(x-.025,1.37),(x+.025,1.37),(x+.025,1.31),(x-.025,1.31)]:
        found,loc,normal,index=shirt_obj.closest_point_on_mesh(Vector((px,-.17,pz)))
        if not found:raise RuntimeError('Pocket could not be fitted to jacket')
        pocket.append((loc.x,-loc.y+.0015,loc.z))
    cloth_parts.append(weighted_mesh('Chest patch pocket',pocket,[(0,1,2,3)],shirt,'spine_03',False))
bpy.ops.object.select_all(action='DESELECT')
for obj in cloth_parts:obj.select_set(True)
bpy.context.view_layer.objects.active=shirt_obj;bpy.ops.object.join()

# Actual shoe uppers and soles, shaped as continuous ring lofts. Cover the base's
# anatomical toes; a dark recolour of a bare foot is not a work shoe.
for side, bone_name in [(-1,'foot_r'),(1,'foot_l')]:
    verts=[];faces=[]
    count=24
    for level in range(4):
        for i in range(count):
            angle=i*math.tau/count
            if level<3:
                x=math.cos(angle)*.053
                y=.027+math.sin(angle)*.14
                z=[.008,.035,.105][level]
                if level==2:z-=max(0,math.sin(angle))*.025
            else:
                x=math.cos(angle)*.042;y=-.012+math.sin(angle)*.055;z=.18
            verts.append((side*.082+x,-y,z))
    for level in range(3):
        for i in range(count):
            j=(i+1)%count
            faces.append((level*count+i,level*count+j,(level+1)*count+j,(level+1)*count+i))
    faces.append(tuple(reversed(range(count))))
    faces.append(tuple(3*count+i for i in range(count)))
    faces=[tuple(reversed(face)) for face in faces]
    mesh=bpy.data.meshes.new('Work shoe loft');mesh.from_pydata(verts,[],faces);mesh.update()
    shoe=bpy.data.objects.new('Left work boot' if side>0 else 'Right work boot',mesh);bpy.context.collection.objects.link(shoe)
    mesh.materials.append(boots);mesh.materials.append(hair_mat)
    for polygon in mesh.polygons:
        polygon.use_smooth=True
        if polygon.index<count:polygon.material_index=1
    shoe.parent=arm;vg=shoe.vertex_groups.new(name=bone_name);vg.add(list(range(len(verts))),1,'REPLACE')
    mod=shoe.modifiers.new('Humanoid skin','ARMATURE');mod.object=arm

# Reference-driven silhouette: broad head, narrow shoulders and restrained shoes.
# Deform the rest geometry and rig together so skin weights remain usable.
def proportions(point, head=False):
    if head:
        blend=max(0,min(1,(point.z-1.53)/.035))
        point.x*=.84+(1.55-.84)*blend
        point.y*=1+.30*blend
    else:
        point.x*=.84
        point.y*=.90
    point.z=point.z*.91 if point.z<=1.54 else 1.54*.91+(point.z-1.54)*1.24

for obj in [o for o in bpy.data.objects if o.type=='MESH']:
    is_head=obj.name in {'Stylized head','Short parted hair'}
    for vertex in obj.data.vertices:
        if obj.name in {'Left work boot','Right work boot'}:
            centre=.082 if vertex.co.x>0 else -.082
            vertex.co.x=centre+(vertex.co.x-centre)*.72
            vertex.co.y*=.70
            vertex.co.z*=.74
        proportions(vertex.co,is_head)
    obj.data.update()
bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT')
for bone in arm.data.edit_bones:
    for point in (bone.head,bone.tail):
        proportions(point,bone.name=='Head')
bpy.ops.object.mode_set(mode='OBJECT')

def world_rotate_bone(name, axis, angle):
    bone = arm.pose.bones[name]
    rotation = Matrix.Rotation(math.radians(angle), 4, axis)
    pivot = bone.head.copy()
    bone.matrix = Matrix.Translation(pivot) @ rotation @ Matrix.Translation(-pivot) @ bone.matrix
    bpy.context.view_layer.update()

world_rotate_bone('upperarm_l', 'Y', 85)
world_rotate_bone('upperarm_r', 'Y', -85)
world_rotate_bone('lowerarm_l', 'X', -8)
world_rotate_bone('lowerarm_r', 'X', -12)
world_rotate_bone('Head', 'Z', -4)
bpy.context.view_layer.update()

# Author a quiet looping idle on the actual skeleton. No floating body parts.
arm.animation_data_clear()
for frame, angle in [(1,0), (37,.7), (73,0), (109,-.45), (145,0)]:
    bone = arm.pose.bones['spine_03']; bone.rotation_mode='XYZ'
    bone.rotation_euler.y = math.radians(angle)
    bone.keyframe_insert(data_path='rotation_euler', frame=frame, group='Quiet breathing')
for bone in arm.pose.bones:
    if bone.name != 'spine_03':
        bone.keyframe_insert(data_path='rotation_quaternion', frame=1, group='Standing pose')
bpy.context.scene.frame_start=1; bpy.context.scene.frame_end=145
bpy.context.scene.render.fps=24
if arm.animation_data and arm.animation_data.action:
    arm.animation_data.action.name='Worker_Idle'
bpy.context.scene.frame_set(1)

# Centre, ground and scale the complete character once. Export geometry/rig only.
characters=[o for o in bpy.data.objects if o.type in {'MESH','ARMATURE'}]
dg=bpy.context.evaluated_depsgraph_get()
coords=[]
for o in characters:
    if o.type=='MESH':
        e=o.evaluated_get(dg)
        coords += [e.matrix_world @ v.co for v in e.data.vertices]
zmin=min(p.z for p in coords); zmax=max(p.z for p in coords)
factor=1.75/(zmax-zmin)
arm.scale *= factor
arm.location.z -= zmin*factor
arm.name='WorkshopWorkerRig'

for mat in list(bpy.data.materials):
    if mat.users == 0: bpy.data.materials.remove(mat)
for image in list(bpy.data.images):
    if image.users == 0: bpy.data.images.remove(image)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'WorkshopWorker.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in characters: o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(OUT/'WorkshopWorker.fbx'), use_selection=True, object_types={'ARMATURE','MESH'},
    add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, mesh_smooth_type='FACE', use_mesh_modifiers=True,
    axis_forward='-Z',axis_up='Y', apply_unit_scale=True)

# A genuine Blender render for checking silhouette and continuous shoulders/limbs.
floor_mat=material('Preview floor',(.2,.21,.2))
bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.012))
bpy.context.object.data.materials.append(floor_mat)
def area(name, location, power, size):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj); obj.location=location
    obj.rotation_euler=(Vector((0,0,1))-obj.location).to_track_quat('-Z','Y').to_euler()
area('Key',(2,3,4),380,4)
area('Fill',(-3,1,2),180,3)
area('Rim',(0,-3,3),250,3)
camera_data=bpy.data.cameras.new('Preview camera'); camera=bpy.data.objects.new('Preview camera',camera_data)
bpy.context.collection.objects.link(camera); camera.location=(2.3,4.5,2)
camera.rotation_euler=(Vector((0,0,1.0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO';camera_data.ortho_scale=2.25
scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.world=bpy.data.worlds.new('Neutral preview world')
scene.world.color=(.23,.23,.23);scene.render.resolution_x=900;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.filepath=str(PROJECT/'Temp/NpcCandidates/worker-preview.png')
bpy.ops.render.render(write_still=True)
print('WORKER_EXPORTED',str(OUT/'WorkshopWorker.fbx'))
