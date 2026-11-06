"""Read-only preparation tools. Never launches editors or writes save files."""
import argparse
import csv
import hashlib
import json
import time
from pathlib import Path
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parent
ASSETS = ROOT.parent
BASE = "https://pokeapi.co/api/v2"


def cached_get(resource, cache_dir, offline=False):
    path = cache_dir / (resource.strip('/').replace('/', '_') + '.json')
    if path.exists():
        return json.loads(path.read_text(encoding='utf-8'))
    if offline:
        raise FileNotFoundError(f'Not cached: {resource}')
    request = Request(f'{BASE}/{resource.strip("/")}/',
                      headers={'User-Agent': 'PokemonEditPreparation/0.1'})
    with urlopen(request, timeout=30) as response:
        data = json.load(response)
    cache_dir.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    time.sleep(0.2)
    return data


def encounter_rows(species_id, encounters, versions):
    rows = []
    for area in encounters:
        for detail in area['version_details']:
            version = detail['version']['name']
            if version not in versions:
                continue
            for encounter in detail['encounter_details']:
                rows.append({
                    'species_id': species_id,
                    'version': version,
                    'location_area': area['location_area']['name'],
                    'method': encounter['method']['name'],
                    'min_level': encounter['min_level'],
                    'max_level': encounter['max_level'],
                    'chance': encounter['chance'],
                    'conditions': '|'.join(v['name'] for v in encounter['condition_values']),
                    'source': f'{BASE}/pokemon/{species_id}/encounters/',
                })
    return rows


def inventory():
    files = []
    for path in sorted(ASSETS.glob('*.sav')):
        raw = path.read_bytes()
        files.append({'path': str(path), 'bytes': len(raw),
                      'sha256': hashlib.sha256(raw).hexdigest(),
                      'detected_version': None,
                      'note': 'Version detection not implemented; filename is not evidence.'})
    return {'pkhex_present': (ASSETS / 'PKHeX.exe').is_file(),
            'pokefinder_present': (ASSETS / 'PokeFinder/PokeFinder.exe').is_file(),
            'saves': files, 'save_writing_supported': False}


def collect(args):
    if not 1 <= args.start <= args.end <= 1025:
        raise ValueError('Expected 1 <= start <= end <= 1025')
    rows, statuses = [], []
    for species_id in range(args.start, args.end + 1):
        data = cached_get(f'pokemon/{species_id}/encounters', args.cache, args.offline)
        found = encounter_rows(species_id, data, set(args.versions))
        rows.extend(found)
        statuses.append({'species_id': species_id,
                         'status': 'encounter_records_found' if found else 'no_matching_records',
                         'record_count': len(found)})
    # Write reports only after all requests succeed; never interpret missing records as unobtainable.
    args.output.mkdir(parents=True, exist_ok=True)
    report = {'scope': 'PokeAPI encounter records only; not a complete obtainability list',
              'versions': args.versions, 'species': statuses, 'encounters': rows}
    (args.output / 'encounters.json').write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    fields = ['species_id', 'version', 'location_area', 'method', 'min_level',
              'max_level', 'chance', 'conditions', 'source']
    with (args.output / 'encounters.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)
    print(f'{len(rows)} records; reports: {args.output.resolve()}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    commands.add_parser('inventory', help='Read file metadata and hash saves; no version guessing')
    fetch = commands.add_parser('encounters', help='Fetch/cache encounter records, not all obtainable species')
    fetch.add_argument('--versions', nargs='+', choices=['firered', 'leafgreen'], default=['firered'])
    fetch.add_argument('--start', type=int, default=1)
    fetch.add_argument('--end', type=int, default=151)
    fetch.add_argument('--cache', type=Path, default=ROOT / 'data/cache')
    fetch.add_argument('--output', type=Path, default=ROOT / 'output')
    fetch.add_argument('--offline', action='store_true')
    args = parser.parse_args()
    try:
        if args.command == 'inventory':
            print(json.dumps(inventory(), ensure_ascii=False, indent=2))
        else:
            collect(args)
    except (OSError, ValueError, KeyError) as error:
        parser.exit(1, f'Error: {error}\n')


if __name__ == '__main__':
    main()
