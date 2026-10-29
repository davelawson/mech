#!/usr/bin/env python3
"""Import mechanical stock-unit facts; generated JSON carries its own source/license metadata."""
import argparse, collections, concurrent.futures, json, pathlib, re, urllib.parse, urllib.request
from dataclasses import dataclass
ROOT = pathlib.Path(__file__).resolve().parents[1]
UNITS = [('firestarter','Firestarter FS9-H'),('jenner','Jenner JR7-D'),('panther','Panther PNT-9R'),('griffin','Griffin GRF-1N'),('shadow-hawk','Shadow Hawk SHD-2H'),('wolverine','Wolverine WVR-6R'),('catapult','Catapult CPLT-C1'),('crusader','Crusader CRD-3R'),('thunderbolt','Thunderbolt TDR-5S'),('awesome','Awesome AWS-8Q'),('victor','Victor VTR-9B'),('zeus','Zeus ZEU-6S'),('atlas','Atlas AS7-D')]
ADDITIONAL = json.loads((ROOT / 'Data' / 'additional-mechs.json').read_text())
UNIT_PATHS = {unit['id']: unit['path'] for unit in ADDITIONAL}
UNITS += [(unit['id'], unit['unit']) for unit in ADDITIONAL]
# Stock profiles only: real-time cooldowns are authored in CombatRules, not imported.
@dataclass(frozen=True)
class WeaponProfile:
    kind: str
    heat: int
    per_projectile: int
    projectiles: int
    minimum: int
    short: int
    medium: int
    long: int
    shots: int = 0
    target_heat: int = 0

WEAPONS = {
    'Medium Laser': WeaponProfile(kind='MediumLaser', heat=3, per_projectile=5, projectiles=1, minimum=0, short=3, medium=6, long=9, shots=0, target_heat=0),
    'Flamer': WeaponProfile(kind='Flamer', heat=3, per_projectile=2, projectiles=1, minimum=0, short=1, medium=2, long=3, shots=0, target_heat=2),
    'Machine Gun': WeaponProfile(kind='MachineGun', heat=0, per_projectile=2, projectiles=1, minimum=0, short=1, medium=2, long=3, shots=200, target_heat=0),
    'SRM 4': WeaponProfile(kind='SRM4', heat=3, per_projectile=2, projectiles=4, minimum=0, short=3, medium=6, long=9, shots=25, target_heat=0),
    'PPC': WeaponProfile(kind='PPC', heat=10, per_projectile=10, projectiles=1, minimum=3, short=6, medium=12, long=18, shots=0, target_heat=0),
    'LRM 10': WeaponProfile(kind='LRM10', heat=4, per_projectile=1, projectiles=10, minimum=6, short=7, medium=14, long=21, shots=12, target_heat=0),
    'AC/5': WeaponProfile(kind='AC5', heat=1, per_projectile=5, projectiles=1, minimum=3, short=6, medium=12, long=18, shots=20, target_heat=0),
    'LRM 5': WeaponProfile(kind='LRM5', heat=2, per_projectile=1, projectiles=5, minimum=6, short=7, medium=14, long=21, shots=24, target_heat=0),
    'SRM 2': WeaponProfile(kind='SRM2', heat=2, per_projectile=2, projectiles=2, minimum=0, short=3, medium=6, long=9, shots=50, target_heat=0),
    'SRM 6': WeaponProfile(kind='SRM6', heat=4, per_projectile=2, projectiles=6, minimum=0, short=3, medium=6, long=9, shots=15, target_heat=0),
    'LRM 15': WeaponProfile(kind='LRM15', heat=5, per_projectile=1, projectiles=15, minimum=6, short=7, medium=14, long=21, shots=8, target_heat=0),
    'Large Laser': WeaponProfile(kind='LargeLaser', heat=8, per_projectile=8, projectiles=1, minimum=0, short=5, medium=10, long=15, shots=0, target_heat=0),
    'Small Laser': WeaponProfile(kind='SmallLaser', heat=1, per_projectile=3, projectiles=1, minimum=0, short=1, medium=2, long=3, shots=0, target_heat=0),
    'AC/20': WeaponProfile(kind='AC20', heat=7, per_projectile=20, projectiles=1, minimum=0, short=3, medium=6, long=9, shots=5, target_heat=0),
    'LRM 20': WeaponProfile(kind='LRM20', heat=6, per_projectile=1, projectiles=20, minimum=6, short=7, medium=14, long=21, shots=6, target_heat=0),
    'AC/10': WeaponProfile(kind='AC10', heat=3, per_projectile=10, projectiles=1, minimum=0, short=5, medium=10, long=15, shots=10, target_heat=0),
    'AC/2': WeaponProfile(kind='AC2', heat=1, per_projectile=2, projectiles=1, minimum=4, short=8, medium=16, long=24, shots=45, target_heat=0),
    'ER Large Laser': WeaponProfile(kind='ERLargeLaser', heat=12, per_projectile=8, projectiles=1, minimum=0, short=7, medium=14, long=19, shots=0, target_heat=0),
}
COMPONENTS = {
    'Life Support': 'LifeSupport',
    'Sensors': 'Sensors',
    'Cockpit': 'Cockpit',
    'Fusion Engine': 'FusionEngine',
    'Gyro': 'Gyro',
    'Shoulder': 'Shoulder',
    'Upper Arm Actuator': 'UpperArmActuator',
    'Lower Arm Actuator': 'LowerArmActuator',
    'Hand Actuator': 'HandActuator',
    'Hip': 'Hip',
    'Upper Leg Actuator': 'UpperLegActuator',
    'Lower Leg Actuator': 'LowerLegActuator',
    'Foot Actuator': 'FootActuator',
    'Heat Sink': 'HeatSink',
    'Jump Jet': 'JumpJet',
}
DEFAULT_REVISION = "71fefad545e254b4a224f0d232eaef24473db1ee"
LOCATIONS = [('Head','HD'),('CenterTorso','CT'),('LeftTorso','LT'),('RightTorso','RT'),('LeftArm','LA'),('RightArm','RA'),('LeftLeg','LL'),('RightLeg','RL')]
STRUCTURE = {20:(6,5,3,4),25:(8,6,4,6),30:(10,7,5,7),35:(11,8,6,8),40:(12,10,6,10),45:(14,11,7,11),50:(16,12,8,12),55:(18,13,9,13),60:(20,14,10,14),65:(21,15,10,15),70:(22,15,11,15),75:(23,16,12,16),80:(25,17,13,17),85:(27,18,14,18),90:(29,19,15,19),95:(30,20,16,20),100:(31,21,17,21)}
def source_url(ident, name, revision):
    path = UNIT_PATHS.get(ident, 'data/mekfiles/meks/3039u/' + name + '.mtf')
    return 'https://raw.githubusercontent.com/MegaMek/mm-data/' + revision + '/' + urllib.parse.quote(path)

def parse_unit(text, ident, url):
    """Translate pinned MTF facts. Critical grouping/order determines persistent equipment IDs."""
    try:
        return _parse_unit(text, ident, url)
    except (KeyError, ValueError, TypeError, AttributeError) as error:
        raise ValueError(f'{ident} ({url}): unsupported or malformed stock data: {error}') from error

def _parse_unit(text, ident, url):
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
        rear_count=sum(x.endswith('(R)') and (x.startswith(weapon) or x.startswith(weapon.replace('AC/','Autocannon/'))) for x in criticals[loc])
        rear=sum(x==line for x in weapon_lines[i+1:]) < rear_count
        weapons.append(dict(Id='weapon-'+str(i),Name=weapon,Location=loc,Rear=rear,Heat=values.heat,DamageProfile=dict(Kind='Missile' if values.projectiles > 1 else 'Direct',PerProjectile=values.per_projectile,ProjectileCount=values.projectiles,TargetHeat=values.target_heat),Minimum=values.minimum,Short=values.short,Medium=values.medium,Long=values.long,AmmoKind=values.kind if values.shots else None,Kind=values.kind))
    components=[]; ammo=[]
    for loc,items in criticals.items():
        for item,count in collections.Counter(items).items():
            if item.startswith('IS Ammo'):
                kind=item.removeprefix('IS Ammo ').replace('LRM-','LRM ').replace('SRM-','SRM ')
                if kind=='MG - Full': kind='Machine Gun'
                if kind=='MG - Half': kind='Machine Gun'
                shots=WEAPONS[kind].shots//(2 if 'Half' in item else 1)
                for n in range(count): ammo.append(dict(Id='ammo-'+str(len(ammo)),Name=kind+' ammunition',Location=loc,AmmoKind=WEAPONS[kind].kind,Shots=shots))
            elif item in COMPONENTS:
                components.append(dict(Id='component-'+str(len(components)),Name=item,Location=loc,Slots=count,Kind=COMPONENTS[item],HeatSinkCount=count if item=='Heat Sink' else 0))
    sink_fields = fields['heat sinks'].split()
    if ' '.join(sink_fields[1:]) not in ('Single', 'Double', 'IS Double'):
        raise ValueError('unsupported heat sink type: ' + fields['heat sinks'])
    return dict(Id=ident,Chassis=fields['chassis'],Variant=fields['model'],Tonnage=mass,WeightClass='Light' if mass<=35 else 'Medium' if mass<=55 else 'Heavy' if mass<=75 else 'Assault',Introduced=int(fields['era']),Role=fields.get('role',''),Walk=walk,Run=(walk*3+1)//2,Jump=jump,HeatSinks=int(fields['heat sinks'].split()[0]),Engine=fields['engine'],HeatSinkType=sink_fields[-1],Locations=locations,Weapons=weapons,Components=components,Ammo=ammo,Source=url)
def build_catalog(revision, source_dir=None):
    if not re.fullmatch(r'[a-fA-F0-9]{40}', revision):
        raise ValueError('revision must be a pinned 40-character SHA')
    def read_unit(unit):
        ident, name = unit
        url = source_url(ident, name, revision)
        text = (source_dir / (ident + '.mtf')).read_text() if source_dir else urllib.request.urlopen(url).read().decode()
        return parse_unit(text, ident, url)
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        units = list(pool.map(read_unit, UNITS))
    return dict(SchemaVersion=1, Source='MegaMek/mm-data',Revision=revision,License='CC BY-NC-SA 4.0',Attribution='Mechanical stock-unit data adapted from MegaMek Data (C) 2025-2026 by The MegaMek Team. BattleTech trademarks and underlying rights remain with their respective owners.',LicenseUrl='https://creativecommons.org/licenses/by-nc-sa/4.0/',Mechs=units)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--revision', default=DEFAULT_REVISION)
    parser.add_argument('--source-dir', type=pathlib.Path, help='Read cached ID.mtf files instead of using the network')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'Data' / 'mech-catalog.json')
    args = parser.parse_args()
    result = build_catalog(args.revision, args.source_dir)
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print('Imported', len(result['Mechs']), 'stock variants from', args.revision)

if __name__ == '__main__':
    main()
