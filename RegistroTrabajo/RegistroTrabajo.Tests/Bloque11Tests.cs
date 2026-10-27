using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque11Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static async Task RechazaCon(ServiceErrorKind kind, Func<Task> accion) =>
        Assert.Equal(kind, (await Assert.ThrowsAnyAsync<ServiceException>(accion)).Kind);

    private IRespaldoService Svc => _t.Svc<IRespaldoService>();

    private async Task<EmpresaInfo> PlataformaAsync()
    {
        var p = await _t.CrearPlataformaAsync();
        _t.NewScope();
        _t.Current.As(p, p.AdminId);
        return p;
    }

    [Fact]
    public async Task Administrador_de_empresa_normal_no_puede_solicitar_ni_consultar()
    {
        var e = await _t.CrearEmpresaAsync("Acme", "admin@a.com");
        _t.Current.As(e, e.AdminId);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Svc.ListarAsync(new RespaldoFiltro(), default));
        Assert.DoesNotContain(Permisos.PlataformaRespaldosSolicitar, PermisosPorDefecto.PorRol[NombresRol.Administrador]);
    }

    [Fact]
    public async Task Respaldo_completo_exitoso_registra_tamano_hash_y_fechas()
    {
        var p = await PlataformaAsync();
        var r = await Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default);
        Assert.Equal(EstadoOperacionRespaldo.Completado, r.Estado);
        Assert.Equal(TipoOperacionRespaldo.Completo, r.Tipo);
        Assert.Equal(2048, r.TamanoBytes);
        Assert.Equal("ABC123", r.HashArchivo);
        Assert.NotNull(r.FechaFinUtc);
        Assert.Equal(p.AdminId, r.UsuarioId);
        Assert.EndsWith(".bak", r.NombreArchivo);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, r.NombreArchivo!);   // la ruta del servidor no se expone
        Assert.Single(_t.Respaldo.Archivos);
        _t.NewScope();
        Assert.True(await _t.Db.Auditoria.AnyAsync(a => a.Modulo == "Respaldos" && a.Accion == "Completar"));
    }

    [Fact]
    public async Task Fallo_del_respaldo_queda_fallido_sin_filtrar_detalles()
    {
        await PlataformaAsync();
        _t.Respaldo.FallaRespaldo = true;
        var r = await Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default);
        Assert.Equal(EstadoOperacionRespaldo.Fallido, r.Estado);
        Assert.Null(r.TamanoBytes);
        Assert.DoesNotContain("secreto", r.Mensaje);
        Assert.DoesNotContain("clave", r.Mensaje);
        Assert.False(await _t.Db.OperacionesRespaldo.AnyAsync(o => o.Estado == EstadoOperacionRespaldo.Completado));
    }

    [Fact]
    public async Task Verificacion_fallida_posterior_al_respaldo_no_se_marca_exitosa()
    {
        await PlataformaAsync();
        _t.Respaldo.FallaVerificacion = true;
        var r = await Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default);
        Assert.Equal(EstadoOperacionRespaldo.Fallido, r.Estado);
    }

    [Fact]
    public async Task Diferencial_exige_un_completo_previo()
    {
        await PlataformaAsync();
        await RechazaCon(ServiceErrorKind.Validation, () => Svc.SolicitarAsync(new SolicitarRespaldoRequest { Tipo = TipoOperacionRespaldo.Diferencial }, default));
        await Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default);
        var d = await Svc.SolicitarAsync(new SolicitarRespaldoRequest { Tipo = TipoOperacionRespaldo.Diferencial }, default);
        Assert.Equal(EstadoOperacionRespaldo.Completado, d.Estado);
    }

    [Fact]
    public async Task Tipo_invalido_se_rechaza()
    {
        await PlataformaAsync();
        await RechazaCon(ServiceErrorKind.Validation, () => Svc.SolicitarAsync(new SolicitarRespaldoRequest { Tipo = 3 }, default));
    }

    [Fact]
    public async Task Operacion_en_curso_bloquea_un_segundo_respaldo_hasta_que_se_considera_abandonada()
    {
        var p = await PlataformaAsync();
        _t.Db.OperacionesRespaldo.Add(new Data.Entities.OperacionRespaldo
        {
            EjecutadaPorUsuarioId = p.AdminId, TipoOperacion = TipoOperacionRespaldo.Completo, Estado = EstadoOperacionRespaldo.Iniciado,
            FechaInicioUtc = _t.Clock.GetUtcNow().UtcDateTime
        });
        await _t.Db.SaveChangesAsync();
        await RechazaCon(ServiceErrorKind.Conflict, () => Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default));
        _t.Clock.Advance(TimeSpan.FromHours(7));
        Assert.Equal(EstadoOperacionRespaldo.Completado, (await Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default)).Estado);
    }

    [Fact]
    public async Task Verificar_un_respaldo_registra_una_operacion_de_verificacion()
    {
        await PlataformaAsync();
        var r = await Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default);
        var v = await Svc.VerificarAsync(r.OperacionRespaldoId, default);
        Assert.Equal(TipoOperacionRespaldo.Verificacion, v.Tipo);
        Assert.Equal(EstadoOperacionRespaldo.Completado, v.Estado);

        _t.Respaldo.Hash = "OTRO";   // el archivo cambió desde que se generó
        var v2 = await Svc.VerificarAsync(r.OperacionRespaldoId, default);
        Assert.Equal(EstadoOperacionRespaldo.Fallido, v2.Estado);
    }

    [Fact]
    public async Task No_se_verifica_una_operacion_fallida_ni_inexistente()
    {
        await PlataformaAsync();
        _t.Respaldo.FallaRespaldo = true;
        var f = await Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default);
        await RechazaCon(ServiceErrorKind.Validation, () => Svc.VerificarAsync(f.OperacionRespaldoId, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Svc.VerificarAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Historial_se_filtra_por_estado_y_tipo_y_se_ordena_por_fecha()
    {
        await PlataformaAsync();
        await Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default);
        _t.Clock.Advance(TimeSpan.FromMinutes(5));
        _t.Respaldo.FallaRespaldo = true;
        await Svc.SolicitarAsync(new SolicitarRespaldoRequest(), default);

        var todos = await Svc.ListarAsync(new RespaldoFiltro(), default);
        Assert.Equal(2, todos.Total);
        Assert.Equal(EstadoOperacionRespaldo.Fallido, todos.Items[0].Estado);
        Assert.Single((await Svc.ListarAsync(new RespaldoFiltro { Estado = EstadoOperacionRespaldo.Completado }, default)).Items);
        Assert.Empty((await Svc.ListarAsync(new RespaldoFiltro { Tipo = TipoOperacionRespaldo.Diferencial }, default)).Items);
    }

    [Fact]
    public void Mensaje_seguro_no_incluye_el_texto_de_la_excepcion()
    {
        var m = RespaldoService.MensajeSeguro(new InvalidOperationException("Password=abc; Server=x"));
        Assert.DoesNotContain("Password", m);
        Assert.DoesNotContain("abc", m);
    }
}
