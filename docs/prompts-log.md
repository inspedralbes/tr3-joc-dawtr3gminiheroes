# Prompts Log (OpenSpec + IA Implementation)

## Metadata
- Project: `MiniHeroes` (Unity)
- Feature: `Player Hit Invulnerability`
- Date: `2026-04-21`
- Author: `Arnau` + IA agent

## Chronological Trace

### 2026-04-21 22:45 - Spec proposal (`opsx:propose`)
**Prompt**
```text
Define an OpenSpec proposal for a Unity feature:
"After the player receives damage, activate a short invulnerability window
to avoid instant chained hits."
Generate:
1) foundations.md (context, goals, constraints)
2) spec.md (expected behavior + acceptance tests)
3) plan.md (implementation strategy)
Scope must stay limited to JohnMovement and current UI.
```

**Result**
- Initial OpenSpec created with bounded scope.

**Detected issue**
- Visual feedback requirement was too vague ("some feedback").

**Correction prompt**
```text
Refine spec.md: make visual feedback explicit and testable.
Add acceptance criteria that feedback appears only during invulnerability.
```

**Why prompt changed**
- Needed measurable acceptance criteria for grading and verification.

---

### 2026-04-21 22:56 - First implementation pass (`opsx:apply`)
**Prompt**
```text
Apply the OpenSpec plan in JohnMovement.cs:
- Add DamageInvulnerabilityDuration (serialized)
- Track invulnerableUntil timestamp
- Ignore ReceiveDamage calls while invulnerability is active
- Keep training and death logic unchanged
Do not touch backend/network code.
```

**Result**
- Core timing logic introduced.

**Detected issue**
- In one revision, timer was started before reducing health, causing the first hit to be ignored in some flows.

**Correction prompt**
```text
Fix ReceiveDamage order:
1) validate hit
2) apply damage
3) then set invulnerableUntil
Ensure first valid hit is never blocked.
```

**Why prompt changed**
- Restored spec compliance for first-hit behavior.

---

### 2026-04-21 23:08 - Feedback pass (`opsx:apply`)
**Prompt**
```text
Add minimal visual feedback for invulnerability in JohnMovement:
- blink sprite alpha while invulnerability is active
- reset alpha to 1 when inactive
Keep solution simple and compatible with current OnGUI flow.
```

**Result**
- Visual cue added.

**Detected issue**
- Alpha occasionally remained reduced after menu transitions.

**Correction prompt**
```text
Ensure sprite alpha is forced to 1f when invulnerability ends
and during reset/death transitions to avoid stale visual state.
```

**Why prompt changed**
- Removed visual artifact and stabilized post-state behavior.

---

### 2026-04-21 23:18 - Verification and refinement
**Prompt**
```text
Review implementation against spec acceptance tests:
1) single hit = -1 HP
2) two quick hits = one damage
3) spaced hits = two damage
4) feedback only while active
5) death triggers on valid lethal hit
List mismatches and propose minimal fixes.
```

**Result**
- No critical mismatch after corrections.

## Evidence Summary (Problem -> Prompt Change -> Fix)
1. Visual requirement ambiguous -> prompt asked for explicit testable cue -> spec updated with acceptance test.
2. First-hit order bug -> prompt enforced damage-before-timer sequence -> behavior fixed.
3. Alpha reset artifact -> prompt enforced explicit reset paths -> visual state fixed.

## Attached Artifacts
- `specs/player-hit-invulnerability/foundations.md`
- `specs/player-hit-invulnerability/spec.md`
- `specs/player-hit-invulnerability/plan.md`
- `docs/prompts-log.md`

---

## Real Implementation Run (this repository)

### 2026-04-21 23:32 - First apply in code (`opsx:apply`)
**Prompt**
```text
Implement Player Hit Invulnerability in JohnMovement.cs:
- add DamageInvulnerabilityDuration serialized float (default 0.6)
- block ReceiveDamage while timer is active
- start timer only after a valid hit applies damage
- preserve death/training logic
```

**Detected issue**
- No explicit player-facing feedback was present yet, so spec F4 was only partially covered.

**Correction prompt**
```text
Add visual feedback while invulnerability is active:
- blink player sprite alpha
- show a small "INVULNERABLE" indicator in existing HUD
- force alpha reset to 1f when invulnerability ends and on death/reset transitions
```

**Result**
- Implemented in `main/MiniHeroes/Assets/Scripts/JohnMovement.cs`.
- Timer logic, hit rejection, and visual cue are now aligned with spec.

### 2026-04-21 23:40 - Regression guard refinement
**Prompt**
```text
Harden visual state transitions:
ensure sprite alpha always resets in training reset, death flow, scene restart and logout.
```

**Result**
- Added explicit reset calls in those transitions to avoid stale alpha artifacts.

### 2026-04-21 23:52 - Batch validation evidence
**Command / Prompt**
```text
Run Unity in batchmode to validate compile after changes:
Unity.exe -batchmode -projectPath .../MiniHeroes -quit -logFile .../unity-validate.log
```

**Result**
- Build pipeline completed with `Tundra build success`.
- Log ends with `Exiting batchmode successfully now!`.
- No `error CS` entries detected in compile log.

**Notes**
- Non-blocking licensing warnings appeared in log, but batch execution finished successfully.
