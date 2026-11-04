"""Offline regressions for the planetary climate import."""
import importlib.util
import json
import pathlib
import sys
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('system_climate_importer', ROOT / 'scripts/import-system-climate.py')
IMPORTER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = IMPORTER
SPEC.loader.exec_module(IMPORTER)

SYSTEM = """
id: Example
xcood: 1.5
ycood: -2.0
primarySlot: {source: book, value: 2}
planet:
  - name: Inner
    sysPos: 1
    pressure: VACUUM
    atmosphere: NONE
    temperature: 400
    water: 0
  - name: {source: canon, value: Example}
    sysPos: {source: book, value: 2}
    pressure: {source: book, value: THIN}
    atmosphere: BREATHABLE
    temperature: {source: book, value: 12}
    water: 65
    event:
      - {date: '2800-01-01', population: 1000.0, socioIndustrial: C-C-C-C-C}
      - {date: '3025-06-01', population: 2000.0}
      - {date: '3050-01-01', population: 9000.0, socioIndustrial: A-A-A-A-A}
"""


class ClimateImportChecks(unittest.TestCase):
    def test_primary_world_and_values_at_campaign_start(self):
        with tempfile.TemporaryDirectory() as temp:
            (pathlib.Path(temp) / 'canon_systems').mkdir()
            (pathlib.Path(temp) / 'canon_systems/Example.yml').write_text(SYSTEM)
            [(ident, x, y, climate)] = IMPORTER.read_source(pathlib.Path(temp))
        self.assertEqual((ident, x, y), ('Example', 1.5, -2.0))
        self.assertEqual(climate, dict(pressure='THIN', atmosphere='BREATHABLE', temperature=12, water=65,
                                       population=2000, socioIndustrial='C-C-C-C-C'))

    def test_affine_fit_recovers_chart_transform(self):
        transform = ((4.8, 0.01, 3000.0), (-0.02, -4.8, 3100.0))
        points = [(x, y) for x in (-300, 0, 120, 450) for y in (-200, 30, 600)]
        fitted = IMPORTER.fit_affine([(p, IMPORTER.apply(transform, *p)) for p in points])
        for actual, expected in zip(fitted, transform):
            for a, e in zip(actual, expected):
                self.assertAlmostEqual(a, e, places=6)

    def test_committed_climate_covers_every_chart_system(self):
        chart = json.loads((ROOT / 'Data/star-systems.json').read_text())['systems']
        climate = json.loads((ROOT / 'Data/system-climate.json').read_text())
        self.assertEqual([s['id'] for s in climate['systems']], [s['id'] for s in chart])
        self.assertLessEqual(climate['medianFitLightYears'], IMPORTER.MAX_MEDIAN_FIT_LIGHT_YEARS)
        for system in climate['systems']:
            self.assertLessEqual(system['distanceLightYears'], IMPORTER.MAX_MATCH_LIGHT_YEARS)
            self.assertIsNotNone(system['temperature'])
            self.assertIsNotNone(system['water'])


if __name__ == '__main__':
    unittest.main()
