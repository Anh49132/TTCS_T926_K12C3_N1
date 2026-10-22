"""Build a reviewed PostgreSQL schema candidate from the read-only inventory.

Only writes SQL to ignored data/. Does not connect to either database.
Unknown SQLite constructs fail closed instead of silently dropping guards.
"""
import argparse
import json
from pathlib import Path
import re

TOKEN = re.compile(r"--[^\n]*|/\*.*?\*/|'(?:''|[^'])*'|\"(?:\"\"|[^\"])*\"|[A-Za-z_][A-Za-z_0-9]*|\d+(?:\.\d+)?|<>|!=|<=|>=|\|\||[^\s]", re.S)


def tokens(sql):
    return [m.group() for m in TOKEN.finditer(sql) if not m.group().startswith(('--', '/*'))]


def quoted(name):
    return '"' + name.replace('"', '""') + '"'


def split_columns(parts):
    start, depth = 0, 0
    result = []
    for i, token in enumerate(parts):
        if token == '(':
            depth += 1
        elif token == ')':
            depth -= 1
        elif token == ',' and depth == 0:
            result.append(parts[start:i])
            start = i + 1
    result.append(parts[start:])
    return result


def expression(parts, mixed):
    result = []
    i = 0
    functions = {'typeof', 'julianday', 'date', 'json_valid', 'json_type', 'instr'}
    while i < len(parts):
        value = parts[i]
        upper = value.upper()
        if upper == 'IS':
            negative = i + 1 < len(parts) and parts[i + 1].upper() == 'NOT'
            right = i + (2 if negative else 1)
            if right < len(parts) and parts[right].upper() not in ('NULL', 'TRUE', 'FALSE'):
                result.extend(['IS', 'DISTINCT' if negative else 'NOT DISTINCT', 'FROM'])
                i = right
                continue
        if upper == 'GLOB':
            pattern = parts[i + 1][1:-1].replace("''", "'")
            regex = '^' + pattern.replace('.', r'\.').replace('*', '.*').replace('?', '.') + '$'
            if result and result[-1].upper() == 'NOT':
                result[-1] = '!~'
            else:
                result.append('~')
            result.append("'" + regex.replace("'", "''") + "'")
            i += 2
            continue
        if value.lower() in functions and i + 1 < len(parts) and parts[i + 1] == '(':
            value = 'nhatot_' + value.lower()
        if value in mixed:
            value = quoted(value)
        if upper in ('OR', 'AND') and i + 1 < len(parts) and parts[i + 1] in ('0', '1'):
            result.extend([upper, 'FALSE' if parts[i + 1] == '0' else 'TRUE'])
            i += 2
            continue
        result.append(value)
        i += 1
    return ' '.join(result)


HELPERS = r'''
CREATE FUNCTION nhatot_typeof(v anyelement) RETURNS text LANGUAGE sql IMMUTABLE AS $$
 SELECT CASE WHEN v IS NULL THEN 'null' WHEN pg_typeof(v)::text IN ('integer','bigint','smallint') THEN 'integer'
 WHEN pg_typeof(v)::text IN ('text','character varying') THEN 'text' WHEN pg_typeof(v)::text='bytea' THEN 'blob' ELSE 'real' END
$$;
CREATE FUNCTION nhatot_instr(v text, needle text) RETURNS integer LANGUAGE sql IMMUTABLE AS $$ SELECT strpos(v,needle) $$;
CREATE FUNCTION nhatot_json_valid(v text) RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$ BEGIN PERFORM v::json; RETURN true; EXCEPTION WHEN OTHERS THEN RETURN false; END $$;
CREATE FUNCTION nhatot_json_type(v text) RETURNS text LANGUAGE plpgsql IMMUTABLE AS $$ BEGIN RETURN json_typeof(v::json); EXCEPTION WHEN OTHERS THEN RETURN NULL; END $$;
CREATE FUNCTION nhatot_julianday(v text) RETURNS numeric LANGUAGE plpgsql IMMUTABLE AS $$ BEGIN RETURN extract(epoch FROM v::timestamp)/86400+2440587.5; EXCEPTION WHEN OTHERS THEN RETURN NULL; END $$;
CREATE FUNCTION nhatot_date(v text, VARIADIC modifiers text[] DEFAULT ARRAY[]::text[]) RETURNS text LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE t timestamp; modifier text;
BEGIN
 t:=v::timestamp;
 FOREACH modifier IN ARRAY modifiers LOOP
   IF modifier='start of month' THEN t:=date_trunc('month',t);
   ELSIF modifier IN ('+7 hours','+1 month','-1 day') THEN t:=t+modifier::interval;
   ELSE RAISE EXCEPTION 'Unsupported date modifier'; END IF;
 END LOOP;
 RETURN to_char(t,'YYYY-MM-DD');
EXCEPTION WHEN OTHERS THEN RETURN NULL;
END $$;
-- Compatibility catalog for existing optional-module readiness checks only.
CREATE VIEW sqlite_master AS SELECT 'table'::text AS type, table_name::text AS name
 FROM information_schema.tables WHERE table_schema=current_schema() AND table_type='BASE TABLE';
CREATE FUNCTION pragma_table_info(requested_table text) RETURNS TABLE(name text) LANGUAGE sql STABLE AS $$
 SELECT c.column_name::text FROM information_schema.columns c
 WHERE c.table_schema=current_schema() AND c.table_name=requested_table
$$;
'''


def build(inventory, model):
    mixed = {column[1] for table in inventory['tables'] for column in table['columns'] if column[1].lower() != column[1]}
    types = {(table['table'], column['column']): column['type'] for table in model for column in table['columns']}
    tables = {table['name']: table for table in inventory['tables']}
    output = ['-- Candidate port v1; no data; do not apply to a nonempty database.', HELPERS]
    done = set()

    def visit(name, chain):
        if name in done:
            return
        if name in chain:
            raise ValueError('Foreign-key cycle needs explicit handling: ' + name)
        table = tables[name]
        for fk in table['foreign_keys']:
            if fk[2] != name:
                visit(fk[2], chain + [name])
        raw = tokens(table['ddl'])
        body = raw[raw.index('(') + 1:-1]
        definitions = []
        for part in split_columns(body):
            field = part[0].strip('"')
            if part[0].upper() not in ('UNIQUE', 'CHECK', 'FOREIGN', 'PRIMARY', 'CONSTRAINT'):
                net = types.get((name, field))
                old = part[1].upper()
                # .NET DateTime has 100 ns precision, PostgreSQL timestamp only 1 us.
                # Preserve SQLite's exact timestamp strings (including the seventh
                # fractional digit); the application must use a matching converter.
                replacement = {'Int32': 'integer', 'Int64': 'bigint', 'Boolean': 'integer', 'DateTime': 'text', 'DateOnly': 'date', 'Decimal': 'numeric', 'Double': 'double precision', 'Byte[]': 'bytea', 'String': 'text'}.get(net)
                if replacement is None:
                    replacement = {'INTEGER': 'bigint', 'TEXT': 'text', 'BLOB': 'bytea', 'REAL': 'double precision'}.get(old)
                if replacement is None and old == 'DECIMAL':
                    replacement = 'numeric'
                if replacement is None:
                    raise ValueError(f'Unknown column type: {name}.{field} {old}')
                # Strip any original DECIMAL precision; amounts remain exact numeric.
                end = 2
                if len(part) > 2 and part[2] == '(':
                    end = part.index(')', 2) + 1
                part = [quoted(field), replacement] + part[end:]
                if 'AUTOINCREMENT' in part:
                    part.remove('AUTOINCREMENT')
                    part.insert(2, 'GENERATED BY DEFAULT AS IDENTITY')
            converted = expression(part, mixed)
            if name == 'anh_phong' and part == ['UNIQUE', '(', 'phong_id', ',', 'thu_tu', ')']:
                # Keep range CHECKs active during a single reorder UPDATE.
                converted += ' DEFERRABLE INITIALLY DEFERRED'
            # A SQLite boolean is an integer; PostgreSQL comparisons are boolean.
            converted = converted.replace('truoc_han = ( ngay_tra_phong < ngay_ket_thuc_doi_chieu )', 'truoc_han = CASE WHEN ngay_tra_phong < ngay_ket_thuc_doi_chieu THEN 1 ELSE 0 END')
            # CHECK expressions keep the original text date validation semantics.
            for check in re.findall(r'CHECK\b.*', converted):
                native = {column[1] for column in table['columns'] if column[2].upper() == 'TEXT' and types.get((name, column[1])) in ('DateTime', 'DateOnly', 'Decimal', 'Double')}
                rewritten = check
                for col in sorted(native, key=len, reverse=True):
                    rewritten = re.sub(r'(?<![\w"])\b' + re.escape(col) + r'\b(?!")', '(' + quoted(col) + '::text)', rewritten)
                converted = converted.replace(check, rewritten)
            # typeof is unnecessary for strongly typed temporal columns; date helper
            # casts above are intentionally scoped to CHECK, never column definitions.
            definitions.append(converted)
        output.append('CREATE TABLE ' + quoted(name) + ' (\n ' + ',\n '.join(definitions) + '\n);')
        done.add(name)

    for name in sorted(tables):
        visit(name, [])
    for index in inventory['indexes']:
        if index['ddl']:
            output.append(expression(tokens(index['ddl']), mixed) + ';')
    for view in inventory.get('views', []):
        output.append(expression(tokens(view['ddl']), mixed) + ';')
    for trigger in inventory['triggers']:
        raw = tokens(trigger['ddl'])
        # SQLite permits the table alias `old` alongside trigger OLD. PL/pgSQL
        # folds both to the transition record and reports ambiguous old.id.
        # This source trigger uses lowercase `old` only for its invoice alias.
        if trigger['name'] == 'hoa_don_thay_the_insert':
            raw = ['previous_invoice' if token == 'old' else token for token in raw]
        begin = next(i for i, token in enumerate(raw) if token.upper() == 'BEGIN')
        when = next((i for i, token in enumerate(raw[:begin]) if token.upper() == 'WHEN'), begin)
        header = expression(raw[:when], mixed)
        condition = expression(raw[when + 1:begin], mixed) if when < begin else 'TRUE'
        body = raw[begin + 1:]
        expected = ['SELECT', 'RAISE', '(', 'ABORT', ',']
        if [token.upper() for token in body[:5]] != expected or body[6:] != [')', ';', 'END']:
            raise ValueError('Unsupported trigger body: ' + trigger['name'])
        message = body[5]
        fn = quoted('fn_' + trigger['name'])
        output.append(f'CREATE FUNCTION {fn}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF {condition} THEN RAISE EXCEPTION USING ERRCODE=\'23514\', MESSAGE={message}; END IF; IF TG_OP=\'DELETE\' THEN RETURN OLD; ELSE RETURN NEW; END IF; END $$;')
        output.append(header + f' FOR EACH ROW EXECUTE FUNCTION {fn}();')
    return '\n\n'.join(output) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('inventory', type=Path)
    parser.add_argument('model', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    if not output.is_relative_to(root / 'data'):
        parser.error('Candidate SQL must stay in ignored data/.')
    sql = build(json.loads(args.inventory.read_text(encoding='utf-8')), json.loads(args.model.read_text(encoding='utf-8')))
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(sql, encoding='utf-8')
    print(json.dumps({'tables': sql.count('CREATE TABLE '), 'triggers': sql.count('CREATE TRIGGER '), 'candidate_only': True}))


if __name__ == '__main__':
    main()
