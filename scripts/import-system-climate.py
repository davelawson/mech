#!/usr/bin/env python3
"""Import each chart system's primary-world climate from pinned MegaMek Data planetary systems.

Requires Python 3 and PyYAML; without --source-dir it also needs git and a network connection.
Systems are matched by position, not name: one affine fit from MegaMek light-year coordinates
to chart pixels, built from unique name matches, places every chart symbol on its source system.
Climate is recorded raw; the game derives biomes from it.
"""
import argparse, collections, json, math, pathlib, statistics, subprocess, sys, tempfile
import yaml

ROOT = pathlib.Path(__file__).resolve().parents[1]
DEFAULT_REVISION = '71fefad545e254b4a224f0d232eaef24473db1ee'
SYSTEMS_PATH = 'data/universe/planetary_systems'
CAMPAIGN_YEAR = '3025'
# A chart symbol may sit up to this far from its source system (multi-world clusters such as Calish II/III).
MAX_MATCH_LIGHT_YEARS = 3.0
MAX_MEDIAN_FIT_LIGHT_YEARS = 0.05
OUTLIER_PIXELS = 10.0


def value(field):
    return field.get('value') if isinstance(field, dict) else field


def norm(name):
    return name.lower().replace('’', "'").strip()


def read_source(source_dir):
    """Return (id, x, y, primary climate) for each canon system, using values in effect at the campaign start."""
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
        systems.append((data['id'], float(data['xcood']), float(data['ycood']), climate))
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


def build(source_dir, revision):
    stars = json.loads((ROOT / 'Data/star-systems.json').read_text())
    chart, ppl = stars['systems'], stars['pixelsPerLightYear']
    source = read_source(source_dir)
    transform, median_px, fitted = fit_chart(source, chart)
    if median_px / ppl > MAX_MEDIAN_FIT_LIGHT_YEARS:
        raise SystemExit(f'Chart fit median residual {median_px / ppl:.3f} ly exceeds {MAX_MEDIAN_FIT_LIGHT_YEARS}; review the source.')
    placed = [(apply(transform, x, y), ident, climate) for ident, x, y, climate in source]
    systems, renamed = [], []
    for star in chart:
        point, ident, climate = min(placed, key=lambda p: math.dist(p[0], (star['x'], star['y'])))
        distance = math.dist(point, (star['x'], star['y'])) / ppl
        if distance > MAX_MATCH_LIGHT_YEARS:
            raise SystemExit(f"{star['name']} ({star['id']}) is {distance:.1f} ly from the nearest source system {ident}.")
        if not norm(ident).startswith(norm(star['name'])):
            renamed.append((star['name'], ident, distance))
        systems.append(dict(id=star['id'], name=star['name'], source=ident, distanceLightYears=round(distance, 2), **climate))
    catalog = dict(
        source=f'https://github.com/MegaMek/mm-data/tree/{revision}/{SYSTEMS_PATH}/canon_systems',
        revision=revision, license='CC BY-NC-SA 4.0', attribution='MegaMek Data (C) 2025-2026 The MegaMek Team',
        campaignYear=int(CAMPAIGN_YEAR), fittedSystems=fitted,
        medianFitLightYears=round(median_px / ppl, 4), systems=systems)
    return catalog, renamed


def fetch(revision, into):
    run = lambda *args: subprocess.run(['git', *args], cwd=into, check=True)
    run('clone', '-q', '--filter=blob:none', '--no-checkout', '--sparse', 'https://github.com/MegaMek/mm-data.git', '.')
    run('sparse-checkout', 'set', SYSTEMS_PATH)
    run('checkout', '-q', revision)
    return pathlib.Path(into) / SYSTEMS_PATH


def report(catalog, renamed):
    systems = catalog['systems']
    print(f"{len(systems)} chart systems matched; fit on {catalog['fittedSystems']}, median {catalog['medianFitLightYears']} ly")
    for field in ('pressure', 'atmosphere'):
        print(field, dict(collections.Counter(s[field] for s in systems).most_common()))
    for field in ('temperature', 'water', 'population', 'socioIndustrial'):
        print(f'missing {field}:', sum(s[field] is None for s in systems))
    print(f'{len(renamed)} chart names differ from their source system:')
    for name, ident, distance in renamed:
        print(f'  {name} -> {ident} ({distance:.2f} ly)')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--revision', default=DEFAULT_REVISION)
    parser.add_argument('--source-dir', type=pathlib.Path, help='Read a checked-out planetary_systems directory instead of using the network')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'Data/system-climate.json')
    args = parser.parse_args()
    with tempfile.TemporaryDirectory() as temp:
        source_dir = args.source_dir or fetch(args.revision, temp)
        catalog, renamed = build(source_dir, args.revision)
    systems = catalog.pop('systems')
    header = json.dumps(catalog, indent=1, ensure_ascii=False)[:-2]
    rows = ',\n'.join('  ' + json.dumps(s, ensure_ascii=False) for s in systems)
    args.output.write_text(f'{header},\n "systems": [\n{rows}\n ]\n}}\n')
    catalog['systems'] = systems
    report(catalog, renamed)


if __name__ == '__main__':
    sys.exit(main())
