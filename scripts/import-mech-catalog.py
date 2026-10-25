#!/usr/bin/env python3
"""Import mechanical stock-unit facts; generated JSON carries its own source/license metadata."""
import argparse, collections, concurrent.futures, json, pathlib, re, urllib.parse, urllib.request
ROOT = pathlib.Path(__file__).resolve().parents[1]
UNITS = [('firestarter','Firestarter FS9-H'),('jenner','Jenner JR7-D'),('panther','Panther PNT-9R'),('griffin','Griffin GRF-1N'),('shadow-hawk','Shadow Hawk SHD-2H'),('wolverine','Wolverine WVR-6R'),('catapult','Catapult CPLT-C1'),('crusader','Crusader CRD-3R'),('thunderbolt','Thunderbolt TDR-5S'),('awesome','Awesome AWS-8Q'),('victor','Victor VTR-9B'),('zeus','Zeus ZEU-6S')]
# Heat, damage, minimum/short/medium/long ranges (hexes), shots per one-ton bin.
WEAPONS = {'Medium Laser':(3,'5',0,3,6,9,0),'Small Laser':(1,'3',0,1,2,3,0),'Large Laser':(8,'8',0,5,10,15,0),'PPC':(10,'10',3,6,12,18,0),'Flamer':(3,'2 / 2 heat',0,1,2,3,0),'Machine Gun':(0,'2',0,1,2,3,200),'AC/5':(1,'5',3,6,12,18,20),'AC/10':(3,'10',0,5,10,15,10),'AC/20':(7,'20',0,3,6,9,5),'LRM 5':(2,'1 / missile',6,7,14,21,24),'LRM 10':(4,'1 / missile',6,7,14,21,12),'LRM 15':(5,'1 / missile',6,7,14,21,8),'SRM 2':(2,'2 / missile',0,3,6,9,50),'SRM 4':(3,'2 / missile',0,3,6,9,25),'SRM 6':(4,'2 / missile',0,3,6,9,15)}
LOCATIONS = [('Head','HD'),('CenterTorso','CT'),('LeftTorso','LT'),('RightTorso','RT'),('LeftArm','LA'),('RightArm','RA'),('LeftLeg','LL'),('RightLeg','RL')]
STRUCTURE = {35:(11,8,6,8),55:(18,13,9,13),65:(21,15,10,15),80:(25,17,13,17)}
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--revision', default='71fefad545e254b4a224f0d232eaef24473db1ee')
revision=parser.parse_args().revision
def read_unit(unit):
    ident,name=unit
    url='https://raw.githubusercontent.com/MegaMek/mm-data/'+revision+'/data/mekfiles/meks/3039u/'+urllib.parse.quote(name+'.mtf')
    text=urllib.request.urlopen(url).read().decode()
    fields=dict(re.findall(r'^([^:\n]+):([^\n]*)$',text,re.M))
    mass=int(fields['mass']); walk=int(fields['walk mp']); jump=int(fields['jump mp']); ct,side,arm,leg=STRUCTURE[mass]
    locations=[]; criticals={}
    for loc,abbr in LOCATIONS:
        spaced=re.sub(r'(?<!^)([A-Z])',r' \1',loc)
        match=re.search(r'^'+spaced+r':\n(.*?)(?:\n\n|\Z)',text,re.M|re.S)
        criticals[loc]=[x.strip() for x in match[1].splitlines() if x.strip()!='-Empty-'] if match else []
        rear={'CT':'RTC','LT':'RTL','RT':'RTR'}.get(abbr)
        locations.append(dict(Location=loc,Armor=int(fields[abbr+' armor']),RearArmor=int(fields.get(rear+' armor','0')) if rear else 0,Structure=3 if abbr=='HD' else ct if abbr=='CT' else side if abbr in ('LT','RT') else arm if abbr in ('LA','RA') else leg))
    weapon_lines=re.search(r'^Weapons:\d+\n(.*?)\n\n',text,re.M|re.S)[1].splitlines()
    weapons=[]
    for i,line in enumerate(weapon_lines):
        weapon,where=line.split(', ',1); loc=where.replace(' ',''); values=WEAPONS[weapon]
        rear=any(x.endswith('(R)') and (x.startswith(weapon) or weapon.replace('AC/','Autocannon/') in x) for x in criticals[loc])
        # If identical mounts occupy the location, the last is the rear mount.
        rear=rear and not any(x==line for x in weapon_lines[i+1:])
        weapons.append(dict(Id='weapon-'+str(i),Name=weapon,Location=loc,Rear=rear,Heat=values[0],Damage=values[1],Minimum=values[2],Short=values[3],Medium=values[4],Long=values[5],AmmoType=weapon if values[6] else None))
    components=[]; ammo=[]
    for loc,items in criticals.items():
        for item,count in collections.Counter(items).items():
            if item.startswith('IS Ammo'):
                kind=item.removeprefix('IS Ammo ').replace('LRM-','LRM ').replace('SRM-','SRM ')
                if kind=='MG - Full': kind='Machine Gun'
                if kind=='MG - Half': kind='Machine Gun'
                shots=WEAPONS[kind][6]//(2 if 'Half' in item else 1)
                for n in range(count): ammo.append(dict(Id='ammo-'+str(len(ammo)),Name=kind+' ammunition',Location=loc,AmmoType=kind,Shots=shots))
            elif item in ('Life Support','Sensors','Cockpit','Fusion Engine','Gyro','Shoulder','Upper Arm Actuator','Lower Arm Actuator','Hand Actuator','Hip','Upper Leg Actuator','Lower Leg Actuator','Foot Actuator','Heat Sink','Jump Jet'):
                components.append(dict(Id='component-'+str(len(components)),Name=item,Location=loc,Slots=count))
    return dict(Id=ident,Chassis=fields['chassis'],Variant=fields['model'],Tonnage=mass,WeightClass='Light' if mass<=35 else 'Medium' if mass<=55 else 'Heavy' if mass<=75 else 'Assault',Introduced=int(fields['era']),Role=fields.get('role',''),Walk=walk,Run=(walk*3+1)//2,Jump=jump,HeatSinks=int(fields['heat sinks'].split()[0]),Engine=fields['engine'],Locations=locations,Weapons=weapons,Components=components,Ammo=ammo,Source=url)
with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool: units=list(pool.map(read_unit,UNITS))
result=dict(Source='MegaMek/mm-data',Revision=revision,License='CC BY-NC-SA 4.0',Attribution='Mechanical stock-unit data adapted from MegaMek Data (C) 2025-2026 by The MegaMek Team. BattleTech trademarks and underlying rights remain with their respective owners.',LicenseUrl='https://creativecommons.org/licenses/by-nc-sa/4.0/',Mechs=units)
(ROOT/'Data'/'mech-catalog.json').write_text(json.dumps(result,indent=2)+'\n')
print('Imported',len(units),'stock 3025 variants from',revision)
for u in units: print(u['Chassis'],u['Variant'],u['Tonnage'],sum(x['Armor']+x['RearArmor'] for x in u['Locations']),len(u['Weapons']),[(x['AmmoType'],x['Shots']) for x in u['Ammo']])
