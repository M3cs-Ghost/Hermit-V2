# Clásico — Release Candidate Manual Checklist (C8.1m)

Run against the real Windows executable (`Builds/Windows/Dev/Hermit V2.exe`) —
it is the authority for visuals, audio and pacing. Test at 1280×720 **and**
1920×1080. Mark each: ✅ pass · ⚠️ note · ❌ fail (with a screenshot).

Tester: ________ Date: ________ Build timestamp: ________

## 1. Session flow (all archetypes)

- [ ] Launch Clásico → countdown → first microgame; no black/empty frame.
- [ ] Full 9-round session reaches Results; score/accuracy look right.
- [ ] **Salir** mid-round → Results immediately; **no sound continues**.
- [ ] Volver → selector → launch again: fresh session, no leftover visuals.
- [ ] Reintentar from Results starts a clean session.

## 2. Western (3-round Encounter)

- [ ] Round 1 cinematic plays; **no wooden ConceptSign before/during it**.
- [ ] Sign appears with the outlaws; account text fits the sign.
- [ ] No "DISPARA" text anywhere (removed in C8.1p); input going live is still obvious from the sign + outlaws/reticle.
- [ ] All 4 outlaw nameplates readable, nothing spills off the plank.
- [ ] Correct shot / wrong shot (countershot + red flash) / timeout (no fake player shot).
- [ ] Rounds 2–3: quick reset, outlaws back to neutral.
- [ ] Salir during the cinematic: no gunshot/music afterwards.

## 3. Balance

- [ ] Debit pick → credit pick flow is clear.
- [ ] Correct / partial / incorrect / timeout each show the right verdict.
- [ ] Teaching recap fully readable before the round ends.
- [ ] No audio tail after Salir or into the next round.

## 4. Detective

- [ ] Spotlight follows keyboard/mouse focus; decision window feels ~4.2s.
- [ ] Correct accusation / wrong accusation (short shake) / timeout.
- [ ] Only the real anomaly shows **IMPOSTOR**; no shared "¡Correcto!" banner over the scene.
- [ ] Recap (Regla / Impostor / explicación) readable, ~1s longer than before.
- [ ] Long names stay on the dossier paper in ≤2 lines — e.g. *Préstamo bancario a largo plazo*, *Documentos por pagar a largo plazo*, *Alquiler pagado por anticipado*.
- [ ] Concise account names (C8.1n): *Préstamo bancario*, *Papelería y útiles*, *Gasto por papelería*, *Edificio*, *Marca registrada*, *Intereses por pagar*, *Anticipo de clientes*, *Gasto de publicidad* read naturally in every archetype.
- [ ] Rule labels read "CUENTAS DEL ACTIVO / DEL PASIVO".

## 5. Game Show

- [ ] Opening (~1.5s): host entrance → PREMIO $1,000,000 above the host → statement slides in; no input until choices light up.
- [ ] Plaque never covers the host's head; statement/HUD not overlapped.
- [ ] Decision window feels ~4.2s.
- [ ] Answer lock → short suspense → reveal (correct: green/gold + confetti; wrong: muted red + correct zone confirmed; timeout: correct zone confirmed).
- [ ] Explanation card readable (~1s longer than before).
- [ ] **Silent** (no hiss/ocean/tones) — only real GameShow_* clips would play; none ship yet.
- [ ] Salir mid-reveal and re-enter: no confetti/tags/card/audio residue; "PREGUNTA 1" again.

## 6. Cross-cutting

- [ ] Readability of every statement/label/explanation at both resolutions.
- [ ] No visual residue between rounds (tints, scales, flashes, overlays).
- [ ] No audio residue between rounds or after exit.
- [ ] Pacing: does a full session feel too slow / too fast?

## 7. User-test observations

| # | Archetype | Observation (what the user did/said) | Severity |
|---|---|---|---|
| 1 | | | |
| 2 | | | |
| 3 | | | |

Open questions to answer from user tests: is 4.2s enough (vs 5s) for Detective
and Game Show? Is the explanation hold long enough?
