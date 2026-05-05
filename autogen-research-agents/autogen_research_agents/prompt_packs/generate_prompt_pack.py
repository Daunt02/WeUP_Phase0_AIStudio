"""Utility to generate prompt-packs from the codebase."""
import argparse
import json
import os
from typing import Dict, Any


def collect_sources(root: str):
    for dirpath, dirs, files in os.walk(root):
        for f in files:
            if f.endswith(('.md', '.py', '.ts', '.tsx', '.cs')):
                yield os.path.join(dirpath, f)


def build_pack(root: str, max_files: int = 200):
    pack = {'items': []}
    for i, p in enumerate(collect_sources(root)):
        if i >= max_files:
            break
        try:
            with open(p, 'r', encoding='utf-8') as fh:
                txt = fh.read(4000)
            pack['items'].append({'path': p, 'excerpt': txt[:2000]})
        except Exception:
            continue
    return pack


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', default='.')
    parser.add_argument('--out', default='prompt_pack.json')
    args = parser.parse_args(argv)
    pack = build_pack(args.root)
    with open(args.out, 'w', encoding='utf-8') as fh:
        json.dump(pack, fh, indent=2)
    print('Wrote', args.out)


if __name__ == '__main__':
    main()
