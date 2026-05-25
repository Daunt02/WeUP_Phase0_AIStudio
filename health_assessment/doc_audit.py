import os
from pathlib import Path
import re

def find_readme_files(base_path: Path) -> list[Path]:
    """Return a list of README* files under the given base path."""
    return list(base_path.rglob('README*'))

def find_docs_files(docs_path: Path) -> list[Path]:
    """Return markdown or text files in the docs directory."""
    patterns = ['*.md', '*.txt', '*.rst']
    files = []
    for pattern in patterns:
        files.extend(docs_path.rglob(pattern))
    return files

def extract_headings(file_path: Path, max_headings: int = 5) -> list[str]:
    """Extract top-level headings (lines starting with '#') from a markdown file."""
    headings = []
    try:
        with file_path.open('r', encoding='utf-8') as f:
            for line in f:
                if line.startswith('#'):
                    headings.append(line.strip())
                    if len(headings) >= max_headings:
                        break
    except Exception:
        pass
    return headings

def audit_documentation(project_root: str) -> dict:
    """Perform a simple audit of documentation files.

    Returns a dict with keys:
        - readmes: list of found README files
        - docs: list of documentation files
        - sample_headings: mapping from file to extracted headings
    """
    root = Path(project_root).resolve()
    readmes = find_readme_files(root)
    docs_dir = root / 'docs'
    docs = find_docs_files(docs_dir) if docs_dir.is_dir() else []
    sample_headings = {}
    for f in readmes + docs:
        sample_headings[str(f)] = extract_headings(f)
    return {
        'readmes': [str(p) for p in readmes],
        'docs': [str(p) for p in docs],
        'sample_headings': sample_headings,
    }

if __name__ == '__main__':
    import json, sys
    proj = sys.argv[1] if len(sys.argv) > 1 else '.'
    result = audit_documentation(proj)
    print(json.dumps(result, indent=2))
