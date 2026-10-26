using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;

namespace RegistroTrabajo.Tests;

/// <summary>
/// Pruebas de integración contra SQL Server local. Se omiten si no hay conexión (cadena propia con RT_TEST_CONNECTION).
/// Todas trabajan DENTRO de una transacción que siempre se revierte: no dejan cambios en la base.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public static readonly string ConnectionString = Environment.GetEnvironmentVariable("RT_TEST_CONNECTION")
        ?? "Server=localhost;Database=RegistroTrabajo;Integrated Security=true;TrustServerCertificate=true;Encrypt=false;Connect Timeout=3";

    public SqlServerFactAttribute()
    {
        try
        {
            using var c = new SqlConnection(ConnectionString);
            c.Open();
        }
        catch
        {
            Skip = "SQL Server no disponible (defina RT_TEST_CONNECTION).";
        }
    }
}

internal static class SqlServerScripts
{
    public static string Ruta(string nombre)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidato = Path.Combine(dir.FullName, "RegistroTrabajo.Data", "Scripts", nombre);
            if (File.Exists(candidato)) return candidato;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"No se encontró {nombre}");
    }

    /// <summary>Ejecuta los scripts (dos veces, para comprobar que son idempotentes) dentro de la transacción indicada.</summary>
    public static async Task EjecutarAsync(SqlConnection conn, SqlTransaction tx, params string[] scripts)
    {
        foreach (var script in scripts)
        {
            var sql = await File.ReadAllTextAsync(Ruta(script));
            for (var i = 0; i < 2; i++)
            {
                await using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 60 };
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public static async Task RevertirAsync(SqlTransaction tx)
    {
        try { await tx.RollbackAsync(); }
        catch (InvalidOperationException) { /* ya revertida por un error del script */ }
    }
}

public class SqlServerSchemaTests
{
    private static async Task<(SqlConnection Conn, SqlTransaction Tx, RegistroTrabajoDbContext Db)> AbrirAsync(params string[] scripts)
    {
        var conn = new SqlConnection(SqlServerFactAttribute.ConnectionString);
        await conn.OpenAsync();
        var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        await SqlServerScripts.EjecutarAsync(conn, tx, scripts);
        var db = new RegistroTrabajoDbContext(new DbContextOptionsBuilder<RegistroTrabajoDbContext>().UseSqlServer(conn).Options);
        await db.Database.UseTransactionAsync(tx);
        return (conn, tx, db);
    }

    [SqlServerFact]
    public async Task Scripts_001_a_007_son_validos_e_idempotentes_sin_dejar_cambios()
    {
        var (conn, tx, db) = await AbrirAsync("003_bloque1_faltantes.sql", "004_bloque3_permisos.sql", "005_bloque4.sql", "006_bloque5.sql", "007_bloque6.sql", "001_auditoria_solo_insercion.sql");
        await using (conn) await using (db)
        try
        {
            Assert.True(await db.PermisosSistema.CountAsync() >= Models.Permisos.Todos.Length + Models.PermisosPlataforma.Todos.Length);
            // Ningún rol de una empresa normal tiene permisos de plataforma (solo los roles de la empresa de plataforma).
            var concedidos = await db.RolPermisos
                .Where(rp => rp.Permiso.Codigo.StartsWith("plataforma.") && !rp.Rol.Empresa.EsPlataforma).CountAsync();
            Assert.Equal(0, concedidos);
        }
        finally { await SqlServerScripts.RevertirAsync(tx); }
    }

    [SqlServerFact]
    public async Task Modelo_ef_coincide_con_el_esquema_real()
    {
        var (conn, tx, db) = await AbrirAsync("003_bloque1_faltantes.sql", "004_bloque3_permisos.sql", "005_bloque4.sql", "006_bloque5.sql", "007_bloque6.sql");
        await using (conn) await using (db)
        try
        {
            // Cada consulta proyecta todas las columnas mapeadas: falla si falta alguna tabla o columna.
            await db.Empresas.AsNoTracking().Take(1).ToListAsync();
            await db.ConfiguracionEmpresa.AsNoTracking().Take(1).ToListAsync();
            await db.Usuarios.AsNoTracking().Take(1).ToListAsync();
            await db.Roles.AsNoTracking().Take(1).ToListAsync();
            await db.PermisosSistema.AsNoTracking().Take(1).ToListAsync();
            await db.UsuarioRoles.AsNoTracking().Take(1).ToListAsync();
            await db.RolPermisos.AsNoTracking().Take(1).ToListAsync();
            await db.SesionesUsuario.AsNoTracking().Take(1).ToListAsync();
            await db.HistorialAcceso.AsNoTracking().Take(1).ToListAsync();
            await db.Auditoria.AsNoTracking().Take(1).ToListAsync();
            await db.TokensConfirmacionCorreo.AsNoTracking().Take(1).ToListAsync();

            Assert.True(await db.PermisosSistema.CountAsync() >= Models.Permisos.Todos.Length);
        }
        finally { await SqlServerScripts.RevertirAsync(tx); }
    }

    [SqlServerFact]
    public async Task Todas_las_tablas_y_columnas_de_la_base_estan_mapeadas_en_el_modelo()
    {
        var (conn, tx, db) = await AbrirAsync("003_bloque1_faltantes.sql", "004_bloque3_permisos.sql", "005_bloque4.sql", "006_bloque5.sql", "007_bloque6.sql");
        await using (conn) await using (db)
        try
        {
            // 1) Cada entidad se consulta (falla si falta una tabla o una columna mapeada).
            var probar = typeof(SqlServerSchemaTests).GetMethod(nameof(Probar), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            foreach (var et in db.Model.GetEntityTypes())
                await (Task)probar.MakeGenericMethod(et.ClrType).Invoke(null, new object[] { db })!;

            // 2) La base no tiene tablas ni columnas que el modelo desconozca.
            var enBase = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = new SqlCommand(
                "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME NOT LIKE '[_][_]%'", conn, tx))
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                {
                    if (!enBase.TryGetValue(r.GetString(0), out var cols)) enBase[r.GetString(0)] = cols = new(StringComparer.OrdinalIgnoreCase);
                    cols.Add(r.GetString(1));
                }

            var mapeadas = db.Model.GetEntityTypes().ToDictionary(e => e.GetTableName()!, e =>
                e.GetProperties().Select(p => p.GetColumnName()).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            Assert.Empty(enBase.Keys.Except(mapeadas.Keys));   // tablas sin entidad
            foreach (var (tabla, columnas) in enBase)
                Assert.True(columnas.SetEquals(mapeadas[tabla]),
                    $"{tabla}: diferencias -> en BD sin mapear: [{string.Join(", ", columnas.Except(mapeadas[tabla]))}], mapeadas sin existir: [{string.Join(", ", mapeadas[tabla].Except(columnas))}]");
        }
        finally { await SqlServerScripts.RevertirAsync(tx); }
    }

    private static async Task Probar<T>(RegistroTrabajoDbContext db) where T : class =>
        await db.Set<T>().AsNoTracking().Take(1).ToListAsync();
}
