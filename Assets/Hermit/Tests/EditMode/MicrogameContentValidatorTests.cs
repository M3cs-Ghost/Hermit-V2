using System.Linq;
using NUnit.Framework;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Tests.EditMode
{
    public class MicrogameContentValidatorTests
    {
        /// <summary>C8.1n account-naming cleanup: concise account names for
        /// the Papelería / Edificio / Préstamo bancario families — same IDs,
        /// classifications and difficulties — and the old verbose names no
        /// longer ship in ANY account-name field of any pool.</summary>
        [Test]
        public void AccountNames_ConciseFamilies_ClassificationsUnchanged_OldVerboseNamesGone()
        {
            void AssertWestern(string id, string label, string category, string difficulty)
            {
                var c = ClasicoMicrogameLibrary.ClassificationPool.Single(x => x.Id == id);
                Assert.AreEqual(label, c.ConceptLabel, id);
                Assert.AreEqual(category, c.CorrectCategory, id);
                Assert.AreEqual(difficulty, c.Difficulty, id);
            }

            AssertWestern("cls_prestamo_bancario", "Préstamo bancario", "PASIVO", "easy");
            AssertWestern("cls_prestamo_largo_plazo", "Préstamo bancario a largo plazo", "PASIVO NO CORRIENTE", "medium");
            AssertWestern("cls_edificio_nocorriente", "Edificio", "ACTIVO NO CORRIENTE", "medium");
            AssertWestern("cls_papeleria_anticipada", "Papelería y útiles", "ACTIVO", "medium");
            AssertWestern("cls_papeleria_consumida", "Gasto por papelería", "GASTO", "medium");

            var balance = ClasicoMicrogameLibrary.DebitCreditPool;
            Assert.AreEqual("Préstamo bancario", balance.Single(x => x.Id == "dc_prestamo_bancario").CorrectCreditAccount);
            Assert.AreEqual("Préstamo bancario", balance.Single(x => x.Id == "dc_pago_cuota_prestamo").CorrectDebitAccount);
            Assert.AreEqual("Gasto por papelería", balance.Single(x => x.Id == "dc_compra_papeleria_efectivo").CorrectDebitAccount);

            var detective = ClasicoMicrogameLibrary.ErrorDetectionPool;
            var prepaid = detective.Single(x => x.Id == "err_prepagados_1");
            CollectionAssert.Contains(prepaid.Items, "Papelería y útiles");
            Assert.AreEqual(3, prepaid.AnomalyIndex, "The prepaid-assets anomaly slot must be unchanged.");
            Assert.AreEqual("Gasto de publicidad", prepaid.Items[prepaid.AnomalyIndex], "The prepaid-assets anomaly is the expense (C8.1o: concise name).");
            var longTerm = detective.Single(x => x.Id == "err_pasivos_nocorrientes_1");
            CollectionAssert.Contains(longTerm.Items, "Préstamo bancario a largo plazo");
            Assert.AreEqual("Cuentas por pagar", longTerm.Items[longTerm.AnomalyIndex], "The non-current-liabilities anomaly must be unchanged.");

            var accountNames = ClasicoMicrogameLibrary.ClassificationPool.Select(c => c.ConceptLabel)
                .Concat(balance.SelectMany(c => c.AccountOptions.Append(c.CorrectDebitAccount).Append(c.CorrectCreditAccount)))
                .Concat(detective.SelectMany(c => c.Items))
                .ToList();
            foreach (var retired in new[]
                     {
                         "Préstamo bancario por pagar", "Préstamo bancario por pagar a largo plazo", "Edificio de la empresa",
                         "Papelería comprada por adelantado para uso futuro", "Papelería y útiles comprados por adelantado para uso futuro",
                         "Papelería y útiles ya consumidos", "Gasto de papelería",
                     })
            {
                CollectionAssert.DoesNotContain(accountNames, retired, $"Retired verbose account name '{retired}' still ships.");
            }
        }

        /// <summary>C8.1o final consistency polish: the last verbose account
        /// labels are concise (same IDs, classifications, difficulties,
        /// options), the Detective explanation that named a renamed item
        /// stays in sync, and the pool is still 125.</summary>
        [Test]
        public void AccountNames_FinalConsistencyPolish_LabelsConcise_LogicUnchanged()
        {
            void AssertWestern(string id, string label, string category)
            {
                var c = ClasicoMicrogameLibrary.ClassificationPool.Single(x => x.Id == id);
                Assert.AreEqual(label, c.ConceptLabel, id);
                Assert.AreEqual(category, c.CorrectCategory, id);
                Assert.AreEqual("medium", c.Difficulty, id);
            }

            AssertWestern("cls_marca_registrada", "Marca registrada", "ACTIVO");
            AssertWestern("cls_intereses_por_pagar", "Intereses por pagar", "PASIVO CORRIENTE");
            AssertWestern("cls_anticipo_clientes", "Anticipo de clientes", "PASIVO");
            CollectionAssert.AreEqual(new[] { "ACTIVO CORRIENTE", "ACTIVO NO CORRIENTE", "PASIVO CORRIENTE", "PASIVO NO CORRIENTE" },
                ClasicoMicrogameLibrary.ClassificationPool.Single(x => x.Id == "cls_intereses_por_pagar").CategoryOptions);

            var prepaid = ClasicoMicrogameLibrary.ErrorDetectionPool.Single(x => x.Id == "err_prepagados_1");
            StringAssert.StartsWith("Gasto de publicidad es un gasto del período", prepaid.Explanation);

            var gastosOperativos = ClasicoMicrogameLibrary.ErrorDetectionPool.Single(x => x.Id == "err_gastosoperativos_2");
            Assert.AreEqual(0, gastosOperativos.AnomalyIndex);
            Assert.AreEqual("Comisiones ganadas", gastosOperativos.Items[0]);
            StringAssert.StartsWith("Comisiones ganadas es una cuenta de ingreso", gastosOperativos.Explanation,
                "The explanation must explain the actual shipped odd-one-out.");

            var accountNames = ClasicoMicrogameLibrary.ClassificationPool.Select(c => c.ConceptLabel)
                .Concat(ClasicoMicrogameLibrary.DebitCreditPool.SelectMany(c => c.AccountOptions))
                .Concat(ClasicoMicrogameLibrary.ErrorDetectionPool.SelectMany(c => c.Items))
                .ToList();
            foreach (var retired in new[]
                     {
                         "Marca registrada de la empresa", "Intereses por pagar del mes", "Gasto de publicidad ya consumido",
                         "Anticipo de clientes por servicios aún no prestados",
                     })
            {
                CollectionAssert.DoesNotContain(accountNames, retired, $"Retired verbose account name '{retired}' still ships.");
            }

            var total = ClasicoMicrogameLibrary.ClassificationPool.Count + ClasicoMicrogameLibrary.TrueFalsePool.Count
                        + ClasicoMicrogameLibrary.DebitCreditPool.Count + ClasicoMicrogameLibrary.ErrorDetectionPool.Count;
            Assert.AreEqual(125, total);
        }

        /// <summary>C8.1o mismatch-class guard: every Detective explanation
        /// must name its own challenge's actual odd-one-out (the recap
        /// explains the impostor the player just saw).</summary>
        [Test]
        public void ShippedErrorDetectionPool_EveryExplanationNamesItsOwnAnomaly()
        {
            foreach (var challenge in ClasicoMicrogameLibrary.ErrorDetectionPool)
            {
                var anomaly = challenge.Items[challenge.AnomalyIndex];
                StringAssert.Contains(anomaly.ToLowerInvariant(), challenge.Explanation.ToLowerInvariant(),
                    $"'{challenge.Id}' explanation does not name its own anomaly '{anomaly}'.");
            }
        }

        /// <summary>C8.1m release-candidate gate: the exact production pool
        /// sizes (Western 30, Game Show 30, Balance 35, Detective 30 = 125)
        /// and no challenge ID reused anywhere across the four pools.</summary>
        [Test]
        public void ProductionPools_HaveExactReleaseCounts_AndUniqueIdsAcrossAllPools()
        {
            Assert.AreEqual(30, ClasicoMicrogameLibrary.ClassificationPool.Count, "Western (Classification)");
            Assert.AreEqual(30, ClasicoMicrogameLibrary.TrueFalsePool.Count, "Game Show (TrueFalse)");
            Assert.AreEqual(35, ClasicoMicrogameLibrary.DebitCreditPool.Count, "Balance (DebitCredit)");
            Assert.AreEqual(30, ClasicoMicrogameLibrary.ErrorDetectionPool.Count, "Detective (ErrorDetection)");

            var ids = ClasicoMicrogameLibrary.ClassificationPool.Select(c => c.Id)
                .Concat(ClasicoMicrogameLibrary.TrueFalsePool.Select(c => c.Id))
                .Concat(ClasicoMicrogameLibrary.DebitCreditPool.Select(c => c.Id))
                .Concat(ClasicoMicrogameLibrary.ErrorDetectionPool.Select(c => c.Id))
                .ToList();
            Assert.AreEqual(125, ids.Count);
            var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            CollectionAssert.IsEmpty(duplicates, $"Duplicate challenge IDs: {string.Join(", ", duplicates)}");
        }

        /// <summary>C8.1m release-candidate gate: every Gold sprite the four
        /// presenters load must resolve from Resources (a missing one would
        /// silently degrade to the procedural fallback at runtime).</summary>
        [Test]
        public void ProductionGoldSprites_AllResolve()
        {
            var paths = new[]
            {
                "Art/Gold/Western/WesternBackground",
                "Art/Gold/Western/UI/Western_ConceptSign",
                "Art/Gold/Western/UI/Western_OutlawNameplate",
                "Art/Gold/Western/Cinematic/Western_Sheriff_Closeup",
                "Art/Gold/Western/Cinematic/Western_OutlawA_Closeup",
                "Art/Gold/Western/Cinematic/Western_Sheriff_HandGun_Closeup",
                "Art/Gold/GameShow/GameShowBackground",
                "Art/Gold/GameShow/Actors/Presentador_Gameplay_01",
                "Art/Gold/Balance/Environment/Balance_Chamber",
                "Art/Gold/Balance/Machine/Balance_MachineMaster",
                "Art/Gold/Balance/Tokens/Balance_AccountToken",
                "Art/Gold/Detective/Characters/Detective_Auditor",
                "Art/Gold/Detective/Environment/Detective_LineupRoom",
                "Art/Gold/Detective/Suspects/Detective_SuspectDossier",
            };

            var missing = paths.Where(p => UnityEngine.Resources.Load<UnityEngine.Sprite>(p) == null).ToList();
            CollectionAssert.IsEmpty(missing, $"Missing production Gold sprites: {string.Join(", ", missing)}");
        }

        /// <summary>C8.1k.1 manual review: the Western label "Ingresos por
        /// servicios ya prestados y cobrados" read like a series title and
        /// was replaced with the plain account name; C8.1k.2 made it
        /// "Ingresos por servicios devengados" so it never duplicates the
        /// Easy cls_ingresos_por_servicios wording — classification,
        /// difficulty and options must be untouched.</summary>
        [Test]
        public void Western_IngresoServicioDevengado_UsesThePlainAccountName_ClassificationUnchanged()
        {
            var challenge = ClasicoMicrogameLibrary.ClassificationPool.Single(c => c.Id == "cls_ingreso_servicio_devengado");

            Assert.AreEqual("Ingresos por servicios devengados", challenge.ConceptLabel);
            Assert.AreEqual(1, ClasicoMicrogameLibrary.ClassificationPool.Count(c => c.ConceptLabel == challenge.ConceptLabel),
                "No two shipped Western challenges may share identical visible wording.");
            Assert.AreEqual("Ingresos por servicios",
                ClasicoMicrogameLibrary.ClassificationPool.Single(c => c.Id == "cls_ingresos_por_servicios").ConceptLabel);
            Assert.AreEqual("INGRESO", challenge.CorrectCategory);
            Assert.AreEqual("medium", challenge.Difficulty);
            CollectionAssert.AreEqual(new[] { "ACTIVO", "PASIVO", "INGRESO", "GASTO" }, challenge.CategoryOptions);
            Assert.IsFalse(ClasicoMicrogameLibrary.ClassificationPool.Any(c => c.ConceptLabel.Contains("ya prestados y cobrados")),
                "The retired wording must not ship anywhere in the Western pool.");
        }

        private static ClassificationChallenge ValidClassification() => new ClassificationChallenge
        {
            Id = "c1",
            ConceptLabel = "Cuentas por cobrar",
            CorrectCategory = "ACTIVO",
            CategoryOptions = new[] { "ACTIVO", "PASIVO", "INGRESO", "GASTO" },
        };

        [Test]
        public void Classification_WellFormed_HasNoIssues()
        {
            Assert.IsEmpty(MicrogameContentValidator.Validate(ValidClassification()));
        }

        [Test]
        public void Classification_MissingId_IsRejected()
        {
            var challenge = ValidClassification();
            challenge.Id = string.Empty;
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void Classification_DuplicateOptions_IsRejected()
        {
            var challenge = ValidClassification();
            challenge.CategoryOptions = new[] { "ACTIVO", "ACTIVO", "INGRESO" };
            Assert.IsTrue(MicrogameContentValidator.Validate(challenge).Any(i => i.Contains("duplicates")));
        }

        [Test]
        public void Classification_CorrectCategoryNotInOptions_IsRejected()
        {
            var challenge = ValidClassification();
            challenge.CorrectCategory = "PATRIMONIO";
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void Classification_CorrectCategoryAppearsTwice_IsRejected()
        {
            var challenge = ValidClassification();
            challenge.CategoryOptions = new[] { "ACTIVO", "ACTIVO", "INGRESO", "GASTO" };
            challenge.CorrectCategory = "ACTIVO";
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        /// <summary>C8.1i content bank expansion: Western now authors a real
        /// Medium tier, so Difficulty is enforced like every other type.</summary>
        [Test]
        public void Classification_InvalidDifficulty_IsRejected()
        {
            var challenge = ValidClassification();
            challenge.Difficulty = "hard";
            Assert.IsTrue(MicrogameContentValidator.Validate(challenge).Any(i => i.Contains("Difficulty")));
        }

        private static TrueFalseChallenge ValidTrueFalse() => new TrueFalseChallenge
        {
            Id = "t1",
            Statement = "El activo va en el debe.",
            IsTrue = true,
            Difficulty = "easy",
            FeedbackExplanation = "Verdadero: el activo es de naturaleza deudora.",
        };

        [Test]
        public void TrueFalse_WellFormed_HasNoIssues()
        {
            Assert.IsEmpty(MicrogameContentValidator.Validate(ValidTrueFalse()));
        }

        [Test]
        public void TrueFalse_EmptyStatement_IsRejected()
        {
            var challenge = ValidTrueFalse();
            challenge.Statement = string.Empty;
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        /// <summary>C8.1i content bank expansion: Game Show's statements now
        /// carry a real teaching explanation, validated the same way
        /// Balance's FeedbackExplanation already is.</summary>
        [Test]
        public void TrueFalse_MissingFeedbackExplanation_IsRejected()
        {
            var challenge = ValidTrueFalse();
            challenge.FeedbackExplanation = string.Empty;
            Assert.IsTrue(MicrogameContentValidator.Validate(challenge).Any(i => i.Contains("FeedbackExplanation")));
        }

        [Test]
        public void TrueFalse_InvalidDifficulty_IsRejected()
        {
            var challenge = ValidTrueFalse();
            challenge.Difficulty = "hard";
            Assert.IsTrue(MicrogameContentValidator.Validate(challenge).Any(i => i.Contains("Difficulty")));
        }

        [Test]
        public void Equation_WellFormed_HasNoIssues()
        {
            var challenge = new EquationChallenge { Id = "e1", KnownValueA = 500, KnownValueB = 300, CorrectValue = 200, StepSize = 50, StartValue = 50 };
            Assert.IsEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void Equation_ZeroStepSize_IsRejected()
        {
            var challenge = new EquationChallenge { Id = "e1", CorrectValue = 200, StepSize = 0, StartValue = 50 };
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void Equation_StartValueUnreachableInWholeSteps_IsRejected()
        {
            var challenge = new EquationChallenge { Id = "e1", CorrectValue = 200, StepSize = 50, StartValue = 65 };
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void Equation_StartValueEqualsCorrectValue_IsRejected()
        {
            var challenge = new EquationChallenge { Id = "e1", CorrectValue = 200, StepSize = 50, StartValue = 200 };
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        private static ErrorDetectionChallenge ValidErrorDetection() => new ErrorDetectionChallenge
        {
            Id = "d1",
            Difficulty = "easy",
            CasePrompt = "Uno de estos sospechosos no pertenece al grupo.",
            RuleLabel = "Cuentas de Activo",
            Items = new[] { "Caja", "Inventario", "Cuentas por cobrar", "Cuentas por pagar" },
            AnomalyIndex = 3,
            Explanation = "Cuentas por pagar es un pasivo; las demás son cuentas de activo.",
        };

        [Test]
        public void ErrorDetection_WellFormed_HasNoIssues()
        {
            Assert.IsEmpty(MicrogameContentValidator.Validate(ValidErrorDetection()));
        }

        [Test]
        public void ErrorDetection_DuplicateItems_IsRejected()
        {
            var challenge = ValidErrorDetection();
            challenge.Items = new[] { "Caja", "Caja", "Inventario" };
            challenge.AnomalyIndex = 0;
            Assert.IsTrue(MicrogameContentValidator.Validate(challenge).Any(i => i.Contains("duplicates")));
        }

        [Test]
        public void ErrorDetection_AnomalyIndexOutOfRange_IsRejected()
        {
            var challenge = ValidErrorDetection();
            challenge.Items = new[] { "Caja", "Inventario", "Ventas" };
            challenge.AnomalyIndex = 5;
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void ErrorDetection_MissingCasePromptOrRuleLabelOrExplanation_IsRejected()
        {
            var missingCasePrompt = ValidErrorDetection();
            missingCasePrompt.CasePrompt = string.Empty;
            Assert.IsTrue(MicrogameContentValidator.Validate(missingCasePrompt).Any(i => i.Contains("CasePrompt")));

            var missingRuleLabel = ValidErrorDetection();
            missingRuleLabel.RuleLabel = string.Empty;
            Assert.IsTrue(MicrogameContentValidator.Validate(missingRuleLabel).Any(i => i.Contains("RuleLabel")));

            var missingExplanation = ValidErrorDetection();
            missingExplanation.Explanation = string.Empty;
            Assert.IsTrue(MicrogameContentValidator.Validate(missingExplanation).Any(i => i.Contains("Explanation")));
        }

        [Test]
        public void ErrorDetection_InvalidDifficulty_IsRejected()
        {
            var challenge = ValidErrorDetection();
            challenge.Difficulty = "hard";
            Assert.IsTrue(MicrogameContentValidator.Validate(challenge).Any(i => i.Contains("Difficulty")));
        }

        private static DebitCreditChallenge ValidDebitCredit() => new DebitCreditChallenge
        {
            Id = "dc1",
            TransactionText = "Se compra mobiliario al crédito.",
            CorrectDebitAccount = "Mobiliario",
            CorrectCreditAccount = "Cuentas por pagar",
            AccountOptions = new[] { "Mobiliario", "Cuentas por pagar", "Caja", "Préstamo bancario por pagar" },
            Difficulty = "easy",
            FeedbackExplanation = "Aumenta el activo Mobiliario; aumenta Cuentas por pagar porque la compra fue a crédito.",
        };

        [Test]
        public void DebitCredit_WellFormed_HasNoIssues()
        {
            Assert.IsEmpty(MicrogameContentValidator.Validate(ValidDebitCredit()));
        }

        [Test]
        public void DebitCredit_EmptyTransactionText_IsRejected()
        {
            var challenge = ValidDebitCredit();
            challenge.TransactionText = string.Empty;
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void DebitCredit_MissingDebitAccount_IsRejected()
        {
            var challenge = ValidDebitCredit();
            challenge.CorrectDebitAccount = string.Empty;
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void DebitCredit_MissingCreditAccount_IsRejected()
        {
            var challenge = ValidDebitCredit();
            challenge.CorrectCreditAccount = string.Empty;
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void DebitCredit_DebitEqualsCredit_IsRejected()
        {
            var challenge = ValidDebitCredit();
            challenge.CorrectCreditAccount = challenge.CorrectDebitAccount;
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void DebitCredit_DuplicateOptions_IsRejected()
        {
            var challenge = ValidDebitCredit();
            challenge.AccountOptions = new[] { "Mobiliario", "Mobiliario", "Caja" };
            Assert.IsTrue(MicrogameContentValidator.Validate(challenge).Any(i => i.Contains("duplicates")));
        }

        [Test]
        public void DebitCredit_CorrectDebitAccountAbsentFromOptions_IsRejected()
        {
            var challenge = ValidDebitCredit();
            challenge.AccountOptions = new[] { "Caja", "Cuentas por pagar", "Ventas" };
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void DebitCredit_CorrectCreditAccountAbsentFromOptions_IsRejected()
        {
            var challenge = ValidDebitCredit();
            challenge.AccountOptions = new[] { "Mobiliario", "Caja", "Ventas" };
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
        }

        /// <summary>C8.1h content-quality audit: the recap panel has a
        /// dedicated slot for this text (BalanceMachinePresenter.ShowRecap),
        /// but every one of the 15 shipped challenges left it empty before
        /// this phase — now enforced.</summary>
        [Test]
        public void DebitCredit_MissingFeedbackExplanation_IsRejected()
        {
            var challenge = ValidDebitCredit();
            challenge.FeedbackExplanation = string.Empty;
            Assert.IsTrue(MicrogameContentValidator.Validate(challenge).Any(i => i.Contains("FeedbackExplanation")));
        }

        [Test]
        public void DebitCredit_InvalidDifficulty_IsRejected()
        {
            var challenge = ValidDebitCredit();
            challenge.Difficulty = "hard";
            Assert.IsTrue(MicrogameContentValidator.Validate(challenge).Any(i => i.Contains("Difficulty")));
        }

        [Test]
        public void ShippedDebitCreditPool_AllValid()
        {
            foreach (var challenge in ClasicoMicrogameLibrary.DebitCreditPool)
            {
                CollectionAssert.IsEmpty(MicrogameContentValidator.Validate(challenge), $"'{challenge.Id}' has validation issues.");
            }
        }

        /// <summary>C8.1i content bank expansion target: Balance 35.</summary>
        [Test]
        public void ShippedDebitCreditPool_HasAtLeastThirtyFiveChallenges()
        {
            Assert.GreaterOrEqual(ClasicoMicrogameLibrary.DebitCreditPool.Count, 35);
        }

        /// <summary>C8.1i content bank expansion target: 20 Easy / 15 Medium.</summary>
        [Test]
        public void ShippedDebitCreditPool_HasAtLeastTwentyEasyAndFifteenMedium()
        {
            var easyCount = ClasicoMicrogameLibrary.DebitCreditPool.Count(c => c.Difficulty == "easy");
            var mediumCount = ClasicoMicrogameLibrary.DebitCreditPool.Count(c => c.Difficulty == "medium");
            Assert.GreaterOrEqual(easyCount, 20, "Expected at least 20 Easy DebitCredit challenges.");
            Assert.GreaterOrEqual(mediumCount, 15, "Expected at least 15 Medium DebitCredit challenges.");
        }

        // --- Regression: the actual shipped content must always validate clean ---

        [Test]
        public void ShippedClassificationPool_AllValid()
        {
            foreach (var challenge in ClasicoMicrogameLibrary.ClassificationPool)
            {
                CollectionAssert.IsEmpty(MicrogameContentValidator.Validate(challenge), $"'{challenge.Id}' has validation issues.");
            }
        }

        [Test]
        public void ShippedTrueFalsePool_AllValid()
        {
            foreach (var challenge in ClasicoMicrogameLibrary.TrueFalsePool)
            {
                CollectionAssert.IsEmpty(MicrogameContentValidator.Validate(challenge), $"'{challenge.Id}' has validation issues.");
            }
        }

        /// <summary>C8.1i content bank expansion target: Western 30.</summary>
        [Test]
        public void ShippedClassificationPool_HasAtLeastThirtyChallenges()
        {
            Assert.GreaterOrEqual(ClasicoMicrogameLibrary.ClassificationPool.Count, 30);
        }

        /// <summary>C8.1i content bank expansion target: 18 Easy / 12 Medium.</summary>
        [Test]
        public void ShippedClassificationPool_HasAtLeastEighteenEasyAndTwelveMedium()
        {
            var easyCount = ClasicoMicrogameLibrary.ClassificationPool.Count(c => c.Difficulty == "easy");
            var mediumCount = ClasicoMicrogameLibrary.ClassificationPool.Count(c => c.Difficulty == "medium");
            Assert.GreaterOrEqual(easyCount, 18, "Expected at least 18 Easy Classification challenges.");
            Assert.GreaterOrEqual(mediumCount, 12, "Expected at least 12 Medium Classification challenges.");
        }

        /// <summary>C8.1i content bank expansion target: Game Show 30.</summary>
        [Test]
        public void ShippedTrueFalsePool_HasAtLeastThirtyChallenges()
        {
            Assert.GreaterOrEqual(ClasicoMicrogameLibrary.TrueFalsePool.Count, 30);
        }

        /// <summary>C8.1i content bank expansion target: 18 Easy / 12 Medium.</summary>
        [Test]
        public void ShippedTrueFalsePool_HasAtLeastEighteenEasyAndTwelveMedium()
        {
            var easyCount = ClasicoMicrogameLibrary.TrueFalsePool.Count(c => c.Difficulty == "easy");
            var mediumCount = ClasicoMicrogameLibrary.TrueFalsePool.Count(c => c.Difficulty == "medium");
            Assert.GreaterOrEqual(easyCount, 18, "Expected at least 18 Easy TrueFalse challenges.");
            Assert.GreaterOrEqual(mediumCount, 12, "Expected at least 12 Medium TrueFalse challenges.");
        }

        [Test]
        public void ShippedEquationPool_AllValid()
        {
            foreach (var challenge in ClasicoMicrogameLibrary.EquationPool)
            {
                CollectionAssert.IsEmpty(MicrogameContentValidator.Validate(challenge), $"'{challenge.Id}' has validation issues.");
            }
        }

        [Test]
        public void ShippedErrorDetectionPool_AllValid()
        {
            foreach (var challenge in ClasicoMicrogameLibrary.ErrorDetectionPool)
            {
                CollectionAssert.IsEmpty(MicrogameContentValidator.Validate(challenge), $"'{challenge.Id}' has validation issues.");
            }
        }

        /// <summary>C8.1i content bank expansion target: Detective 30.</summary>
        [Test]
        public void ShippedErrorDetectionPool_HasAtLeastThirtyChallenges()
        {
            Assert.GreaterOrEqual(ClasicoMicrogameLibrary.ErrorDetectionPool.Count, 30);
        }

        /// <summary>C8.1i content bank expansion target: 18 Easy / 12 Medium.</summary>
        [Test]
        public void ShippedErrorDetectionPool_HasAtLeastEighteenEasyAndTwelveMedium()
        {
            var easyCount = ClasicoMicrogameLibrary.ErrorDetectionPool.Count(c => c.Difficulty == "easy");
            var mediumCount = ClasicoMicrogameLibrary.ErrorDetectionPool.Count(c => c.Difficulty == "medium");
            Assert.GreaterOrEqual(easyCount, 18, "Expected at least 18 Easy ErrorDetection challenges.");
            Assert.GreaterOrEqual(mediumCount, 12, "Expected at least 12 Medium ErrorDetection challenges.");
        }

        /// <summary>Content-quality regression guard for the exact defect
        /// the C8.1g.1 audit found: every one of the original 6 shipped
        /// challenges authored its anomaly at the same slot (index 3) —
        /// a real, learnable position-cheat even though C8.1g.2's runtime
        /// display shuffle (see ClasicoSessionDirectorTests) is the actual
        /// fix. This guards the CONTENT's own authored variety so a future
        /// content addition can't silently regress back to "always slot
        /// 3" while still passing every other validator rule.</summary>
        [Test]
        public void ShippedErrorDetectionPool_AnomalyIndexIsNotAlwaysTheSameAuthoredSlot()
        {
            var distinctAnomalySlots = ClasicoMicrogameLibrary.ErrorDetectionPool
                .Select(c => c.AnomalyIndex)
                .Distinct()
                .Count();
            Assert.Greater(distinctAnomalySlots, 1, "Every shipped ErrorDetection challenge authors its anomaly at the same slot — a learnable position-cheat.");
        }
    }
}
