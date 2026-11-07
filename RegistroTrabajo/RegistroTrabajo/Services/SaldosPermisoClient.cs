using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

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
