"""Health report: basic static analysis and doc coverage heuristics."""
import os
from typing import Dict, Any


def code_stats(root: str) -> Dict[str, int]:
    counts = {'files': 0, 'lines': 0, 'py': 0, 'ts': 0, 'md': 0}
    for dirpath, dirs, files in os.walk(root):
        for f in files:
            counts['files'] += 1
            if f.endswith('.py'):
                counts['py'] += 1
            if f.endswith('.ts') or f.endswith('.tsx'):
                counts['ts'] += 1
            if f.endswith('.md'):
                counts['md'] += 1
            try:
                with open(os.path.join(dirpath, f), 'r', encoding='utf-8') as fh:
                    counts['lines'] += sum(1 for _ in fh)
            except Exception:
                continue
    return counts


def run(context: Dict[str, Any]) -> Dict[str, Any]:
    root = context.get('root', '.')
    stats = code_stats(root)
    report = {
        'summary': stats,
        'doc_coverage_estimate': round((stats.get('md', 0) / max(1, stats.get('files', 1))) * 100, 2)
    }
    return report


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', default='.')
    args = parser.parse_args()
    print(run({'root': args.root}))
