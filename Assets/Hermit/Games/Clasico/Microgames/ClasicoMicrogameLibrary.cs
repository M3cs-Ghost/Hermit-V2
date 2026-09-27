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

        // C8.1i content bank expansion: a second, independent 4-way
        // category set for Western's Medium tier — current/non-current is a
        // genuinely different classification axis than account family, so it
        // gets its own CategoryOptions set rather than being force-fit into
        // FourCategories. Still exactly 4 mutually-exclusive options, still
        // "click the one correct target" — no mechanic change.
        private const string CategoryActivoCorriente = "ACTIVO CORRIENTE";
        private const string CategoryActivoNoCorriente = "ACTIVO NO CORRIENTE";
        private const string CategoryPasivoCorriente = "PASIVO CORRIENTE";
        private const string CategoryPasivoNoCorriente = "PASIVO NO CORRIENTE";
        private static readonly string[] CorrienteCategories =
            { CategoryActivoCorriente, CategoryActivoNoCorriente, CategoryPasivoCorriente, CategoryPasivoNoCorriente };

        public static readonly IReadOnlyList<ClassificationChallenge> ClassificationPool = new[]
        {
            // --- Easy: broad, obvious families (18 total) ---
            NewClassification("cls_cuentas_por_cobrar", "Cuentas por cobrar", CategoryActivo),
            NewClassification("cls_cuentas_por_pagar", "Cuentas por pagar", CategoryPasivo),
            NewClassification("cls_ventas", "Ventas", CategoryIngreso),
            NewClassification("cls_sueldos", "Sueldos y salarios", CategoryGasto),
            NewClassification("cls_inventario", "Inventario", CategoryActivo),
            NewClassification("cls_prestamo_bancario", "Préstamo bancario", CategoryPasivo),
            NewClassification("cls_ingresos_intereses", "Ingresos por intereses", CategoryIngreso),
            // C8.1h: "Renta del local" was ambiguous on its own — "renta" in
            // Spanish can mean either rent PAID (an expense, the intent
            // here) or rental INCOME collected (if the business were the
            // landlord), so a player could defensibly argue INGRESO. Also
            // fixes a cross-archetype terminology inconsistency: Balance
            // already uses the unambiguous "Gasto de alquiler" for this
            // exact concept.
            NewClassification("cls_renta_local", "Gasto de alquiler", CategoryGasto),
            // C8.1i content bank expansion:
            NewClassification("cls_caja", "Caja", CategoryActivo),
            NewClassification("cls_bancos", "Bancos", CategoryActivo),
            NewClassification("cls_mobiliario", "Mobiliario", CategoryActivo),
            NewClassification("cls_vehiculo_reparto", "Vehículo de reparto", CategoryActivo),
            NewClassification("cls_sueldos_por_pagar", "Sueldos por pagar", CategoryPasivo),
            NewClassification("cls_impuestos_por_pagar", "Impuestos por pagar", CategoryPasivo),
            NewClassification("cls_ingresos_por_servicios", "Ingresos por servicios", CategoryIngreso),
            // Deliberately pairs with "Gasto de alquiler" above: the tenant's
            // expense and the landlord's income for the exact same kind of
            // arrangement, each named so there is no way to confuse the two
            // — the direct antidote to the original cls_renta_local ambiguity.
            NewClassification("cls_ingresos_arrendamiento", "Ingresos por arrendamiento", CategoryIngreso),
            NewClassification("cls_servicios_publicos", "Servicios públicos", CategoryGasto),
            NewClassification("cls_gasto_publicidad", "Gasto de publicidad", CategoryGasto),

            // --- Medium: closer classifications (12 total) ---
            // Corriente vs. no corriente — a second, independent 4-way
            // category axis (see CorrienteCategories), not a re-ask of the
            // family question above.
            NewClassification("cls_documentos_cobrar_corriente", "Documentos por cobrar a corto plazo",
                CategoryActivoCorriente, "medium", CorrienteCategories),
            NewClassification("cls_terreno_nocorriente", "Terreno",
                CategoryActivoNoCorriente, "medium", CorrienteCategories),
            NewClassification("cls_edificio_nocorriente", "Edificio",
                CategoryActivoNoCorriente, "medium", CorrienteCategories),
            NewClassification("cls_documentos_pagar_corto", "Documentos por pagar a corto plazo",
                CategoryPasivoCorriente, "medium", CorrienteCategories),
            NewClassification("cls_intereses_por_pagar", "Intereses por pagar",
                CategoryPasivoCorriente, "medium", CorrienteCategories),
            NewClassification("cls_prestamo_largo_plazo", "Préstamo bancario a largo plazo",
                CategoryPasivoNoCorriente, "medium", CorrienteCategories),
            // Gasto vs. activo prepagado — same standard 4 categories, but
            // the two "Papelería" entries deliberately share an account
            // family with opposite answers, so the wording (not the noun)
            // must decide the answer. C8.1n: concise account names — the
            // asset is "Papelería y útiles", the expense "Gasto por
            // papelería" (was "...ya consumidos" / "...comprados por
            // adelantado para uso futuro").
            NewClassification("cls_seguro_anticipado_w", "Seguro pagado por anticipado", CategoryActivo, "medium"),
            NewClassification("cls_papeleria_consumida", "Gasto por papelería", CategoryGasto, "medium"),
            NewClassification("cls_papeleria_anticipada",
                "Papelería y útiles", CategoryActivo, "medium"),
            // Ingreso vs. pasivo por ingreso diferido. C8.1k.1: was "Ingresos
            // por servicios ya prestados y cobrados" — manual review found it
            // read like a series title; the plain account name is enough.
            // C8.1k.2: "devengados" keeps it distinct from the Easy
            // cls_ingresos_por_servicios ("Ingresos por servicios").
            NewClassification("cls_ingreso_servicio_devengado",
                "Ingresos por servicios devengados", CategoryIngreso, "medium"),
            NewClassification("cls_anticipo_clientes",
                "Anticipo de clientes", CategoryPasivo, "medium"),
            // Intangible-asset recognition — still an asset even though it
            // isn't physical.
            NewClassification("cls_marca_registrada", "Marca registrada", CategoryActivo, "medium"),
        };

        public static readonly IReadOnlyList<TrueFalseChallenge> TrueFalsePool = new[]
        {
            // --- Easy: fundamentals (18 total) ---
            NewTrueFalse("tf_activo_debe", "El activo se registra normalmente en el debe.", true,
                feedbackExplanation: "Verdadero: el activo es de naturaleza deudora; normalmente aumenta con un cargo (debe)."),
            NewTrueFalse("tf_pasivo_posee", "El pasivo representa lo que la empresa posee.", false,
                feedbackExplanation: "Falso: el pasivo representa lo que la empresa DEBE a terceros, no lo que posee — eso es el activo."),
            NewTrueFalse("tf_ingresos_patrimonio", "Los ingresos aumentan el patrimonio.", true,
                feedbackExplanation: "Verdadero: los ingresos aumentan el patrimonio porque incrementan la utilidad de la empresa."),
            NewTrueFalse("tf_gastos_patrimonio", "Los gastos disminuyen el patrimonio.", true,
                feedbackExplanation: "Verdadero: los gastos disminuyen el patrimonio porque reducen la utilidad de la empresa."),
            NewTrueFalse("tf_cxp_activo", "Cuentas por pagar es una cuenta de activo.", false,
                feedbackExplanation: "Falso: Cuentas por pagar es una obligación con terceros, por lo tanto es una cuenta de pasivo, no de activo."),
            NewTrueFalse("tf_efectivo_corriente", "El efectivo es un activo corriente.", true,
                feedbackExplanation: "Verdadero: el efectivo es el activo más líquido y se clasifica siempre como corriente."),
            NewTrueFalse("tf_cxc_obligacion", "Una cuenta por cobrar es una obligación de la empresa.", false,
                feedbackExplanation: "Falso: una cuenta por cobrar es un derecho de la empresa a recibir dinero, no una obligación — las obligaciones son pasivos."),
            NewTrueFalse("tf_patrimonio_formula", "El patrimonio es igual a Activo menos Pasivo.", true,
                feedbackExplanation: "Verdadero: despejando la ecuación contable Activo = Pasivo + Patrimonio, se obtiene Patrimonio = Activo − Pasivo."),
            // C8.1i content bank expansion:
            NewTrueFalse("tf_pasivo_debe", "El pasivo se registra normalmente en el debe.", false,
                feedbackExplanation: "Falso: el pasivo tiene naturaleza acreedora y se registra normalmente en el haber, no en el debe."),
            NewTrueFalse("tf_capital_haber", "El capital se registra normalmente en el haber.", true,
                feedbackExplanation: "Verdadero: el capital es una cuenta de patrimonio, de naturaleza acreedora, y aumenta con créditos (haber)."),
            NewTrueFalse("tf_ecuacion_contable", "La ecuación contable establece que Activo = Pasivo + Patrimonio.", true,
                feedbackExplanation: "Verdadero: esta es la ecuación contable fundamental; todo recurso de la empresa (activo) proviene de deuda (pasivo) o de los dueños (patrimonio)."),
            NewTrueFalse("tf_gasto_debe", "Los gastos se registran normalmente en el debe.", true,
                feedbackExplanation: "Verdadero: los gastos tienen naturaleza deudora porque disminuyen el patrimonio."),
            NewTrueFalse("tf_ingreso_haber", "Los ingresos se registran normalmente en el haber.", true,
                feedbackExplanation: "Verdadero: los ingresos tienen naturaleza acreedora porque aumentan el patrimonio."),
            NewTrueFalse("tf_inventario_pasivo", "El inventario es una cuenta de pasivo.", false,
                feedbackExplanation: "Falso: el inventario es un activo, un recurso que la empresa posee para la venta."),
            NewTrueFalse("tf_capital_pasivo", "El capital es una cuenta de pasivo.", false,
                feedbackExplanation: "Falso: el capital es una cuenta de patrimonio, no una obligación con terceros."),
            NewTrueFalse("tf_bancos_corriente", "La cuenta Bancos es un activo corriente.", true,
                feedbackExplanation: "Verdadero: el dinero en el banco es efectivo disponible, uno de los activos corrientes más líquidos."),
            NewTrueFalse("tf_doble_partida", "Toda transacción contable afecta al menos dos cuentas.", true,
                feedbackExplanation: "Verdadero: este es el principio de partida doble — todo cargo tiene su contrapartida en un abono equivalente."),
            NewTrueFalse("tf_retiro_patrimonio", "El patrimonio disminuye cuando el propietario retira efectivo del negocio.", true,
                feedbackExplanation: "Verdadero: un retiro del propietario disminuye el patrimonio, de forma similar a como lo hacen los gastos."),

            // --- Medium: accrual, timing, effects on the equation (12 total) ---
            NewTrueFalse("tf_devengado_ingreso",
                "Bajo el criterio de devengado, un ingreso se reconoce cuando se presta el servicio, aunque no se haya cobrado todavía.",
                true, "medium",
                "Verdadero: el principio de devengado reconoce el ingreso cuando se gana (se presta el servicio), no cuando se cobra en efectivo."),
            NewTrueFalse("tf_devengado_gasto",
                "Bajo el criterio de devengado, un gasto se reconoce solo cuando se paga en efectivo.",
                false, "medium",
                "Falso: bajo devengado, el gasto se reconoce cuando se incurre (se recibe el beneficio), sin importar si ya se pagó en efectivo."),
            NewTrueFalse("tf_seguro_anticipado_activo",
                "El seguro pagado por anticipado es un activo hasta que se consume su beneficio.",
                true, "medium",
                "Verdadero: mientras el beneficio del seguro no se haya usado, representa un derecho futuro y por lo tanto es un activo."),
            NewTrueFalse("tf_cxc_naturaleza",
                "Una cuenta por cobrar aumenta cuando se carga (debe).",
                true, "medium",
                "Verdadero: Cuentas por cobrar es un activo, de naturaleza deudora, por lo que un cargo la aumenta."),
            NewTrueFalse("tf_cxp_naturaleza",
                "Una cuenta por pagar disminuye cuando se acredita (haber).",
                false, "medium",
                "Falso: Cuentas por pagar es de naturaleza acreedora; un crédito (haber) la AUMENTA — para disminuirla se debe cargar (debe)."),
            NewTrueFalse("tf_terreno_corriente",
                "El terreno se clasifica como un activo corriente.",
                false, "medium",
                "Falso: el terreno es un activo no corriente, de uso duradero, que no se espera convertir en efectivo dentro de un año."),
            NewTrueFalse("tf_prestamo_largoplazo_corriente",
                "Un préstamo bancario por pagar a cinco años se clasifica como pasivo corriente.",
                false, "medium",
                "Falso: una obligación que vence en más de un año se clasifica como pasivo no corriente."),
            NewTrueFalse("tf_venta_credito_efectos",
                "Cuando se vende mercadería al crédito, se registra un aumento en Cuentas por cobrar y una disminución en Inventario.",
                true, "medium",
                "Verdadero: la venta al crédito reconoce el derecho de cobro (Cuentas por cobrar) mientras se retira del inventario la mercadería entregada."),
            NewTrueFalse("tf_anticipo_cliente_ingreso",
                "Un anticipo de un cliente por un servicio aún no prestado ya es un ingreso.",
                false, "medium",
                "Falso: mientras el servicio no se haya prestado, el anticipo es un pasivo (ingreso diferido), no un ingreso ganado."),
            NewTrueFalse("tf_ecuacion_efecto_compra_credito",
                "Comprar mobiliario al crédito aumenta tanto el activo como el pasivo, sin afectar el patrimonio.",
                true, "medium",
                "Verdadero: Mobiliario (activo) aumenta y Cuentas por pagar (pasivo) aumenta en la misma cantidad; el patrimonio no se ve afectado."),
            NewTrueFalse("tf_gasto_efectivo_pasivo",
                "Pagar un gasto en efectivo aumenta el pasivo de la empresa.",
                false, "medium",
                "Falso: pagar un gasto en efectivo disminuye el activo (Caja) y el patrimonio; no afecta el pasivo."),
            NewTrueFalse("tf_cobro_cxc_patrimonio",
                "Cobrar una cuenta por cobrar en efectivo no afecta el patrimonio de la empresa.",
                true, "medium",
                "Verdadero: solo se intercambia un activo por otro (Cuentas por cobrar disminuye, Caja aumenta); el patrimonio no cambia porque no hay un ingreso o gasto nuevo."),
        };

        /// <summary>C8.1 legacy content — no longer drawn by
        /// <see cref="ClasicoSessionDirector"/> (Balance now draws
        /// <see cref="DebitCreditPool"/>, see C8.1f). Kept, not deleted: a
        /// real content pool + engine + presenter path, unlike the fully
        /// dead C8.1e ContestantHead/PrizeBoard, which had zero references
        /// left anywhere before being removed outright.</summary>
        public static readonly IReadOnlyList<EquationChallenge> EquationPool = new[]
        {
            NewEquation("eq_500_300", 500f, 300f, 50f, 3),
            NewEquation("eq_800_650", 800f, 650f, 25f, 3),
            NewEquation("eq_1200_700", 1200f, 700f, 100f, 2),
            NewEquation("eq_300_100", 300f, 100f, 50f, 2),
            NewEquation("eq_1000_400", 1000f, 400f, 100f, 2),
            NewEquation("eq_650_150", 650f, 150f, 50f, 3),
        };

        /// <summary>C8.1f — Balance's shipped content: a short transaction,
        /// which account is debited ("se carga") and which is credited
        /// ("se acredita"), drawn from a single shared account pool (see
        /// Docs/C8_1F_BALANCE_MECHANIC_REDESIGN.md, section 8). Easy/Medium
        /// only this phase, per the redesign doc's own instruction to defer
        /// Hard adjusting-entry concepts.
        ///
        /// C8.1h content-quality audit (Docs/CLASICO_CONTENT_AUDIT.md):
        /// five items originally left the cash-settlement method to
        /// inference ("al contado", or no payment-method wording at all)
        /// while offering BOTH "Caja" and "Bancos" as options — a
        /// defensible alternate answer, not just a theoretical one. Every
        /// transaction now states "en efectivo" explicitly whenever Caja is
        /// the correct cash account, matching the already-unambiguous
        /// bank-loan items below (which always named "el banco"/"Bancos"
        /// explicitly). Every entry also now carries a real
        /// <c>FeedbackExplanation</c> — previously empty on all 15, despite
        /// the recap panel having a dedicated slot for it.</summary>
        public static readonly IReadOnlyList<DebitCreditChallenge> DebitCreditPool = new[]
        {
            NewDebitCredit("dc_aporte_capital", "El propietario aporta efectivo al negocio.",
                "Caja", "Capital", new[] { "Caja", "Capital", "Mobiliario", "Cuentas por pagar" },
                feedbackExplanation: "Aumenta Caja porque ingresa efectivo al negocio; aumenta Capital porque el propietario incrementa su aporte patrimonial."),
            NewDebitCredit("dc_compra_equipo_efectivo", "Se compra equipo y se paga en efectivo.",
                "Equipo", "Caja", new[] { "Equipo", "Caja", "Mobiliario", "Gasto de alquiler" },
                feedbackExplanation: "Aumenta el activo Equipo; disminuye Caja porque el pago se realizó de inmediato en efectivo."),
            NewDebitCredit("dc_pago_alquiler", "Se paga el alquiler del mes en efectivo.",
                "Gasto de alquiler", "Caja", new[] { "Gasto de alquiler", "Caja", "Sueldos y salarios", "Bancos" },
                feedbackExplanation: "Se carga Gasto de alquiler porque es un costo del período; se acredita Caja porque se pagó en efectivo."),
            NewDebitCredit("dc_servicio_contado", "Se presta un servicio y se cobra en efectivo.",
                "Caja", "Ingresos por servicios", new[] { "Caja", "Ingresos por servicios", "Cuentas por cobrar", "Ventas" },
                feedbackExplanation: "Aumenta Caja porque se cobró de inmediato; se acredita Ingresos por servicios (no Ventas, que es para mercadería) porque el negocio prestó un servicio."),
            NewDebitCredit("dc_pago_sueldos", "Se pagan los sueldos del mes en efectivo.",
                "Sueldos y salarios", "Caja", new[] { "Sueldos y salarios", "Caja", "Gasto de alquiler", "Bancos" },
                feedbackExplanation: "Se carga Sueldos y salarios porque es un gasto del período; se acredita Caja porque se pagó en efectivo."),
            NewDebitCredit("dc_compra_mobiliario_contado", "Se compra mobiliario y se paga en efectivo.",
                "Mobiliario", "Caja", new[] { "Mobiliario", "Caja", "Equipo", "Cuentas por pagar" },
                feedbackExplanation: "Aumenta el activo Mobiliario; disminuye Caja porque el pago fue inmediato en efectivo, no a crédito."),
            NewDebitCredit("dc_compra_mobiliario_credito", "Se compra mobiliario al crédito.",
                "Mobiliario", "Cuentas por pagar", new[] { "Mobiliario", "Cuentas por pagar", "Caja", "Préstamo bancario" },
                "medium", "Aumenta el activo Mobiliario; aumenta Cuentas por pagar porque la compra 'al crédito' crea una obligación con el proveedor, no un préstamo bancario."),
            NewDebitCredit("dc_compra_inventario_credito", "Se compra inventario al crédito.",
                "Inventario", "Cuentas por pagar", new[] { "Inventario", "Cuentas por pagar", "Mobiliario", "Caja" },
                "medium", "Aumenta el activo Inventario; aumenta Cuentas por pagar porque la mercadería se compró a crédito, no al contado."),
            NewDebitCredit("dc_servicio_credito", "Se presta un servicio al crédito.",
                "Cuentas por cobrar", "Ingresos por servicios", new[] { "Cuentas por cobrar", "Ingresos por servicios", "Caja", "Ventas" },
                "medium", "Aumenta Cuentas por cobrar porque el cobro queda pendiente; se acredita Ingresos por servicios (no Ventas) porque se prestó un servicio, no se vendió mercadería."),
            NewDebitCredit("dc_cobro_cuenta", "Se cobra en efectivo una cuenta por cobrar.",
                "Caja", "Cuentas por cobrar", new[] { "Caja", "Cuentas por cobrar", "Bancos", "Ventas" },
                "medium", "Aumenta Caja porque se cobró en efectivo; disminuye Cuentas por cobrar porque el cliente salda su deuda."),
            NewDebitCredit("dc_pago_proveedor", "Se paga en efectivo una cuenta pendiente a un proveedor.",
                "Cuentas por pagar", "Caja", new[] { "Cuentas por pagar", "Caja", "Préstamo bancario", "Bancos" },
                "medium", "Disminuye Cuentas por pagar porque se salda la deuda con el proveedor; disminuye Caja porque el pago fue en efectivo."),
            NewDebitCredit("dc_prestamo_bancario", "Se obtiene un préstamo bancario, depositado en el banco.",
                "Bancos", "Préstamo bancario", new[] { "Bancos", "Préstamo bancario", "Caja", "Cuentas por pagar" },
                "medium", "Aumenta Bancos porque el préstamo se depositó en la cuenta bancaria; aumenta Préstamo bancario porque nace una obligación con el banco."),
            NewDebitCredit("dc_pago_cuota_prestamo", "Se paga una cuota del préstamo bancario desde el banco.",
                "Préstamo bancario", "Bancos", new[] { "Préstamo bancario", "Bancos", "Caja", "Cuentas por pagar" },
                "medium", "Disminuye Préstamo bancario porque se abona a la deuda; disminuye Bancos porque el pago salió de la cuenta bancaria."),
            NewDebitCredit("dc_venta_credito", "Se venden mercaderías al crédito.",
                "Cuentas por cobrar", "Ventas", new[] { "Cuentas por cobrar", "Ventas", "Caja", "Ingresos por servicios" },
                "medium", "Aumenta Cuentas por cobrar porque el cobro queda pendiente; se acredita Ventas (no Ingresos por servicios) porque se vendió mercadería."),
            NewDebitCredit("dc_seguro_anticipado", "Se paga en efectivo, por anticipado, el seguro del negocio.",
                "Seguro pagado por anticipado", "Caja", new[] { "Seguro pagado por anticipado", "Caja", "Bancos", "Gasto de alquiler" },
                "medium", "Aumenta el activo Seguro pagado por anticipado porque el beneficio aún no se ha consumido; disminuye Caja porque el pago fue en efectivo."),

            // --- C8.1i content bank expansion: Easy (14 new, 20 total) ---
            NewDebitCredit("dc_venta_contado", "Se vende mercadería y se cobra de inmediato en efectivo.",
                "Caja", "Ventas", new[] { "Caja", "Ventas", "Cuentas por cobrar", "Ingresos por servicios" },
                feedbackExplanation: "Se carga Caja porque se cobró de inmediato; se acredita Ventas porque se vendió mercadería, no un servicio."),
            NewDebitCredit("dc_compra_papeleria_efectivo", "Se compra papelería y útiles de oficina y se paga en efectivo.",
                "Gasto por papelería", "Caja", new[] { "Gasto por papelería", "Caja", "Mobiliario", "Bancos" },
                feedbackExplanation: "Se carga Gasto por papelería porque se consume de inmediato como un costo del período; se acredita Caja porque el pago fue en efectivo."),
            NewDebitCredit("dc_pago_servicios_publicos", "Se pagan los servicios públicos del mes mediante transferencia bancaria.",
                "Servicios públicos", "Bancos", new[] { "Servicios públicos", "Bancos", "Caja", "Gasto de alquiler" },
                feedbackExplanation: "Se carga Servicios públicos porque es un gasto del período; se acredita Bancos porque el pago se realizó mediante transferencia bancaria, no en efectivo."),
            NewDebitCredit("dc_compra_inventario_efectivo", "Se compra inventario y se paga en efectivo.",
                "Inventario", "Caja", new[] { "Inventario", "Caja", "Mobiliario", "Cuentas por pagar" },
                feedbackExplanation: "Aumenta el activo Inventario; disminuye Caja porque el pago fue inmediato en efectivo, no a crédito."),
            NewDebitCredit("dc_deposito_bancario_capital", "El propietario deposita efectivo directamente en la cuenta bancaria del negocio.",
                "Bancos", "Capital", new[] { "Bancos", "Capital", "Caja", "Cuentas por pagar" },
                feedbackExplanation: "Aumenta Bancos porque el aporte se depositó directamente en el banco; aumenta Capital porque el propietario incrementa su aporte patrimonial."),
            NewDebitCredit("dc_compra_equipo_transferencia", "Se compra equipo y se paga mediante transferencia bancaria.",
                "Equipo", "Bancos", new[] { "Equipo", "Bancos", "Caja", "Mobiliario" },
                feedbackExplanation: "Aumenta el activo Equipo; disminuye Bancos porque el pago se realizó mediante transferencia bancaria."),
            NewDebitCredit("dc_pago_publicidad_efectivo", "Se paga en efectivo un anuncio publicitario del mes.",
                "Gasto de publicidad", "Caja", new[] { "Gasto de publicidad", "Caja", "Sueldos y salarios", "Bancos" },
                feedbackExplanation: "Se carga Gasto de publicidad porque es un costo del período; se acredita Caja porque el pago fue en efectivo."),
            NewDebitCredit("dc_compra_mercaderia_transferencia", "Se compra mercadería para la venta y se paga mediante transferencia bancaria.",
                "Inventario", "Bancos", new[] { "Inventario", "Bancos", "Caja", "Cuentas por pagar" },
                feedbackExplanation: "Aumenta el activo Inventario; disminuye Bancos porque el pago se realizó mediante transferencia bancaria, no en efectivo ni a crédito."),
            NewDebitCredit("dc_venta_servicios_transferencia", "Se presta un servicio y el cliente paga de inmediato mediante transferencia bancaria.",
                "Bancos", "Ingresos por servicios", new[] { "Bancos", "Ingresos por servicios", "Caja", "Ventas" },
                feedbackExplanation: "Aumenta Bancos porque el cobro fue inmediato por transferencia; se acredita Ingresos por servicios (no Ventas) porque se prestó un servicio."),
            NewDebitCredit("dc_pago_mantenimiento_transferencia", "Se paga mediante transferencia bancaria el mantenimiento del equipo del mes.",
                "Gasto de mantenimiento", "Bancos", new[] { "Gasto de mantenimiento", "Bancos", "Caja", "Equipo" },
                feedbackExplanation: "Se carga Gasto de mantenimiento porque es un costo del período; se acredita Bancos porque el pago se realizó mediante transferencia bancaria."),
            NewDebitCredit("dc_compra_mobiliario_transferencia", "Se compra mobiliario para la oficina y se paga mediante transferencia bancaria.",
                "Mobiliario", "Bancos", new[] { "Mobiliario", "Bancos", "Caja", "Equipo" },
                feedbackExplanation: "Aumenta el activo Mobiliario; disminuye Bancos porque el pago se realizó mediante transferencia bancaria."),
            NewDebitCredit("dc_retiro_propietario", "El propietario retira efectivo del negocio para uso personal.",
                "Retiros del propietario", "Caja", new[] { "Retiros del propietario", "Caja", "Capital", "Gasto de alquiler" },
                feedbackExplanation: "Se carga Retiros del propietario porque disminuye el patrimonio; se acredita Caja porque salió efectivo del negocio."),
            NewDebitCredit("dc_deposito_efectivo_a_banco", "Se deposita efectivo de Caja en la cuenta bancaria del negocio.",
                "Bancos", "Caja", new[] { "Bancos", "Caja", "Capital", "Cuentas por pagar" },
                feedbackExplanation: "Aumenta Bancos porque el efectivo ingresa a la cuenta bancaria; disminuye Caja porque el efectivo sale de la caja del negocio."),
            NewDebitCredit("dc_compra_vehiculo_efectivo", "Se compra un vehículo de reparto y se paga en efectivo.",
                "Vehículo de reparto", "Caja", new[] { "Vehículo de reparto", "Caja", "Equipo", "Mobiliario" },
                feedbackExplanation: "Aumenta el activo Vehículo de reparto; disminuye Caja porque el pago fue inmediato en efectivo."),

            // --- C8.1i content bank expansion: Medium (6 new, 15 total) ---
            NewDebitCredit("dc_anticipo_cliente", "Un cliente entrega efectivo por adelantado por un servicio que la empresa aún no ha prestado.",
                "Caja", "Anticipo de clientes", new[] { "Caja", "Anticipo de clientes", "Ingresos por servicios", "Cuentas por cobrar" },
                "medium", "Aumenta Caja porque se recibió efectivo; se acredita Anticipo de clientes (pasivo) porque el servicio aún no se ha prestado — todavía no es un ingreso ganado."),
            NewDebitCredit("dc_reconocimiento_ingreso_diferido", "La empresa presta finalmente el servicio de un anticipo que un cliente había entregado el mes anterior.",
                "Anticipo de clientes", "Ingresos por servicios", new[] { "Anticipo de clientes", "Ingresos por servicios", "Caja", "Cuentas por cobrar" },
                "medium", "Disminuye Anticipo de clientes porque la obligación pendiente se cancela; aumenta Ingresos por servicios porque el servicio ya fue prestado y ahora sí se gana el ingreso."),
            NewDebitCredit("dc_gasto_acumulado", "Se reconoce el gasto de sueldos del mes, el cual será pagado hasta el próximo mes.",
                "Sueldos y salarios", "Sueldos por pagar", new[] { "Sueldos y salarios", "Sueldos por pagar", "Caja", "Bancos" },
                "medium", "Se carga Sueldos y salarios porque el gasto ya se generó en el período; se acredita Sueldos por pagar porque el pago queda pendiente para el mes siguiente."),
            NewDebitCredit("dc_pago_sueldos_acumulados", "Se paga en efectivo el sueldo que se había reconocido como pendiente el mes anterior.",
                "Sueldos por pagar", "Caja", new[] { "Sueldos por pagar", "Caja", "Sueldos y salarios", "Bancos" },
                "medium", "Disminuye Sueldos por pagar porque se salda la obligación pendiente; disminuye Caja porque el pago fue en efectivo. No se vuelve a cargar Sueldos y salarios porque el gasto ya se había reconocido antes."),
            NewDebitCredit("dc_interes_por_cobrar_acumulado", "La empresa ya ganó interés sobre un préstamo otorgado a un tercero, pero aún no lo cobra en efectivo.",
                "Intereses por cobrar", "Ingresos por intereses", new[] { "Intereses por cobrar", "Ingresos por intereses", "Caja", "Cuentas por cobrar" },
                "medium", "Aumenta Intereses por cobrar porque el interés ya se ganó aunque no se ha cobrado; se acredita Ingresos por intereses porque el ingreso ya se devengó."),
            NewDebitCredit("dc_pago_proveedor_transferencia", "Se paga mediante transferencia bancaria una cuenta pendiente con un proveedor.",
                "Cuentas por pagar", "Bancos", new[] { "Cuentas por pagar", "Bancos", "Caja", "Préstamo bancario" },
                "medium", "Disminuye Cuentas por pagar porque se salda la deuda con el proveedor; disminuye Bancos porque el pago se realizó mediante transferencia bancaria, no en efectivo."),
        };

        // C8.1g.2: expanded from the original 6 (all sharing the same
        // authored AnomalyIndex=3 — the exact "always the 4th suspect"
        // position-cheat the C8.1g.1 audit flagged) to 12 — 6 Easy (broad,
        // obvious families) + 6 Medium (closer classifications: current/
        // non-current, debit/credit nature, expense/revenue family), per
        // brief section 13. AnomalyIndex is now also spread across every
        // slot (0-3) as a defense-in-depth measure — the actual anti-cheat
        // fix is the runtime display shuffle in ClasicoSessionDirector,
        // which randomizes screen position every round regardless of this
        // authored index, but authored variety keeps this pool honest on
        // its own terms too. Every entry is a defensible, single-anomaly,
        // standard introductory-accounting classification — no hidden
        // assumptions, no ambiguous edge cases.
        public static readonly IReadOnlyList<ErrorDetectionChallenge> ErrorDetectionPool = new[]
        {
            // --- Easy: broad, obvious families ---
            NewErrorDetection(
                "err_activos_1", "CUENTAS DEL ACTIVO",
                new[] { "Caja", "Bancos", "Cuentas por cobrar", "Cuentas por pagar" }, 3,
                "Cuentas por pagar es una cuenta de pasivo; las demás son cuentas de activo."),
            // C8.1h: "Préstamos por pagar" renamed to match the exact
            // canonical account name Balance/Western already use — cross-
            // archetype terminology consistency (Docs/CLASICO_CONTENT_AUDIT.md,
            // section 12). C8.1n: that canonical name is now "Préstamo
            // bancario" everywhere ("por pagar" is redundant on a liability).
            NewErrorDetection(
                "err_pasivos_1", "CUENTAS DEL PASIVO",
                new[] { "Caja", "Cuentas por pagar", "Préstamo bancario", "Impuestos por pagar" }, 0,
                "Caja es una cuenta de activo; las demás son cuentas de pasivo."),
            // C8.1h: "Renta" and bare "Servicios" were both genuinely
            // ambiguous — "renta" can mean rent PAID (expense) or rental
            // INCOME collected, and "Servicios" alone reads just as easily
            // as service REVENUE (as it correctly does elsewhere in this
            // same pool, err_ingresosventas_1's "Ingresos por servicios").
            // Renamed to the unambiguous expense-only terms this pool
            // already uses correctly in err_gastosoperativos_1 ("Alquiler"/
            // "Servicios públicos").
            NewErrorDetection(
                "err_gastos_1", "CUENTAS DE GASTO",
                new[] { "Sueldos", "Ventas", "Alquiler", "Servicios públicos" }, 1,
                "Ventas es una cuenta de ingreso; las demás son cuentas de gasto."),
            NewErrorDetection(
                "err_ingresos_1", "CUENTAS DE INGRESO",
                new[] { "Ventas", "Ingresos por intereses", "Inventario", "Comisiones ganadas" }, 2,
                "Inventario es una cuenta de activo; las demás son cuentas de ingreso."),
            NewErrorDetection(
                "err_patrimonio_1", "CUENTAS DE PATRIMONIO",
                new[] { "Capital", "Bancos", "Utilidades retenidas", "Aportes de socios" }, 1,
                "Bancos es una cuenta de activo; las demás son cuentas de patrimonio."),
            NewErrorDetection(
                "err_activos_2", "CUENTAS DEL ACTIVO",
                new[] { "Sueldos por pagar", "Terreno", "Equipo", "Mobiliario" }, 0,
                "Sueldos por pagar es una cuenta de pasivo; las demás son cuentas de activo."),

            // --- Medium: closer classifications ---
            NewErrorDetection(
                "err_corrientes_1", "ACTIVOS CORRIENTES",
                new[] { "Caja", "Bancos", "Cuentas por cobrar", "Terreno" }, 3,
                "Terreno es un activo no corriente; las demás son activos corrientes (se convierten en efectivo dentro de un año).",
                "medium"),
            NewErrorDetection(
                "err_nocorrientes_1", "ACTIVOS NO CORRIENTES",
                new[] { "Caja", "Equipo", "Mobiliario", "Terreno" }, 0,
                "Caja es un activo corriente; las demás son activos no corrientes (de uso duradero).",
                "medium"),
            NewErrorDetection(
                "err_deudoras_1", "CUENTAS DE NATURALEZA DEUDORA",
                new[] { "Cuentas por pagar", "Caja", "Gastos", "Cuentas por cobrar" }, 0,
                "Cuentas por pagar aumenta con crédito; las demás aumentan con débito.",
                "medium"),
            NewErrorDetection(
                "err_acreedoras_1", "CUENTAS DE NATURALEZA ACREEDORA",
                new[] { "Capital", "Ingresos por servicios", "Caja", "Cuentas por pagar" }, 2,
                "Caja aumenta con débito; las demás aumentan con crédito.",
                "medium"),
            // C8.1h: "Renta" -> "Alquiler" — same rent-paid-vs-rental-income
            // ambiguity fix as err_gastos_1 above.
            NewErrorDetection(
                "err_gastosoperativos_1", "GASTOS OPERATIVOS",
                new[] { "Ingresos por intereses", "Sueldos", "Alquiler", "Servicios públicos" }, 0,
                "Ingresos por intereses es una cuenta de ingreso; las demás son gastos operativos.",
                "medium"),
            NewErrorDetection(
                "err_ingresosventas_1", "INGRESOS POR VENTAS Y SERVICIOS",
                new[] { "Ventas", "Ingresos por servicios", "Gasto de publicidad", "Comisiones ganadas" }, 2,
                "Gasto de publicidad es una cuenta de gasto; las demás son cuentas de ingreso.",
                "medium"),

            // --- C8.1i content bank expansion: Easy (12 new, 18 total) ---
            NewErrorDetection(
                "err_activos_3", "CUENTAS DEL ACTIVO",
                new[] { "Mobiliario", "Equipo", "Sueldos y salarios", "Vehículo de reparto" }, 2,
                "Sueldos y salarios es una cuenta de gasto; las demás son cuentas de activo."),
            NewErrorDetection(
                "err_pasivos_2", "CUENTAS DEL PASIVO",
                new[] { "Documentos por pagar", "Intereses por pagar", "Caja", "Sueldos por pagar" }, 2,
                "Caja es una cuenta de activo; las demás son cuentas de pasivo."),
            NewErrorDetection(
                "err_ingresos_2", "CUENTAS DE INGRESO",
                new[] { "Ingresos por servicios", "Comisiones ganadas", "Ingresos por intereses", "Gasto de publicidad" }, 3,
                "Gasto de publicidad es una cuenta de gasto; las demás son cuentas de ingreso."),
            NewErrorDetection(
                "err_gastos_2", "CUENTAS DE GASTO",
                new[] { "Gasto de mantenimiento", "Gasto de publicidad", "Comisiones ganadas", "Servicios públicos" }, 2,
                "Comisiones ganadas es una cuenta de ingreso; las demás son cuentas de gasto."),
            NewErrorDetection(
                "err_patrimonio_2", "CUENTAS DE PATRIMONIO",
                new[] { "Capital", "Retiros del propietario", "Aportes de socios", "Cuentas por cobrar" }, 3,
                "Cuentas por cobrar es una cuenta de activo; las demás son cuentas de patrimonio."),
            NewErrorDetection(
                "err_activos_4", "CUENTAS DEL ACTIVO",
                new[] { "Cuentas por pagar", "Bancos", "Inventario", "Terreno" }, 0,
                "Cuentas por pagar es una cuenta de pasivo; las demás son cuentas de activo."),
            NewErrorDetection(
                "err_pasivos_3", "CUENTAS DEL PASIVO",
                new[] { "Impuestos por pagar", "Ventas", "Préstamo bancario", "Cuentas por pagar" }, 1,
                "Ventas es una cuenta de ingreso; las demás son cuentas de pasivo."),
            NewErrorDetection(
                "err_ingresos_3", "CUENTAS DE INGRESO",
                new[] { "Ventas", "Ingresos por arrendamiento", "Ingresos por servicios", "Mobiliario" }, 3,
                "Mobiliario es una cuenta de activo; las demás son cuentas de ingreso."),
            NewErrorDetection(
                "err_gastos_3", "CUENTAS DE GASTO",
                new[] { "Sueldos y salarios", "Gasto de alquiler", "Cuentas por cobrar", "Servicios públicos" }, 2,
                "Cuentas por cobrar es una cuenta de activo; las demás son cuentas de gasto."),
            NewErrorDetection(
                "err_activos_5", "CUENTAS DEL ACTIVO",
                new[] { "Caja", "Documentos por cobrar", "Impuestos por pagar", "Equipo" }, 2,
                "Impuestos por pagar es una cuenta de pasivo; las demás son cuentas de activo."),
            NewErrorDetection(
                "err_pasivos_4", "CUENTAS DEL PASIVO",
                new[] { "Anticipo de clientes", "Documentos por pagar", "Terreno", "Intereses por pagar" }, 2,
                "Terreno es una cuenta de activo; las demás son cuentas de pasivo."),
            NewErrorDetection(
                "err_ingresos_4", "CUENTAS DE INGRESO",
                new[] { "Comisiones ganadas", "Ingresos por intereses", "Ventas", "Retiros del propietario" }, 3,
                "Retiros del propietario disminuye el patrimonio y no es una cuenta de ingreso; las demás son cuentas de ingreso."),

            // --- C8.1i content bank expansion: Medium (6 new, 12 total) ---
            NewErrorDetection(
                "err_pasivos_nocorrientes_1", "PASIVOS NO CORRIENTES",
                new[] { "Préstamo bancario a largo plazo", "Documentos por pagar a largo plazo", "Hipoteca por pagar", "Cuentas por pagar" }, 3,
                "Cuentas por pagar es un pasivo corriente (se liquida en el corto plazo); las demás son pasivos no corrientes.",
                "medium"),
            NewErrorDetection(
                "err_prepagados_1", "ACTIVOS PAGADOS POR ANTICIPADO",
                new[] { "Seguro pagado por anticipado", "Alquiler pagado por anticipado", "Papelería y útiles", "Gasto de publicidad" }, 3,
                "Gasto de publicidad es un gasto del período, no un activo; las demás son beneficios futuros aún no consumidos (activos).",
                "medium"),
            NewErrorDetection(
                "err_gastosoperativos_2", "GASTOS OPERATIVOS",
                new[] { "Comisiones ganadas", "Gasto de mantenimiento", "Gasto de publicidad", "Sueldos y salarios" }, 0,
                "Comisiones ganadas es una cuenta de ingreso; las demás son gastos operativos.",
                "medium"),
            NewErrorDetection(
                "err_ingresos_5", "CUENTAS DE INGRESO",
                new[] { "Ventas", "Ingresos por servicios", "Cuentas por cobrar", "Comisiones ganadas" }, 2,
                "Cuentas por cobrar es un activo (un derecho de cobro), no una cuenta de ingreso; las demás sí son cuentas de ingreso.",
                "medium"),
            NewErrorDetection(
                "err_deudoras_2", "CUENTAS DE NATURALEZA DEUDORA",
                new[] { "Mobiliario", "Gasto de mantenimiento", "Ingresos por servicios", "Documentos por cobrar" }, 2,
                "Ingresos por servicios aumenta con crédito (naturaleza acreedora); las demás aumentan con débito (naturaleza deudora).",
                "medium"),
            NewErrorDetection(
                "err_acreedoras_2", "CUENTAS DE NATURALEZA ACREEDORA",
                new[] { "Anticipo de clientes", "Préstamo bancario", "Documentos por cobrar", "Comisiones ganadas" }, 2,
                "Documentos por cobrar es un activo, de naturaleza deudora; las demás son de naturaleza acreedora (pasivo o ingreso).",
                "medium"),
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

        private static ClassificationChallenge NewClassification(
            string id, string concept, string correctCategory, string difficulty = "easy", string[] categoryOptions = null)
        {
            return new ClassificationChallenge
            {
                Id = id,
                ConceptLabel = concept,
                CorrectCategory = correctCategory,
                CategoryOptions = categoryOptions ?? (string[])FourCategories.Clone(),
                Difficulty = difficulty,
            };
        }

        private static TrueFalseChallenge NewTrueFalse(
            string id, string statement, bool isTrue, string difficulty = "easy", string feedbackExplanation = null)
        {
            return new TrueFalseChallenge
            {
                Id = id,
                Statement = statement,
                IsTrue = isTrue,
                Difficulty = difficulty,
                FeedbackExplanation = feedbackExplanation,
            };
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

        private static ErrorDetectionChallenge NewErrorDetection(
            string id, string ruleLabel, string[] items, int anomalyIndex, string explanation, string difficulty = "easy")
        {
            return new ErrorDetectionChallenge
            {
                Id = id,
                Difficulty = difficulty,
                CasePrompt = "Uno de estos sospechosos no pertenece al grupo.",
                RuleLabel = ruleLabel,
                Items = items,
                AnomalyIndex = anomalyIndex,
                Explanation = explanation,
            };
        }

        private static DebitCreditChallenge NewDebitCredit(
            string id, string transactionText, string debitAccount, string creditAccount, string[] accountOptions,
            string difficulty = "easy", string feedbackExplanation = null)
        {
            return new DebitCreditChallenge
            {
                Id = id,
                TransactionText = transactionText,
                CorrectDebitAccount = debitAccount,
                CorrectCreditAccount = creditAccount,
                AccountOptions = accountOptions,
                Difficulty = difficulty,
                FeedbackExplanation = feedbackExplanation,
            };
        }
    }
}
