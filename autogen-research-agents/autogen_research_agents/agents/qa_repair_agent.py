"""QA repair agent: monitors environment health, auto-repairs, versions, and ensures portability.

This agent checks local service health, triggers builds/tests, attempts automated formatting or
basic fixes (repair), creates a versioned snapshot (commit) if healthy, and verifies portability
artifacts (like Dockerfiles).
"""
import subprocess
import os
import time
import requests
from typing import Dict, Any, List

def check_live_endpoints(endpoints: List[str]) -> Dict[str, Any]:
    status = {}
    for url in endpoints:
        try:
            res = requests.get(url, timeout=5)
            status[url] = "Live" if res.status_code < 400 else f"Error {res.status_code}"
        except Exception as e:
            status[url] = f"Offline ({e})"
    return status

def run_build_and_tests(root: str) -> Dict[str, Any]:
    res = {}
    try:
        # Run dotnet build
        build_proc = subprocess.run(["dotnet", "build"], cwd=root, capture_output=True, text=True)
        res['build_success'] = build_proc.returncode == 0
        res['build_output'] = build_proc.stdout[-500:] if build_proc.stdout else ""
        
        if res['build_success']:
            # Run dotnet test
            test_proc = subprocess.run(["dotnet", "test"], cwd=root, capture_output=True, text=True)
            res['test_success'] = test_proc.returncode == 0
            res['test_output'] = test_proc.stdout[-500:] if test_proc.stdout else ""
        else:
            res['test_success'] = False
            res['test_output'] = "Build failed, tests not run."
    except Exception as e:
        res['error'] = str(e)
    return res

def attempt_repair(root: str) -> bool:
    # Basic repair: dotnet format, restore
    try:
        subprocess.run(["dotnet", "restore"], cwd=root, capture_output=True)
        subprocess.run(["dotnet", "format"], cwd=root, capture_output=True)
        return True
    except:
        return False

def ensure_versioned(root: str) -> str:
    # Creates a versioned tag or commit if clean
    try:
        status_proc = subprocess.run(["git", "status", "--porcelain"], cwd=root, capture_output=True, text=True)
        if status_proc.returncode == 0 and not status_proc.stdout.strip():
            # Workspace is clean
            # Generate a QA tag
            tag_name = f"qa-passed-{int(time.time())}"
            subprocess.run(["git", "tag", tag_name], cwd=root)
            return f"Tagged {tag_name}"
        elif status_proc.returncode == 0:
            return "Workspace has uncommitted changes, not tagging."
        return "Not a git repo."
    except Exception as e:
        return f"Versioning error: {e}"

def check_portability(root: str) -> Dict[str, Any]:
    # Check for Dockerfile, docker-compose.yml
    has_dockerfile = False
    has_compose = False
    for r, d, f in os.walk(root):
        if 'Dockerfile' in f:
            has_dockerfile = True
        if 'docker-compose.yml' in f:
            has_compose = True
        if has_dockerfile and has_compose:
            break
    return {
        'has_dockerfile': has_dockerfile,
        'has_compose': has_compose,
        'portable': has_dockerfile or has_compose
    }

def run(context: Dict[str, Any]) -> Dict[str, Any]:
    root = context.get('root', os.getcwd())
    endpoints = context.get('endpoints', ['http://localhost:5000/health', 'http://127.0.0.1:3000/'])
    
    report = {}
    
    # 1. Check live environment
    report['live_status'] = check_live_endpoints(endpoints)
    
    # 2. Build and Tests
    build_test = run_build_and_tests(root)
    report['qa_checks'] = build_test
    
    # 3. Repair if needed
    if not build_test.get('build_success') or not build_test.get('test_success'):
        report['repair_attempted'] = attempt_repair(root)
        # re-check after repair
        report['qa_checks_after_repair'] = run_build_and_tests(root)
    
    # 4. Versioned continuously
    # Only version if QA passed
    qa_passed = report.get('qa_checks_after_repair', build_test).get('test_success', False)
    if qa_passed:
        report['versioning'] = ensure_versioned(root)
    else:
        report['versioning'] = "Skipped, QA did not pass."
        
    # 5. Ported continuously
    report['portability'] = check_portability(root)
    
    return report

if __name__ == '__main__':
    print(run({'root': os.getcwd()}))
