"""Pure schema-conversion regressions. Never opens a real database."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('schema_port', Path(__file__).parents[1] / 'Build-PostgresSchema.py')
port = importlib.util.module_from_spec(spec)
spec.loader.exec_module(port)


class SchemaPortTests(unittest.TestCase):
    def test_null_safe_equality_keeps_null_checks(self):
        result = port.expression(port.tokens('NEW.x IS OLD.x AND NEW.y IS NOT OLD.y AND x IS NULL AND y IS NOT NULL'), set())
        self.assertIn('NEW . x IS NOT DISTINCT FROM OLD . x', result)
        self.assertIn('NEW . y IS DISTINCT FROM OLD . y', result)
        self.assertIn('x IS NULL AND y IS NOT NULL', result)

    def test_strings_are_not_rewritten_as_identifiers(self):
        result = port.expression(port.tokens("RoleCode = 'RoleCode IS GLOB date()'"), {'RoleCode'})
        self.assertEqual(result, '"RoleCode" = \'RoleCode IS GLOB date()\'')

    def test_timestamp_preserves_100ns_and_date_checks_are_scoped(self):
        inventory = {'tables': [{'name': 'sample', 'foreign_keys': [],
            'columns': [[0, 'stamp', 'TEXT', 1, None, 0], [1, 'day', 'TEXT', 1, None, 0]],
            'ddl': 'CREATE TABLE sample(stamp TEXT NOT NULL, day TEXT NOT NULL CHECK(length(day)=10 AND date(day)=day))'}],
            'indexes': [], 'triggers': [], 'views': []}
        model = [{'table': 'sample', 'columns': [{'column': 'stamp', 'type': 'DateTime'}, {'column': 'day', 'type': 'DateOnly'}]}]
        sql = port.build(inventory, model)
        self.assertIn('"stamp" text NOT NULL', sql)
        self.assertIn('"day" date NOT NULL CHECK', sql)
        self.assertIn('length ( ("day"::text) )', sql)
        self.assertNotIn('timestamp without time zone NOT NULL', sql)

    def test_unknown_column_type_stops(self):
        inventory = {'tables': [{'name': 'x', 'foreign_keys': [], 'columns': [[0, 'id', 'MYSTERY', 1, None, 1]], 'ddl': 'CREATE TABLE x(id MYSTERY)'}], 'indexes': [], 'triggers': []}
        with self.assertRaises(ValueError): port.build(inventory, [])

    def test_view_is_preserved(self):
        inventory = {'tables': [], 'indexes': [], 'triggers': [], 'views': [{'name': 'v', 'ddl': 'CREATE VIEW v AS SELECT 1 AS total'}]}
        self.assertIn('CREATE VIEW v AS SELECT 1 AS total;', port.build(inventory, []))

    def test_non_abort_trigger_is_rejected(self):
        inventory = {'tables': [], 'indexes': [], 'triggers': [{'name': 'unsafe', 'ddl': 'CREATE TRIGGER unsafe BEFORE INSERT ON x BEGIN SELECT 1; END'}]}
        with self.assertRaises(ValueError): port.build(inventory, [])

    def test_glob_is_anchored_with_negation(self):
        result = port.expression(port.tokens("phone NOT GLOB '*[^0-9]*'"), set())
        self.assertEqual(result, "phone !~ '^.*[^0-9].*$'")


if __name__ == '__main__': unittest.main()
