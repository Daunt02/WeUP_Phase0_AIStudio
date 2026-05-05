"""Whitepaper generator: assembles docs and code excerpts into phased research whitepapers.

This scaffold concatenates markdown sources and simple heuristics to produce phased outlines.
"""
import os
from typing import Dict, Any, List


def collect_markdown(root: str) -> List[str]:
    docs = []
    for dirpath, dirs, files in os.walk(root):
        for f in files:
            if f.endswith('.md'):
                p = os.path.join(dirpath, f)
                try:
                    with open(p, 'r', encoding='utf-8') as fh:
                        docs.append(fh.read())
                except Exception:
                    continue
    return docs


def assemble_whitepaper(root: str, phases: int = 3) -> Dict[str, Any]:
    md = collect_markdown(root)
    combined = '\n\n'.join(md)
    sections = []
    chunk_len = max(1, len(combined) // phases)
    for i in range(phases):
        start = i * chunk_len
        sections.append(combined[start:start + chunk_len])
    return {'phases': phases, 'sections': sections}


def run(context: Dict[str, Any]) -> Dict[str, Any]:
    root = context.get('root', '.')
    phases = int(context.get('phases', 3))
    return assemble_whitepaper(root, phases)


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', default='.')
    parser.add_argument('--phases', type=int, default=3)
    args = parser.parse_args()
    print(run({'root': args.root, 'phases': args.phases}))
