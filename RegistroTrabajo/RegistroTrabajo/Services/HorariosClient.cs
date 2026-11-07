using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class HorariosClient(ApiClient api)
{
    // Turnos
    public Task<ApiResult<List<TurnoDto>>> TurnosAsync(bool soloActivos = false) => api.GetAsync<List<TurnoDto>>("api/turnos" + Qs.De(("soloActivos", soloActivos)));
    public Task<ApiResult<TurnoDto>> CrearTurnoAsync(GuardarTurnoRequest r) => api.PostAsync<TurnoDto>("api/turnos", r);
    public Task<ApiResult<TurnoDto>> ActualizarTurnoAsync(Guid id, GuardarTurnoRequest r) => api.SendAsync<TurnoDto>(HttpMethod.Put, $"api/turnos/{id}", r);
    public Task<ApiResult<TurnoDto>> EstadoTurnoAsync(Guid id, bool estado) => api.PostAsync<TurnoDto>($"api/turnos/{id}/estado", new CambiarEstadoRolRequest { Estado = estado });
    public Task<ApiResult<List<TurnoDto>>> TurnosInicialesAsync() => api.PostAsync<List<TurnoDto>>("api/turnos/iniciales");

    // Horarios
    public Task<ApiResult<List<HorarioLaboralDto>>> ListarAsync(bool soloActivos = false) => api.GetAsync<List<HorarioLaboralDto>>("api/horarios" + Qs.De(("soloActivos", soloActivos)));
    public Task<ApiResult<HorarioLaboralDto>> CrearAsync(GuardarHorarioRequest r) => api.PostAsync<HorarioLaboralDto>("api/horarios", r);
    public Task<ApiResult<HorarioLaboralDto>> ActualizarAsync(Guid id, GuardarHorarioRequest r) => api.SendAsync<HorarioLaboralDto>(HttpMethod.Put, $"api/horarios/{id}", r);
    public Task<ApiResult<HorarioLaboralDto>> NuevaVersionAsync(Guid id, GuardarHorarioRequest r) => api.PostAsync<HorarioLaboralDto>($"api/horarios/{id}/nueva-version", r);
    public Task<ApiResult<HorarioLaboralDto>> EstadoAsync(Guid id, bool estado) => api.PostAsync<HorarioLaboralDto>($"api/horarios/{id}/estado", new CambiarEstadoRolRequest { Estado = estado });

    // Asignaciones
    public Task<ApiResult<List<AsignacionHorarioDto>>> AsignacionesAsync(Guid? trabajadorId = null) =>
        api.GetAsync<List<AsignacionHorarioDto>>("api/horarios/asignaciones" + Qs.De(("trabajadorId", trabajadorId)));

    public Task<ApiResult<AsignacionHorarioDto>> AsignarAsync(AsignarHorarioRequest r) => api.PostAsync<AsignacionHorarioDto>("api/horarios/asignaciones", r);
    public Task<ApiResult> CerrarAsignacionAsync(Guid id, DateOnly hasta) => api.SendAsync(HttpMethod.Post, $"api/horarios/asignaciones/{id}/cerrar", new CerrarAsignacionBody { FechaHasta = hasta });

    public Task<ApiResult<HorarioAplicableDto>> AplicableAsync(Guid trabajadorId, DateOnly fecha) =>
        api.GetAsync<HorarioAplicableDto>("api/horarios/aplicable" + Qs.De(("trabajadorId", trabajadorId), ("fecha", fecha.ToString("yyyy-MM-dd"))));

    // Descansos y festivos
    public Task<ApiResult<List<DescansoTrabajadorDto>>> DescansosAsync(Guid? trabajadorId = null) =>
        api.GetAsync<List<DescansoTrabajadorDto>>("api/calendario-laboral/descansos" + Qs.De(("trabajadorId", trabajadorId)));

    public Task<ApiResult<DescansoTrabajadorDto>> CrearDescansoAsync(GuardarDescansoRequest r) => api.PostAsync<DescansoTrabajadorDto>("api/calendario-laboral/descansos", r);
    public Task<ApiResult<DescansoTrabajadorDto>> TerminarDescansoAsync(Guid id) => api.PostAsync<DescansoTrabajadorDto>($"api/calendario-laboral/descansos/{id}/terminar");
    public Task<ApiResult<List<DiaFestivoDto>>> FestivosAsync() => api.GetAsync<List<DiaFestivoDto>>("api/calendario-laboral/festivos");
    public Task<ApiResult<DiaFestivoDto>> CrearFestivoAsync(GuardarDiaFestivoRequest r) => api.PostAsync<DiaFestivoDto>("api/calendario-laboral/festivos", r);
    public Task<ApiResult> EliminarFestivoAsync(Guid id) => api.SendAsync(HttpMethod.Delete, $"api/calendario-laboral/festivos/{id}");
}

public class CerrarAsignacionBody
{
    public DateOnly FechaHasta { get; set; }
}
