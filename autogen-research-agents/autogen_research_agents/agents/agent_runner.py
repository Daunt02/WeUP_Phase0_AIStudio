"""Agent runner: discovers and runs agents across the workspace.

This is intentionally lightweight scaffolding. Each agent exposes a `run()` function
that accepts a `context` dict and returns a dict result.
"""
import argparse
import importlib
import os
import pkgutil
from typing import Dict, Any

AGENTS_PACKAGE = 'autogen_research_agents.agents'


def discover_agents():
    agents = {}
    pkg = importlib.import_module(AGENTS_PACKAGE)
    package_path = os.path.dirname(pkg.__file__)
    for finder, name, ispkg in pkgutil.iter_modules([package_path]):
        if name == 'agent_runner':
            continue
        mod = importlib.import_module(f'{AGENTS_PACKAGE}.{name}')
        if hasattr(mod, 'run'):
            agents[name] = mod
    return agents


def run_all_agents(root: str) -> Dict[str, Any]:
    agents = discover_agents()
    results = {}
    context = {'root': root}
    for name, mod in agents.items():
        try:
            print(f'Running agent: {name}')
            results[name] = mod.run(context) or {}
        except Exception as e:
            results[name] = {'error': str(e)}
    return results


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument('--scan', action='store_true', help='Run all agents against the repository')
    parser.add_argument('--root', default=os.getcwd(), help='Workspace root to analyze')
    args = parser.parse_args(argv)

    if args.scan:
        res = run_all_agents(args.root)
        print('Agent run summary:')
        for k, v in res.items():
            print(f'- {k}: {type(v)}')


if __name__ == '__main__':
    main()
