using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

internal sealed record DatosEmpresa(string Nombre, string? NombreComercial, string? RFC, string? CorreoContacto, bool EsPlataforma);

internal sealed record DatosAdmin(string Correo, string Nombre, string Apellido, string Contrasena, bool DebeCambiarContrasena);

/// <summary>Alta de una empresa completa (configuración, roles de sistema con sus permisos y administrador). Solo agrega al contexto; quien llama guarda.</summary>
internal static class EmpresaFactory
{
    public static (Empresa Empresa, Usuario Admin) Agregar(RegistroTrabajoDbContext db, IPasswordService passwords, DateTime now,
        IReadOnlyList<PermisoSistema> catalogo, DatosEmpresa datos, DatosAdmin admin, bool conRolPlataforma)
    {
        var requeridos = conRolPlataforma ? Permisos.Todos.Concat(PermisosPlataforma.Todos) : Permisos.Todos;
        var faltantes = requeridos.Except(catalogo.Select(p => p.Codigo)).ToList();
        if (faltantes.Count > 0)
            throw ServiceException.Conflict("El catálogo PermisosSistema está incompleto (aplique los scripts SQL pendientes): " + string.Join(", ", faltantes));

        var empresa = new Empresa
        {
            EmpresaId = Guid.NewGuid(), Nombre = datos.Nombre.Trim(), NombreComercial = Helpers.Limpiar(datos.NombreComercial),
            RFC = Helpers.Limpiar(datos.RFC)?.ToUpperInvariant(), CorreoContacto = Helpers.Limpiar(datos.CorreoContacto),
            Estado = EstadoEmpresa.Activa, EsPlataforma = datos.EsPlataforma, FechaCreacionUtc = now
        };
        db.Empresas.Add(empresa);
        db.ConfiguracionEmpresa.Add(new ConfiguracionEmpresa
        {
            EmpresaId = empresa.EmpresaId, ZonaHorariaId = "Central Standard Time (Mexico)", Cultura = "es-MX",
            FormatoFecha = "dd/MM/yyyy", FormatoHora = "HH:mm", CierreSesionInactividadMinutos = 30,
            DuracionSesionMaximaHoras = 12, PermitirValidacionGps = false, FechaCreacionUtc = now
        });

        Guid rolAdminId = default;
        foreach (var nombreRol in NombresRol.Iniciales)
        {
            var rol = AgregarRol(db, catalogo, empresa.EmpresaId, nombreRol, PermisosPorDefecto.PorRol[nombreRol], now);
            if (nombreRol == NombresRol.Administrador) rolAdminId = rol.RolId;
        }

        var correo = admin.Correo.Trim();
        var usuario = new Usuario
        {
            UsuarioId = Guid.NewGuid(), EmpresaId = empresa.EmpresaId, Correo = correo,
            CorreoNormalizado = Helpers.NormalizarCorreo(correo), Nombre = admin.Nombre.Trim(), Apellido = admin.Apellido.Trim(),
            Estado = EstadoUsuario.Activo, DebeCambiarContrasena = admin.DebeCambiarContrasena, FechaCreacionUtc = now,
            CorreoConfirmadoUtc = now, HashContrasena = string.Empty
        };
        usuario.HashContrasena = passwords.Hash(usuario, admin.Contrasena);
        db.Usuarios.Add(usuario);
        db.UsuarioRoles.Add(new UsuarioRol { EmpresaId = empresa.EmpresaId, UsuarioId = usuario.UsuarioId, RolId = rolAdminId, AsignadoUtc = now });

        if (conRolPlataforma)
        {
            var rolPlataforma = AgregarRol(db, catalogo, empresa.EmpresaId, NombresRol.AdministradorPlataforma, PermisosPlataforma.Todos, now);
            db.UsuarioRoles.Add(new UsuarioRol { EmpresaId = empresa.EmpresaId, UsuarioId = usuario.UsuarioId, RolId = rolPlataforma.RolId, AsignadoUtc = now });
        }
        return (empresa, usuario);
    }

    private static Rol AgregarRol(RegistroTrabajoDbContext db, IReadOnlyList<PermisoSistema> catalogo, Guid empresaId,
        string nombre, IEnumerable<string> permisos, DateTime now)
    {
        var rol = new Rol { RolId = Guid.NewGuid(), EmpresaId = empresaId, Nombre = nombre, EsSistema = true, Estado = true, FechaCreacionUtc = now };
        db.Roles.Add(rol);
        foreach (var codigo in permisos)
            db.RolPermisos.Add(new RolPermiso
            {
                EmpresaId = empresaId, RolId = rol.RolId, PermisoId = catalogo.First(p => p.Codigo == codigo).PermisoId,
                Permitido = true, FechaActualizacionUtc = now
            });
        return rol;
    }
}

public interface IBootstrapService
{
    /// <summary>Crea la primera empresa, sus roles iniciales y su administrador. Solo funciona con la BD sin empresas.</summary>
    Task<Guid> CrearEmpresaInicialAsync(string nombreEmpresa, string correoAdmin, string nombre, string apellido,
        string contrasena, CancellationToken ct, bool debeCambiarContrasena = true);

    /// <summary>Crea la empresa de plataforma y su administrador (quien da de alta las demás empresas). Solo una por base de datos.</summary>
    Task<Guid> CrearEmpresaPlataformaAsync(string nombreEmpresa, string correoAdmin, string nombre, string apellido,
        string contrasena, CancellationToken ct);

    /// <summary>Crea (si no existe) un usuario con un rol de sistema en la primera empresa. Uso de inicialización/desarrollo, sin usuario autenticado.</summary>
    Task<bool> CrearUsuarioConRolAsync(string rolNombre, string correo, string nombre, string apellido,
        string contrasena, CancellationToken ct, bool restablecerContrasena = false);

    /// <summary>Restablece la contraseña de una cuenta existente (cualquier empresa) y limpia intentos fallidos y bloqueo. Uso de desarrollo, sin usuario autenticado. Devuelve false si el correo no existe.</summary>
    Task<bool> RestablecerContrasenaExistenteAsync(string correo, string contrasena, CancellationToken ct);
}

public class BootstrapService : IBootstrapService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public BootstrapService(RegistroTrabajoDbContext db, IPasswordService passwords, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _passwords = passwords; _auditoria = auditoria; _clock = clock;
    }

    public async Task<Guid> CrearEmpresaInicialAsync(string nombreEmpresa, string correoAdmin, string nombre,
        string apellido, string contrasena, CancellationToken ct, bool debeCambiarContrasena = true)
    {
        Validar(nombreEmpresa, correoAdmin, nombre, apellido, contrasena);
        if (await _db.Empresas.AnyAsync(ct))
            throw ServiceException.Conflict("La inicialización ya se realizó: existen empresas registradas.");
        return await CrearAsync(new DatosEmpresa(nombreEmpresa, null, null, null, false),
            new DatosAdmin(correoAdmin, nombre, apellido, contrasena, debeCambiarContrasena), false, "InicializarEmpresa", ct);
    }

    public async Task<Guid> CrearEmpresaPlataformaAsync(string nombreEmpresa, string correoAdmin, string nombre,
        string apellido, string contrasena, CancellationToken ct)
    {
        Validar(nombreEmpresa, correoAdmin, nombre, apellido, contrasena);
        if (await _db.Empresas.AnyAsync(e => e.EsPlataforma, ct))
            throw ServiceException.Conflict("Ya existe una empresa de plataforma.");
        return await CrearAsync(new DatosEmpresa(nombreEmpresa, null, null, null, true),
            new DatosAdmin(correoAdmin, nombre, apellido, contrasena, true), true, "InicializarPlataforma", ct);
    }

    private void Validar(string nombreEmpresa, string correoAdmin, string nombre, string apellido, string contrasena)
    {
        if (string.IsNullOrWhiteSpace(nombreEmpresa) || string.IsNullOrWhiteSpace(correoAdmin)
            || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(apellido))
            throw ServiceException.Validation("Faltan datos obligatorios.");
        _passwords.ValidarPolitica(contrasena);
    }

    private async Task<Guid> CrearAsync(DatosEmpresa empresa, DatosAdmin admin, bool plataforma, string accion, CancellationToken ct)
    {
        if (await _db.Usuarios.AnyAsync(u => u.CorreoNormalizado == Helpers.NormalizarCorreo(admin.Correo), ct))
            throw ServiceException.Conflict("El correo ya está registrado.");

        var catalogo = await _db.PermisosSistema.ToListAsync(ct);
        var (e, u) = EmpresaFactory.Agregar(_db, _passwords, _clock.GetUtcNow().UtcDateTime, catalogo, empresa, admin, plataforma);
        _auditoria.Registrar("Empresa", nameof(Empresa), e.EmpresaId.ToString(), accion, null,
            new { empresa = e.Nombre, administrador = u.Correo }, empresaId: e.EmpresaId, usuarioId: u.UsuarioId);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveSafeAsync(ct);
        await tx.CommitAsync(ct);
        return e.EmpresaId;
    }

    public async Task<bool> RestablecerContrasenaExistenteAsync(string correo, string contrasena, CancellationToken ct)
    {
        _passwords.ValidarPolitica(contrasena);
        var normalizado = Helpers.NormalizarCorreo(correo);
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.CorreoNormalizado == normalizado, ct);
        if (usuario is null) return false;

        usuario.HashContrasena = _passwords.Hash(usuario, contrasena);
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHastaUtc = null;
        usuario.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        _auditoria.Registrar("Usuarios", nameof(Usuario), usuario.UsuarioId.ToString(), "RestablecerContrasenaDesarrollo",
            empresaId: usuario.EmpresaId, usuarioId: usuario.UsuarioId);
        await _db.SaveSafeAsync(ct);
        return true;
    }

    public async Task<bool> CrearUsuarioConRolAsync(string rolNombre, string correo, string nombre, string apellido,
        string contrasena, CancellationToken ct, bool restablecerContrasena = false)
    {
        _passwords.ValidarPolitica(contrasena);
        var normalizado = Helpers.NormalizarCorreo(correo);
        var existente = await _db.Usuarios.FirstOrDefaultAsync(u => u.CorreoNormalizado == normalizado, ct);
        if (existente is not null)
        {
            if (!restablecerContrasena) return false;
            existente.HashContrasena = _passwords.Hash(existente, contrasena);
            existente.IntentosFallidos = 0;
            existente.BloqueadoHastaUtc = null;
            existente.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
            _auditoria.Registrar("Usuarios", nameof(Usuario), existente.UsuarioId.ToString(), "RestablecerContrasenaDesarrollo",
                empresaId: existente.EmpresaId, usuarioId: existente.UsuarioId);
            await _db.SaveSafeAsync(ct);
            return true;
        }

        var empresa = await _db.Empresas.OrderBy(e => e.FechaCreacionUtc).FirstOrDefaultAsync(ct)
            ?? throw ServiceException.Conflict("No hay empresas: ejecute primero la inicialización.");
        var rol = await _db.Roles.FirstOrDefaultAsync(r => r.EmpresaId == empresa.EmpresaId && r.Nombre == rolNombre, ct)
            ?? throw ServiceException.NotFound($"No existe el rol '{rolNombre}' en la empresa.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var u = new Usuario
        {
            UsuarioId = Guid.NewGuid(), EmpresaId = empresa.EmpresaId, Correo = correo.Trim(), CorreoNormalizado = normalizado,
            Nombre = nombre.Trim(), Apellido = apellido.Trim(), Estado = EstadoUsuario.Activo,
            DebeCambiarContrasena = false, CorreoConfirmadoUtc = now, FechaCreacionUtc = now, HashContrasena = string.Empty
        };
        u.HashContrasena = _passwords.Hash(u, contrasena);
        _db.Usuarios.Add(u);
        _db.UsuarioRoles.Add(new UsuarioRol { EmpresaId = empresa.EmpresaId, UsuarioId = u.UsuarioId, RolId = rol.RolId, AsignadoUtc = now });
        _auditoria.Registrar("Usuarios", nameof(Usuario), u.UsuarioId.ToString(), "CrearInicial", null,
            new { u.Correo, rol = rolNombre }, empresaId: empresa.EmpresaId, usuarioId: u.UsuarioId);
        await _db.SaveSafeAsync(ct);
        return true;
    }
}
