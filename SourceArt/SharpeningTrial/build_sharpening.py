"""Original M6 models in metres, authored/exported with Blender. No external assets."""
import bpy, bmesh, math, sys
from pathlib import Path
from mathutils import Vector
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Art/SharpeningTrial'
OUT.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
def v(p): return (-p[0], -p[2], p[1])
def mat(name, color, metal=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metal; bs.inputs['Roughness'].default_value=.65
    return m
paint=mat('M6_SageEnamel',(.20,.29,.28)); steel=mat('M6_Steel',(.43,.48,.50),.65)
stone=mat('M6_Abrasive',(.24,.25,.27)); rubber=mat('M6_Rubber',(.055,.065,.065))
edge=mat('M6_UnfinishedEdge',(.04,.78,1),.15); bright=mat('M6_HonedEdge',(.70,.77,.80),.8)
edge.node_tree.nodes.get('Principled BSDF').inputs['Emission Color'].default_value=(.04,.78,1,1)
edge.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value=1.5
def root(name):
    o=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(o); return o
def finish(o,name,parent,material,bevel=.003):
    o.name=name; o.parent=parent; o.data.materials.append(material)
    if bevel:
        mod=o.modifiers.new('Soft machined edges','BEVEL'); mod.width=bevel; mod.segments=3
        o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o
def box(name,p,size,parent,material,bevel=.003):
    bpy.ops.mesh.primitive_cube_add(size=1, location=v(p)); o=bpy.context.object
    o.dimensions=(size[0],size[2],size[1]); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,parent,material,bevel)
def cylinder(name,p,r,depth,parent,material):
    bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=r,depth=depth,location=v(p),rotation=(0,math.pi/2,0))
    return finish(bpy.context.object,name,parent,material,.002)
machine=root('M6_Grinder')
def authored_mesh(name,verts,faces,parent,material,bevel=.001,smooth=False):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata([v(p) for p in verts],[],faces)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
    for polygon in mesh.polygons:polygon.use_smooth=smooth
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
    return finish(o,name,parent,material,bevel)
def lathe(name,profile,center,parent,material):
    verts=[];faces=[];segments=64
    for x,radius in profile:
        for j in range(segments):
            a=j*2*math.pi/segments;verts.append((x,center[0]+radius*math.sin(a),center[1]+radius*math.cos(a)))
    for i in range(len(profile)-1):
        for j in range(segments):a=i*segments+j;b=i*segments+(j+1)%segments;faces.append((a,b,b+segments,a+segments))
    faces.extend([tuple(range(segments-1,-1,-1)),tuple((len(profile)-1)*segments+j for j in range(segments))])
    return authored_mesh(name,verts,faces,parent,material,.0006,True)

box('Cast iron mounting base',(.12,.026,.13),(.49,.04,.37),machine,paint,.013)
for x in (-.065,.305):
    for z in (-.01,.275):
        box('Vibration isolator',(x,.008,z),(.065,.018,.060),machine,rubber,.007)
        bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.008,depth=.006,location=v((x,.049,z)))
        finish(bpy.context.object,'Base fastener',machine,steel,.0006)
profile=[(.047,-.006),(.12,.007),(.17,.055),(.17,.22),(.12,.27),(.047,.288)]
verts=[(x,y,z) for x in (.075,.28) for y,z in profile];n=len(profile)
faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
authored_mesh('Arched cast motor pedestal',verts,faces,machine,paint,.010)
lathe('Ribbed motor casting',[(.068,.047),(.073,.064),(.088,.084),(.105,.09),(.272,.09),(.29,.084),(.31,.063),(.318,.052)],(.25,.14),machine,paint)
for x in (.112,.14,.168,.196,.224,.252):
    lathe('Cooling rib',[(x-.002,.089),(x,.096),(x+.003,.096),(x+.005,.089)],(.25,.14),machine,paint)
lathe('Front end bearing',[(.038,.025),(.045,.036),(.061,.041),(.074,.04)],(.25,.14),machine,steel)
lathe('Spindle',[(.006,.012),(.06,.012)],(.25,.14),machine,steel)
cylinder('Rear vent darkness',(.317,.25,.14),.056,.003,machine,rubber)
lathe('Vent rim',[(.318,.055),(.323,.060),(.328,.057)],(.25,.14),machine,paint)
for i in range(10):
    a=i*2*math.pi/10;vertices=[]
    for radius in (.017,.052):
        for da in (-.045,.045):vertices.append((.329,.25+radius*math.sin(a+da),.14+radius*math.cos(a+da)))
    authored_mesh('Rear ventilation grille',vertices,[(0,1,3,2)],machine,paint,.0003)
cylinder('Wheel',(0,.25,.14),.13,.045,machine,stone)
lathe('Grinding wheel flange',[(-.035,.013),(-.033,.033),(-.028,.038),(-.024,.035)],(.25,.14),machine,steel)
bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.012,depth=.010,location=v((-.039,.25,.14)),rotation=(0,math.pi/2,0))
finish(bpy.context.object,'Spindle retaining nut',machine,steel,.0008)
box('WheelMark',(-.0235,.318,.14),(.001,.026,.004),machine,steel,.0004)
# Pressed steel guard: 260 degrees of coverage, with the actual front grinding sector open.
verts=[];faces=[];segments=72
for i in range(segments+1):
    a=math.radians(230+260*i/segments)
    for x,r in ((-.031,.135),(-.031,.145),(.037,.145),(.037,.135)):
        verts.append((x,.25+r*math.sin(a),.14+r*math.cos(a)))
for i in range(segments):
    for j in range(4):a=i*4+j;b=i*4+(j+1)%4;faces.append((a,b,b+4,a+4))
faces.extend([(3,2,1,0),tuple(segments*4+j for j in range(4))])
authored_mesh('Pressed wheel safety guard',verts,faces,machine,paint,.0015)
for degree in (40,95,275):
    a=math.radians(degree)
    cylinder('Guard screw',(-.035,.25+.139*math.sin(a),.14+.139*math.cos(a)),.004,.005,machine,steel)
box('Tool rest mounting arm',(.046,.16,-.003),(.015,.095,.025),machine,steel,.002)
cylinder('Tool rest adjustment screw',(.057,.175,-.003),.009,.017,machine,rubber)
box('Machined tool rest',(0,.207,-.030),(.104,.009,.074),machine,steel,.002)
box('Switch dust seal',(.165,.092,-.014),(.086,.059,.012),machine,rubber,.006)
red=mat('M6_StopButton',(.36,.075,.055));green=mat('M6_StartButton',(.08,.27,.20))
box('Stop rocker',(.145,.092,-.023),(.033,.040,.012),machine,red,.004)
box('Start rocker',(.184,.092,-.023),(.030,.040,.012),machine,green,.004)
shield=mat('M6_ClearShield',(.55,.68,.69));shield.diffuse_color=(.55,.68,.69,.16)
box('Shield support',(.055,.408,.075),(.012,.12,.014),machine,steel,.002)
cylinder('Shield thumb screw',(.052,.451,.072),.008,.024,machine,rubber)
box('Clear eye shield',(0,.401,-.019),(.118,.108,.002),machine,shield,.002)
for x in (-.061,.061):box('Shield frame',(x,.401,-.019),(.004,.115,.006),machine,steel,.001)
for y in (.345,.457):box('Shield frame',(0,y,-.019),(.122,.004,.006),machine,steel,.001)
curve=bpy.data.curves.new('Power lead','CURVE');curve.dimensions='3D';curve.bevel_depth=.0035;curve.bevel_resolution=3
spline=curve.splines.new('BEZIER');spline.bezier_points.add(3)
for point,p in zip(spline.bezier_points,[(.30,.09,.26),(.37,.025,.33),(.33,.012,.43),(.45,.012,.49)]):
    point.co=v(p);point.handle_left_type='AUTO';point.handle_right_type='AUTO'
o=bpy.data.objects.new('Rubber power lead',curve);bpy.context.collection.objects.link(o);o.parent=machine;o.data.materials.append(rubber)
knife=root('M6_Knife')
blade_steel=mat('M6_KitchenSteel',(.46,.51,.54),.7)
bevel=mat('M6_CuttingBevel',(.67,.73,.76),.8)
grip=mat('M6_KitchenGrip',(.045,.055,.065))

def cutting_point(u): return (-.14 + .28*u, 0, -.03*(1-u)**4)
def spine_point(u): return (-.14 + .28*u, 0, -.03-.036*math.sqrt(math.sin(math.pi*u/2)))
def blade_surface(u, across, coating=False):
    x,_,edge_z=cutting_point(u); _,_,spine_z=spine_point(u)
    # Thin cutting edge facing +Z (the wheel), full-thickness spine facing -Z.
    thickness=min(across/.075,1)*.0018*min(u*20,1)
    return (x, thickness + (.00008 if coating else 0), edge_z+(spine_z-edge_z)*across)

rows=96
verts=[]; faces=[]; face_materials=[]
for side in (1,-1):
    for i in range(rows+1):
        for across in (0,.075,1):
            x,y,z=blade_surface(i/rows,across)
            verts.append((x,y*side,z))
for side in range(2):
    offset=side*(rows+1)*3
    for i in range(rows):
        for j in range(2):
            a=offset+i*3+j; b=a+3
            faces.append((a,b,b+1,a+1));face_materials.append(1 if j==0 else 0)
bottom=(rows+1)*3
for i in range(rows):
    a=i*3; b=a+3
    faces.extend([(a,a+bottom,b+bottom,b),(a+2,b+2,b+bottom+2,a+bottom+2)])
    face_materials.extend([1,0])
for i in (0,rows):
    a=i*3; faces.append((a,a+1,a+2,a+bottom+2,a+bottom+1,a+bottom));face_materials.append(0)
mesh=bpy.data.meshes.new('ContinuousChefBlade');mesh.from_pydata([v(p) for p in verts],[],faces)
mesh.materials.append(blade_steel);mesh.materials.append(bevel)
for poly,index in zip(mesh.polygons,face_materials): poly.material_index=index
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
o=bpy.data.objects.new('Blade',mesh);bpy.context.collection.objects.link(o);o.parent=knife

# One continuous overlay, no twelve separate strips or visible divisions.
verts=[];uvs=[];faces=[]
for i in range(rows+1):
    for across in (0,.075,1):
        verts.append(v(blade_surface(i/rows,across,True)));uvs.append((i/rows,across))
for i in range(rows):
    for j in range(2):
        a=i*3+j;b=a+3;faces.append((a+1,b+1,b,a))
mesh=bpy.data.meshes.new('ContinuousSpraySurface');mesh.from_pydata(verts,[],faces)
uv=mesh.uv_layers.new(name='BladeCoordinates')
for poly in mesh.polygons:
    for loop in poly.loop_indices:uv.data[loop].uv=uvs[mesh.loops[loop].vertex_index]
o=bpy.data.objects.new('PowderCoating',mesh);bpy.context.collection.objects.link(o);o.parent=knife;o.data.materials.append(edge)

# A contoured riveted kitchen handle, no guard or clipped hunting-knife point.
verts=[];faces=[];rings=[(.143,.005,.010),(.155,.011,.016),(.19,.013,.018),(.235,.012,.018),(.267,.010,.015),(.277,.004,.008)]
for x,ry,rz in rings:
    for j in range(24):
        a=j*2*math.pi/24;verts.append(v((x,math.sin(a)*ry,-.048+math.cos(a)*rz)))
for i in range(len(rings)-1):
    for j in range(24):a=i*24+j;b=i*24+(j+1)%24;faces.append((a,b,b+24,a+24))
faces.extend([tuple(range(23,-1,-1)),tuple((len(rings)-1)*24+j for j in range(24))])
mesh=bpy.data.meshes.new('ContouredKitchenGrip');mesh.from_pydata(verts,[],faces)
o=bpy.data.objects.new('KitchenGrip',mesh);bpy.context.collection.objects.link(o);finish(o,'KitchenGrip',knife,grip,.001)
for x in (.169,.211,.251):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=.0026,depth=.027,location=v((x,0,-.048)))
    finish(bpy.context.object,'GripRivet',knife,blade_steel,.0003)
for i in range(12):
    anchor=root('CuttingEdge_%02d'%i);anchor.parent=knife;anchor.location=v(cutting_point((i+.5)/12))
anchor=root('Spine');anchor.parent=knife;anchor.location=v(spine_point(.5))
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/SharpeningTrial/M6_Sharpening.blend'))
for group in ((knife,) if '--knife-only' in sys.argv else (machine,knife)):
    bpy.ops.object.select_all(action='DESELECT'); group.select_set(True)
    for child in group.children_recursive: child.select_set(True)
    bpy.context.view_layer.objects.active=group
    bpy.ops.export_scene.fbx(filepath=str(OUT/(group.name+'.fbx')),use_selection=True,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
