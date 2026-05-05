"""Crawl4AI agent: crawls websites and scrapes event-like data for ingestion.

This is a scaffolding example. Extend with site-specific parsers, rate-limiting, politeness, and storage.
"""
import argparse
import json
import time
import requests
import urllib.robotparser
from bs4 import BeautifulSoup
from urllib.parse import urljoin, urlparse
from typing import Dict, Any, List

# Simple per-domain rate limiting state
_last_request_time: Dict[str, float] = {}


def obeys_robots(url: str, user_agent: str = '*') -> bool:
    parsed = urlparse(url)
    robots_url = f"{parsed.scheme}://{parsed.netloc}/robots.txt"
    rp = urllib.robotparser.RobotFileParser()
    try:
        rp.set_url(robots_url)
        rp.read()
        return rp.can_fetch(user_agent, url)
    except Exception:
        # If robots.txt cannot be read, be conservative and allow but log
        return True


def rate_limit_for(url: str, delay: float = 1.0):
    domain = urlparse(url).netloc
    now = time.time()
    last = _last_request_time.get(domain, 0)
    wait = max(0, delay - (now - last))
    if wait > 0:
        time.sleep(wait)
    _last_request_time[domain] = time.time()


def extract_events_from_html(html: str, base_url: str) -> List[Dict[str, str]]:
    soup = BeautifulSoup(html, 'html.parser')
    events = []
    # Naive heuristic: look for elements with 'event' in class or id
    candidates = soup.select('[class*=event], [id*=event]')
    for c in candidates:
        text = c.get_text(separator=' ', strip=True)
        events.append({'snippet': text[:400]})
    # fallback: look for common tags and date/time hints
    for tag in soup.find_all(['article', 'li', 'div']):
        t = tag.get_text(separator=' ', strip=True)
        if len(t) > 200 and ("date" in t.lower() or "time" in t.lower()):
            events.append({'snippet': t[:400]})
    return events


def run(context: Dict[str, Any]) -> Dict[str, Any]:
    # Context keys: seed_urls (list) or seed (str), depth, rate_limit, out_path, parsers
    seed = context.get('seed')
    seed_urls = context.get('seed_urls') or ([seed] if seed else [])
    rate_delay = float(context.get('rate_limit', 1.0))
    out_path = context.get('out_path', 'autogen-research-agents/data/crawl_results.jsonl')
    site_parsers = context.get('parsers', {})
    scraped = []
    for url in seed_urls:
        if not url:
            continue
        try:
            # robots.txt check
            allowed = obeys_robots(url)
            if not allowed:
                scraped.append({'url': url, 'error': 'Blocked by robots.txt'})
                continue
            rate_limit_for(url, delay=rate_delay)
            resp = requests.get(url, timeout=10)
            resp.raise_for_status()
            parser = None
            parsed = urlparse(url)
            domain = parsed.netloc
            if domain in site_parsers:
                # site-specific parser function must be a callable provided in context
                parser = site_parsers[domain]
            if parser and callable(parser):
                events = parser(resp.text, url)
            else:
                events = extract_events_from_html(resp.text, url)
            record = {'url': url, 'events': events}
            scraped.append(record)
            # persist each record to JSONL for reliability
            try:
                import os
                os.makedirs(os.path.dirname(out_path), exist_ok=True)
                with open(out_path, 'a', encoding='utf-8') as fh:
                    fh.write(json.dumps(record, ensure_ascii=False) + '\n')
            except Exception:
                pass
        except Exception as e:
            scraped.append({'url': url, 'error': str(e)})
    return {'scraped': scraped, 'out_path': out_path}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--seed', help='Seed URL to crawl', required=True)
    args = parser.parse_args()
    ctx = {'seed': args.seed}
    out = run(ctx)
    print(out)
