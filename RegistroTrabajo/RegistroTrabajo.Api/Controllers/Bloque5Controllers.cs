using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Ninguna ruta ni cuerpo recibe EmpresaId: la empresa sale siempre del token y los servicios filtran por ella.
// La hora de asistencia la genera el servidor. Consultas y correcciones de otros trabajadores validan el alcance dentro del servicio.

[ApiController, Authorize, Route("api/turnos")]
public class TurnosController : ControllerBase
{
    private readonly ITurnosService _svc;
    public TurnosController(ITurnosService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<IReadOnlyList<TurnoDto>> Listar([FromQuery] bool soloActivos, CancellationToken ct) => await _svc.ListarAsync(soloActivos, ct);

    [HttpPost, RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<TurnoDto> Crear(GuardarTurnoRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<TurnoDto> Actualizar(Guid id, GuardarTurnoRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<TurnoDto> CambiarEstado(Guid id, CambiarEstadoRolRequest r, CancellationToken ct) => await _svc.CambiarEstadoAsync(id, r.Estado, ct);

    [HttpPost("iniciales"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<IReadOnlyList<TurnoDto>> SembrarIniciales(CancellationToken ct) => await _svc.SembrarInicialesAsync(ct);
}

[ApiController, Authorize, Route("api/horarios")]
public class HorariosController : ControllerBase
{
    private readonly IHorariosService _svc;
    public HorariosController(IHorariosService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<IReadOnlyList<HorarioLaboralDto>> Listar([FromQuery] bool soloActivos, CancellationToken ct) => await _svc.ListarAsync(soloActivos, ct);

    [HttpGet("{id:guid}"), RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<HorarioLaboralDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);

    [HttpPost, RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<HorarioLaboralDto> Crear(GuardarHorarioRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<HorarioLaboralDto> Actualizar(Guid id, GuardarHorarioRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/nueva-version"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<HorarioLaboralDto> NuevaVersion(Guid id, GuardarHorarioRequest r, CancellationToken ct) => await _svc.NuevaVersionAsync(id, r, ct);

    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<HorarioLaboralDto> CambiarEstado(Guid id, CambiarEstadoRolRequest r, CancellationToken ct) => await _svc.CambiarEstadoAsync(id, r.Estado, ct);

    [HttpGet("asignaciones"), RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<IReadOnlyList<AsignacionHorarioDto>> Asignaciones([FromQuery] Guid? trabajadorId, CancellationToken ct) => await _svc.ListarAsignacionesAsync(trabajadorId, ct);

    [HttpPost("asignaciones"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<AsignacionHorarioDto> Asignar(AsignarHorarioRequest r, CancellationToken ct) => await _svc.AsignarAsync(r, ct);

    [HttpPost("asignaciones/{id:guid}/cerrar"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<IActionResult> CerrarAsignacion(Guid id, CerrarAsignacionRequest r, CancellationToken ct)
    {
        await _svc.CerrarAsignacionAsync(id, r.FechaHasta, ct);
        return NoContent();
    }

    [HttpGet("aplicable"), RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<HorarioAplicableDto> Aplicable([FromQuery] Guid trabajadorId, [FromQuery] DateOnly fecha, CancellationToken ct) => await _svc.AplicableAsync(trabajadorId, fecha, ct);
}

public class CerrarAsignacionRequest
{
    public DateOnly FechaHasta { get; set; }
}

[ApiController, Authorize, Route("api/calendario-laboral")]
public class CalendarioLaboralController : ControllerBase
{
    private readonly ICalendarioLaboralService _svc;
    public CalendarioLaboralController(ICalendarioLaboralService svc) => _svc = svc;

    [HttpGet("descansos"), RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<IReadOnlyList<DescansoTrabajadorDto>> Descansos([FromQuery] Guid? trabajadorId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct) =>
        await _svc.ListarDescansosAsync(trabajadorId, desde, hasta, ct);

    [HttpPost("descansos"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<DescansoTrabajadorDto> CrearDescanso(GuardarDescansoRequest r, CancellationToken ct) => await _svc.CrearDescansoAsync(r, ct);

    [HttpPut("descansos/{id:guid}"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<DescansoTrabajadorDto> ActualizarDescanso(Guid id, GuardarDescansoRequest r, CancellationToken ct) => await _svc.ActualizarDescansoAsync(id, r, ct);

    [HttpPost("descansos/{id:guid}/terminar"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<DescansoTrabajadorDto> TerminarDescanso(Guid id, CancellationToken ct) => await _svc.TerminarDescansoAsync(id, ct);

    [HttpGet("festivos"), RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<IReadOnlyList<DiaFestivoDto>> Festivos([FromQuery] int? anio, CancellationToken ct) => await _svc.ListarFestivosAsync(anio, ct);

    [HttpPost("festivos"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<DiaFestivoDto> CrearFestivo(GuardarDiaFestivoRequest r, CancellationToken ct) => await _svc.CrearFestivoAsync(r, ct);

    [HttpPut("festivos/{id:guid}"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<DiaFestivoDto> ActualizarFestivo(Guid id, GuardarDiaFestivoRequest r, CancellationToken ct) => await _svc.ActualizarFestivoAsync(id, r, ct);

    [HttpDelete("festivos/{id:guid}"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<IActionResult> EliminarFestivo(Guid id, CancellationToken ct)
    {
        await _svc.EliminarFestivoAsync(id, ct);
        return NoContent();
    }
}

[ApiController, Authorize, Route("api/asistencia")]
public class AsistenciaController : ControllerBase
{
    private readonly IAsistenciaService _svc;
    private readonly ICorreccionesAsistenciaService _correcciones;

    public AsistenciaController(IAsistenciaService svc, ICorreccionesAsistenciaService correcciones)
    {
        _svc = svc; _correcciones = correcciones;
    }

    [HttpGet("estado"), RequierePermiso(Permisos.AsistenciaRegistrar)]
    public async Task<EstadoJornadaDto> Estado(CancellationToken ct) => await _svc.EstadoActualAsync(ct);

    [HttpPost("entrada"), RequierePermiso(Permisos.AsistenciaRegistrar)]
    public async Task<RegistroAsistenciaDto> Entrada(RegistrarAsistenciaRequest r, CancellationToken ct) => await _svc.RegistrarEntradaAsync(r, ct);

    [HttpPost("salida"), RequierePermiso(Permisos.AsistenciaRegistrar)]
    public async Task<RegistroAsistenciaDto> Salida(RegistrarAsistenciaRequest r, CancellationToken ct) => await _svc.RegistrarSalidaAsync(r, ct);

    [HttpGet("mias"), RequierePermiso(Permisos.AsistenciaConsultarPropia)]
    public async Task<PagedResult<RegistroAsistenciaDto>> Mias([FromQuery] AsistenciaFiltro f, CancellationToken ct) => await _svc.MisRegistrosAsync(f, ct);

    [HttpGet]
    public async Task<PagedResult<RegistroAsistenciaDto>> Listar([FromQuery] AsistenciaFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);

    [HttpGet("{id:guid}")]
    public async Task<RegistroAsistenciaDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);

    [HttpPost("{id:guid}/corregir")]
    public async Task<RegistroAsistenciaDto> Corregir(Guid id, CorregirAsistenciaRequest r, CancellationToken ct) => await _correcciones.CorregirAsync(id, r, ct);

    [HttpPost("{id:guid}/anular")]
    public async Task<RegistroAsistenciaDto> Anular(Guid id, AnularAsistenciaRequest r, CancellationToken ct) => await _correcciones.AnularAsync(id, r, ct);

    [HttpPost("manual")]
    public async Task<RegistroAsistenciaDto> Manual(CrearRegistroManualRequest r, CancellationToken ct) => await _correcciones.CrearManualAsync(r, ct);
}
