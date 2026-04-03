---
name: Commit per Prompt
description: User wants a git commit after every prompt (P-number), plus repair commits for bundles
type: feedback
---

Commit after every completed prompt. Use repair commits for bundle-level fixes (e.g., "Bundle 5 Repairs — R5-01 through R5-03").

**Why:** User explicitly follows the pattern "committing to GitHub after each Prompt."

**How to apply:** Always `git add` + `git commit` + `git push` after completing a prompt or repair batch. Never batch multiple prompts into one commit.
