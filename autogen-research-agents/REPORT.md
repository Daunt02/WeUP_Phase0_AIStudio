# Autogen Research Agents - Detailed Report

## Overview

This document describes the agent suite added under `autogen-research-agents`. The goal is to provide a set of research-focused agents that:

- Inspect the entire codebase and documentation
- Produce prompt-packs suitable for GitHub Copilot or other LLMs
- Generate health reports and metrics summarizing code and docs
- Assemble phased research whitepapers driven by the repository's contents
- Run a Crawl4AI suite to discover and scrape event data for WeUP ingestion

This report explains each agent, how to use them, and the benefits each brings to the system.

1. `agent_runner` (orchestrator)

---

Purpose:

The `agent_runner` is the orchestrator. It dynamically discovers agent modules in the `agents` package and invokes each one's `run()` function with a simple `context` dictionary. It is intentionally minimal so you can plug in further agents quickly.

How it works:

- Uses Python's `pkgutil` and `importlib` to enumerate modules in the `autogen_research_agents.agents` package.
- Skips itself and any module without a `run(context)` function.
- Constructs a `context` with at least a `root` key (the repository root) and passes it to agents.
- Collects returned dicts and prints a concise summary.

Usage:

Run from the workspace root:

python -m autogen_research_agents.agents.agent_runner --scan --root .

Benefits:

- Centralized control of agent executions
- Plug-and-play extension for new agents
- Uniform minimal interface (`run(context)`) simplifies integration

2. `crawl4ai_agent` (Crawl4AI)

---

Purpose:

The Crawl4AI agent crawls websites for event-like data, producing snippets suitable for downstream event-building and entity extraction pipelines.

How it works (scaffold):

- Accepts `seed` or `seed_urls` in the context.
- Performs a simple HTTP GET (via `requests`) and parses HTML with `BeautifulSoup`.
- Uses heuristics: selects nodes with class/id containing `event`, and looks for text blocks containing date/time hints.
- Returns a JSON structure listing scraped URLs and found snippets.

Usage:

python -m autogen_research_agents.agents.crawl4ai_agent --seed https://example.com/events

Extending it:

- Replace heuristics with site-specific parsers (CSS selectors, XPath)
- Add politeness: `robots.txt` checks and rate-limiting
- Use an async crawler for scale (e.g., `aiohttp` + `asyncio`) and a queue
- Integrate HTML-to-structured parsing using heuristics or ML models to extract event name, datetime, location, and description
- Store results into the WeUP ingestion endpoint or a staging DB

Benefits:

- Bootstraps discovery of public event data useful to WeUP
- Provides an extensible starting point for event-specific scrapers
- Enables rapid experiments with scraped data and extraction heuristics

3. `copilot_agent` (Prompt-Pack Builder)

---

Purpose:

`copilot_agent` builds structured prompt-packs by sampling documentation and code files across the repository. These packs can be used to seed GitHub Copilot, LLM prompt engineering workflows, or human-in-the-loop review.

How it works (scaffold):

- Walks the filesystem and reads the heads/excerpts of source files (`.md`, `.py`, `.ts`, `.tsx`, `.cs`).
- Assembles the excerpts into a JSON 'pack' containing `source` and `prompt` fields.
- Optionally writes the pack to disk for use by other tooling.

Usage:

python -m autogen_research_agents.agents.copilot_agent --root . --out my_pack.json --name my-repo-pack

Extending it:

- Add classification of prompts by intent (test, doc, implementation detail, API, TODO)
- Produce curated prompt templates that combine code + instruction (e.g., "Refactor the following function to be more testable:")
- Integrate with Copilot flows (manual paste or tooling that sends prompts to Copilot/IDE extensions)
- Add heuristics that prioritize public API surface files, contracts, and README docs

Benefits:

- Rapidly produces organized prompt material from the real repo context
- Improves Copilot relevance by feeding it precise, curated context bundles
- Helps create prompt-based tasks for research, refactoring, or documentation improvements

4. `health_report` (Codebase health)

---

Purpose:

Provides a fast, reproducible snapshot of basic repository metrics to help research and engineering teams understand surface-level health indicators.

How it works (scaffold):

- Walks the tree and counts files, lines, and common extensions
- Reports a simple doc coverage estimate (number of `.md` files vs total files)
- Returns a dictionary that can be extended with more metrics (test coverage hooks, lint pass/fail, cyclomatic complexity)

Usage:

python -m autogen_research_agents.health.health_report --root .

Extensions:

- Integrate with existing CI (e.g., run `pytest --junitxml` and include test counts)
- Compute code complexity metrics (radon), style/lint reports (eslint, flake8), and dependency graphs
- Produce a human-readable report with charts or a markdown summary in the repository

Benefits:

- Quick visibility into documentation density and repo scale
- Baseline for tracking health regressions over time
- Input signal for prioritizing maintenance or documentation work

5. `prompt_packs.generate_prompt_pack` (CLI pack builder)

---

Purpose:

This utility provides a command-line focused way to assemble prompt-packs with limits and output files. It's especially useful when you want a single-file pack to archive or feed into LLM experiments.

Usage:

python -m autogen_research_agents.prompt_packs.generate_prompt_pack --root . --out prompt_pack.json

Benefits:

- Reproducible pack generation
- Limits the number of files/bytes to avoid runaway size

6. `whitepapers.generate_whitepaper` (Phased whitepaper assembly)

---

Purpose:

Creates draft whitepapers by concatenating repository markdown and slicing content into phases. It's a scaffold to generate the first draft of multi-phase research outputs.

How it works:

- Gathers all markdown content across the repository
- Concatenates and slices into `phases` sections (configurable)

Usage:

python -m autogen_research_agents.whitepapers.generate_whitepaper --root . --phases 3

Extending it:

- Use extractive summarization (via an LLM) to craft section headers and synthesize prose
- Create templates for each phase (background, approach, experiments, roadmap)
- Produce export formats: PDF, markdown with frontmatter, or LaTeX

Benefits:

- Rapidly produce structured draft artifacts for research meetings
- Encourages a document-driven research process tied to actual repo artifacts

## Integration Patterns

1. Iterative research loop

- Use `copilot_agent` to create prompt-packs targeting areas of interest (e.g., ingestion, event model).
- Run `agent_runner` to execute `health_report` and `whitepaper` generators to produce baseline artifacts.
- Use Crawl4AI to collect example event data and feed it into the event-building pipeline.
- Iterate: refine prompt packs, re-run Copilot tasks, update code, and regenerate reports.

2. Human-in-the-loop prompt engineering

- Analysts review prompt-packs produced by `copilot_agent` and craft higher-level templates.
- Use those prompts in Copilot or an LLM playground to generate code suggestions or whitepaper prose.
- Commit high-quality outputs back into the repo as `docs/` or `whitepapers/` drafts.

## Security and Safety Considerations

- Crawling: obey `robots.txt`, respect rate limits, and handle PII carefully. This scaffold does not implement robots checks — extend before large crawls.
- Code access: prompt packs may include sensitive code or secrets if present — ensure `.gitignore` and secret scanning are in place.
- LLM use: when sending prompts to external services, ensure compliance with licensing and export controls.

## Extending the System

This scaffold is intentionally small and focused on providing a repeatable pattern. Recommended next steps:

- Add an authentication-backed Copilot integration if you have a private API or internal tooling that can accept prompt packs.
- Replace simplistic heuristics with ML-powered extractors for events and document summarization.
- Add tests for each agent and a CI job that runs `agent_runner` in a smoke-test mode.
- Build a simple UI (static site) that lists available prompt-packs, health reports, and whitepaper drafts.

## Developer Notes

- Agents are discovered by enumerating modules in the `autogen_research_agents.agents` package. To add a new agent, place a module there exposing `run(context)`.
- The `context` dictionary is deliberately permissive. Standardize important keys (`root`, `seed`, `out_path`) across agents as you evolve the system.
- Keep long-running or network-bound agents (e.g., crawlers) decoupled from the synchronous `agent_runner` to avoid blocking.

## Quick File Map

- `autogen_research_agents/agents/agent_runner.py` — orchestrator
- `autogen_research_agents/agents/crawl4ai_agent.py` — Crawl4AI scaffold
- `autogen_research_agents/agents/copilot_agent.py` — prompt-pack builder
- `autogen_research_agents/health/health_report.py` — codebase health
- `autogen_research_agents/prompt_packs/generate_prompt_pack.py` — CLI pack builder
- `autogen_research_agents/whitepapers/generate_whitepaper.py` — draft whitepaper assembly

## What's Next / Recommended Adoption Plan

1. Review and adapt crawling policies (robots, rate limits) before running Crawl4AI at scale.
2. Run `prompt_packs.generate_prompt_pack` and inspect `prompt_pack.json` to curate prompts.
3. Create specific prompt templates for targeted Copilot tasks: tests, refactors, API docs.
4. Integrate health checks with CI and track trends over time.
5. Use `whitepapers.generate_whitepaper` as a first draft input to an LLM to produce polished whitepapers.

## Contact and Attribution

This scaffold and report were created to accelerate repository-level research workflows. Adapt as needed and open PRs to evolve the agents.
