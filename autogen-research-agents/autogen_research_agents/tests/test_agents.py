import os
import json
import tempfile
from autogen_research_agents.agents import copilot_agent
from autogen_research_agents.health import health_report


def test_build_curated_pack(tmp_path):
    # create sample files
    root = tmp_path / "repo"
    root.mkdir()
    f1 = root / "README.md"
    f1.write_text("# Project\n\nThis is a README with API notes.")
    f2 = root / "module.py"
    f2.write_text("def add(a, b):\n    return a + b\n# TODO: add edge cases")
    out = tmp_path / "pack.json"
    res = copilot_agent.run({'root': str(root), 'out_path': str(out), 'pack_name': 'test-pack', 'max_items': 10})
    assert res['pack_name'] == 'test-pack'
    assert 'counts' in res
    # file was written
    assert out.exists()


def test_health_report(tmp_path):
    root = tmp_path / 'repo'
    root.mkdir()
    (root / 'a.py').write_text('print(1)')
    (root / 'README.md').write_text('# doc')
    r = health_report.run({'root': str(root)})
    assert 'summary' in r
    assert r['summary']['files'] >= 2
