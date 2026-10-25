using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IContactoEmergenciaService
{
    Task<IReadOnlyList<ContactoEmergenciaDto>> ListarAsync(Guid trabajadorId, CancellationToken ct);
    Task<ContactoEmergenciaDto> CrearAsync(Guid trabajadorId, GuardarContactoEmergenciaRequest request, CancellationToken ct);
    Task<ContactoEmergenciaDto> ActualizarAsync(Guid trabajadorId, Guid contactoId, GuardarContactoEmergenciaRequest request, CancellationToken ct);
    Task EliminarAsync(Guid trabajadorId, Guid contactoId, CancellationToken ct);
}

/// <summary>Contactos de emergencia: dato sensible, solo para quien tiene el permiso o para el propio trabajador (lectura).</summary>
public class ContactoEmergenciaService : IContactoEmergenciaService
{
    private const int MaxContactos = 5;

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;

    public ContactoEmergenciaService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria;
    }

    public async Task<IReadOnlyList<ContactoEmergenciaDto>> ListarAsync(Guid trabajadorId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        var propio = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct) == trabajadorId;
        if (!propio && !await _permisos.TienePermisoAsync(Permisos.TrabajadoresVerSensibles, ct))
            throw ServiceException.Forbidden("No tiene permiso para consultar los contactos de emergencia.");

        return await _db.ContactosEmergencia.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.TrabajadorId == trabajadorId)
            .OrderByDescending(c => c.EsPrincipal).ThenBy(c => c.NombreCompleto)
            .Select(c => ToDto(c)).ToListAsync(ct);
    }

    public async Task<ContactoEmergenciaDto> CrearAsync(Guid trabajadorId, GuardarContactoEmergenciaRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        var existentes = await _db.ContactosEmergencia.Where(c => c.EmpresaId == empresaId && c.TrabajadorId == trabajadorId).ToListAsync(ct);
        if (existentes.Count >= MaxContactos) throw ServiceException.Conflict($"Solo se permiten {MaxContactos} contactos de emergencia.");

        var c = new ContactoEmergencia { ContactoEmergenciaId = Guid.NewGuid(), EmpresaId = empresaId, TrabajadorId = trabajadorId };
        Aplicar(c, r);
        // Un único principal; el primer contacto lo es por defecto.
        if (existentes.Count == 0) c.EsPrincipal = true;
        if (c.EsPrincipal) foreach (var o in existentes) o.EsPrincipal = false;

        _db.ContactosEmergencia.Add(c);
        _auditoria.Registrar("Trabajadores", nameof(ContactoEmergencia), c.ContactoEmergenciaId.ToString(), "CrearContactoEmergencia",
            null, new { trabajadorId, c.NombreCompleto, c.Parentesco, c.EsPrincipal });
        await _db.SaveSafeAsync(ct);
        return ToDto(c);
    }

    public async Task<ContactoEmergenciaDto> ActualizarAsync(Guid trabajadorId, Guid contactoId, GuardarContactoEmergenciaRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var c = await CargarAsync(empresaId, trabajadorId, contactoId, ct);
        var antes = new { c.NombreCompleto, c.Parentesco, c.EsPrincipal };
        Aplicar(c, r);
        if (c.EsPrincipal)
        {
            var otros = await _db.ContactosEmergencia.Where(x => x.EmpresaId == empresaId && x.TrabajadorId == trabajadorId && x.ContactoEmergenciaId != contactoId).ToListAsync(ct);
            foreach (var o in otros) o.EsPrincipal = false;
        }
        _auditoria.Registrar("Trabajadores", nameof(ContactoEmergencia), contactoId.ToString(), "ActualizarContactoEmergencia",
            antes, new { c.NombreCompleto, c.Parentesco, c.EsPrincipal });
        await _db.SaveSafeAsync(ct);
        return ToDto(c);
    }

    public async Task EliminarAsync(Guid trabajadorId, Guid contactoId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var c = await CargarAsync(empresaId, trabajadorId, contactoId, ct);
        _db.ContactosEmergencia.Remove(c);
        _auditoria.Registrar("Trabajadores", nameof(ContactoEmergencia), contactoId.ToString(), "EliminarContactoEmergencia",
            new { trabajadorId, c.NombreCompleto, c.Parentesco }, null);
        await _db.SaveSafeAsync(ct);
    }

    private async Task<ContactoEmergencia> CargarAsync(Guid empresaId, Guid trabajadorId, Guid contactoId, CancellationToken ct) =>
        await _db.ContactosEmergencia.FirstOrDefaultAsync(c => c.ContactoEmergenciaId == contactoId
            && c.EmpresaId == empresaId && c.TrabajadorId == trabajadorId, ct)
        ?? throw ServiceException.NotFound("Contacto de emergencia no encontrado.");

    private static void Aplicar(ContactoEmergencia c, GuardarContactoEmergenciaRequest r)
    {
        var nombre = (r.NombreCompleto ?? "").Trim();
        var tel = (r.Telefono ?? "").Trim();
        if (nombre.Length < 2) throw ServiceException.Validation("El nombre del contacto es obligatorio.");
        if (tel.Length < 5) throw ServiceException.Validation("El teléfono del contacto es obligatorio.");
        c.NombreCompleto = nombre;
        c.Parentesco = Helpers.Limpiar(r.Parentesco);
        c.Telefono = tel;
        c.TelefonoAlternativo = Helpers.Limpiar(r.TelefonoAlternativo);
        c.EsPrincipal = r.EsPrincipal;
    }

    private static ContactoEmergenciaDto ToDto(ContactoEmergencia c) => new()
    {
        ContactoEmergenciaId = c.ContactoEmergenciaId, NombreCompleto = c.NombreCompleto, Parentesco = c.Parentesco,
        Telefono = c.Telefono, TelefonoAlternativo = c.TelefonoAlternativo, EsPrincipal = c.EsPrincipal
    };
}

public interface ISolicitudCambioPerfilService
{
    Task<SolicitudCambioPerfilDto> CrearAsync(CambioPerfilDatos datos, CancellationToken ct);
    Task<IReadOnlyList<SolicitudCambioPerfilDto>> MisSolicitudesAsync(CancellationToken ct);
    Task<SolicitudCambioPerfilDto> CancelarAsync(Guid solicitudId, CancellationToken ct);
    Task<PagedResult<SolicitudCambioPerfilDto>> ListarAsync(SolicitudPerfilFiltro filtro, CancellationToken ct);
    Task<SolicitudCambioPerfilDto> ResolverAsync(Guid solicitudId, ResolverSolicitudRequest request, CancellationToken ct);
}

/// <summary>
/// El trabajador pide cambiar su correo/teléfono/dirección; el expediente oficial no cambia hasta que un revisor
/// autorizado apruebe (política por defecto: siempre con aprobación).
/// </summary>
public class SolicitudCambioPerfilService : ISolicitudCambioPerfilService
{
    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public SolicitudCambioPerfilService(RegistroTrabajoDbContext db, ICurrentUser current, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _auditoria = auditoria; _clock = clock;
    }

    public async Task<SolicitudCambioPerfilDto> CrearAsync(CambioPerfilDatos datos, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await MiTrabajadorAsync(empresaId, true, ct);
        if (t.EstadoLaboral == EstadoLaboral.Baja) throw ServiceException.Validation("El trabajador está dado de baja.");

        var limpio = new CambioPerfilDatos
        {
            CorreoContacto = Helpers.Limpiar(datos.CorreoContacto),
            Telefono = Helpers.Limpiar(datos.Telefono),
            Direccion = Helpers.Limpiar(datos.Direccion)
        };
        if (limpio.CorreoContacto is not null && (!limpio.CorreoContacto.Contains('@') || limpio.CorreoContacto.Length > 254))
            throw ServiceException.Validation("El correo de contacto no es válido.");
        var cambia = (limpio.CorreoContacto is not null && limpio.CorreoContacto != t.CorreoContacto)
            || (limpio.Telefono is not null && limpio.Telefono != t.Telefono)
            || (limpio.Direccion is not null && limpio.Direccion != t.Direccion);
        if (!cambia) throw ServiceException.Validation("La solicitud no contiene cambios respecto a su perfil actual.");
        if (await _db.SolicitudesCambioPerfil.AnyAsync(s => s.EmpresaId == empresaId && s.TrabajadorId == t.TrabajadorId
            && s.Estado == EstadoSolicitudPerfil.Pendiente, ct))
            throw ServiceException.Conflict("Ya tiene una solicitud pendiente de revisión.");

        var s = new SolicitudCambioPerfil
        {
            SolicitudCambioPerfilId = Guid.NewGuid(), EmpresaId = empresaId, TrabajadorId = t.TrabajadorId,
            DatosSolicitadosJson = JsonSerializer.Serialize(limpio, Json), Estado = EstadoSolicitudPerfil.Pendiente,
            SolicitadaUtc = _clock.GetUtcNow().UtcDateTime
        };
        _db.SolicitudesCambioPerfil.Add(s);
        _auditoria.Registrar("Perfil", nameof(SolicitudCambioPerfil), s.SolicitudCambioPerfilId.ToString(), "SolicitarCambio", null, new { campos = Campos(limpio) });
        await _db.SaveSafeAsync(ct);
        return ToDto(s, $"{t.Nombres} {t.Apellidos}");
    }

    public async Task<IReadOnlyList<SolicitudCambioPerfilDto>> MisSolicitudesAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await MiTrabajadorAsync(empresaId, false, ct);
        var filas = await _db.SolicitudesCambioPerfil.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.TrabajadorId == t.TrabajadorId)
            .OrderByDescending(s => s.SolicitadaUtc).ToListAsync(ct);
        return filas.Select(s => ToDto(s, $"{t.Nombres} {t.Apellidos}")).ToList();
    }

    public async Task<SolicitudCambioPerfilDto> CancelarAsync(Guid solicitudId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await MiTrabajadorAsync(empresaId, false, ct);
        // Se filtra por el trabajador propio: las solicitudes ajenas responden 404.
        var s = await _db.SolicitudesCambioPerfil.FirstOrDefaultAsync(x => x.SolicitudCambioPerfilId == solicitudId
            && x.EmpresaId == empresaId && x.TrabajadorId == t.TrabajadorId, ct)
            ?? throw ServiceException.NotFound("Solicitud no encontrada.");
        if (s.Estado != EstadoSolicitudPerfil.Pendiente) throw ServiceException.Conflict("La solicitud ya fue resuelta.");

        s.Estado = EstadoSolicitudPerfil.Cancelada;
        s.RevisadaUtc = _clock.GetUtcNow().UtcDateTime;
        _auditoria.Registrar("Perfil", nameof(SolicitudCambioPerfil), solicitudId.ToString(), "CancelarSolicitud");
        await _db.SaveSafeAsync(ct);
        return ToDto(s, $"{t.Nombres} {t.Apellidos}");
    }

    public async Task<PagedResult<SolicitudCambioPerfilDto>> ListarAsync(SolicitudPerfilFiltro f, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = _db.SolicitudesCambioPerfil.AsNoTracking().Where(s => s.EmpresaId == empresaId);
        if (f.Estado is { } e) q = q.Where(s => s.Estado == e);
        var pagina = await q.OrderByDescending(s => s.SolicitadaUtc)
            .Select(s => new { s, Nombre = s.Trabajador.Nombres + " " + s.Trabajador.Apellidos })
            .ToPagedAsync(f, ct);
        return new PagedResult<SolicitudCambioPerfilDto>
        {
            Items = pagina.Items.Select(x => ToDto(x.s, x.Nombre)).ToList(),
            Page = pagina.Page, PageSize = pagina.PageSize, Total = pagina.Total
        };
    }

    public async Task<SolicitudCambioPerfilDto> ResolverAsync(Guid solicitudId, ResolverSolicitudRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var s = await _db.SolicitudesCambioPerfil.FirstOrDefaultAsync(x => x.SolicitudCambioPerfilId == solicitudId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Solicitud no encontrada.");
        if (s.Estado != EstadoSolicitudPerfil.Pendiente) throw ServiceException.Conflict("La solicitud ya fue resuelta.");
        if (await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, actorId, ct) == s.TrabajadorId)
            throw ServiceException.Forbidden("No puede resolver su propia solicitud.");
        var comentario = Helpers.Limpiar(r.Comentario);
        if (!r.Aprobar && comentario is null) throw ServiceException.Validation("Indique el motivo del rechazo.");

        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, s.TrabajadorId, true, ct);
        var datos = Leer(s.DatosSolicitadosJson);
        if (r.Aprobar)
        {
            if (t.EstadoLaboral == EstadoLaboral.Baja) throw ServiceException.Validation("El trabajador está dado de baja; rechace la solicitud.");
            var antes = new { t.CorreoContacto, t.Telefono, t.Direccion };
            if (datos.CorreoContacto is not null) t.CorreoContacto = datos.CorreoContacto;
            if (datos.Telefono is not null) t.Telefono = datos.Telefono;
            if (datos.Direccion is not null) t.Direccion = datos.Direccion;
            t.FechaActualizacionUtc = now;
            _db.HistorialLaboral.Add(TrabajadorHelper.Historial(t, TipoCambioLaboral.Perfil, antes,
                new { t.CorreoContacto, t.Telefono, t.Direccion }, TrabajadorHelper.Hoy(_clock), "Solicitud de cambio de perfil aprobada", actorId, now));
        }

        s.Estado = r.Aprobar ? EstadoSolicitudPerfil.Aprobada : EstadoSolicitudPerfil.Rechazada;
        s.RevisadaPorUsuarioId = actorId;
        s.RevisadaUtc = now;
        s.ComentarioRevision = comentario;
        _auditoria.Registrar("Perfil", nameof(SolicitudCambioPerfil), solicitudId.ToString(), r.Aprobar ? "AprobarSolicitud" : "RechazarSolicitud",
            new { Estado = EstadoSolicitudPerfil.Pendiente }, new { s.Estado, campos = Campos(datos) });
        await _db.SaveSafeAsync(ct);
        return ToDto(s, $"{t.Nombres} {t.Apellidos}");
    }

    private async Task<Trabajador> MiTrabajadorAsync(Guid empresaId, bool tracking, CancellationToken ct)
    {
        var id = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct)
            ?? throw ServiceException.NotFound("Su cuenta no está vinculada a un expediente de trabajador.");
        return await TrabajadorHelper.CargarAsync(_db, empresaId, id, tracking, ct);
    }

    private static CambioPerfilDatos Leer(string json) =>
        JsonSerializer.Deserialize<CambioPerfilDatos>(json, Json) ?? new CambioPerfilDatos();

    private static string[] Campos(CambioPerfilDatos d) =>
        new[] { d.CorreoContacto is null ? null : "CorreoContacto", d.Telefono is null ? null : "Telefono", d.Direccion is null ? null : "Direccion" }
            .Where(x => x is not null).Select(x => x!).ToArray();

    private static SolicitudCambioPerfilDto ToDto(SolicitudCambioPerfil s, string nombre) => new()
    {
        SolicitudCambioPerfilId = s.SolicitudCambioPerfilId, TrabajadorId = s.TrabajadorId, TrabajadorNombre = nombre,
        Datos = Leer(s.DatosSolicitadosJson), Estado = s.Estado, SolicitadaUtc = s.SolicitadaUtc,
        RevisadaPorUsuarioId = s.RevisadaPorUsuarioId, RevisadaUtc = s.RevisadaUtc, ComentarioRevision = s.ComentarioRevision
    };
}
