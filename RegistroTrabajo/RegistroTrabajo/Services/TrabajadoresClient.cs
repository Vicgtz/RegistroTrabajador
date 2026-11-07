using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

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
