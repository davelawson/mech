#!/usr/bin/env python3
"""Import each chart system's primary-world climate and 3025 owner from pinned MegaMek Data planetary systems.

Requires Python 3 and PyYAML; without --source-dir it also needs git and a network connection.
Systems are matched by position, not name: one affine fit from MegaMek light-year coordinates
to chart pixels, built from unique name matches, places every chart symbol on its source system.
Climate is recorded raw; the game derives biomes from it. Owners go to a separate file,
Data/system-factions.json, with the names the owning factions used at the campaign start.
"""
import argparse, collections, json, math, pathlib, statistics, subprocess, sys, tempfile
import yaml

ROOT = pathlib.Path(__file__).resolve().parents[1]
DEFAULT_REVISION = '71fefad545e254b4a224f0d232eaef24473db1ee'
SYSTEMS_PATH = 'data/universe/planetary_systems'
FACTIONS_PATH = 'data/universe/factions'
CAMPAIGN_YEAR = '3025'
CAMPAIGN_START = '3025-01-01'
# A chart symbol may sit up to this far from its source system (multi-world clusters such as Calish II/III).
MAX_MATCH_LIGHT_YEARS = 3.0
MAX_MEDIAN_FIT_LIGHT_YEARS = 0.05
OUTLIER_PIXELS = 10.0


def value(field):
    return field.get('value') if isinstance(field, dict) else field


def norm(name):
    return name.lower().replace('’', "'").strip()


def owner(data, planet):
    """The faction codes holding the system on the campaign start date (several when contested), or None."""
    events = sorted((data.get('event') or []) + (planet.get('event') or []), key=lambda e: str(e.get('date')))
    held = None
    for event in events:
        if str(event.get('date')) <= CAMPAIGN_START and 'faction' in event:
            held = value(event['faction'])
    return list(held) if held else None


def faction_names(factions_dir):
    """Faction code -> name in effect at the campaign start."""
    names = {}
    for path in sorted(factions_dir.glob('*.yml')):
        data = yaml.safe_load(path.read_text())
        name = data['name']
        for year, renamed in sorted((data.get('nameChanges') or {}).items()):
            if int(year) <= int(CAMPAIGN_YEAR):
                name = renamed
        names[data['key']] = name
    return names


def read_source(source_dir):
    """Return (id, x, y, primary climate, owner) for each canon system, using values in effect at the campaign start."""
    systems = []
    for path in sorted((source_dir / 'canon_systems').glob('*.yml')):
        data = yaml.safe_load(path.read_text())
        slot = value(data.get('primarySlot'))
        planet = next((p for p in data.get('planet') or [] if value(p.get('sysPos')) == slot), {})
        population = usilr = None
        for event in planet.get('event') or []:
            if str(event.get('date'))[:4] <= CAMPAIGN_YEAR:
                population = value(event.get('population', population))
                usilr = value(event.get('socioIndustrial', usilr))
        climate = dict(pressure=value(planet.get('pressure')), atmosphere=value(planet.get('atmosphere')),
                       temperature=value(planet.get('temperature')), water=value(planet.get('water')),
                       population=int(population) if population is not None else None, socioIndustrial=usilr)
        systems.append((data['id'], float(data['xcood']), float(data['ycood']), climate, owner(data, planet)))
    if not systems:
        raise SystemExit(f'No canon systems under {source_dir}')
    return systems


def solve(matrix, rhs):
    rows = [row[:] + [rhs[i]] for i, row in enumerate(matrix)]
    for c in range(3):
        pivot = max(range(c, 3), key=lambda r: abs(rows[r][c]))
        rows[c], rows[pivot] = rows[pivot], rows[c]
        for r in range(3):
            if r != c:
                f = rows[r][c] / rows[c][c]
                rows[r] = [a - f * b for a, b in zip(rows[r], rows[c])]
    return [rows[i][3] / rows[i][i] for i in range(3)]


def fit_affine(pairs):
    """Least-squares (x, y) light-years -> chart pixels; pairs are ((lx, ly), (px, py))."""
    basis = [(lx, ly, 1.0) for (lx, ly), _ in pairs]
    normal = [[sum(b[i] * b[j] for b in basis) for j in range(3)] for i in range(3)]
    return tuple(solve(normal, [sum(b[i] * p[axis] for b, (_, p) in zip(basis, pairs)) for i in range(3)]) for axis in (0, 1))


def apply(transform, x, y):
    return tuple(c[0] * x + c[1] * y + c[2] for c in transform)


def fit_chart(source, chart):
    """Fit on names unique to both sides, dropping mislabelled outliers, then refit."""
    by_name = collections.defaultdict(list)
    for system in source:
        by_name[norm(system[0])].append(system)
    chart_names = collections.Counter(norm(s['name']) for s in chart)
    pairs = [((m[1], m[2]), (s['x'], s['y'])) for s in chart
             if chart_names[norm(s['name'])] == 1 and len(by_name[norm(s['name'])]) == 1
             for m in by_name[norm(s['name'])]]
    for _ in range(3):
        transform = fit_affine(pairs)
        residuals = [math.dist(apply(transform, *ly), px) for ly, px in pairs]
        limit = max(5 * statistics.median(residuals), OUTLIER_PIXELS)
        pairs = [p for p, r in zip(pairs, residuals) if r < limit]
    return transform, statistics.median(residuals), len(pairs)


def build(source_dir, factions_dir, revision):
    stars = json.loads((ROOT / 'Data/star-systems.json').read_text())
    chart, ppl = stars['systems'], stars['pixelsPerLightYear']
    source = read_source(source_dir)
    transform, median_px, fitted = fit_chart(source, chart)
    if median_px / ppl > MAX_MEDIAN_FIT_LIGHT_YEARS:
        raise SystemExit(f'Chart fit median residual {median_px / ppl:.3f} ly exceeds {MAX_MEDIAN_FIT_LIGHT_YEARS}; review the source.')
    placed = [(apply(transform, x, y), ident, climate, held) for ident, x, y, climate, held in source]
    systems, owners, renamed = [], [], []
    for star in chart:
        point, ident, climate, held = min(placed, key=lambda p: math.dist(p[0], (star['x'], star['y'])))
        distance = math.dist(point, (star['x'], star['y'])) / ppl
        if distance > MAX_MATCH_LIGHT_YEARS:
            raise SystemExit(f"{star['name']} ({star['id']}) is {distance:.1f} ly from the nearest source system {ident}.")
        if not norm(ident).startswith(norm(star['name'])):
            renamed.append((star['name'], ident, distance))
        systems.append(dict(id=star['id'], name=star['name'], source=ident, distanceLightYears=round(distance, 2), **climate))
        owners.append(dict(id=star['id'], name=star['name'], factions=held or []))
    catalog = dict(
        source=f'https://github.com/MegaMek/mm-data/tree/{revision}/{SYSTEMS_PATH}/canon_systems',
        revision=revision, license='CC BY-NC-SA 4.0', attribution='MegaMek Data (C) 2025-2026 The MegaMek Team',
        campaignYear=int(CAMPAIGN_YEAR), fittedSystems=fitted,
        medianFitLightYears=round(median_px / ppl, 4), systems=systems)
    names = faction_names(factions_dir)
    used = sorted({code for entry in owners for code in entry['factions']})
    missing = [code for code in used if code not in names]
    if missing:
        raise SystemExit(f'Owner codes without a faction definition: {missing}')
    factions = dict(
        source=f'https://github.com/MegaMek/mm-data/tree/{revision}/{SYSTEMS_PATH}/canon_systems',
        revision=revision, license='CC BY-NC-SA 4.0', attribution='MegaMek Data (C) 2025-2026 The MegaMek Team',
        campaignStart=CAMPAIGN_START, factions={code: names[code] for code in used}, systems=owners)
    return catalog, factions, renamed


def fetch(revision, into):
    run = lambda *args: subprocess.run(['git', *args], cwd=into, check=True)
    run('clone', '-q', '--filter=blob:none', '--no-checkout', '--sparse', 'https://github.com/MegaMek/mm-data.git', '.')
    run('sparse-checkout', 'set', SYSTEMS_PATH, FACTIONS_PATH)
    run('checkout', '-q', revision)
    return pathlib.Path(into) / SYSTEMS_PATH, pathlib.Path(into) / FACTIONS_PATH


def write(path, catalog, rows_key):
    rows_data = catalog.pop(rows_key)
    header = json.dumps(catalog, indent=1, ensure_ascii=False)[:-2]
    rows = ',\n'.join('  ' + json.dumps(s, ensure_ascii=False) for s in rows_data)
    path.write_text(f'{header},\n "{rows_key}": [\n{rows}\n ]\n}}\n')
    catalog[rows_key] = rows_data


def report(catalog, factions, renamed):
    systems = catalog['systems']
    print(f"{len(systems)} chart systems matched; fit on {catalog['fittedSystems']}, median {catalog['medianFitLightYears']} ly")
    for field in ('pressure', 'atmosphere'):
        print(field, dict(collections.Counter(s[field] for s in systems).most_common()))
    for field in ('temperature', 'water', 'population', 'socioIndustrial'):
        print(f'missing {field}:', sum(s[field] is None for s in systems))
    owned = collections.Counter('/'.join(s['factions']) or '-' for s in factions['systems'])
    print('owners', dict(owned.most_common()))
    print(f'{len(renamed)} chart names differ from their source system:')
    for name, ident, distance in renamed:
        print(f'  {name} -> {ident} ({distance:.2f} ly)')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--revision', default=DEFAULT_REVISION)
    parser.add_argument('--source-dir', type=pathlib.Path, help='Read a checked-out mm-data universe directory (holding planetary_systems and factions) instead of using the network')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'Data/system-climate.json')
    parser.add_argument('--factions-output', type=pathlib.Path, default=ROOT / 'Data/system-factions.json')
    args = parser.parse_args()
    with tempfile.TemporaryDirectory() as temp:
        systems_dir, factions_dir = (args.source_dir / 'planetary_systems', args.source_dir / 'factions') if args.source_dir else fetch(args.revision, temp)
        catalog, factions, renamed = build(systems_dir, factions_dir, args.revision)
    write(args.output, catalog, 'systems')
    write(args.factions_output, factions, 'systems')
    report(catalog, factions, renamed)


if __name__ == '__main__':
    sys.exit(main())
