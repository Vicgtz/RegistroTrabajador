using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class CatalogosClient(ApiClient api)
{
    public Task<ApiResult<List<CatalogoItemDto>>> DepartamentosAsync(bool soloActivos = false) =>
        api.GetAsync<List<CatalogoItemDto>>("api/departamentos" + Qs.De(("soloActivos", soloActivos)));

    public Task<ApiResult<CatalogoItemDto>> CrearDepartamentoAsync(GuardarCatalogoRequest r) => api.PostAsync<CatalogoItemDto>("api/departamentos", r);

    public Task<ApiResult<CatalogoItemDto>> ActualizarDepartamentoAsync(Guid id, GuardarCatalogoRequest r) =>
        api.SendAsync<CatalogoItemDto>(HttpMethod.Put, $"api/departamentos/{id}", r);

    public Task<ApiResult<CatalogoItemDto>> EstadoDepartamentoAsync(Guid id, bool estado) =>
        api.PostAsync<CatalogoItemDto>($"api/departamentos/{id}/estado", new CambiarEstadoRolRequest { Estado = estado });

    public Task<ApiResult<List<CatalogoItemDto>>> PuestosAsync(bool soloActivos = false) =>
        api.GetAsync<List<CatalogoItemDto>>("api/puestos" + Qs.De(("soloActivos", soloActivos)));

    public Task<ApiResult<CatalogoItemDto>> CrearPuestoAsync(GuardarCatalogoRequest r) => api.PostAsync<CatalogoItemDto>("api/puestos", r);

    public Task<ApiResult<CatalogoItemDto>> ActualizarPuestoAsync(Guid id, GuardarCatalogoRequest r) =>
        api.SendAsync<CatalogoItemDto>(HttpMethod.Put, $"api/puestos/{id}", r);

    public Task<ApiResult<CatalogoItemDto>> EstadoPuestoAsync(Guid id, bool estado) =>
        api.PostAsync<CatalogoItemDto>($"api/puestos/{id}/estado", new CambiarEstadoRolRequest { Estado = estado });
}

public class TrabajadoresClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<TrabajadorResumenDto>>> ListarAsync(TrabajadorFiltro f) =>
        api.GetAsync<PagedResult<TrabajadorResumenDto>>("api/trabajadores" + Qs.De(("page", f.Page), ("pageSize", f.PageSize),
            ("buscar", f.Buscar), ("departamentoId", f.DepartamentoId), ("puestoId", f.PuestoId), ("estadoLaboral", f.EstadoLaboral)));

    public Task<ApiResult<TrabajadorDetalleDto>> ObtenerAsync(Guid id) => api.GetAsync<TrabajadorDetalleDto>($"api/trabajadores/{id}");
    public Task<ApiResult<TrabajadorDetalleDto>> MiExpedienteAsync() => api.GetAsync<TrabajadorDetalleDto>("api/trabajadores/mi-expediente");
    public Task<ApiResult<TrabajadorDetalleDto>> CrearAsync(CrearTrabajadorRequest r) => api.PostAsync<TrabajadorDetalleDto>("api/trabajadores", r);

    public Task<ApiResult<TrabajadorDetalleDto>> ActualizarAsync(Guid id, ActualizarTrabajadorRequest r) =>
        api.SendAsync<TrabajadorDetalleDto>(HttpMethod.Put, $"api/trabajadores/{id}", r);

    public Task<ApiResult<TrabajadorDetalleDto>> CambiarEstadoAsync(Guid id, CambiarEstadoLaboralRequest r) =>
        api.PostAsync<TrabajadorDetalleDto>($"api/trabajadores/{id}/estado", r);

    public Task<ApiResult<TrabajadorDetalleDto>> BajaAsync(Guid id, BajaTrabajadorRequest r) =>
        api.PostAsync<TrabajadorDetalleDto>($"api/trabajadores/{id}/baja", r);

    public Task<ApiResult<TrabajadorDetalleDto>> ReingresoAsync(Guid id, string? motivo) =>
        api.PostAsync<TrabajadorDetalleDto>($"api/trabajadores/{id}/reingreso", new ReingresoRequest { Motivo = motivo });

    public Task<ApiResult<List<HistorialLaboralDto>>> HistorialAsync(Guid id) =>
        api.GetAsync<List<HistorialLaboralDto>>($"api/trabajadores/{id}/historial");

    public Task<ApiResult<List<SupervisorAsignadoDto>>> SupervisoresAsync(Guid id) =>
        api.GetAsync<List<SupervisorAsignadoDto>>($"api/trabajadores/{id}/supervisores");

    public Task<ApiResult<SupervisorAsignadoDto>> AsignarSupervisorAsync(Guid id, AsignarSupervisorRequest r) =>
        api.PostAsync<SupervisorAsignadoDto>($"api/trabajadores/{id}/supervisores", r);

    public Task<ApiResult<SupervisorAsignadoDto>> FinalizarSupervisionAsync(Guid id, long supervisionId, DateOnly? fin) =>
        api.PostAsync<SupervisorAsignadoDto>($"api/trabajadores/{id}/supervisores/{supervisionId}/finalizar", new FinalizarSupervisionRequest { FechaFin = fin });

    public Task<ApiResult<List<ContactoEmergenciaDto>>> ContactosAsync(Guid id) =>
        api.GetAsync<List<ContactoEmergenciaDto>>($"api/trabajadores/{id}/contactos-emergencia");

    public Task<ApiResult<ContactoEmergenciaDto>> CrearContactoAsync(Guid id, GuardarContactoEmergenciaRequest r) =>
        api.PostAsync<ContactoEmergenciaDto>($"api/trabajadores/{id}/contactos-emergencia", r);

    public Task<ApiResult<ContactoEmergenciaDto>> ActualizarContactoAsync(Guid id, Guid contactoId, GuardarContactoEmergenciaRequest r) =>
        api.SendAsync<ContactoEmergenciaDto>(HttpMethod.Put, $"api/trabajadores/{id}/contactos-emergencia/{contactoId}", r);

    public Task<ApiResult> EliminarContactoAsync(Guid id, Guid contactoId) =>
        api.SendAsync(HttpMethod.Delete, $"api/trabajadores/{id}/contactos-emergencia/{contactoId}");
}

public class SolicitudesPerfilClient(ApiClient api)
{
    public Task<ApiResult<SolicitudCambioPerfilDto>> CrearAsync(CambioPerfilDatos r) => api.PostAsync<SolicitudCambioPerfilDto>("api/solicitudes-perfil", r);
    public Task<ApiResult<List<SolicitudCambioPerfilDto>>> MiasAsync() => api.GetAsync<List<SolicitudCambioPerfilDto>>("api/solicitudes-perfil/mias");
    public Task<ApiResult<SolicitudCambioPerfilDto>> CancelarAsync(Guid id) => api.PostAsync<SolicitudCambioPerfilDto>($"api/solicitudes-perfil/{id}/cancelar");

    public Task<ApiResult<PagedResult<SolicitudCambioPerfilDto>>> ListarAsync(SolicitudPerfilFiltro f) =>
        api.GetAsync<PagedResult<SolicitudCambioPerfilDto>>("api/solicitudes-perfil" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("estado", f.Estado)));

    public Task<ApiResult<SolicitudCambioPerfilDto>> ResolverAsync(Guid id, bool aprobar, string? comentario) =>
        api.PostAsync<SolicitudCambioPerfilDto>($"api/solicitudes-perfil/{id}/resolver", new ResolverSolicitudRequest { Aprobar = aprobar, Comentario = comentario });
}

public static class FormatoLaboral
{
    public static string Estado(byte e) => e switch
    {
        EstadoLaboral.Activo => "Activo", EstadoLaboral.Inactivo => "Inactivo", EstadoLaboral.Baja => "Baja", _ => "Desconocido"
    };

    public static string ClaseEstado(byte e) => e switch
    {
        EstadoLaboral.Activo => "text-bg-success", EstadoLaboral.Inactivo => "text-bg-warning", _ => "text-bg-secondary"
    };

    public static string EstadoSolicitud(byte e) => e switch
    {
        EstadoSolicitudPerfil.Pendiente => "Pendiente", EstadoSolicitudPerfil.Aprobada => "Aprobada",
        EstadoSolicitudPerfil.Rechazada => "Rechazada", EstadoSolicitudPerfil.Cancelada => "Cancelada", _ => "Desconocido"
    };

    public static string ClaseSolicitud(byte e) => e switch
    {
        EstadoSolicitudPerfil.Pendiente => "text-bg-warning", EstadoSolicitudPerfil.Aprobada => "text-bg-success",
        EstadoSolicitudPerfil.Rechazada => "text-bg-danger", _ => "text-bg-secondary"
    };

    public static string Fecha(DateOnly? d) => d is null ? "—" : d.Value.ToString("dd/MM/yyyy");
}
