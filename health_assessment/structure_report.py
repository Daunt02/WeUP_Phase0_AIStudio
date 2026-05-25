# health_assessment package utilities

"""Utilities for building a structural report of the codebase.

- Detects C#, Python, and JavaScript source files.
- Counts files per language, gathers import/using statements to build a simple dependency graph.
- Computes test coverage by locating files matching *test* patterns.
"""

import os
from pathlib import Path
from collections import defaultdict

def find_source_files(root: Path) -> dict[str, list[Path]]:
    """Return a mapping language -> list of source file paths.

    Languages supported: Python (.py), C# (.cs), JavaScript (.js, .ts).
    """
    patterns = {
        'python': ['*.py'],
        'csharp': ['*.cs'],
        'javascript': ['*.js', '*.ts'],
    }
    files = defaultdict(list)
    for lang, pats in patterns.items():
        for pat in pats:
            files[lang].extend(root.rglob(pat))
    return files

def count_test_files(files: list[Path]) -> int:
    """Count files whose name contains 'test' (case‑insensitive)."""
    return sum(1 for p in files if 'test' in p.stem.lower())

def build_dependency_graph(files: list[Path]) -> dict[str, set[str]]:
    """Very simple dependency extraction.

    For Python, looks for `import xxx` or `from xxx import`.
    For C#, looks for `using xxx;`.
    For JavaScript/TypeScript, looks for `import xxx from` or `require('xxx')`.
    Returns a dict module -> set(dependencies).
    """
    graph = defaultdict(set)
    for file in files:
        try:
            text = file.read_text(encoding='utf-8')
        except Exception:
            continue
        if file.suffix == '.py':
            for line in text.splitlines():
                line = line.strip()
                if line.startswith('import '):
                    parts = line.split()
                    if len(parts) > 1:
                        graph[file.name].add(parts[1].split('.')[0])
                elif line.startswith('from '):
                    parts = line.split()
                    if len(parts) > 1:
                        graph[file.name].add(parts[1].split('.')[0])
        elif file.suffix == '.cs':
            for line in text.splitlines():
                line = line.strip()
                if line.startswith('using '):
                    ns = line[len('using '):].split(';')[0].strip()
                    graph[file.name].add(ns)
        elif file.suffix in {'.js', '.ts'}:
            for line in text.splitlines():
                line = line.strip()
                if line.startswith('import '):
                    parts = line.split()
                    if 'from' in parts:
                        idx = parts.index('from')
                        if idx + 1 < len(parts):
                            graph[file.name].add(parts[idx + 1].strip('"\'"'))
                elif 'require(' in line:
                    start = line.find('require(') + len('require(')
                    end = line.find(')', start)
                    if end != -1:
                        dep = line[start:end].strip('"\'"')
                        graph[file.name].add(dep)
    return {k: list(v) for k, v in graph.items()}

def generate_structure_report(project_root: str) -> dict:
    """Generate a dictionary summarising the code structure.

    Returns keys:
        - language_file_counts
        - total_test_files
        - dependency_graph (module -> list of deps)
    """
    root = Path(project_root).resolve()
    src = find_source_files(root)
    language_file_counts = {lang: len(paths) for lang, paths in src.items()}
    all_files = [p for lst in src.values() for p in lst]
    total_test_files = count_test_files(all_files)
    dependency_graph = build_dependency_graph(all_files)
    return {
        'language_file_counts': language_file_counts,
        'total_test_files': total_test_files,
        'dependency_graph': dependency_graph,
    }

if __name__ == '__main__':
    import json, sys
    proj = sys.argv[1] if len(sys.argv) > 1 else '.'
    report = generate_structure_report(proj)
    print(json.dumps(report, indent=2))
