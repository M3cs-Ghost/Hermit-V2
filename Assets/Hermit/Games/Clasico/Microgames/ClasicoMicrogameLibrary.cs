using System;
using System.Collections.Generic;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// C8.1's sample content pools plus the archetype-sequencing helper —
    /// deliberately in-code, not a ScriptableObject asset. This phase is a
    /// prototype vertical slice ("no content migration masiva", per
    /// Docs/C8_1_GOLD_MICROGAME_SLICE.md) — a designer-editable
    /// Inspector-driven content pipeline for microgames is real future work,
    /// not something four sample pools with no second author need yet. Enough
    /// entries per pool for each Gold microgame to appear multiple times in
    /// one run without repeating (see <see cref="ClasicoSessionDirector"/>).
    /// </summary>
    public static class ClasicoMicrogameLibrary
    {
        private const string CategoryActivo = "ACTIVO";
        private const string CategoryPasivo = "PASIVO";
        private const string CategoryIngreso = "INGRESO";
        private const string CategoryGasto = "GASTO";
        private static readonly string[] FourCategories = { CategoryActivo, CategoryPasivo, CategoryIngreso, CategoryGasto };

        public static readonly IReadOnlyList<ClassificationChallenge> ClassificationPool = new[]
        {
            NewClassification("cls_cuentas_por_cobrar", "Cuentas por cobrar", CategoryActivo),
            NewClassification("cls_cuentas_por_pagar", "Cuentas por pagar", CategoryPasivo),
            NewClassification("cls_ventas", "Ventas", CategoryIngreso),
            NewClassification("cls_sueldos", "Sueldos y salarios", CategoryGasto),
            NewClassification("cls_inventario", "Inventario", CategoryActivo),
            NewClassification("cls_prestamo_bancario", "Préstamo bancario por pagar", CategoryPasivo),
            NewClassification("cls_ingresos_intereses", "Ingresos por intereses", CategoryIngreso),
            NewClassification("cls_renta_local", "Renta del local", CategoryGasto),
        };

        public static readonly IReadOnlyList<TrueFalseChallenge> TrueFalsePool = new[]
        {
            NewTrueFalse("tf_activo_debe", "El activo se registra normalmente en el debe.", true),
            NewTrueFalse("tf_pasivo_posee", "El pasivo representa lo que la empresa posee.", false),
            NewTrueFalse("tf_ingresos_patrimonio", "Los ingresos aumentan el patrimonio.", true),
            NewTrueFalse("tf_gastos_patrimonio", "Los gastos disminuyen el patrimonio.", true),
            NewTrueFalse("tf_cxp_activo", "Cuentas por pagar es una cuenta de activo.", false),
            NewTrueFalse("tf_efectivo_corriente", "El efectivo es un activo corriente.", true),
            NewTrueFalse("tf_cxc_obligacion", "Una cuenta por cobrar es una obligación de la empresa.", false),
            NewTrueFalse("tf_patrimonio_formula", "El patrimonio es igual a Activo menos Pasivo.", true),
        };

        public static readonly IReadOnlyList<EquationChallenge> EquationPool = new[]
        {
            NewEquation("eq_500_300", 500f, 300f, 50f, 3),
            NewEquation("eq_800_650", 800f, 650f, 25f, 3),
            NewEquation("eq_1200_700", 1200f, 700f, 100f, 2),
            NewEquation("eq_300_100", 300f, 100f, 50f, 2),
            NewEquation("eq_1000_400", 1000f, 400f, 100f, 2),
            NewEquation("eq_650_150", 650f, 150f, 50f, 3),
        };

        public static readonly IReadOnlyList<ErrorDetectionChallenge> ErrorDetectionPool = new[]
        {
            NewErrorDetection("err_activos", "Cuentas de Activo", new[] { "Caja", "Inventario", "Cuentas por cobrar", "Cuentas por pagar" }, 3),
            NewErrorDetection("err_pasivos", "Cuentas de Pasivo", new[] { "Cuentas por pagar", "Préstamos por pagar", "Impuestos por pagar", "Caja" }, 3),
            NewErrorDetection("err_gastos", "Cuentas de Gasto", new[] { "Sueldos", "Renta", "Servicios", "Ventas" }, 3),
            NewErrorDetection("err_ingresos", "Cuentas de Ingreso", new[] { "Ventas", "Ingresos por intereses", "Comisiones ganadas", "Inventario" }, 3),
            NewErrorDetection("err_corrientes", "Activos Corrientes", new[] { "Caja", "Bancos", "Cuentas por cobrar", "Terreno" }, 3),
            NewErrorDetection("err_deudoras", "Cuentas Deudoras", new[] { "Caja", "Inventario", "Gastos", "Cuentas por pagar" }, 3),
        };

        /// <summary>Builds the run's archetype order: every archetype appears
        /// at least twice for a count of 8+ (cycling AimSelect/ChooseSide/
        /// Balance/DetectError), then shuffled until no two adjacent slots
        /// share an archetype — "no reshuffling questions alone" from the
        /// Design Lock applies here too: variety comes from *order*, not
        /// just content.
        ///
        /// Rejection sampling (reshuffle-and-check) rather than patching a
        /// bad shuffle in place: an earlier in-place "swap the clash away"
        /// version could provably cycle back to its own starting state when
        /// three copies of one archetype clustered together (see
        /// Docs/C8_1_GOLD_MICROGAME_SLICE.md, "Migration strategy" for the
        /// actual failing case a test caught). Reshuffling from scratch has
        /// no such failure mode — each attempt is independently checked, and
        /// for an array this small and this lightly loaded, a duplicate-free
        /// shuffle is common, not rare.</summary>
        public static MicrogameArchetype[] BuildSequence(int count, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var archetypes = new[] { MicrogameArchetype.AimSelect, MicrogameArchetype.ChooseSide, MicrogameArchetype.Balance, MicrogameArchetype.DetectError };
            var sequence = new List<MicrogameArchetype>(count);
            for (var i = 0; i < count; i++)
            {
                sequence.Add(archetypes[i % archetypes.Length]);
            }

            const int maxAttempts = 500;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                Shuffle(sequence, rng);
                if (!HasAdjacentDuplicate(sequence))
                {
                    return sequence.ToArray();
                }
            }

            // Never expected to be reached for any count this phase actually
            // uses (500 independent shuffles of a lightly-loaded array), but
            // the unshuffled round-robin order itself is always duplicate-free
            // by construction (cycling through all 4 archetypes before any
            // repeat) — a safe, simple fallback rather than returning a
            // known-bad sequence.
            var fallback = new MicrogameArchetype[count];
            for (var i = 0; i < count; i++)
            {
                fallback[i] = archetypes[i % archetypes.Length];
            }

            return fallback;
        }

        /// <summary>Internal (not private) so <see cref="ClasicoSessionDirector"/>
        /// can reuse the exact same no-adjacent-duplicate check when building
        /// its own smaller Western-Encounter-aware sequence (C8.1d.1) instead
        /// of re-deriving an equivalent rule by hand.</summary>
        internal static bool HasAdjacentDuplicate(IReadOnlyList<MicrogameArchetype> sequence)
        {
            for (var i = 1; i < sequence.Count; i++)
            {
                if (sequence[i].Equals(sequence[i - 1]))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static ClassificationChallenge NewClassification(string id, string concept, string correctCategory)
        {
            return new ClassificationChallenge
            {
                Id = id,
                ConceptLabel = concept,
                CorrectCategory = correctCategory,
                CategoryOptions = (string[])FourCategories.Clone(),
            };
        }

        private static TrueFalseChallenge NewTrueFalse(string id, string statement, bool isTrue)
        {
            return new TrueFalseChallenge { Id = id, Statement = statement, IsTrue = isTrue };
        }

        private static EquationChallenge NewEquation(string id, float activo, float pasivo, float step, int startOffsetSteps)
        {
            var correct = activo - pasivo;
            return new EquationChallenge
            {
                Id = id,
                KnownValueA = activo,
                KnownValueB = pasivo,
                CorrectValue = correct,
                StepSize = step,
                StartValue = correct - step * startOffsetSteps,
            };
        }

        private static ErrorDetectionChallenge NewErrorDetection(string id, string groupLabel, string[] items, int anomalyIndex)
        {
            return new ErrorDetectionChallenge { Id = id, GroupLabel = groupLabel, Items = items, AnomalyIndex = anomalyIndex };
        }
    }
}
