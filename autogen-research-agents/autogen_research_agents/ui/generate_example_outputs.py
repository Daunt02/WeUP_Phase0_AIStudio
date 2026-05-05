"""Generate example artifact JSON files for the UI to consume."""
import json
import os


def write(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as fh:
        json.dump(obj, fh, indent=2)


def main():
    write('autogen-research-agents/prompt_pack.json', {'name': 'example', 'items': []})
    write('autogen-research-agents/health_report.json', {'summary': {'files': 0}})
    write('autogen-research-agents/whitepaper.json', {'phases': 3, 'sections': []})


if __name__ == '__main__':
    main()
