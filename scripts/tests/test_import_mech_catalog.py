"""Offline regressions for the stock catalog translation boundary."""
import importlib.util
import json
import pathlib
import sys
import unittest
from unittest.mock import patch

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('mech_catalog_importer', ROOT / 'scripts/import-mech-catalog.py')
IMPORTER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = IMPORTER
SPEC.loader.exec_module(IMPORTER)
FIXTURES = ROOT / 'Tests/Fixtures/mtf'


class CatalogImportChecks(unittest.TestCase):
    def parse(self, ident, text=None):
        name = dict(IMPORTER.UNITS)[ident]
        return IMPORTER.parse_unit(text if text is not None else (FIXTURES / (ident + '.mtf')).read_text(),
                                   ident, IMPORTER.source_url(ident, name, IMPORTER.DEFAULT_REVISION))

    def test_pinned_mechanical_excerpts_reproduce_committed_catalog(self):
        catalog = {m['Id']: m for m in json.loads((ROOT / 'Data/mech-catalog.json').read_text())['Mechs']}
        for ident in ('firestarter', 'atlas', 'crusader', 'mauler'):
            with self.subTest(ident=ident):
                self.assertEqual(self.parse(ident), catalog[ident])

    def test_rear_mounts_preserve_order_and_ids(self):
        atlas = self.parse('atlas')
        rear = [(w['Id'], w['Location'], w['Kind']) for w in atlas['Weapons'] if w['Rear']]
        self.assertEqual(rear, [('weapon-5', 'CenterTorso', 'MediumLaser'), ('weapon-6', 'CenterTorso', 'MediumLaser')])
        self.assertEqual(sum(w['Rear'] for w in self.parse('firestarter')['Weapons']), 1)

    def test_numeric_missiles_and_target_heat(self):
        crusader = self.parse('crusader')
        for kind, per, count in [('LRM15', 1, 15), ('SRM6', 2, 6)]:
            weapon = next(w for w in crusader['Weapons'] if w['Kind'] == kind)
            self.assertEqual(weapon['DamageProfile'], dict(Kind='Missile', PerProjectile=per, ProjectileCount=count, TargetHeat=0))
            self.assertEqual(weapon['AmmoKind'], kind)
        flamer = next(w for w in self.parse('firestarter')['Weapons'] if w['Kind'] == 'Flamer')
        self.assertEqual(flamer['DamageProfile'], dict(Kind='Direct', PerProjectile=2, ProjectileCount=1, TargetHeat=2))
        self.assertIsNone(flamer['AmmoKind'])

    def test_grouped_sinks_preserve_counts_and_internal_remainder(self):
        atlas = self.parse('atlas')
        groups = [c for c in atlas['Components'] if c['Kind'] == 'HeatSink']
        self.assertTrue(any(c['Slots'] == 2 for c in groups))
        self.assertTrue(all(c['Slots'] == c['HeatSinkCount'] for c in groups))
        self.assertEqual(sum(c['HeatSinkCount'] for c in groups), 8)
        mauler = self.parse('mauler')
        self.assertEqual((mauler['HeatSinks'], mauler['HeatSinkType']), (11, 'Double'))
        self.assertFalse(any(c['Kind'] == 'HeatSink' for c in mauler['Components']))

    def test_full_and_half_bins_share_typed_compatibility(self):
        text = (FIXTURES / 'firestarter.mtf').read_text()
        full = next(a for a in self.parse('firestarter', text)['Ammo'] if a['AmmoKind'] == 'MachineGun')
        half = next(a for a in self.parse('firestarter', text.replace('IS Ammo MG - Full', 'IS Ammo MG - Half'))['Ammo']
                    if a['AmmoKind'] == 'MachineGun')
        self.assertEqual((full['Shots'], half['Shots']), (200, 100))
        self.assertEqual({k: v for k, v in full.items() if k != 'Shots'}, {k: v for k, v in half.items() if k != 'Shots'})

    def test_unknown_profiles_fail_with_unit_and_source_context(self):
        text = (FIXTURES / 'firestarter.mtf').read_text()
        for changed in (text.replace('Medium Laser', 'Future Blaster'), text.replace('IS Ammo MG - Full', 'IS Ammo Unknown'),
                        text.replace('10 Single', '10 Future')):
            with self.subTest(changed=changed[-20:]):
                with self.assertRaisesRegex(ValueError, r'firestarter \(https://raw.githubusercontent.com/MegaMek/mm-data/'):
                    self.parse('firestarter', changed)

    def test_cached_generation_is_deterministic_and_network_free(self):
        units = [u for u in IMPORTER.UNITS if u[0] in ('firestarter', 'atlas', 'crusader', 'mauler')]
        with patch.object(IMPORTER, 'UNITS', units), patch.object(IMPORTER.urllib.request, 'urlopen', side_effect=AssertionError('network')):
            first = IMPORTER.build_catalog(IMPORTER.DEFAULT_REVISION, FIXTURES)
            self.assertEqual(first, IMPORTER.build_catalog(IMPORTER.DEFAULT_REVISION, FIXTURES))
        self.assertEqual([m['Id'] for m in first['Mechs']], [u[0] for u in units])
        self.assertEqual((first['SchemaVersion'], first['Revision'], first['License']), (1, IMPORTER.DEFAULT_REVISION, 'CC BY-NC-SA 4.0'))
        with self.assertRaisesRegex(ValueError, 'pinned'):
            IMPORTER.build_catalog('main', FIXTURES)


if __name__ == '__main__':
    unittest.main()
