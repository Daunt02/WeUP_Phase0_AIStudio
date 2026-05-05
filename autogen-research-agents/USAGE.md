# Usage: Autogen Research Agents

Quick start (Python 3.10+ recommended):

1. Run the agent runner to analyze the repo and produce artifacts:

   python -m autogen_research_agents.agents.agent_runner --scan

2. Generate a prompt pack:

   python -m autogen_research_agents.prompt_packs.generate_prompt_pack --out prompt_pack.json

3. Run Crawl4AI to crawl target sites for events:

   python -m autogen_research_agents.agents.crawl4ai_agent --seed https://example.com/events

See each module for flags and advanced usage.
