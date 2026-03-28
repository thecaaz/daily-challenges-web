<!--
  Repository policy: Copilot / automated coding agents
  Do NOT run any Copilot or automated coding agents in the cloud
  using this repository's code without explicit, documented permission.
-->

# COPILOT / AUTOMATED CODING AGENT POLICY

Purpose
-------
This repository does not permit running GitHub Copilot (including Copilot
coding agents, Copilot for CLI, or other automated coding agents) in any
cloud environment, CI runner, remote workspace, or third-party service that
executes code from this repository without explicit, documented permission
from a repository owner or authorized maintainer.

Policy
------
- Prohibited: any automated/cloud execution of Copilot or coding agents that
  modifies, creates, or merges code in this repository without prior
  authorization.
- Allowed only after: the requester opens an issue or PR describing the
  intended run and receives an explicit approval comment from a repository
  owner or maintainer.

Required approval workflow
-------------------------
1. Open an issue titled `copilot-run-request: <brief reason>` describing:
   - The purpose and scope of the run
   - The precise commands, actions, or tooling to be executed
   - The Git ref / branch to be used
   - The identity of the person or service that will run the agent
2. A repository owner or maintainer must approve by commenting on the issue
   with an explicit approval statement and date. Approval must be visible
   in the issue timeline.
3. The approved run must reference the issue number in any job logs,
   commit messages, or PRs produced by the run.

Scope
-----
This policy applies to:

- GitHub Copilot, Copilot coding agents, and vendor-provided automated
  coding assistants.
- CI/CD workflows, GitHub Actions, Codespaces, external build agents,
  or any cloud-hosted execution environments that run code from this repo.

Enforcement & Reporting
-----------------------
- If you detect a Copilot/cloud run that lacked approval, open an issue
  titled `copilot-policy-violation` and tag repository maintainers.
- Maintainers may revoke remote access tokens, uninstall the Copilot app,
  or disable the offending workflow until the issue is resolved.

Next steps (recommended)
------------------------
- Add an automated check (GitHub Action) that flags PRs or workflows
  mentioning `copilot`, `copilot-github`, or `mcp_github` for manual review.
- Org admins can remove the GitHub Copilot app or restrict its permissions
  at the organization/repository level if desired.

Contact
-------
Open an issue to request permission or report a violation.
