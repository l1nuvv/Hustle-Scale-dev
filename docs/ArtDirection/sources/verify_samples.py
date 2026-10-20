"""Static integrity of the art pack, not a Unity/gameplay test."""
from pathlib import Path
import json
import re
import struct
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
errors = []
links = 0
for file in ROOT.rglob('*.md'):
    for target in re.findall(r'!?\[[^\]]*\]\(([^)]+)\)', file.read_text(encoding='utf-8')):
        if '://' in target or target.startswith('#'):
            continue
        links += 1
        if not (file.parent / target.split('#')[0]).exists():
            errors.append(f'Missing link {file.relative_to(ROOT)} -> {target}')

svgs = list(ROOT.rglob('*.svg'))
for file in svgs:
    try:
        ET.fromstring(file.read_text(encoding='utf-8'))
    except ET.ParseError as ex:
        errors.append(f'SVG {file.name}: {ex}')
screens = list((ROOT/'selected/interface').glob('*.svg'))
if len(screens) != 12:
    errors.append(f'Expected 12 UI screens, found {len(screens)}')
html = (ROOT/'index.html').read_text(encoding='utf-8')
for target in re.findall(r'(?:src|href)="([^"]+)"',html):
    if '://' not in target and not (ROOT/target).exists():
        errors.append(f'HTML missing resource: {target}')

def luminance(color):
    a = [int(color[i:i+2],16)/255 for i in (1,3,5)]
    a = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in a]
    return .2126*a[0]+.7152*a[1]+.0722*a[2]

paper = '#EEE8D9'
contrast = {}
for name,color in {'ink':'#202E37','muted':'#59636A','verified':'#315E48','fault':'#963E35','equipment':'#365E73'}.items():
    a,b = sorted([luminance(paper),luminance(color)])
    value=(b+.05)/(a+.05)
    contrast[name+'/paper']=round(value,2)
    if value < 4.5:
        errors.append(f'Insufficient contrast {name}: {value}')

# Read a TrueType cmap format 4 directly to avoid adding a fontTools dependency.
data=(ROOT/'selected/typography/LiberationSans.ttf').read_bytes()
u16=lambda p:struct.unpack_from('>H',data,p)[0]
u32=lambda p:struct.unpack_from('>I',data,p)[0]
tables={}
for i in range(u16(4)):
    p=12+i*16
    tables[data[p:p+4].decode('ascii')]=(u32(p+8),u32(p+12))
cmap=tables['cmap'][0]
formats=[]
for i in range(u16(cmap+2)):
    p=cmap+4+i*8
    platform,encoding,offset=u16(p),u16(p+2),u32(p+4)
    q=cmap+offset
    if platform in (0,3) and u16(q)==4:
        formats.append(q)
def has_glyph(cp):
    for q in formats:
        count=u16(q+6)//2
        ends=q+14; starts=ends+2*count+2; deltas=starts+2*count; ranges=deltas+2*count
        for i in range(count):
            if u16(starts+i*2)<=cp<=u16(ends+i*2):
                delta=u16(deltas+i*2); ro=u16(ranges+i*2)
                glyph=u16(ranges+i*2+ro+2*(cp-u16(starts+i*2))) if ro else cp
                return glyph!=0 and ((glyph+delta)&0xffff)!=0
    return False
sample=''.join(chr(c) for c in range(0x410,0x450))+'Ёё0123456789$№'
missing=[c for c in sample if not has_glyph(ord(c))]
if missing: errors.append('Missing required font glyphs: '+''.join(missing))
brief='Простая игровая 3D-стилизация в стиле Schedule I; конкретный дизайн и контент — собственные.'
active_docs=['HUSTLE_AND_SCALE_ART_DIRECTION_v1.0.md','PRODUCTION_GUIDELINES.md','RESEARCH.md','VISUAL_EVALUATION.md','VALIDATION.md']
for name in active_docs:
    content=(ROOT/name).read_text(encoding='utf-8')
    if brief not in content:
        errors.append('Missing current owner direction: '+name)
    if any(old in content for old in ['Рекомендуется **B','Лучший в целом, для предметных взаимодействий','B — рекомендация, не подтверждённый выбор']):
        errors.append('Obsolete style recommendation: '+name)
gallery=html.split('<details',1)[0]
for old in ['exploration/direction-A/','exploration/direction-C/','exploration/direction-D/','B / Простая']:
    if old in gallery:
        errors.append('Historical study in active gallery: '+old)
prompts=json.loads((ROOT/'sources/PROMPTS.json').read_text(encoding='utf-8'))
if prompts.get('active_style',{}).get('owner_direction')!=brief:
    errors.append('Prompt brief differs from owner direction')
for file in ROOT.rglob('*.png'):
    content=file.read_bytes()
    if not content.startswith(b'\x89PNG\r\n\x1a\n'):
        errors.append(f'Invalid PNG {file.name}')
print(json.dumps({'ui_screens':len(screens),'svg_files':len(svgs),'local_markdown_links':links,
    'png_files':len(list(ROOT.rglob('*.png'))),'contrast_ratios':contrast,
    'required_font_glyphs':len(sample),'missing_glyphs':missing,'active_direction_consistent':not any('direction' in e or 'style' in e or 'gallery' in e for e in errors),
    'errors':errors},ensure_ascii=False,indent=2))
raise SystemExit(bool(errors))
