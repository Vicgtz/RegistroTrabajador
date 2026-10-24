using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class ResumenInicio
{
    public EmpresaDto? Empresa { get; set; }
    public int? UsuariosTotal { get; set; }
    public int? SesionesActivas { get; set; }
    public IReadOnlyList<HistorialAccesoDto>? AccesosRecientes { get; set; }
}

/// <summary>Datos de la pantalla de inicio. Solo pide lo que el perfil permite ver; la API vuelve a validar cada permiso.</summary>
public class InicioClient(ApiClient api)
{
    public async Task<ResumenInicio> ObtenerResumenAsync(PerfilDto perfil)
    {
        bool Puede(string p) => perfil.Permisos.Contains(p);

        var empresa = Puede(Permisos.EmpresaConsultar) ? api.GetAsync<EmpresaDto>("api/empresa") : null;
        var usuarios = Puede(Permisos.UsuariosConsultar) ? api.GetAsync<PagedResult<UsuarioDto>>("api/usuarios?page=1&pageSize=1") : null;
        var sesiones = Puede(Permisos.SesionesConsultar) ? api.GetAsync<PagedResult<SesionDto>>("api/sesiones?page=1&pageSize=1") : null;
        var accesos = Puede(Permisos.AccesosConsultar) ? api.GetAsync<PagedResult<HistorialAccesoDto>>("api/accesos?page=1&pageSize=5") : null;

        await Task.WhenAll(new Task?[] { empresa, usuarios, sesiones, accesos }.Where(t => t is not null)!);

        return new ResumenInicio
        {
            Empresa = empresa is null ? null : (await empresa).Data,
            UsuariosTotal = usuarios is null ? null : (await usuarios).Data?.Total,
            SesionesActivas = sesiones is null ? null : (await sesiones).Data?.Total,
            AccesosRecientes = accesos is null ? null : (await accesos).Data?.Items
        };
    }
}
