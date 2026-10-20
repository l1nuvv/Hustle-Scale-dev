// Render our editable SVG sources only. No browser, image edits or remote assets.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(require.resolve('sharp', {paths:[process.argv[2] || __dirname]}));
const root = path.resolve(__dirname, '..');
const output = path.resolve(root, '../..', 'Temp/ArtDirection');
fs.mkdirSync(output, {recursive:true});
const cases = [
  ['selected/interface/hud.svg',1600,900,'hud-16x9'],
  ['selected/interface/inventory.svg',1600,1000,'inventory-16x10'],
  ['selected/interface/sale.svg',2100,900,'sale-21x9'],
  ['selected/interface/finance.svg',1600,900,'finance-16x9'],
  ['selected/interface/inspection.svg',1600,900,'inspection-16x9'],
  ['selected/typography/type-palette-signage.svg',1600,900,'type-sheet'],
  ['selected/environment/spatial-rules.svg',1600,900,'spatial-sheet']
];
(async()=>{
  for (const [file,width,height,name] of cases) {
    const svg=fs.readFileSync(path.join(root,file));
    await sharp(svg).resize(width,height,{fit:'contain',background:'#202e37'})
      .png().toFile(path.join(output,name+'.png'));
  }
  console.log('Rendered '+cases.length+' static SVG samples to '+output);
})().catch(e=>{console.error(e);process.exitCode=1;});
