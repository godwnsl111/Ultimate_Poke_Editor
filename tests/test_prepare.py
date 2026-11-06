import unittest
from poke_prepare import encounter_rows


class EncounterTests(unittest.TestCase):
    def test_version_filter_and_conditions(self):
        entry = {'min_level': 3, 'max_level': 5, 'chance': 20,
                 'method': {'name': 'walk'}, 'condition_values': [{'name': 'test-condition'}]}
        data = [{'location_area': {'name': 'test-area'}, 'version_details': [
            {'version': {'name': version}, 'encounter_details': [entry]}
            for version in ['firered', 'leafgreen', 'red']]}]
        rows = encounter_rows(25, data, {'firered'})
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]['version'], 'firered')
        self.assertEqual(rows[0]['conditions'], 'test-condition')
        self.assertEqual(rows[0]['min_level'], 3)

    def test_empty_is_no_records(self):
        self.assertEqual(encounter_rows(1, [], {'firered'}), [])


if __name__ == '__main__':
    unittest.main()
