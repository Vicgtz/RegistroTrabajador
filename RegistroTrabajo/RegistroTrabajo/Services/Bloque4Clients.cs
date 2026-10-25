using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class TiposPermisoClient(ApiClient api)
{
    public Task<ApiResult<List<TipoPermisoDto>>> ListarAsync(bool soloActivos = false) =>
        api.GetAsync<List<TipoPermisoDto>>("api/tipos-permiso" + Qs.De(("soloActivos", soloActivos)));

    public Task<ApiResult<TipoPermisoDto>> CrearAsync(GuardarTipoPermisoRequest r) => api.PostAsync<TipoPermisoDto>("api/tipos-permiso", r);

    public Task<ApiResult<TipoPermisoDto>> ActualizarAsync(Guid id, GuardarTipoPermisoRequest r) =>
        api.SendAsync<TipoPermisoDto>(HttpMethod.Put, $"api/tipos-permiso/{id}", r);

    public Task<ApiResult<TipoPermisoDto>> EstadoAsync(Guid id, bool estado) =>
        api.PostAsync<TipoPermisoDto>($"api/tipos-permiso/{id}/estado", new CambiarEstadoRolRequest { Estado = estado });

    public Task<ApiResult<ReglaPermisoDto>> CrearReglaAsync(Guid tipoId, GuardarReglaPermisoRequest r) =>
        api.PostAsync<ReglaPermisoDto>($"api/tipos-permiso/{tipoId}/reglas", r);

    public Task<ApiResult<ReglaPermisoDto>> ActualizarReglaAsync(Guid tipoId, Guid reglaId, GuardarReglaPermisoRequest r) =>
        api.SendAsync<ReglaPermisoDto>(HttpMethod.Put, $"api/tipos-permiso/{tipoId}/reglas/{reglaId}", r);

    public Task<ApiResult<List<TipoPermisoDto>>> SembrarInicialesAsync() => api.PostAsync<List<TipoPermisoDto>>("api/tipos-permiso/iniciales");
}

public class SolicitudesPermisoClient(ApiClient api)
{
    private static string Qf(SolicitudPermisoFiltro f) => Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("trabajadorId", f.TrabajadorId),
        ("tipoPermisoId", f.TipoPermisoId), ("estado", f.Estado), ("desde", f.Desde?.ToString("yyyy-MM-dd")), ("hasta", f.Hasta?.ToString("yyyy-MM-dd")));

    public Task<ApiResult<CalculoDuracionDto>> CalcularAsync(CalculoDuracionRequest r) => api.PostAsync<CalculoDuracionDto>("api/solicitudes-permiso/calculo", r);
    public Task<ApiResult<SolicitudPermisoDto>> CrearAsync(CrearSolicitudPermisoRequest r) => api.PostAsync<SolicitudPermisoDto>("api/solicitudes-permiso", r);

    public Task<ApiResult<SolicitudPermisoDto>> ActualizarAsync(Guid id, ActualizarSolicitudPermisoRequest r) =>
        api.SendAsync<SolicitudPermisoDto>(HttpMethod.Put, $"api/solicitudes-permiso/{id}", r);

    public Task<ApiResult<SolicitudPermisoDto>> CancelarAsync(Guid id) => api.PostAsync<SolicitudPermisoDto>($"api/solicitudes-permiso/{id}/cancelar");

    public Task<ApiResult<PagedResult<SolicitudPermisoDto>>> MiasAsync(SolicitudPermisoFiltro f) =>
        api.GetAsync<PagedResult<SolicitudPermisoDto>>("api/solicitudes-permiso/mias" + Qf(f));

    public Task<ApiResult<PagedResult<SolicitudPermisoDto>>> ListarAsync(SolicitudPermisoFiltro f) =>
        api.GetAsync<PagedResult<SolicitudPermisoDto>>("api/solicitudes-permiso" + Qf(f));

    public Task<ApiResult<PagedResult<SolicitudPermisoDto>>> BandejaAsync(SolicitudPermisoFiltro f) =>
        api.GetAsync<PagedResult<SolicitudPermisoDto>>("api/solicitudes-permiso/bandeja" + Qf(f));

    public Task<ApiResult<SolicitudPermisoDto>> ResolverAsync(Guid id, bool aprobar, string? comentario) =>
        api.PostAsync<SolicitudPermisoDto>($"api/solicitudes-permiso/{id}/resolver", new ResolverPermisoRequest { Aprobar = aprobar, Comentario = comentario });

    public Task<ApiResult<SolicitudCancelacionDto>> SolicitarCancelacionAsync(Guid id, string? motivo) =>
        api.PostAsync<SolicitudCancelacionDto>($"api/solicitudes-permiso/{id}/solicitar-cancelacion", new SolicitarCancelacionRequest { Motivo = motivo });

    public Task<ApiResult<PagedResult<SolicitudCancelacionDto>>> CancelacionesAsync(CancelacionPermisoFiltro f) =>
        api.GetAsync<PagedResult<SolicitudCancelacionDto>>("api/cancelaciones-permiso" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("estado", f.Estado)));

    public Task<ApiResult<SolicitudCancelacionDto>> ResolverCancelacionAsync(Guid id, bool aprobar, string? comentario) =>
        api.PostAsync<SolicitudCancelacionDto>($"api/cancelaciones-permiso/{id}/resolver", new ResolverPermisoRequest { Aprobar = aprobar, Comentario = comentario });
}

public class SaldosPermisoClient(ApiClient api)
{
    public Task<ApiResult<List<SaldoPermisoDto>>> ListarAsync(Guid? trabajadorId, short? anio) =>
        api.GetAsync<List<SaldoPermisoDto>>("api/saldos-permiso" + Qs.De(("trabajadorId", trabajadorId), ("anio", anio)));

    public Task<ApiResult<List<MovimientoSaldoDto>>> MovimientosAsync(Guid saldoId) =>
        api.GetAsync<List<MovimientoSaldoDto>>($"api/saldos-permiso/{saldoId}/movimientos");

    public Task<ApiResult<SaldoPermisoDto>> AjustarAsync(AjustarSaldoRequest r) => api.PostAsync<SaldoPermisoDto>("api/saldos-permiso/ajustes", r);

    public Task<ApiResult<GenerarSaldosDto>> GenerarAsync(short anio) => api.PostAsync<GenerarSaldosDto>($"api/saldos-permiso/generar/{anio}");
}

public class GenerarSaldosDto
{
    public int Creados { get; set; }
}

public static class FormatoPermisos
{
    public static string Estado(byte e) => e switch
    {
        EstadoSolicitudPermiso.Pendiente => "Pendiente", EstadoSolicitudPermiso.Aprobada => "Aprobada",
        EstadoSolicitudPermiso.Rechazada => "Rechazada", EstadoSolicitudPermiso.Cancelada => "Cancelada", _ => "Concluida"
    };

    public static string Clase(byte e) => e switch
    {
        EstadoSolicitudPermiso.Pendiente => "text-bg-warning", EstadoSolicitudPermiso.Aprobada => "text-bg-success",
        EstadoSolicitudPermiso.Rechazada => "text-bg-danger", _ => "text-bg-secondary"
    };

    public static string EstadoCancelacion(byte e) => e switch
    {
        EstadoCancelacionPermiso.Pendiente => "Pendiente", EstadoCancelacionPermiso.Aprobada => "Aprobada",
        EstadoCancelacionPermiso.Rechazada => "Rechazada", _ => "Cancelada"
    };

    public static string Unidad(byte unidad) => unidad == UnidadPermiso.Horas ? "horas" : "días";

    public static string Cantidad(decimal cantidad, byte unidad) => $"{cantidad:0.##} {Unidad(unidad)}";

    /// <summary>Las fechas del permiso son locales de la empresa (no UTC): se muestran tal cual.</summary>
    public static string Rango(DateTime ini, DateTime fin, bool porHoras) => porHoras
        ? $"{ini:dd/MM/yyyy} {ini:HH:mm}–{fin:HH:mm}"
        : ini.Date == fin.Date ? $"{ini:dd/MM/yyyy}" : $"{ini:dd/MM/yyyy} – {fin:dd/MM/yyyy}";

    public static string Movimiento(byte tipo) => tipo switch
    {
        TipoMovimientoSaldo.Asignacion => "Asignación", TipoMovimientoSaldo.Consumo => "Consumo",
        TipoMovimientoSaldo.Devolucion => "Devolución", _ => "Ajuste a la baja"
    };
}
