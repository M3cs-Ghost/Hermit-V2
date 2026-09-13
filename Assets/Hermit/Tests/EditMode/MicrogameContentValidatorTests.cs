using System.Linq;
using NUnit.Framework;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Tests.EditMode
{
    public class MicrogameContentValidatorTests
    {
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

        [Test]
        public void TrueFalse_WellFormed_HasNoIssues()
        {
            var challenge = new TrueFalseChallenge { Id = "t1", Statement = "El activo va en el debe.", IsTrue = true };
            Assert.IsEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void TrueFalse_EmptyStatement_IsRejected()
        {
            var challenge = new TrueFalseChallenge { Id = "t1", Statement = string.Empty, IsTrue = true };
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
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

        [Test]
        public void ErrorDetection_WellFormed_HasNoIssues()
        {
            var challenge = new ErrorDetectionChallenge { Id = "d1", GroupLabel = "Cuentas de Activo", Items = new[] { "Caja", "Inventario", "Cuentas por cobrar", "Cuentas por pagar" }, AnomalyIndex = 3 };
            Assert.IsEmpty(MicrogameContentValidator.Validate(challenge));
        }

        [Test]
        public void ErrorDetection_DuplicateItems_IsRejected()
        {
            var challenge = new ErrorDetectionChallenge { Id = "d1", Items = new[] { "Caja", "Caja", "Inventario" }, AnomalyIndex = 0 };
            Assert.IsTrue(MicrogameContentValidator.Validate(challenge).Any(i => i.Contains("duplicates")));
        }

        [Test]
        public void ErrorDetection_AnomalyIndexOutOfRange_IsRejected()
        {
            var challenge = new ErrorDetectionChallenge { Id = "d1", Items = new[] { "Caja", "Inventario", "Ventas" }, AnomalyIndex = 5 };
            Assert.IsNotEmpty(MicrogameContentValidator.Validate(challenge));
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
    }
}
