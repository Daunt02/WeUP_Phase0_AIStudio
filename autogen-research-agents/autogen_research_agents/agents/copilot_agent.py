"""Copilot integration agent: assembles prompt-pack bundles for GitHub Copilot and local usage.

This file is intentionally a scaffold. It generates structured prompts and groups them into 'packs'.
Use these packs to feed into Copilot, other LLMs, or manual review workflows.
"""
import json
from typing import Dict, Any, List
import os
import re


def analyze_repo_for_prompts(root: str) -> List[Dict[str, str]]:
    prompts = []
    for dirpath, dirs, files in os.walk(root):
        for f in files:
            if f.endswith(('.md', '.py', '.ts', '.tsx', '.cs')):
                path = os.path.join(dirpath, f)
                try:
                    with open(path, 'r', encoding='utf-8') as fh:
                        head = fh.read(4096)
                        prompts.append({'source': os.path.relpath(path, root), 'text': head[:4000]})
                except Exception:
                    continue
    return prompts


def categorize_prompt_item(item: Dict[str, str]) -> str:
    text = item.get('text', '').lower()
    source = item.get('source', '').lower()
    # Heuristics
    if 'test' in source or re.search(r"\bpytest\b|unittest|assert ", text):
        return 'test'
    if 'todo' in text or 'refactor' in text or 'fixme' in text:
        return 'refactor'
    if 'api' in source or 'controller' in source or 'endpoint' in text:
        return 'api-doc'
    if source.endswith('.md') or 'readme' in source:
        return 'docs'
    return 'implementation'


PROMPT_TEMPLATES = {
    'test': 'Write unit tests for the following code snippet, focusing on edge cases and input validation:\n\n{snippet}',
    'refactor': 'Refactor the following code to improve readability and testability. Keep behavior unchanged:\n\n{snippet}',
    'api-doc': 'Generate API documentation (endpoint, parameters, responses) for the following code or description:\n\n{snippet}',
    'docs': 'Summarize the following documentation and suggest improvements or missing sections:\n\n{snippet}',
    'implementation': 'Explain the intent of the following code and suggest potential improvements or tests:\n\n{snippet}'
}


def build_curated_pack(root: str, name: str = 'curated', max_items: int = 200) -> Dict[str, Any]:
    items = analyze_repo_for_prompts(root)
    categorized = {'test': [], 'refactor': [], 'api-doc': [], 'docs': [], 'implementation': []}
    for i, it in enumerate(items):
        if i >= max_items:
            break
        cat = categorize_prompt_item(it)
        snippet = it.get('text', '')[:2000]
        template = PROMPT_TEMPLATES.get(cat, PROMPT_TEMPLATES['implementation'])
        prompt_text = template.format(snippet=snippet)
        categorized[cat].append({'source': it.get('source'), 'prompt': prompt_text})
    pack = {'name': name, 'counts': {k: len(v) for k, v in categorized.items()}, 'items': categorized}
    return pack


def run(context: Dict[str, Any]) -> Dict[str, Any]:
    root = context.get('root', os.getcwd())
    name = context.get('pack_name', 'autogen-curated')
    out_path = context.get('out_path')
    max_items = int(context.get('max_items', 200))
    pack = build_curated_pack(root, name, max_items=max_items)
    if out_path:
        os.makedirs(os.path.dirname(out_path), exist_ok=True)
        with open(out_path, 'w', encoding='utf-8') as fh:
            json.dump(pack, fh, indent=2)
    return {'pack_name': name, 'counts': pack['counts']}


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', default='.')
    parser.add_argument('--out', help='Output file for curated prompt pack')
    parser.add_argument('--name', default='autogen-curated')
    parser.add_argument('--max', type=int, default=200)
    args = parser.parse_args()
    print(run({'root': args.root, 'out_path': args.out, 'pack_name': args.name, 'max_items': args.max}))
