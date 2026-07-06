---
description: Post-incident analysis agent — run 5 whys, write PM-NNN postmortem with ONE AND DONE structural fixes, then hand off each pending fix to speckit.specify for structured implementation.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Context Requirements

Before proceeding, you **MUST** have access to:
1. **Incident document** (from `docs/incidents/{ID}.md` — from the incident agent's work)
2. **System architecture**
3. **Deployment model** (how services are deployed, config management, validation)

If these are not provided in user input, **ask the user** for the path to the incident document.

---

## Step 1: Extract Incident Context

1. **Read the incident document** provided (or ask user for the path)
2. **Extract key information**:
   - Start time, duration
   - Affected components
   - Root cause hypothesis from incident agent
   - Cause-class category (state-drift, dependency-failure, resource-exhaustion, bug-regression, deployment-incomplete, human-process)
   - Timeline of mitigations and fixes applied
   - System state at time of incident (logs, configuration)

3. **Understand the system**:
   - Read the relevant architecture/deployment documentation
   - Map the affected components to the system design
   - Understand the normal/expected state

---

## Step 2: Five Whys Analysis

Run the **5 Whys** structured root cause analysis:

1. **State the symptom** (what users experienced):
   - e.g., "Stencil print queue was stuck; no new jobs processed for 8 hours"

2. **Ask "Why?"** five times, drilling deeper:
   ```
   Why #1: Why did the queue stop processing?
   Answer: The worker service became unreachable.
   
   Why #2: Why did the worker service become unreachable?
   Answer: DNS resolution of `worker-service:8001` was failing.
   
   Why #3: Why was DNS resolution failing?
   Answer: The Kubernetes ClusterIP service for the worker was never created.
   
   Why #4: Why was the service never created?
   Answer: The Helm chart template for worker service was missing from the deployment.
   
   Why #5: Why was the template missing?
   Answer: It was never added to the chart. Service creation was assumed to be automatic, but it wasn't part of the deployment process.
   ```

3. **Document all answers** in the postmortem (see Postmortem Template below)

---

## Step 3: Design ONE AND DONE Structural Fix

The **ONE AND DONE** principle: Abstract the concrete bug to its class, then design a structural fix that makes the entire class mechanically impossible.

**Pattern**:
- **Concrete bug**: "Worker service missing from Helm chart"
- **Bug class**: "Services required by control-plane are not automatically created during deployment"
- **Structural fix**: "Audit all inter-service dependencies in the deployment model; enforce that any service that another service depends on must be:
  1. Listed in a `dependencies.yml` file in the chart
  2. Auto-generated from that list in the Helm template
  3. Validated in pre-deployment checks (no missing services)"

**For each root cause**, design a ONE AND DONE fix:
- It should be **mechanical/automated** (not "be more careful" or "remember to check")
- It should **prevent the entire class of bugs** (not just this one instance)
- It should be **verifiable** (you can test that the fix works)

---

## Step 4: Generate Postmortem Document

Create the postmortem file at: `docs/postmortems/PM-NNN_SHORT_TITLE.md`

**Next available PM number**:
1. Check `docs/postmortems/` for existing PM files
2. Find the highest NNN number
3. Use NNN + 1 for this postmortem

**Postmortem Template** (use this structure):

```markdown
# PM-NNN: Short Descriptive Title

**Date**: YYYY-MM-DD  
**Duration**: HH:MM (start to resolution)  
**Operator**: Copilot  
**Incident Document**: [docs/incidents/{ID}.md](../incidents/{ID}.md)  
**Status**: COMPLETE

## Timeline

(Copy and condense the Timeline from the incident document)

| Time | Event |
|---|---|
| HH:MM | Incident started: [symptom] |
| HH:MM | [Action taken] |
| HH:MM | [Mitigation applied] |
| HH:MM | [Fix deployed] |
| HH:MM | [Verified resolved] |

**Total duration**: X hours Y minutes

---

## Root Causes

### Primary Cause: [Cause Title]

**What failed**: [System/component]  
**Why it failed**: [Mechanism]  
**Why we didn't catch it**: [Why validation/testing missed it]

### Contributing Factor: [If any]

[Similar structure]

---

## Five Whys Analysis

```
Symptom: [What users saw]

Why #1: Q: [First why]
        A: [Answer]

Why #2: Q: [Drill deeper]
        A: [Answer]

Why #3: Q: [Continue]
        A: [Answer]

Why #4: Q: [Continue]
        A: [Answer]

Why #5: Q: [Get to systemic level]
        A: [Answer]
```

---

## ONE AND DONE Structural Fix

**Bug Class**: [Abstract from concrete bug to its class]

**Structural Fix**: 
- **What will change**: [System, process, or automation]
- **How it prevents recurrence**: [Mechanism that makes this class of bug impossible]
- **How to verify it works**: [Test or validation step]
- **Owner**: [Who implements this]
- **Target Date**: [When this should land]
- **Spec/Task**: [Link to speckit.specify output or tasks.md entry]

---

## Fixes Made (Immediate)

(From incident document)

### Fix 1: [Mitigation name]
- **Commit**: [hash]
- **Deployed**: [when]
- **Verified**: [how]

### Fix 2: [Permanent fix name]
- **Commit**: [hash]
- **Deployed**: [when]
- **Verified**: [how]

---

## Fixes Pending (Structural)

### Fix 1: [ONE AND DONE fix name]
- **Spec/Task**: (TID pending speckit.specify output)
- **Description**: [ONE AND DONE description]
- **Owner**: [Who will implement]
- **Priority**: [HIGH / MEDIUM]
- **Target**: [Target date or milestone]

---

## Lessons

(What did we learn? What should we do differently going forward?)

- **Lesson 1**: [e.g., "Services required by other services must be listed as dependencies in deployment"]
- **Lesson 2**: [e.g., "Pre-deployment validation should check for missing services"]
- **Lesson 3**: [e.g., "Incident response runbooks should include service dependency checks"]

---

## Cause-Class Tags

(Tag this postmortem so we can correlate recurrences)

- `state-drift`: Services/config diverged from expected state
- `deployment-incomplete`: Service deployed but dependencies missing
- `process-gap`: Manual step forgotten in runbook
- `validation-gap`: No pre-deployment check caught the issue

**Tags for this incident**: [List which apply]

---

## References

- Incident Document: [docs/incidents/{ID}.md](../incidents/{ID}.md)
- Architecture: {TODO: Need to insert long live place where this will be}
- System Map: [docs/platform-architecture.md](../platform-architecture.md)

---

## Approvals

- [ ] ONE AND DONE fix reviewed (structural soundness)
- [ ] Lessons accepted by team
- [ ] Spec/task tickets created for pending fixes
- [ ] Closed (when all pending fixes are implemented)
```

---

## Step 5: Create Postmortem File

1. **Assign PM number**:
   - Scan `docs/postmortems/` for highest NNN
   - Use NNN + 1 (e.g., if PM-001 exists, use PM-002)

2. **Create the file** at `docs/postmortems/PM-NNN_short_title.md`
   - Use the template above
   - Fill in all sections from the incident analysis

3. **Also update docs/POSTMORTEM_INDEX.md** (if it exists):
   ```markdown
   | PM | Title | Date | Cause-Class | Recurring? |
   |---|---|---|---|---|
   | [PM-NNN](postmortems/PM-NNN_short_title.md) | [Title] | YYYY-MM-DD | [Tags] | [Link to prior if recurring] |
   ```

---

## Step 6: Generate Implementation Specs for ONE AND DONE Fixes

For each ONE AND DONE structural fix:

1. **Create a feature spec** using speckit.specify:
   - Call: `/speckit.specify "ONE AND DONE: [Fix Title]"`
   - Provide context from the postmortem
   - Let speckit generate the spec in a new folder: `specs/SPXXX-[fix-title]/`

2. **Result**: speckit will generate:
   - `spec.md` (requirements, acceptance criteria)
   - `plan.md` (implementation phases, architecture)
   - `tasks.md` (actionable tasks, dependency-ordered)

3. **Update the postmortem**:
   - Add the spec link to the "Fixes Pending" section
   - Add the task list link

4. **Update docs/POSTMORTEM_INDEX.md**:
   - Link the postmortem to its corresponding spec

---

## Step 7: Report to User

1. **Output the postmortem summary**:
   ```
   Postmortem complete: docs/postmortems/PM-NNN_short_title.md
   
   ## Summary
   
   **Incident**: [Title]
   **Duration**: [X hours Y minutes]
   **Root Cause**: [Primary cause]
   **ONE AND DONE Fix**: [Structural fix title]
   
   ## Next Steps
   
   Structural fix ready for implementation:
   - Spec: specs/SPXXX-[fix-title]/spec.md
   - Tasks: specs/SPXXX-[fix-title]/tasks.md
   
   To begin implementation, run: /speckit.implement
   ```

2. **Commit the postmortem**:
   ```powershell
   git add docs/postmortems/PM-NNN_short_title.md docs/POSTMORTEM_INDEX.md
   git commit -m "PM-NNN: [Title] postmortem and structural fixes"
   ```

---

## Tools Available

You have access to:
- **view**, **create**, **edit**, **glob**, **grep** — file I/O and navigation
- **powershell** — run commands locally
- **ask_user** — gather clarification from the operator
- **task** (via Copilot) — launch speckit.specify for spec generation
- **sql** — query session database

---

## Done When

- [x] Incident document read and understood
- [x] Five Whys analysis completed and documented
- [x] ONE AND DONE structural fixes designed
- [x] Postmortem document created at `docs/postmortems/PM-NNN_short_title.md`
- [x] All sections completed (Timeline, Root Causes, Five Whys, ONE AND DONE, Lessons)
- [x] Cause-class tags applied
- [x] docs/POSTMORTEM_INDEX.md updated
- [x] Spec/tasks generated for each ONE AND DONE fix (via speckit.specify)
- [x] User notified; postmortem ready for team review