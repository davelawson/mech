#!/usr/bin/env node
// Restyle the pinned official vector map; never redraw or retype its geography.
// Requires Node.js and Poppler's pdftoppm. Run from any working directory.
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const crypto = require('node:crypto');
const { execFileSync } = require('node:child_process');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '..');
const source = fs.readFileSync(path.join(root, 'docs/sources/inner-sphere-3025.pdf'));
assert.equal(crypto.createHash('sha256').update(source).digest('hex'),
  '40966bc3a317e7c7cc071b4c34ed6638a78eac8a659b237ef110393deaedb238',
  'The source PDF changed; review its drawing commands before regenerating.');
const objects = new Map([...source.toString('latin1').matchAll(
  /(?:^|[\r\n])(\d+) 0 obj[\r\n]([\s\S]*?)endobj/g
)].map(match => [Number(match[1]), Buffer.from(match[2], 'latin1')]));

function stream(id) {
  const object = objects.get(id).toString('latin1');
  const header = object.slice(0, object.indexOf('stream'));
  const start = object.indexOf('stream') + 'stream'.length;
  const newline = object.slice(start).match(/^\r?\n/)[0].length;
  const length = Number(header.match(/\/Length (\d+)/)[1]);
  return zlib.inflateSync(objects.get(id).subarray(start + newline, start + newline + length)).toString('latin1');
}
function setStream(id, content) {
  const object = objects.get(id).toString('latin1');
  let header = object.slice(0, object.indexOf('stream'));
  const compressed = zlib.deflateSync(Buffer.from(content, 'latin1'));
  header = header.replace(/\/Length \d+/, `/Length ${compressed.length}`);
  objects.set(id, Buffer.concat([Buffer.from(header + 'stream\n', 'latin1'), compressed, Buffer.from('\nendstream\n')]));
}
function rgb(hex, stroke = false) {
  const values = hex.match(/../g).map(channel => (parseInt(channel, 16) / 255).toFixed(5));
  return `${values.join(' ')} ${stroke ? 'RG' : 'rg'}`;
}
function replaceOnce(text, original, replacement) {
  assert.equal(text.split(original).length, 2, `Expected one occurrence: ${original.slice(0, 80)}`);
  return text.replace(original, replacement);
}

const contentIds = [9, 10, 11, 12, 13, 14, 22, 23];
let content = contentIds.map(stream).join('');
// These two forms contain only the hex outlines and their coordinate numbers.
for (const name of ['Fm132', 'Fm133']) content = replaceOnce(content, `/${name} Do`, '');
content = replaceOnce(content,
  '1.595 -1.455 Td\n[(         INTERSTELLAR HEX = 30 )0.5 (LIGHT )43.4 (Y)5 (EARS)]TJ\n', '');
// The final sample coordinate and hex outline belong only to the legend.
const legendHex = content.indexOf('/TT1 1 Tf\n0 Tc 3 0 0 3 1625.156 1512.1324 Tm');
assert(legendHex > 0 && content.slice(legendHex).includes('(0101)Tj'));
content = content.slice(0, legendHex) + 'ET\n';

const palette = new Map([
  ['0 0 0 0.25 k', '050b15'], // empty space
  ['0.2 0.9 1 0.1 k', '3d2520'], // Taurian Concordat
  ['1 0.4 0 0 k', '102c47'], // Lyran Commonwealth
  ['0.4 0.9 0.01 0 k', '301f3e'], // Free Worlds League
  ['0 0.14 1 0 k', '3b351e'], // Federated Suns
  ['0.85 0.25 1 0 k', '173b2d'], // Capellan Confederation
  ['0 1 1 0 k', '421e2a'], // Draconis Combine
  ['0.25 0.25 0.4 0 k', '363128'], // periphery territories
  ['0.7 0.1 0.4 0.1 k', '183c3b'], // Magistracy of Canopus
  ['0 0.15 0.35 0.15 k', '3b3228'],
  ['0 0 0 0.75 k', '1a2434'] // Dark Nebula
]);
for (const [original, color] of palette) {
  assert(content.includes(original), `Missing source color: ${original}`);
  content = content.split(original + '\n').join(rgb(color) + '\n');
}
// Preserve dark capital-symbol centers while adapting text independently below.
content = content.replace(/^0 0 0 1 k$/gm, rgb('07111f'));
const borderEnd = content.indexOf('BT\n' + rgb('07111f'));
assert(borderEnd > 0);
content = content.slice(0, borderEnd).replace(/^0 0 0 1 K$/gm, rgb('70899d', true))
  + content.slice(borderEnd).replace(/^0 0 0 1 K$/gm, rgb('74899e', true));
// A dark legend, with its original text, symbols, scale and compass retained.
content = replaceOnce(content, '0 0 0 0 k\n1578.36 1448.76 386.4 240.24 re\nf',
  rgb('0b1421') + '\n1578.36 1448.76 386.4 240.24 re\nf\n' + rgb('667d93', true));
// Legend line work inherits the drawing state, so lighten it after the legend starts.
const legendStart = content.indexOf('/PlacedPDF /MC1 BDC');
content = content.slice(0, legendStart) + content.slice(legendStart)
  .replace(/^0 0 0 1 K$/gm, rgb('9aaec2', true));

// Compass direction lettering is outlined vector geometry rather than PDF text.
const compassEnd = content.indexOf('EMC ', legendStart);
assert(compassEnd > legendStart);
content = content.slice(0, legendStart) + content.slice(legendStart, compassEnd)
  .replaceAll(rgb('07111f'), rgb('c6d2df')) + content.slice(compassEnd);

// Text and symbol fills share PDF state. Restore each original fill after text
// so light labels do not turn the dark centers of capital symbols white.
let fill = '0 g', inText = false;
const fills = [];
content = content.split('\n').flatMap(line => {
  if (/^q(?: |$)/.test(line)) fills.push(fill);
  if (/(?:^| )Q$/.test(line)) { assert(fills.length > 0); fill = fills.pop(); }
  if (/^[\d.]+(?: [\d.]+){0,3} (?:k|g|rg)$/.test(line)) {
    fill = line;
    if (inText) return [rgb('c6d2df')];
  }
  if (line === 'BT') { inText = true; return [line, rgb('c6d2df')]; }
  if (line === 'ET') { inText = false; return [line, fill]; }
  return [line];
}).join('\n');
assert.equal(fills.length, 0);

// Fixed seed: faint decorative stars stay fixed while the map pans and zooms.
// Draw above territory fills and below all cartographic borders, systems and labels.
let seed = 3025;
function random() { seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 4294967296; }
let stars = 'q\n/GS0 gs\n';
for (let i = 0; i < 1100; i++) {
  const x = 4 + random() * 2008, y = 4 + random() * 1720;
  const radius = 0.18 + random() * 0.32;
  stars += rgb(i % 7 === 0 ? '53677d' : '33465b') + '\n';
  // Small square points remain below a named system's 2.237-point radius.
  stars += `${x.toFixed(3)} ${y.toFixed(3)} ${(radius * 2).toFixed(3)} ${(radius * 2).toFixed(3)} re f\n`;
}
stars += 'Q\n';
content = replaceOnce(content, 'q\n0 g\n/GS1 gs\n0 TL/Fm0 Do', stars + 'q\n0 g\n/GS1 gs\n0 TL/Fm0 Do');

// The source uses Darken blending for fine border forms, which hides light ink.
objects.set(28, Buffer.from(objects.get(28).toString('latin1').replace('/BM/Darken', '/BM/Normal'), 'latin1'));
const page = objects.get(6).toString('latin1');
for (const [, name, id] of page.matchAll(/\/(Fm\d+) (\d+) 0 R/g)) {
  if (Number(name.slice(2)) <= 131) {
    const commands = stream(Number(id));
    assert(commands.includes('0 0 0 0.3 K'));
    setStream(Number(id), commands.replaceAll('0 0 0 0.3 K', rgb('435b71', true)));
  }
}
setStream(9, content);
objects.set(6, Buffer.from(page.replace(/\/Contents\[[^\]]+\]/, '/Contents[9 0 R]'), 'latin1'));

// Write an ordinary PDF with the unchanged font resources and vector paths.
// The original is linearized; its byte offsets and linearization object are not reusable.
objects.delete(4);
let offset = 0;
const chunks = [], offsets = new Map();
function append(value) { const buffer = Buffer.isBuffer(value) ? value : Buffer.from(value, 'latin1'); chunks.push(buffer); offset += buffer.length; }
append('%PDF-1.6\n%\xE2\xE3\xCF\xD3\n');
for (const [id, object] of [...objects].sort(([a], [b]) => a - b)) {
  offsets.set(id, offset); append(`${id} 0 obj\n`); append(object); append('\nendobj\n');
}
const xref = offset, size = Math.max(...objects.keys()) + 1;
append(`xref\n0 ${size}\n0000000000 65535 f \n`);
for (let id = 1; id < size; id++) append(offsets.has(id)
  ? `${String(offsets.get(id)).padStart(10, '0')} 00000 n \n` : '0000000000 00000 f \n');
append(`trailer\n<</Size ${size}/Root 5 0 R/Info 3 0 R>>\nstartxref\n${xref}\n%%EOF\n`);
const work = path.join(root, 'TestResults/star-map-work');
fs.mkdirSync(work, { recursive: true });
const pdf = path.join(work, 'starscape.pdf');
fs.writeFileSync(pdf, Buffer.concat(chunks));
execFileSync('pdftoppm', ['-singlefile', '-jpeg', '-jpegopt', 'quality=96', '-scale-to', '7000', pdf,
  path.join(root, 'Assets/inner-sphere-3025-starscape')], { stdio: 'inherit' });
console.log('Rendered Assets/inner-sphere-3025-starscape.jpg (7000 × 6000).');
