"""Blender source for the existing workshop neighbourhood; dimensions come from Unity.

Run: blender --background --python SourceArt/CityEnvironment/build_environment.py
No layout, gameplay anchors or colliders are generated here.
"""
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
OUT = REPO / 'Assets/Art/CityEnvironment/Models'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.scene.unit_settings.system = 'METRIC'
materials = {}
roots = []
current = None

# FBX/Unity convention used by the project's M2/M3 Blender sources.
def v(x, y, z):
    return (-x, -z, y)

def root(name):
    global current
    current = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(current)
    roots.append(current)
    return current

def mesh(name, points, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata([v(*p) for p in points], [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.parent = current
    if mat not in materials:
        materials[mat] = bpy.data.materials.new(mat)
    data.materials.append(materials[mat])
    uv = data.uv_layers.new(name='Metres')
    for face in data.polygons:
        normal = face.normal
        axes = (0, 1) if abs(normal.z) > .7 else ((0, 2) if abs(normal.y) > .7 else (1, 2))
        for loop_index in face.loop_indices:
            p = data.vertices[data.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (p[axes[0]] / 3, p[axes[1]] / 3)
    return obj

def box(name, center, size, mat, bevel=0):
    x,y,z = center; w,h,d = (n/2 for n in size)
    obj = mesh(name, [(x+a*w,y+b*h,z+c*d) for a,b,c in
        [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]],
        [(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)], mat)
    # Edge finishing is applied once after the semantic join, rather than forcing
    # thousands of dependency-graph updates for individual window members.
    return obj

def tube(name, a, b, radius, mat):
    delta = Vector(b)-Vector(a)
    direction = delta.normalized()
    perpendicular = direction.cross(Vector((0,1,0)))
    if perpendicular.length < .1: perpendicular = direction.cross(Vector((1,0,0)))
    perpendicular.normalize(); other = direction.cross(perpendicular)
    points = [tuple(Vector(p)+radius*(math.cos(i*math.tau/8)*perpendicular+math.sin(i*math.tau/8)*other)) for p in (a,b) for i in range(8)]
    return mesh(name, points, [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]+[tuple(reversed(range(8))),tuple(range(8,16))], mat)

def roof(x,z,w,d,h, rise=1.25):
    # A folded sheet roof with physical thickness, ridge and overhanging eaves.
    w+=.65; d+=.65
    for side in (-1,1):
        edge=x+side*w/2
        mesh('Roof standing seam', [(x,h+rise,z-d/2),(edge,h,z-d/2),(edge,h,z+d/2),(x,h+rise,z+d/2),
             (x,h+rise-.14,z-d/2),(edge,h-.14,z-d/2),(edge,h-.14,z+d/2),(x,h+rise-.14,z+d/2)],
             [(0,1,2,3),(7,6,5,4),(0,4,5,1),(3,2,6,7),(1,5,6,2)],'Roof')
        for iz in range(int(d/1.2)+1):
            zz=z-d/2+iz*1.2
            tube('Roof folded seam',(x,h+rise+.02,zz),(edge,h+.02,zz),.025,'Roof')
        tube('Roof rain gutter',(edge,h-.12,z-d/2),(edge,h-.12,z+d/2),.09,'Steel')
        tube('Downpipe',(edge,h-.12,z+d/2-.3),(edge,.2,z+d/2-.3),.065,'Steel')
    tube('Roof ridge cap',(x,h+rise+.02,z-d/2-.06),(x,h+rise+.02,z+d/2+.06),.1,'Roof')
    for end in (-1,1):
        zz=z+end*(d-.65)/2
        mesh('Gable plaster',[(x-(w-.65)/2,h,zz),(x+(w-.65)/2,h,zz),(x,h+rise,zz)],[(0,1,2),(2,1,0)],'Plaster')

def window(x,y,z,w=1.5,h=1.5,side=False,facing=-1):
    # Recessed opaque glass: inexpensive and readable, no transparency sorting.
    def part(name,c,s,m):
        if side: c=(c[2],c[1],c[0]); s=(s[2],s[1],s[0])
        box(name,c,s,m,.025)
    part('Window recessed glass',(x,y,z),(w,h,.08),'Glass')
    for dx in (-w/2,w/2,0): part('Window frame',(x+dx,y,z+facing*.06),(.075,h+.15,.13),'Ivory')
    for dy in (-h/2,h/2,0): part('Window frame',(x,y+dy,z+facing*.06),(w+.13,.07,.13),'Ivory')
    part('Window sill',(x,y-h/2-.07,z+facing*.12),(w+.3,.14,.34),'Stone')

def building(entry):
    x,z,w,d,h = (entry[k] for k in ('x','z','w','d','h'))
    wall = 'Brick' if 'Loading' in entry['name'] else 'Plaster'
    box('Building walls',(x,h/2,z),(w,h,d),wall,.06)
    box('Masonry plinth',(x,.32,z),(w+.08,.64,d+.08),'Stone',.025)
    # Separate roofs, doors, cornices and human-scale bays replace a solid box silhouette.
    roof(x,z,w,d,h,min(1.9,h*.18))
    for end in (-1,1):
        zz=z+end*(d/2+.04)
        box('Facade cornice',(x,h-.18,zz),(w+.13,.24,.16),'Ivory',.025)
        for ix in range(max(1,int(w/4))):
            xx=x-w/2+(ix+.5)*w/max(1,int(w/4))
            for level in range(max(1,int(h/3.2))):
                yy=1.9+level*3.1
                if yy+1<h and not (level==0 and abs(xx-x)<1.6): window(xx,yy,zz,1.5,1.5,facing=end)
        box('Door recessed blue metal',(x,1.25,zz),(1.25,2.5,.11),'Blue',.04)
        box('Door transom',(x,2.65,zz),(1.45,.18,.2),'Ivory',.02)
        tube('Door pull',(x+.43,.95,zz-.12),(x+.43,1.25,zz-.12),.025,'Steel')
    # Side elevations use vertical bays rather than a continuous dark stripe.
    for side in (-1,1):
        for iz in range(max(1,int(d/5))):
            zz=z-d/2+(iz+.5)*d/max(1,int(d/5))
            for level in range(max(1,int(h/3.2))):
                yy=1.9+level*3.1
                if yy+1<h: window(zz,yy,x+side*(w/2+.04),1.5,1.5,True,side)

def perforated_wall(cx,cz,width,height,side=False,door=False):
    # Actual window apertures, not glass decals over a solid workshop wall.
    openings=[]
    if door: openings.append((-1.2,1.2,0,2.8))
    for i in range(int(width/4)):
        p=-width/2+(i+.5)*width/int(width/4)
        if door and abs(p)<3: continue
        openings.append((p-1,p+1,1.3,2.75))
    xs=sorted(set([-width/2,width/2]+[q for op in openings for q in op[:2]]))
    ys=sorted(set([0,height]+[q for op in openings for q in op[2:]]))
    for a,b in zip(xs,xs[1:]):
        for low,high in zip(ys,ys[1:]):
            if any(l<(a+b)/2<r and bot<(low+high)/2<top for l,r,bot,top in openings): continue
            c=(cx+(a+b)/2,(low+high)/2,cz); s=(b-a,high-low,.4)
            if side: c=(cx,(low+high)/2,cz+(a+b)/2); s=(.4,high-low,b-a)
            box('Workshop wall',c,s,'Plaster')
    for l,r,bot,top in openings:
        if bot==0: continue
        if side: window(cz+(l+r)/2,(bot+top)/2,cx,r-l,top-bot,True,1 if cx>0 else -1)
        else: window(cx+(l+r)/2,(bot+top)/2,cz,r-l,top-bot,facing=1 if cz>0 else -1)

root('NeighbourhoodArchitecture')
for entry in json.loads((HERE/'layout.json').read_text()):
    if entry['name'].startswith(('Court ','Market ','Loading ')) and ' volume ' not in entry['name']:
        building(entry)
print('Neighbourhood facade geometry created', flush=True)

root('WorkshopShell')
perforated_wall(0,-10,24,4,door=True)
perforated_wall(0,10,24,4)
perforated_wall(-12,0,20,4,side=True)
perforated_wall(12,0,20,4,side=True)
roof(0,0,24,20,4.0,1.5)  # Gable starts exactly at the 4m wall head, with overlapping eaves.
for side in (-1,1):
    box('Entry jamb',(side*1.25,1.4,-10.25),(.14,2.8,.18),'Blue',.02)
    box('Workshop skirting',(side*6.65,.48,-10.23),(10.7,.95,.1),'Blue',.02)
# Sloped entrance hood with thin bracing, leaves the 2.4m clear opening untouched.
mesh('Entry folded canopy',[(-3.6,3.95,-10.1),(3.6,3.95,-10.1),(3.6,3.75,-11.4),(-3.6,3.75,-11.4)],[(0,1,2,3),(3,2,1,0)],'Roof')
for x in (-3.5,3.5): tube('Entry canopy brace',(x,3.3,-10.2),(x,3.75,-11.2),.035,'Steel')

root('SellerStall')
# Local coordinates relative to the existing CitySeller interaction object.
for x in (-2.35,2.35):
    for z in (-.65,.7): tube('Canopy steel upright',(x,0,z),(x,2.74,z),.065,'Blue')
for x in (-2.35,2.35): tube('Canopy arched rib',(x,2.65,-.8),(x,2.9,.05),.06,'Steel'); tube('Canopy arched rib',(x,2.9,.05),(x,2.65,.9),.06,'Steel')
for i in range(10):
    x=-2.5+i*.5
    m='CanvasBlue' if i%2==0 else 'Ivory'
    mesh('Tailored canvas awning',[(x,2.65,-.9),(x+.5,2.65,-.9),(x+.5,2.9,.05),(x,2.9,.05),(x+.5,2.65,1),(x,2.65,1)],[(0,1,2,3),(3,2,4,5),(5,4,2,3),(3,2,1,0)],m)
    box('Canvas valance',(x+.25,2.56,-.9),(.5,.2,.035),m,.015)
box('Counter wooden top',(0,1.17,.1),(4.55,.12,1.1),'Timber',.045)
for x in (-2.1,2.1):
    box('Counter frame',(x,.55,.1),(.13,1.1,.85),'Blue',.025)
box('Counter inset frontage',(0,.64,-.35),(4.3,1.04,.09),'Timber',.025)
for x in (-1.4,-.7,0,.7,1.4): box('Counter timber joint',(x,.6,-.41),(.025,.85,.015),'Stone')
box('Lower storage shelf',(0,.22,.2),(4.2,.09,.8),'Timber',.025)
for x in (-1.4,-.6,.7,1.5):
    box('Stock carton',(x,.52,.22),(.55,.5,.52),'Cardboard',.035)
    box('Stock carton folded lid',(x,.78,.22),(.57,.025,.54),'Cardboard',.015)
    box('Stock carton tape',(x,.798,.22),(.12,.01,.54),'Ivory')

# Merge by semantic material inside each export: bounded draw calls, editable source.
for export_root in roots:
    groups={}
    for obj in list(export_root.children): groups.setdefault(obj.data.materials[0].name,[]).append(obj)
    for mat,objects in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects: obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        bpy.ops.object.join()
        obj=bpy.context.object
        obj.name=('Roof ' if mat=='Roof' else 'Building surface ')+mat
        if mat in ('Plaster','Brick','Blue','Timber','Cardboard') and not (mat=='Plaster' and export_root.name=='WorkshopShell'):
            mod=obj.modifiers.new('Manufactured edge chamfers','BEVEL')
            mod.width=.015; mod.segments=1
            bpy.ops.object.modifier_apply(modifier=mod.name)
            mod=obj.modifiers.new('Weighted face normals','WEIGHTED_NORMAL')
            bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        # Recalculate outward normals after DCC joins.
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    export_root.select_set(True)
    for obj in export_root.children: obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(export_root.name+'.fbx')),use_selection=True,
        object_types={'MESH','EMPTY'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,bake_space_transform=True,mesh_smooth_type='FACE',use_mesh_modifiers=True)
    print('Exported',export_root.name,flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'CityEnvironment.blend'))
print('CITY_ENVIRONMENT_EXPORTED',len(roots),'FBX assets;',sum(len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH'),'polygons')
