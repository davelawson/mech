#!/usr/bin/env python3
"""Extract the pinned vector chart's system geography without OCR or network.

Requires Python 3 and Poppler's pdftotext. Run normally to regenerate the catalog,
or with --check to verify that the checked-in catalog matches this extraction.
The PDF hash, marker/label counts, explicit joins, and visual-review assertions
make source or extraction changes fail for review instead of silently drifting.
"""
import hashlib
import json
import math
from pathlib import Path
import re
import sys
import subprocess
import xml.etree.ElementTree as ET
import zlib

ROOT = Path(__file__).resolve().parents[1]
SOURCE_SHA256 = '40966bc3a317e7c7cc071b4c34ed6638a78eac8a659b237ef110393deaedb238'
SCALE = 7000 / 2016
HEIGHT = 1728
# The source's 120-light-year ruler runs 169.302 PDF points; the game texture
# preserves the page geometry at 7000/2016 pixels per point.
PIXELS_PER_LIGHT_YEAR = 169.302 / 120 * SCALE

def read_source():
    raw = (ROOT / 'docs/sources/inner-sphere-3025.pdf').read_bytes()
    assert hashlib.sha256(raw).hexdigest() == SOURCE_SHA256, 'Review changed PDF before extraction.'
    objects = {int(m[1]): m[2] for m in re.finditer(rb'(?:^|[\r\n])(\d+) 0 obj[\r\n]([\s\S]*?)endobj', raw)}
    def stream(i):
        obj = objects[i]
        p = obj.index(b'stream')
        length = int(re.search(rb'/Length (\d+)', obj[:p])[1])
        return zlib.decompress(obj[p+6:].lstrip(b'\r\n')[:length]).decode('latin1')
    content = ''.join(stream(i) for i in [9,10,11,12,13,14,22,23])
    content = content[:content.index('/PlacedPDF /MC1 BDC')]
    return objects, content

def markers(content):
    result = []
    for m in re.finditer(r'q 1 0 0 1 ([\d.-]+) ([\d.-]+) cm\n(.*?)\nQ', content, re.S):
        lines = [line.strip() for line in m[3].splitlines()]
        if len(lines) not in (6, 7) or lines[0] != '0 0 m' or sum(l.endswith(' c') for l in lines) != 4:
            continue
        nums = [float(x) for x in re.findall(r'-?[\d.]+', '\n'.join(lines[:-1]))]
        xs, ys = nums[::2], nums[1::2]
        width, height = max(xs)-min(xs), max(ys)-min(ys)
        if abs(width-height) >= .06 or not 1 < width < 20:
            continue
        x = float(m[1]) + (max(xs)+min(xs))/2
        y = HEIGHT - float(m[2]) - (max(ys)+min(ys))/2
        existing=next((i for i,p in enumerate(result) if math.hypot(x-p[0],y-p[1])<.08),None)
        if existing is None: result.append((x,y,width))
        elif width>result[existing][2]: result[existing]=(result[existing][0],result[existing][1],width)
    return result

def poppler_labels():
    root=ET.fromstring(subprocess.check_output(['pdftotext','-bbox',str(ROOT/'docs/sources/inner-sphere-3025.pdf'),'-']))
    words=[]
    for w in root.findall('.//{http://www.w3.org/1999/xhtml}word'):
        a={k:float(v) for k,v in w.attrib.items()}
        if 5.4<a['yMax']-a['yMin']<5.7: words.append([a['xMin'],a['yMin'],a['xMax'],a['yMax'],w.text])
    lines=[]
    for w in sorted(words,key=lambda w:(round(w[1],3),w[0])):
        if lines and abs(w[1]-lines[-1][1])<.03 and -.1<w[0]-lines[-1][2]<2.5:
            lines[-1][2]=w[2];lines[-1][4]+=' '+w[4]
        else: lines.append(w)
    return [{'name':l[4], 'box':l[:4]} for l in lines]

def distance(point,label):
    l=label['box']
    return math.hypot(max(l[0]-point[0],0,point[0]-l[2]),max(l[1]-point[1],0,point[1]-l[3]))

# These are label fragments in the pinned source, verified against text coordinates.
# Most use separate lines; Harrow's Sun and Tellman's Mistake share a baseline.
MULTILINE = [('Kookens','Pleasure Pit'),('Halstead','Station'),('Rigel','Kentarus'),
 ('Alulua','Australis'),('Deneb','Kaitos'),('Small','World'),('New','Rhodes III'),
 ('Epsilon','Indi'),('New','Canton'),('New','Macao'),('New','Sagan'),
 ("Jansen's",'Hold'),('New','Vandenburg'),("Harrow's",'Sun'),("Tellman's",'Mistake')]
# New is an orphan text fragment with no dot. Flannagan's Nebulea and Dark
# Nebula are regions; the Pleiades annotation encloses three individually named
# stars. Other named clusters have a center symbol and remain destinations.
ANNOTATIONS = {'Dark','Nebula',"Flannagan's Nebulea",'New',
 'Pleiades Cluster','100','50','40','(60)'}

def prepare_names(names):
    names=[dict(n) for n in names]
    for first,last in MULTILINE:
        pairs=[(a,b) for a in names for b in names if a is not b and a['name']==first and b['name']==last and math.dist(a['box'][:2],b['box'][:2])<30]
        assert len(pairs)==1,(first,last,pairs)
        a,b=pairs[0]
        a['name']+=' '+b['name']
        a['box']=[min(a['box'][0],b['box'][0]),min(a['box'][1],b['box'][1]),max(a['box'][2],b['box'][2]),max(a['box'][3],b['box'][3])]
        names.remove(b)
    # Defend against repeated PDF text at the same position (e.g. Fomalhaut).
    # Poppler normally suppresses those duplicate painted glyphs already.
    kept=[]
    for n in names:
        if not any(n['name']==k['name'] and math.dist(n['box'][:2],k['box'][:2])<.1 for k in kept):kept.append(n)
    return [n for n in kept if n['name'] not in ANNOTATIONS]

def hungarian(cost):
    """Minimum-cost perfect matching, with deterministic iteration order."""
    n=len(cost);m=len(cost[0]);u=[0.]*(n+1);v=[0.]*(m+1);p=[0]*(m+1);way=[0]*(m+1)
    assert n<=m
    for i in range(1,n+1):
        p[0]=i;j0=0;minimum=[math.inf]*(m+1);used=[False]*(m+1)
        while True:
            used[j0]=True;i0=p[j0];delta=math.inf;j1=0;row=cost[i0-1]
            for j in range(1,m+1):
                if used[j]:continue
                cur=row[j-1]-u[i0]-v[j]
                if cur<minimum[j]:minimum[j]=cur;way[j]=j0
                if minimum[j]<delta:delta=minimum[j];j1=j
            for j in range(m+1):
                if used[j]:u[p[j]]+=delta;v[j]-=delta
                else:minimum[j]-=delta
            j0=j1
            if p[j0]==0:break
        while True:
            j1=way[j0];p[j0]=p[j1];j0=j1
            if j0==0:break
    assignment=[-1]*n
    for j in range(1,m+1):
        if p[j]:assignment[p[j]-1]=j-1
    return assignment

def write_catalog(points,names):
    # Retain named clusters with their own central symbol; the Pleiades region
    # label surrounds three individually named systems and is excluded above.
    names=prepare_names(names)
    assert len(points)==len(names)==2153,(len(points),len(names))
    # Distinctive capital rings disambiguate labels in dense portions of the map.
    # Names and positions are pinned to this source, not inferred from faction lore.
    # Additional pins below trace the chart's leader lines; they correct the
    # Harrow/Bergman and Castor/Devil's Rock proximity ambiguities.
    capitals = {
      (722.46,236.30): 'Oberon VI', (1575.46,681.20): 'Alpheratz',
      (672.81,1442.12): 'Canopus IV', (581.39,682.76): 'Tharkad',
      (1163.49,463.14): 'Pesht', (1142.99,1436.57): 'Taurus',
      (1109.99,545.66): 'Luthien', (611.84,1121.00): 'Atreus',
      (337.68,986.35): 'Circinus', (931.74,352.24): 'Rasalhague',
      (819.79,1331.35): 'Andurien', (1198.10,1305.88): 'New Syrtis',
      (855.82,456.26): 'Tamar', (623.87,672.33): 'Donegal',
      (1049.38,706.71): 'Benjamin', (894.12,872.88): 'Dieron',
      (1338.28,652.04): 'Galedon V', (771.26,1187.17): 'Oriente',
      (666.01,1143.56): 'Regulus', (887.13,1212.50): 'Sian',
      (1247.31,1047.47): 'New Avalon', (1029.43,1191.27): 'St. Ives',
      (1020.02,952.18): 'Tikonov', (930.15,1132.63): 'Capella',
      (953.59,1108.64): 'Sarna', (798.02,822.19): 'Skye',
      (766.70,998.19): 'Marik', (877.20,893.54): 'Terra',
      (1200.40,902.54): 'Robinson', (267.20,1140.90): 'Illyria',
      (1800.35,1217.81): 'Tortuga Prime', (218.49,1151.64): 'Lothario',
      (313.06,1307.95): 'Alphard',
    }
    capitals.update({
      (1138.27,1432.51): 'Ishtar', (1147.84,1432.00): 'Jamestown',
      (1143.06,1428.48): 'Samantha', (1112.57,1431.25): "MacLeod's Land",
      (1132.71,1418.90): "Jansen's Hold", (1176.24,1341.25): 'Electra',
      (1175.15,1342.35): 'Maia', (1467.90,1379.38): 'Pirates haven',
      (702.37,757.20): 'Halmyre Deans', (852.51,201.05): 'Nyserta',
      (1240.85,794.89): "Harrow's Sun", (1246.59,789.50): "Bergman's Planet",
      (844.52,907.97): 'Graham IV', (833.39,915.15): 'Pollux',
      (821.05,916.72): 'Castor', (826.93,914.79): "Devil's Rock",

    })
    anchors={}
    for xy,name in capitals.items():
        pi=min(range(len(points)),key=lambda i:math.dist(points[i][:2],xy))
        assert math.dist(points[pi][:2],xy)<.02,(xy,points[pi])
        candidates=[i for i,n in enumerate(names) if n['name']==name]
        ni=min(candidates,key=lambda i:distance(points[pi],names[i]))
        anchors[pi]=ni
    reverse={ni:pi for pi,ni in anchors.items()}
    costs=[[distance(p,n) if (i not in anchors or anchors[i]==j) and (j not in reverse or reverse[j]==i) else 1e12
            for j,n in enumerate(names)] for i,p in enumerate(points)]
    assignment=hungarian(costs)
    assert sorted(assignment)==list(range(len(names))),'Every source name must match exactly one marker.'
    systems=[]
    for p,index in zip(points,assignment):
        label=names[index]
        x,y,w=p;a,b,c,d=label['box']
        systems.append({'id':f'pdf-{round(x*10000)}-{round(y*10000)}','name':label['name'],'x':round(x*SCALE,6),'y':round(y*SCALE,6),'labelBounds':[round(a*SCALE,6),round(b*SCALE,6),round((c-a)*SCALE,6),round((d-b)*SCALE,6)]})
    reviewed_names=set(capitals.values())
    displaced=[{'id':system['id'],'name':system['name'],'labelDistancePdfPoints':round(distance(point,names[index]),4)} for point,index,system in zip(points,assignment,systems) if distance(point,names[index])>7]
    audit=[item for item in displaced if item['name'] not in reviewed_names]
    assert not audit,('Unreviewed displaced labels',audit)

    assert len({item['id'] for item in systems})==len(systems)
    assert all(0<item['x']<7000 and 0<item['y']<6000 and item['name'] and item['labelBounds'][2]>0 and item['labelBounds'][3]>0 for item in systems)
    data={'source':'docs/sources/inner-sphere-3025.pdf','sourceSha256':SOURCE_SHA256,'imageWidth':7000,'imageHeight':6000,'pixelsPerLightYear':PIXELS_PER_LIGHT_YEAR,'excludedAnnotations':sorted(ANNOTATIONS),'associationAudit':{'method':'One-to-one minimum total label-rectangle distance, constrained by source capital symbols and visually traced leader lines.','sourceLabelCount':2177,'mergedNameFragments':15,'sourceVerifiedAnchors':len(anchors),'allAssociationsVisuallyVerified':False,'reviewedDisplacedLabels':[item for item in displaced if item['name'] in reviewed_names],'reviewCandidates':audit},'systems':sorted(systems,key=lambda s:s['id'])}
    rendered=json.dumps(data,indent=2,ensure_ascii=False)+'\n'
    target=ROOT/'Data/star-systems.json'
    if '--check' in sys.argv:
        assert target.read_text()==rendered,'Catalog differs; regenerate with this extractor.'
        print('Catalog matches reproducible extraction.')
    else:
        target.write_text(rendered)
        print('Wrote',len(systems),'systems;',len(displaced),'displaced labels reviewed;',len(audit),'pending review.')

def main():
    _,content=read_source()
    points=markers(content); names=poppler_labels()
    assert len(points)==2153 and len(names)==2177
    print('Source:',len(points),'marker centers;',len(names),'label lines.')
    if '--inspect' in sys.argv:
        for item in names:
            if len(item['name']) < 3 or item['name'] in ('New','Small','World','Epsilon','Indi','Harrow’s','Harrow\'s','Sun','Station','Hold'):
                print(item)
        return
    write_catalog(points,names)

if __name__=='__main__':main()
