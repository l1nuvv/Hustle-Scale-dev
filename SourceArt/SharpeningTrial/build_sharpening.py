"""Original M6 models in metres, authored/exported with Blender. No external assets."""
import bpy, math
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
edge=mat('M6_UnfinishedEdge',(.20,.22,.24),.5); bright=mat('M6_HonedEdge',(.70,.77,.80),.8)
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
box('Foot', (0,.025,.06),(.36,.05,.34),machine,rubber,.012)
box('Cast pedestal',(0,.10,.07),(.28,.12,.24),machine,paint,.02)
cylinder('Motor',(.13,.25,.14),.092,.20,machine,paint)
cylinder('Bearing',(.045,.25,.14),.05,.07,machine,steel)
cylinder('Wheel',(0,.25,.14),.13,.045,machine,stone)
cylinder('WheelHub',(-.027,.25,.14),.035,.012,machine,steel)
# An asymmetric inset on the wheel makes rotation readable.
box('WheelMark',(.024,.32,.14),(.002,.035,.008),machine,steel,.001)
box('Switch',(.16,.174,-.025),(.05,.024,.035),machine,rubber,.004)
knife=root('M6_Knife')
# Tapered forged blade with a pointed tip; edge runs on X at Y=Z=0.
verts=[(-.14,0,0),(.14,0,0),(.14,.003,-.055),(-.08,.003,-.055),
       (-.14,.002,0),(.14,.005,0),(.14,.008,-.055),(-.08,.008,-.055)]
mesh=bpy.data.meshes.new('ForgedBlade'); mesh.from_pydata([v(p) for p in verts],[],[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)])
o=bpy.data.objects.new('Blade',mesh); bpy.context.collection.objects.link(o); finish(o,'Blade',knife,steel,.001)
box('Bolster',(.15,.006,-.027),(.018,.022,.060),knife,steel,.004)
box('WalnutGrip',(.215,.007,-.027),(.115,.027,.048),knife,rubber,.012)
for x in (.185,.235):
    box('HandlePin',(x,.022,-.027),(.008,.002,.008),knife,steel,.003)
for i in range(12):
    x=-.14+(i+.5)*.28/12
    box('Edge_%02d'%i,(x,.001,-.0015),(.28/12,.0015,.003),knife,edge,.0003)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/SharpeningTrial/M6_Sharpening.blend'))
for group in (machine,knife):
    bpy.ops.object.select_all(action='DESELECT'); group.select_set(True)
    for child in group.children_recursive: child.select_set(True)
    bpy.context.view_layer.objects.active=group
    bpy.ops.export_scene.fbx(filepath=str(OUT/(group.name+'.fbx')),use_selection=True,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
