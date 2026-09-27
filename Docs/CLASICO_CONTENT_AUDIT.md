# C8.1h — Clásico Final Content Quality Audit

**Status: AUDIT COMPLETE — corrections applied directly to the production
content source.** This is a content-quality/pedagogical-accuracy pass only.
No mechanic, visual, audio, or timing/scoring changes were made (see
section 12 for the one narrowly-scoped exception: two new validator
fields).

## 1. Executive summary

Every challenge in the four archetypes' shipped production pools —
`ClasicoMicrogameLibrary.cs` — was reviewed against the acceptance rule
"one clearly defensible intended answer, or the item fails." The audit
found:

- **Game Show (TrueFalsePool): clean.** All 8 statements are accounting-
  correct, unambiguous, and free of unjustified absolutes. No changes.
- **Western (ClassificationPool): one real ambiguity.** "Renta del local"
  can mean rent *paid* (expense) or rental *income* (if the business were
  the landlord) — rewritten to the unambiguous "Gasto de alquiler", which
  also fixes a cross-archetype terminology mismatch (Balance already used
  this exact phrase).
- **Balance (DebitCreditPool): the highest-priority findings, as
  expected.** Five transactions left the cash-settlement method
  ("Caja" vs "Bancos") to inference — two only in theory (the ambiguous
  account isn't offered as a choice) and **three where the wrong-but-
  defensible answer was literally one of the four buttons on screen**
  (`dc_cobro_cuenta`, `dc_pago_proveedor`, `dc_seguro_anticipado`). All
  five were rewritten to state the payment method explicitly. Separately,
  and independent of any ambiguity: **all 15 shipped challenges had an
  empty `FeedbackExplanation`**, despite the recap panel having a
  dedicated, already-wired slot for it — every round's "why" was silently
  never shown. All 15 now carry a real explanation.
- **Detective (ErrorDetectionPool): terminology-only findings.** Two items
  used the bare word "Renta" (same rent-paid/rental-income ambiguity as
  Western) and one used unqualified "Servicios" (which reads as service
  *revenue* elsewhere in this same pool) — both renamed to the
  already-correct unambiguous terms this pool uses elsewhere
  ("Alquiler", "Servicios públicos"). One more item's "Préstamos por
  pagar" was renamed to match the exact canonical account name
  ("Préstamo bancario por pagar") Balance and Western already use.

No challenge was found to have an outright **incorrect** intended answer —
every fix in this audit is a wording/terminology precision fix, not a
correctness reversal. No challenge was removed; pool sizes are unchanged.

## 2. Total items reviewed

| Archetype | Pool | Items reviewed |
|---|---|---|
| Western Shootout | `ClassificationPool` | 8 |
| TV Game Show | `TrueFalsePool` | 8 |
| Balance Machine | `DebitCreditPool` | 15 |
| Detective Lineup | `ErrorDetectionPool` | 12 |
| **Total** | | **43** |

`EquationPool` (6 items) was inspected but **excluded from the audit
matrix**: it is explicitly retired, dead content — `ClasicoSessionDirector`
no longer draws it (Balance draws `DebitCreditPool` instead, per C8.1f).
It is never shown to a player, so it carries no pedagogical-accuracy risk
in production. Left untouched.

## 3. Issues found by archetype

| Archetype | Ambiguity | Terminology | Missing explanation | Accounting error |
|---|---|---|---|---|
| Western | 1 | 1 (same item) | 0 (no explanation surface exists) | 0 |
| Game Show | 0 | 0 | 0 (no explanation surface exists) | 0 |
| Balance | 5 (3 actionable, 2 theoretical) | 1 (standardized "en efectivo" phrasing) | 15 (all of them) | 0 |
| Detective | 0 | 4 (3 renames across 2 items + 1 canonical-name fix) | 0 (already present pre-audit) | 0 |

## 4. Ambiguous items found

1. **`cls_renta_local` (Western)** — "Renta del local" is compatible with
   both "rent paid" (GASTO, intended) and "rental income" (INGRESO, if the
   business owned and leased out the property). No transaction context
   exists to rule either out.
2. **`dc_servicio_contado` (Balance)** — "al contado" establishes
   *not-on-credit* but not *which* cash account. Theoretical only: "Bancos"
   is not offered as an option, so no wrong answer is actually selectable.
   Fixed anyway for wording precision and internal consistency.
3. **`dc_compra_mobiliario_contado` (Balance)** — same "al contado" issue
   as #2, same theoretical-only status (Bancos not offered).
4. **`dc_cobro_cuenta` (Balance) — actionable.** "Se cobra una cuenta por
   cobrar" never states cash vs. bank deposit, and **"Bancos" is one of
   the four offered options.** A player reasoning "collections are
   normally deposited to the bank" would defensibly pick Bancos and be
   marked wrong.
5. **`dc_pago_proveedor` (Balance) — actionable.** Same pattern: paying a
   supplier is never tied to a payment method, and Bancos is offered.
6. **`dc_seguro_anticipado` (Balance) — actionable.** Same pattern for a
   prepaid-insurance payment, and Bancos is offered.

All six were rewritten (section 9).

## 5. Incorrect items found

**None.** Every intended debit/credit pair, every True/False truth value,
every Western category, and every Detective anomaly was verified
accounting-correct on its own terms. This audit found *wording precision*
problems, not *correctness* problems.

## 6. Weak distractors found

None. Every distractor set was reviewed for plausibility (section 11):
each wrong option is wrong for a specific, real accounting reason (e.g.
Balance's `dc_servicio_credito` offers "Ventas" as a distractor
specifically to test *merchandise revenue vs. service revenue*, not as a
throwaway absurd option), none are synonyms of the correct answer, and
none are accidentally defensible once the six wording fixes above are
applied.

## 7. Terminology inconsistencies

| Concept | Was | Now | Where |
|---|---|---|---|
| Rent expense | "Renta del local" / "Renta" | "Gasto de alquiler" (Western) / "Alquiler" (Detective, terse-label style) | `cls_renta_local`, `err_gastos_1`, `err_gastosoperativos_1` |
| Utilities expense | bare "Servicios" | "Servicios públicos" | `err_gastos_1` (already correct elsewhere in the same pool) |
| Bank loan payable | "Préstamos por pagar" (generic plural) | "Préstamo bancario por pagar" (canonical, matches Balance/Western exactly) | `err_pasivos_1` |
| Cash-payment phrasing | mixed "mediante efectivo" / "al contado" / "en efectivo" | standardized on "en efectivo" throughout Balance | 6 `DebitCreditPool` items |

`DEBE`/`HABER` (machine labels) and `CARGAR`/`ACREDITAR` (teaching
language) were already used consistently in Balance's own presenter code
and required no changes — confirmed, not modified.

## 8. Difficulty changes

**None applied.** Balance's existing Easy (1–6: single cash transactions)
/ Medium (7–15: credit, collections, payments to creditors, bank loans,
prepaid insurance) split was reviewed and found well-calibrated — every
Medium item tests a genuinely harder concept (credit vs. cash, AR/AP, loan
vs. payable, timing/prepayment), never mere wording difficulty. Detective's
Easy/Medium split (section 10) was likewise reviewed and found
well-calibrated, with one borderline case flagged for human judgment
rather than unilaterally changed (see section 21).

Western and Game Show currently have **no Medium-tier content at all** —
every item defaults to `Difficulty = "easy"` and nothing reads that field
at runtime for either archetype. This is a pre-existing content-breadth
gap, not a defect in the existing items; flagged as a remaining risk
(section 12 of the final report) rather than acted on, since introducing a
new tier is a content-*expansion* decision beyond this audit's "correct
what's wrong" scope.

## 9. Rewritten items

### Western

| ID | Old | New | Why |
|---|---|---|---|
| `cls_renta_local` | "Renta del local" | "Gasto de alquiler" | Removed rent-paid/rental-income ambiguity; matches Balance's existing phrase |

### Balance

| ID | Old transaction | New transaction | Why |
|---|---|---|---|
| `dc_compra_equipo_efectivo` | "Se compra equipo mediante efectivo." | "Se compra equipo y se paga en efectivo." | Standardize cash-payment phrasing |
| `dc_servicio_contado` | "Se presta un servicio al contado." | "Se presta un servicio y se cobra en efectivo." | Remove theoretical Caja/Bancos ambiguity |
| `dc_compra_mobiliario_contado` | "Se compra mobiliario al contado." | "Se compra mobiliario y se paga en efectivo." | Same |
| `dc_cobro_cuenta` | "Se cobra una cuenta por cobrar." | "Se cobra en efectivo una cuenta por cobrar." | **Actionable** — Bancos was an offered, defensible wrong answer |
| `dc_pago_proveedor` | "Se paga una cuenta pendiente a un proveedor." | "Se paga en efectivo una cuenta pendiente a un proveedor." | **Actionable** — same |
| `dc_seguro_anticipado` | "Se paga por anticipado el seguro del negocio." | "Se paga en efectivo, por anticipado, el seguro del negocio." | **Actionable** — same |

All 15 Balance items also gained a `FeedbackExplanation` (see the full
matrix, section "Balance — full audit matrix" below, for the exact text of
each).

### Detective

| ID | Old `Items[]` | New `Items[]` | Why |
|---|---|---|---|
| `err_pasivos_1` | …, "Préstamos por pagar", … | …, "Préstamo bancario por pagar", … | Canonical cross-archetype name |
| `err_gastos_1` | Sueldos, Ventas, "Renta", "Servicios" | Sueldos, Ventas, "Alquiler", "Servicios públicos" | Remove rent/income and service-revenue ambiguity |
| `err_gastosoperativos_1` | …, "Renta", … | …, "Alquiler", … | Same rent-paid/rental-income fix |

No `Explanation` text needed edits — none of the three referenced the
renamed words by name, so the existing "why" text (e.g. "Ventas es una
cuenta de ingreso; las demás son cuentas de gasto.") remained accurate
as-is.

## 10. Removed/replaced items

**None.** Every one of the 43 shipped challenges was repairable by wording
correction alone; none were irreparably ambiguous or incorrect enough to
warrant removal.

## 11. Final pool counts

| Archetype | Easy | Medium | Total |
|---|---|---|---|
| Western (`ClassificationPool`) | 8 | 0 | 8 |
| Game Show (`TrueFalsePool`) | 8 | 0 | 8 |
| Balance (`DebitCreditPool`) | 6 | 9 | 15 |
| Detective (`ErrorDetectionPool`) | 6 | 6 | 12 |
| **Total** | **28** | **15** | **43** |

Unchanged from pre-audit counts — no pool grew or shrank.

## 12. Remaining content risks

- **Western/Game Show have no Medium tier** and no per-content teaching-
  explanation display surface at all (only the shared generic
  "¡Correcto!"/"Incorrecto" banner). Adding either is a content-*and*-
  presentation decision beyond this audit's scope (Western/Game Show
  presentation is frozen/accepted per the brief) — flagged for a future
  phase, not attempted here.
- **`err_ingresosventas_1` (Detective, Medium)** is borderline — see the
  human-review shortlist below.
- Two Balance items (`dc_servicio_contado`, `dc_compra_mobiliario_contado`)
  were fixed for wording precision even though their *current* answer
  options don't actually expose the ambiguity (Bancos isn't offered). If
  either item's `AccountOptions` is ever edited in a future content pass
  to include Bancos, the fix already in place will keep them safe — no
  further action needed, noted for context only.
- `Difficulty` is validated for Balance and Detective but still isn't read
  by any runtime system for *any* archetype (it's authoring metadata
  only, not yet wired into pacing/scoring). Not a content defect, but
  worth knowing before assuming difficulty labels affect live play.

## Balance — full audit matrix (highest-priority pool)

| ID | Difficulty | Transaction (final) | Debit | Credit | Distractors | Ambiguity risk (pre-fix) | Correction | Final explanation |
|---|---|---|---|---|---|---|---|---|
| dc_aporte_capital | easy | El propietario aporta efectivo al negocio. | Caja | Capital | Mobiliario, Cuentas por pagar | None ("efectivo" explicit) | Added explanation only | Aumenta Caja porque ingresa efectivo al negocio; aumenta Capital porque el propietario incrementa su aporte patrimonial. |
| dc_compra_equipo_efectivo | easy | Se compra equipo y se paga en efectivo. | Equipo | Caja | Mobiliario, Gasto de alquiler | None (Bancos not offered) | Standardized phrasing + explanation | Aumenta el activo Equipo; disminuye Caja porque el pago se realizó de inmediato en efectivo. |
| dc_pago_alquiler | easy | Se paga el alquiler del mes en efectivo. | Gasto de alquiler | Caja | Sueldos y salarios, Bancos | None | Added explanation only | Se carga Gasto de alquiler porque es un costo del período; se acredita Caja porque se pagó en efectivo. |
| dc_servicio_contado | easy | Se presta un servicio y se cobra en efectivo. | Caja | Ingresos por servicios | Cuentas por cobrar, Ventas | Theoretical (Bancos not offered) | Wording fix + explanation | Aumenta Caja porque se cobró de inmediato; se acredita Ingresos por servicios (no Ventas) porque el negocio prestó un servicio. |
| dc_pago_sueldos | easy | Se pagan los sueldos del mes en efectivo. | Sueldos y salarios | Caja | Gasto de alquiler, Bancos | None | Added explanation only | Se carga Sueldos y salarios porque es un gasto del período; se acredita Caja porque se pagó en efectivo. |
| dc_compra_mobiliario_contado | easy | Se compra mobiliario y se paga en efectivo. | Mobiliario | Caja | Equipo, Cuentas por pagar | Theoretical (Bancos not offered) | Wording fix + explanation | Aumenta el activo Mobiliario; disminuye Caja porque el pago fue inmediato en efectivo, no a crédito. |
| dc_compra_mobiliario_credito | medium | Se compra mobiliario al crédito. | Mobiliario | Cuentas por pagar | Caja, Préstamo bancario por pagar | None ("al crédito" = trade payable, distinct from a loan) | Added explanation only | Aumenta el activo Mobiliario; aumenta Cuentas por pagar porque la compra 'al crédito' crea una obligación con el proveedor, no un préstamo bancario. |
| dc_compra_inventario_credito | medium | Se compra inventario al crédito. | Inventario | Cuentas por pagar | Mobiliario, Caja | None | Added explanation only | Aumenta el activo Inventario; aumenta Cuentas por pagar porque la mercadería se compró a crédito, no al contado. |
| dc_servicio_credito | medium | Se presta un servicio al crédito. | Cuentas por cobrar | Ingresos por servicios | Caja, Ventas | None | Added explanation only | Aumenta Cuentas por cobrar porque el cobro queda pendiente; se acredita Ingresos por servicios (no Ventas) porque se prestó un servicio, no se vendió mercadería. |
| dc_cobro_cuenta | medium | Se cobra en efectivo una cuenta por cobrar. | Caja | Cuentas por cobrar | Bancos, Ventas | **Actionable** (Bancos offered) | Wording fix + explanation | Aumenta Caja porque se cobró en efectivo; disminuye Cuentas por cobrar porque el cliente salda su deuda. |
| dc_pago_proveedor | medium | Se paga en efectivo una cuenta pendiente a un proveedor. | Cuentas por pagar | Caja | Préstamo bancario por pagar, Bancos | **Actionable** (Bancos offered) | Wording fix + explanation | Disminuye Cuentas por pagar porque se salda la deuda con el proveedor; disminuye Caja porque el pago fue en efectivo. |
| dc_prestamo_bancario | medium | Se obtiene un préstamo bancario, depositado en el banco. | Bancos | Préstamo bancario por pagar | Caja, Cuentas por pagar | None ("el banco" explicit) | Added explanation only | Aumenta Bancos porque el préstamo se depositó en la cuenta bancaria; aumenta Préstamo bancario por pagar porque nace una obligación con el banco. |
| dc_pago_cuota_prestamo | medium | Se paga una cuota del préstamo bancario desde el banco. | Préstamo bancario por pagar | Bancos | Caja, Cuentas por pagar | None ("desde el banco" explicit) | Added explanation only | Disminuye Préstamo bancario por pagar porque se abona a la deuda; disminuye Bancos porque el pago salió de la cuenta bancaria. |
| dc_venta_credito | medium | Se venden mercaderías al crédito. | Cuentas por cobrar | Ventas | Caja, Ingresos por servicios | None | Added explanation only | Aumenta Cuentas por cobrar porque el cobro queda pendiente; se acredita Ventas (no Ingresos por servicios) porque se vendió mercadería. |
| dc_seguro_anticipado | medium | Se paga en efectivo, por anticipado, el seguro del negocio. | Seguro pagado por anticipado | Caja | Bancos, Gasto de alquiler | **Actionable** (Bancos offered) | Wording fix + explanation | Aumenta el activo Seguro pagado por anticipado porque el beneficio aún no se ha consumido; disminuye Caja porque el pago fue en efectivo. |

## Detective — full audit matrix

| ID | Difficulty | RuleLabel | Items (final) | Anomaly | Correction | Explanation |
|---|---|---|---|---|---|---|
| err_activos_1 | easy | CUENTAS DE ACTIVO | Caja, Bancos, Cuentas por cobrar, **Cuentas por pagar** | Cuentas por pagar | None | Cuentas por pagar es una cuenta de pasivo; las demás son cuentas de activo. |
| err_pasivos_1 | easy | CUENTAS DE PASIVO | **Caja**, Cuentas por pagar, Préstamo bancario por pagar, Impuestos por pagar | Caja | Renamed "Préstamos por pagar" → canonical "Préstamo bancario por pagar" | Caja es una cuenta de activo; las demás son cuentas de pasivo. |
| err_gastos_1 | easy | CUENTAS DE GASTO | Sueldos, **Ventas**, Alquiler, Servicios públicos | Ventas | Renamed "Renta"→"Alquiler", "Servicios"→"Servicios públicos" | Ventas es una cuenta de ingreso; las demás son cuentas de gasto. |
| err_ingresos_1 | easy | CUENTAS DE INGRESO | Ventas, Ingresos por intereses, **Inventario**, Comisiones ganadas | Inventario | None | Inventario es una cuenta de activo; las demás son cuentas de ingreso. |
| err_patrimonio_1 | easy | CUENTAS DE PATRIMONIO | Capital, **Bancos**, Utilidades retenidas, Aportes de socios | Bancos | None | Bancos es una cuenta de activo; las demás son cuentas de patrimonio. |
| err_activos_2 | easy | CUENTAS DE ACTIVO | **Sueldos por pagar**, Terreno, Equipo, Mobiliario | Sueldos por pagar | None | Sueldos por pagar es una cuenta de pasivo; las demás son cuentas de activo. |
| err_corrientes_1 | medium | ACTIVOS CORRIENTES | Caja, Bancos, Cuentas por cobrar, **Terreno** | Terreno | None | Terreno es un activo no corriente; las demás son activos corrientes. |
| err_nocorrientes_1 | medium | ACTIVOS NO CORRIENTES | **Caja**, Equipo, Mobiliario, Terreno | Caja | None | Caja es un activo corriente; las demás son activos no corrientes. |
| err_deudoras_1 | medium | CUENTAS DE NATURALEZA DEUDORA | **Cuentas por pagar**, Caja, Gastos, Cuentas por cobrar | Cuentas por pagar | None | Cuentas por pagar aumenta con crédito; las demás aumentan con débito. |
| err_acreedoras_1 | medium | CUENTAS DE NATURALEZA ACREEDORA | Capital, Ingresos por servicios, **Caja**, Cuentas por pagar | Caja | None | Caja aumenta con débito; las demás aumentan con crédito. |
| err_gastosoperativos_1 | medium | GASTOS OPERATIVOS | **Ingresos por intereses**, Sueldos, Alquiler, Servicios públicos | Ingresos por intereses | Renamed "Renta"→"Alquiler" | Ingresos por intereses es una cuenta de ingreso; las demás son gastos operativos. |
| err_ingresosventas_1 | medium | INGRESOS POR VENTAS Y SERVICIOS | Ventas, Ingresos por servicios, **Gasto de publicidad**, Comisiones ganadas | Gasto de publicidad | None (see human-review shortlist — difficulty borderline) | Gasto de publicidad es una cuenta de gasto; las demás son cuentas de ingreso. |

## Western — full audit matrix

| ID | Concept (final) | Category | Correction |
|---|---|---|---|
| cls_cuentas_por_cobrar | Cuentas por cobrar | ACTIVO | None |
| cls_cuentas_por_pagar | Cuentas por pagar | PASIVO | None |
| cls_ventas | Ventas | INGRESO | None |
| cls_sueldos | Sueldos y salarios | GASTO | None |
| cls_inventario | Inventario | ACTIVO | None |
| cls_prestamo_bancario | Préstamo bancario por pagar | PASIVO | None |
| cls_ingresos_intereses | Ingresos por intereses | INGRESO | None |
| cls_renta_local | **Gasto de alquiler** | GASTO | Renamed from "Renta del local" — rent-paid/rental-income ambiguity |

## Game Show — full audit matrix

| ID | Statement | Truth value | Correction |
|---|---|---|---|
| tf_activo_debe | El activo se registra normalmente en el debe. | TRUE | None |
| tf_pasivo_posee | El pasivo representa lo que la empresa posee. | FALSE | None |
| tf_ingresos_patrimonio | Los ingresos aumentan el patrimonio. | TRUE | None |
| tf_gastos_patrimonio | Los gastos disminuyen el patrimonio. | TRUE | None |
| tf_cxp_activo | Cuentas por pagar es una cuenta de activo. | FALSE | None |
| tf_efectivo_corriente | El efectivo es un activo corriente. | TRUE | None |
| tf_cxc_obligacion | Una cuenta por cobrar es una obligación de la empresa. | FALSE | None |
| tf_patrimonio_formula | El patrimonio es igual a Activo menos Pasivo. | TRUE | None |

---

# C8.1i — Content Bank Expansion

## 13. Previous vs. new counts

| Archetype | Previous | New | Added |
|---|---|---|---|
| Western (Classification) | 8 | 30 | 22 |
| Game Show (TrueFalse) | 8 | 30 | 22 |
| Balance (DebitCredit) | 15 | 35 | 20 |
| Detective (ErrorDetection) | 12 | 30 | 18 |
| **Total** | **43** | **125** | **82** |

## 14. Difficulty distribution (final, verified against the shipped pools)

| Archetype | Easy | Medium | Total |
|---|---|---|---|
| Western | 18 | 12 | 30 |
| Game Show | 18 | 12 | 30 |
| Balance | 20 | 15 | 35 |
| Detective | 18 | 12 | 30 |
| **Total** | **74** | **51** | **125** |

Matches the brief's target distribution exactly. Verified by `MicrogameContentValidatorTests` (`ShippedXPool_HasAtLeastNEasyAndMMedium`, one per archetype) counting `Difficulty` on the live shipped pools, not a separately-maintained number.

## 15. Model changes required for this expansion

Two data-model additions, both purely additive and neither touching a presenter:

- `ClassificationChallenge.CategoryOptions` can now be overridden per-challenge (new optional `categoryOptions` parameter on `NewClassification`) instead of always cloning the same 4 labels. Used to add a second, independent 4-way axis — `{ACTIVO CORRIENTE, ACTIVO NO CORRIENTE, PASIVO CORRIENTE, PASIVO NO CORRIENTE}` — for Western's Medium tier. The AimSelect presenter already renders whatever `CategoryOptions` a challenge supplies (confirmed by reading `WesternShootoutPresenter.cs`), so this needed no presenter change — same mechanic, same 4 buttons, new label content.
- `TrueFalseChallenge` gained a `FeedbackExplanation` field (mirroring `DebitCreditChallenge`'s existing one). Game Show's presenter has no explanation-display mechanism today (confirmed absent in the C8.1h audit) and none was added — Game Show's presentation is frozen. The field exists so every statement's "why" is authored, validated, and ready for a future presentation hookup, rather than being lost.
- Both `ClassificationChallenge` and `TrueFalseChallenge` now have their `Difficulty` field validated (`"easy"`/`"medium"` only), matching the rule `DebitCreditChallenge`/`ErrorDetectionChallenge` already enforced. Previously unvalidated because neither pool varied difficulty before this phase.

No mechanic, presenter, audio, or scoring code was touched.

## 16. Concept coverage matrix

**Western (Classification)** — CategoryOptions in parentheses where non-standard.

| Concept | Difficulty | Count |
|---|---|---|
| Activo (broad family) | Easy | 6 |
| Pasivo (broad family) | Easy | 4 |
| Ingreso (broad family) | Easy | 4 |
| Gasto (broad family) | Easy | 4 |
| Activo corriente/no corriente (CorrienteCategories) | Medium | 6 |
| Gasto vs. activo prepagado | Medium | 3 |
| Ingreso vs. pasivo por ingreso diferido | Medium | 2 |
| Activo intangible | Medium | 1 |

**Game Show (TrueFalse)**

| Concept | Difficulty | Count |
|---|---|---|
| Normal balances (activo/pasivo/ingreso/gasto/capital, debe/haber) | Easy | 8 |
| Basic classification (activo/pasivo family) | Easy | 3 |
| Accounting equation / double-entry | Easy | 2 |
| Effects of transactions on equity/equation | Easy | 1 |
| Accrual vs. cash timing (devengado) | Medium | 2 |
| Normal-balance consequences (cargar/acreditar effect) | Medium | 2 |
| Current/non-current classification | Medium | 2 |
| Revenue recognition / deferred revenue | Medium | 2 |
| Transaction effects on the equation | Medium | 2 |
| Common misconception (gasto en efectivo ≠ pasivo) | Medium | 1 |
| Effect of collecting a receivable on equity | Medium | 1 |

**Balance (DebitCredit)**

| Concept | Difficulty | Count |
|---|---|---|
| Cash sale/purchase (Caja) | Easy | 6 |
| Bank payment/purchase (Bancos/transferencia) | Easy | 6 |
| Owner capital contribution | Easy | 2 |
| Owner withdrawal | Easy | 1 |
| Cash-to-bank transfer | Easy | 1 |
| Basic service revenue | Easy | 2 |
| Credit purchase (mobiliario/inventario) | Medium | 2 |
| Credit sale (mercadería/servicio) | Medium | 2 |
| Collection/payment of a pre-existing balance | Medium | 3 |
| Bank loan received/repaid | Medium | 2 |
| Prepaid asset (seguro) | Medium | 1 |
| Customer advance / unearned revenue (both directions) | Medium | 2 |
| Accrued expense (both directions) | Medium | 2 |
| Accrued revenue (interest receivable) | Medium | 1 |

**Detective (ErrorDetection)**

| Concept | Difficulty | Count |
|---|---|---|
| Activo (broad family) | Easy | 5 |
| Pasivo (broad family) | Easy | 4 |
| Ingreso (broad family) | Easy | 4 |
| Gasto (broad family) | Easy | 3 |
| Patrimonio (broad family) | Easy | 2 |
| Activo corriente/no corriente | Medium | 2 |
| Pasivo no corriente | Medium | 1 |
| Naturaleza deudora/acreedora | Medium | 4 |
| Gastos operativos | Medium | 2 |
| Activos pagados por anticipado | Medium | 1 |
| Ingreso vs. cuenta por cobrar (boundary) | Medium | 1 |

(Not saved as runtime data, per the brief — this table is documentation only.)

## 17. Near-duplicates identified and reworked during authoring

Caught and corrected before shipping (none of these reached the final pool in duplicate form):

1. **Game Show**: an early draft of a Medium item read "Cuando se vende mercadería al crédito, el activo total de la empresa no cambia." (False) — rejected because its truth depends on an unstated assumption (sale at cost with zero margin), which a competent accountant could reasonably dispute. Reworked to `tf_venta_credito_efectos`, which asserts only the two unconditional bookkeeping effects (Cuentas por cobrar up, Inventario down) instead of a net-total claim.
2. **Balance**: an early draft for "accrued revenue" reused `dc_servicio_credito`'s exact debit/credit pair (Cuentas por cobrar / Ingresos por servicios) with only the transaction wording changed — a textbook near-duplicate by the brief's own definition (same reasoning path, same answer). Reworked to `dc_interes_por_cobrar_acumulado`, which uses a genuinely different account pair (Intereses por cobrar / Ingresos por intereses) and a different recognition scenario (accrued interest, not a credit sale).
3. **Balance**: considered adding several more "pay a gasto in cash" Easy items differing only in which specific gasto account was named (papelería/publicidad/mantenimiento/servicios públicos). Kept these — each forces distinguishing a specific, plausible expense account name from a set of plausible-sounding distractors, which is real scenario-vocabulary reasoning, not a cosmetic amount/name swap — but diversified payment method across them (mixed Caja/Bancos) specifically so they aren't mechanically identical to each other.
4. **Western**: reused the exact concept "Terreno", "Cuentas por pagar", "Impuestos por pagar" account names across both a broad-family Easy item and a corriente/no-corriente Medium item was considered, then avoided — final Medium corriente items use entirely fresh concepts (Edificio, Vehículo de reparto, Documentos por cobrar/pagar, Intereses por pagar, Hipoteca) not reused from the Easy tier, to eliminate any appearance of a same-concept duplicate even though the CategoryOptions and reasoning differ.

## 18. Ambiguous drafts rejected or reworked

Beyond item 1 in section 17 above (Game Show margin-assumption item), no other draft item reached a state requiring rejection — every other new item was checked against the C8.1h ambiguity-pair list (Caja/Bancos, Cuentas por cobrar/cliente vs Documentos por cobrar, Cuentas por pagar vs Documentos por pagar vs Préstamo, payment method, credit vs cash, timing/accrual) at draft time and written with explicit disambiguating language from the start (`"en efectivo"`, `"mediante transferencia bancaria"`, `"al crédito"`, `"a corto/largo plazo"`, `"ya consumidos"` vs `"por adelantado para uso futuro"`, `"aún no prestado"` vs `"ya prestado y cobrado"`).

## 19. Final quality findings (second audit pass, all 125 challenges)

Re-audited all 125 challenges (not only the 82 new ones) against the C8.1h acceptance rule:

- **Unique intended answer**: confirmed for all 125. No item found where a competent accountant could defend two answers.
- **Accounting correctness**: confirmed for all 125 — every debit/credit pair, classification, and normal-balance assignment verified.
- **Ambiguity**: none found in the 82 new items beyond the one caught and reworked in section 17. The 43 pre-existing items were already cleared in the C8.1h pass and were not reopened except where cross-referenced above.
- **Distractor quality**: every new item's wrong options are plausible-but-wrong for a specific, statable reason (a real account of the wrong family/timing/method), never a synonym or an absurd option.
- **Difficulty placement**: every Medium item's difficulty comes from an accounting concept (corriente/no corriente, accrual timing, normal-balance direction, prepaid vs. consumed, deferred vs. earned revenue) — none is Medium merely because of confusing wording.
- **Terminology**: all new items reuse the exact canonical account names established in C8.1h (Caja, Bancos, Cuentas por cobrar/pagar, Préstamo bancario por pagar, Gasto de alquiler, Servicios públicos) plus a small number of new canonical accounts introduced this phase and used consistently everywhere they recur: Documentos por cobrar, Documentos por pagar, Hipoteca por pagar, Intereses por pagar, Intereses por cobrar, Anticipo de clientes, Retiros del propietario, Vehículo de reparto, Edificio, Marca registrada, Gasto de papelería, Gasto de mantenimiento, Ingresos por arrendamiento.
- **Explanation coverage**: 100% — every Balance item (35/35) and every Game Show item (30/30, backfilled onto the original 8 as well as the 22 new) now carries a `FeedbackExplanation`. Detective's 30/30 carry `Explanation` (required since C8.1g.2). Western has no explanation field (not required by the brief for this archetype, and Western's presenter has no slot for one).

## 20. Human-review shortlist (new items)

Carried over from C8.1h, still open:
- `err_ingresosventas_1` — Medium difficulty placement was borderline in the original audit; not reopened this phase.
- `WesternShootoutPresenter` lifecycle: `WesternShootout_Abort_DuringOutlawSettleOverlap_StopsCleanly_AndReEntryIsClean` reproduced 3/3 in isolation in the prior session — a lifecycle concern, explicitly not touched by this content-only phase per the brief's section 19.

New from this phase — flagged for judgment, not silently resolved:
1. **`cls_marca_registrada`** ("Marca registrada de la empresa" → ACTIVO, Medium) — intangible-asset recognition is a slightly more abstract leap than the other Medium items (no physical form, no "corriente/no corriente" cue in the wording). Confirm the wording alone is enough for a player who has only seen tangible-asset examples so far.
2. **`err_ingresos_5`** (CUENTAS DE INGRESO, anomaly = Cuentas por cobrar) — this pairs an asset against three income accounts; double-check the RuleLabel reads unambiguously as "which of these IS an income account" rather than "which is related to income," since Cuentas por cobrar does arise from a sale. The Explanation states this directly, but it's worth a second pair of eyes given how close the concepts are.
3. **The three same-pattern "pay a gasto in cash/bank" Easy Balance items** (`dc_compra_papeleria_efectivo`, `dc_pago_publicidad_efectivo`, `dc_pago_mantenimiento_transferencia`) — kept per the reasoning in section 17 item 3, but flagged in case a reviewer judges this specific trio still reads as repetitive in actual play sequence (they're spread across a 35-item pool so back-to-back appearance is unlikely, but not impossible).
4. **Western's total Medium/Easy split for "corriente" content** (6 of Western's 12 Medium items share the same `CorrienteCategories` option set) — a deliberate concentration for a coherent teaching arc, but worth confirming this doesn't read as repetitive if several land in the same session (Western's shuffle draws individual challenges, not concept clusters, so this is a probability judgment call, not a certainty).
