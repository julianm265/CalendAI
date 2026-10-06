using CalendarioBackend.Core.Documents;

namespace CalendarioBackend.Tests;

public class DocumentEventAnalyzerTests
{
    private static readonly DateOnly Reference = new(2026, 10, 5);
    private readonly DocumentEventAnalyzer _analyzer = new();

    private DocumentAnalysis Analyze(DateOrder order = DateOrder.Auto, params string[] lines) =>
        _analyzer.Analyze(lines.Select(line => new DocumentLine(line, 1)).ToArray(), order, Reference);

    private DocumentAnalysis Analyze(params string[] lines) => Analyze(DateOrder.Auto, lines);

    // ------------------------------------------------------------------ fechas

    [Theory]
    [InlineData("Entrega 25/03/2026", 2026, 3, 25)]
    [InlineData("Entrega 25-03-2026", 2026, 3, 25)]
    [InlineData("Entrega 25.03.2026", 2026, 3, 25)]
    [InlineData("Entrega 2026-03-25", 2026, 3, 25)]
    [InlineData("Entrega 2026/03/25", 2026, 3, 25)]
    [InlineData("Entrega 25/03/26", 2026, 3, 25)]
    [InlineData("Entrega 25 de marzo de 2026", 2026, 3, 25)]
    [InlineData("Entrega 25 marzo 2026", 2026, 3, 25)]
    [InlineData("Entrega 25-mar-2026", 2026, 3, 25)]
    [InlineData("Entrega 25 Mar. 2026", 2026, 3, 25)]
    [InlineData("Entrega March 25, 2026", 2026, 3, 25)]
    [InlineData("Entrega 25th March 2026", 2026, 3, 25)]
    [InlineData("Entrega el viernes 25 de septiembre del 2026", 2026, 9, 25)]
    public void Reconoce_cualquier_formato_de_fecha_sin_ambiguedad(string linea, int year, int month, int day)
    {
        var result = Analyze(linea);

        var evento = Assert.Single(result.Eventos);
        Assert.Equal(new DateOnly(year, month, day), evento.Fecha);
        Assert.False(evento.EsAmbigua);
    }

    [Fact]
    public void Una_fecha_con_dia_mayor_a_12_revela_el_orden_del_documento()
    {
        var result = Analyze("Entrega 25/03/2026", "Revisión 03/04/2026");

        Assert.Equal(DateOrder.DayFirst, result.OrdenAplicado);
        Assert.Contains(result.Eventos, e => e.Fecha == new DateOnly(2026, 4, 3) && !e.EsAmbigua);
    }

    [Fact]
    public void Un_documento_en_formato_mes_dia_se_interpreta_como_tal()
    {
        var result = Analyze("Kickoff 12/25/2026", "Review 03/04/2026");

        Assert.Equal(DateOrder.MonthFirst, result.OrdenAplicado);
        Assert.Contains(result.Eventos, e => e.Fecha == new DateOnly(2026, 3, 4) && !e.EsAmbigua);
    }

    [Fact]
    public void Sin_evidencia_se_asume_dia_mes_pero_se_marca_ambigua_con_alternativa()
    {
        var result = Analyze("Reunión 03/04/2026");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal(new DateOnly(2026, 4, 3), evento.Fecha);
        Assert.True(evento.EsAmbigua);
        Assert.Equal(new DateOnly(2026, 3, 4), evento.FechaAlternativa);
        Assert.True(result.HayFechasAmbiguas);
    }

    [Fact]
    public void El_formato_elegido_por_el_usuario_elimina_la_ambiguedad()
    {
        var result = Analyze(DateOrder.MonthFirst, "Reunión 03/04/2026");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal(new DateOnly(2026, 3, 4), evento.Fecha);
        Assert.False(evento.EsAmbigua);
    }

    [Fact]
    public void El_dia_de_la_semana_desambigua_la_fecha()
    {
        // 06/04/2026 es lunes; 04/06/2026 es jueves.
        var result = Analyze("Lunes 06/04/2026 Reunión general");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal(new DateOnly(2026, 4, 6), evento.Fecha);
        Assert.False(evento.EsAmbigua);
    }

    [Fact]
    public void Las_fechas_que_no_existen_en_el_calendario_se_descartan()
    {
        var result = Analyze("Pago 31/04/2026", "Cierre 29/02/2027", "Versión 1.2.3 lanzada");

        Assert.Empty(result.Eventos);
    }

    [Fact]
    public void Una_fecha_sin_anio_toma_el_anio_del_documento()
    {
        var result = Analyze("Cierre de matrículas 20 de enero de 2027", "15 de marzo – Inicio de clases");

        var evento = Assert.Single(result.Eventos, e => e.NombreEvento == "Inicio de clases");
        Assert.Equal(new DateOnly(2027, 3, 15), evento.Fecha);
        Assert.True(evento.AnioInferido);
        Assert.Equal("Inicio de clases", evento.NombreEvento);
    }

    [Fact]
    public void Reconoce_rangos_de_fechas()
    {
        var result = Analyze(
            "Vacaciones del 5 al 7 de octubre de 2026",
            "Curso: 28 de septiembre al 2 de octubre de 2026");

        Assert.Contains(result.Eventos, e =>
            e.Fecha == new DateOnly(2026, 10, 5) && e.FechaFin == new DateOnly(2026, 10, 7));
        Assert.Contains(result.Eventos, e =>
            e.Fecha == new DateOnly(2026, 9, 28) && e.FechaFin == new DateOnly(2026, 10, 2));
    }

    [Fact]
    public void Una_lista_de_dias_genera_un_evento_por_dia()
    {
        var result = Analyze("Jornadas 5 y 6 de octubre de 2026");

        Assert.Equal(2, result.Eventos.Count);
        Assert.All(result.Eventos, e => Assert.Equal("Jornadas", e.NombreEvento));
        Assert.Equal(new[] { new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6) }, result.Eventos.Select(e => e.Fecha));
    }

    // ------------------------------------------------------------------ nombre, lugar y hora

    [Fact]
    public void Extrae_nombre_lugar_y_hora_de_una_linea()
    {
        var result = Analyze("Reunión de planeación 15/03/2026 10:00 am, Sala 2");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal("Reunión de planeación", evento.NombreEvento);
        Assert.Equal("Sala 2", evento.Lugar);
        Assert.Equal(new TimeOnly(10, 0), evento.Hora);
    }

    [Fact]
    public void Extrae_los_datos_de_una_frase_narrativa()
    {
        var result = Analyze(
            "El comité se reunirá el viernes 15 de mayo de 2026 en el Auditorio Central a las 3:30 p.m. para revisar el presupuesto.");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal(new DateOnly(2026, 5, 15), evento.Fecha);
        Assert.Equal("Auditorio Central", evento.Lugar);
        Assert.Equal(new TimeOnly(15, 30), evento.Hora);
        Assert.Contains("comité", evento.NombreEvento);
    }

    [Fact]
    public void Usa_los_campos_con_etiqueta_en_lineas_separadas()
    {
        var result = Analyze("Taller de IA", "15 de marzo de 2026", "Lugar: Auditorio Central", "Hora: 10:00");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal("Taller de IA", evento.NombreEvento);
        Assert.Equal("Auditorio Central", evento.Lugar);
        Assert.Equal(new TimeOnly(10, 0), evento.Hora);
    }

    [Fact]
    public void Usa_los_campos_con_etiqueta_en_una_misma_linea()
    {
        var result = Analyze("Evento: Feria de empleo | Fecha: 20/08/2026 | Lugar: Plaza Mayor | Hora: 9:00 am");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal("Feria de empleo", evento.NombreEvento);
        Assert.Equal("Plaza Mayor", evento.Lugar);
        Assert.Equal(new TimeOnly(9, 0), evento.Hora);
    }

    [Fact]
    public void Reconoce_un_lugar_escrito_como_nombre_propio()
    {
        var result = Analyze("Concierto de gala - 3 de julio de 2026 en Medellín, Colombia");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal("Concierto de gala", evento.NombreEvento);
        Assert.Equal("Medellín, Colombia", evento.Lugar);
    }

    [Fact]
    public void No_confunde_una_frase_con_mayuscula_con_un_lugar()
    {
        var result = Analyze("Curso en Excel avanzado 3 de julio de 2026");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal("Curso en Excel avanzado", evento.NombreEvento);
        Assert.Null(evento.Lugar);
    }

    [Fact]
    public void Lee_la_hora_de_una_fecha_iso_con_hora()
    {
        var result = Analyze("Entrega 2026-10-05T10:30:00");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal("Entrega", evento.NombreEvento);
        Assert.Equal(new TimeOnly(10, 30), evento.Hora);
    }

    [Fact]
    public void Limpia_viñetas_y_parentesis_del_nombre()
    {
        var result = Analyze(
            "• 15 de marzo de 2026: Reunión de directivos en el Edificio B",
            "• 16 de marzo de 2026 - Taller de liderazgo (Sala 4) 8:00 a.m.");

        Assert.Equal("Reunión de directivos", result.Eventos[0].NombreEvento);
        Assert.Equal("Edificio B", result.Eventos[0].Lugar);
        Assert.Equal("Taller de liderazgo", result.Eventos[1].NombreEvento);
        Assert.Equal("Sala 4", result.Eventos[1].Lugar);
        Assert.Equal(new TimeOnly(8, 0), result.Eventos[1].Hora);
    }

    [Fact]
    public void Dos_eventos_el_mismo_dia_no_se_fusionan()
    {
        var result = Analyze("9 de junio de 2026 - Apertura", "9 de junio de 2026 - Clausura");

        Assert.Equal(2, result.Eventos.Count);
        Assert.Equal(new[] { "Apertura", "Clausura" }, result.Eventos.Select(e => e.NombreEvento));
    }

    [Fact]
    public void Varias_fechas_en_una_linea_toman_cada_una_su_etiqueta()
    {
        var result = Analyze("Inicio: 25/03/2026 Fin: 30/03/2026");

        Assert.Equal(new[] { "Inicio", "Fin" }, result.Eventos.Select(e => e.NombreEvento));
    }

    [Fact]
    public void Las_fechas_de_emision_tienen_confianza_baja()
    {
        var result = Analyze("Bogotá, fecha de emisión: 25/10/2026");

        var evento = Assert.Single(result.Eventos);
        Assert.Equal("baja", evento.Confianza);
    }

    // ------------------------------------------------------------------ tablas

    [Fact]
    public void Lee_una_tabla_con_encabezados()
    {
        var lines = new[]
        {
            new DocumentLine("Fecha Actividad Lugar Hora", null, new[] { "Fecha", "Actividad", "Lugar", "Hora" }),
            new DocumentLine("", null, new[] { "15/03/2026", "Taller de IA", "Sala 2", "10:00" }),
            new DocumentLine("", null, new[] { "16/03/2026", "Conferencia", "Auditorio Central", "14:30" })
        };

        var result = _analyzer.Analyze(lines, DateOrder.Auto, Reference);

        Assert.Equal(2, result.Eventos.Count);
        Assert.Equal("Taller de IA", result.Eventos[0].NombreEvento);
        Assert.Equal("Sala 2", result.Eventos[0].Lugar);
        Assert.Equal(new TimeOnly(10, 0), result.Eventos[0].Hora);
        Assert.Equal("Conferencia", result.Eventos[1].NombreEvento);
        Assert.Equal("Auditorio Central", result.Eventos[1].Lugar);
        Assert.Equal(new TimeOnly(14, 30), result.Eventos[1].Hora);
    }

    [Fact]
    public void Lee_una_tabla_sin_encabezados_con_las_columnas_en_cualquier_orden()
    {
        var lines = new[]
        {
            new DocumentLine("", null, new[] { "Foro de datos", "16/03/2026", "Auditorio Central", "2 p.m." })
        };

        var result = _analyzer.Analyze(lines, DateOrder.Auto, Reference);

        var evento = Assert.Single(result.Eventos);
        Assert.Equal("Foro de datos", evento.NombreEvento);
        Assert.Equal("Auditorio Central", evento.Lugar);
        Assert.Equal(new TimeOnly(14, 0), evento.Hora);
    }

    [Fact]
    public void Un_documento_vacio_no_genera_eventos()
    {
        var result = _analyzer.Analyze(Array.Empty<DocumentLine>(), DateOrder.Auto, Reference);

        Assert.Empty(result.Eventos);
    }
}
