using GRID.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Text;
using System.Text.RegularExpressions;

namespace GRID.Services
{
    // Imported: a dump copied into the backup directory by hand, under a name of its own.
    public enum BackupKind { Manual, Auto, PreRestore, Upload, Imported }

    public record BackupFile(string Name, BackupKind Kind, DateTime CreatedUtc, long SizeBytes);

    public record BackupResult(bool Success, string? Error = null, string? FileName = null)
    {
        public static BackupResult Fail(string error) => new(false, error);
    }

    /// <summary>Result of reading a backup file without restoring it.</summary>
    public record BackupInspection(bool IsValid, string? Error, IReadOnlyList<string> Migrations);

    /// <summary>
    /// Creates, lists and restores plain-SQL dumps of the app database with the pg_dump/psql
    /// binaries that ship in the Docker image. Backups are written gzipped to Backup:Path
    /// (default /app/backups, a bind mount in docker-compose.yml); plain .sql files placed
    /// there or uploaded (e.g. a pg_dump from an older install) are accepted too.
    /// </summary>
    public partial class BackupService
    {
        // Uploaded backups can be far bigger than Kestrel's 30 MB default (the audit log grows).
        public const long MaxUploadBytes = 2L * 1024 * 1024 * 1024;

        private const string DumpHeader = "-- PostgreSQL database dump";
        private const string DumpTrailer = "-- PostgreSQL database dump complete";
        private const string ClusterDumpHeader = "-- PostgreSQL database cluster dump";
        private const string MigrationsCopyLine = "COPY public.\"__EFMigrationsHistory\"";
        private const string MigrationsInsertLine = "INSERT INTO public.\"__EFMigrationsHistory\"";

        // Names of the backups this service writes; they carry the kind and creation time.
        [GeneratedRegex(@"^grid-(\d{8}-\d{6})-(manual|auto|pre-restore|upload)\.sql\.gz$")]
        private static partial Regex FileNamePattern();

        // pg_dump --inserts / --column-inserts write one INSERT per row instead of COPY.
        [GeneratedRegex(@"^INSERT INTO public\.""__EFMigrationsHistory"" (?:\([^)]*\) )?VALUES \('([^']+)'")]
        private static partial Regex MigrationInsertPattern();

        // Statements that tie a dump to the server it came from: owners and grants name that
        // server's roles, and --create adds its own CREATE DATABASE and \connect. pg_dump writes
        // each on one line. Restores skip them, like pg_restore --no-owner --no-privileges, so a
        // dump from an install whose database user had another name still loads; everything
        // ends up owned by the user GRID connects as.
        [GeneratedRegex(@"^(?:ALTER [A-Z ]+ .+ OWNER TO .+;|(?:GRANT|REVOKE) .+;|ALTER DEFAULT PRIVILEGES .+;|(?:CREATE|ALTER) DATABASE .+;|\\connect .+)$")]
        private static partial Regex ServerSpecificStatementPattern();

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PermissionService _permissionService;
        private readonly ILogger<BackupService> _logger;
        private readonly string _connectionString;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private volatile bool _isRestoring;

        public BackupService(IConfiguration config, IWebHostEnvironment env, IServiceScopeFactory scopeFactory,
            PermissionService permissionService, ILogger<BackupService> logger)
        {
            _scopeFactory = scopeFactory;
            _permissionService = permissionService;
            _logger = logger;
            _connectionString = config["ConnectionStrings:DefaultConnection"]
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            BackupDirectory = config["Backup:Path"]
                ?? (env.IsDevelopment() ? Path.Combine(env.ContentRootPath, "backups") : "/app/backups");
        }

        public string BackupDirectory { get; }

        /// <summary>True while a restore is replacing the database; requests get a 503 meanwhile.</summary>
        public bool IsRestoring => _isRestoring;

        public IReadOnlyList<BackupFile> ListBackups()
        {
            if (!Directory.Exists(BackupDirectory)) return [];

            return new DirectoryInfo(BackupDirectory).EnumerateFiles()
                .Where(f => IsBackupFileName(f.Name))
                .Select(f => ParseFileName(f.Name) is { } parsed
                    ? new BackupFile(f.Name, parsed.Kind, parsed.CreatedUtc, f.Length)
                    : new BackupFile(f.Name, BackupKind.Imported, f.LastWriteTimeUtc, f.Length))
                .OrderByDescending(b => b.CreatedUtc)
                .ToList();
        }

        /// <summary>Full path of an existing backup, or null if the name is invalid or missing.</summary>
        public string? GetBackupPath(string? name)
        {
            if (name == null || !IsBackupFileName(name)) return null;
            var path = Path.Combine(BackupDirectory, name);
            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// A .sql or .sql.gz file directly inside the backup directory. Names arrive in requests,
        /// so anything with a directory part is refused, as are hidden and in-progress files.
        /// </summary>
        internal static bool IsBackupFileName(string name) =>
            name.Length > 0 && name == Path.GetFileName(name) && !name.StartsWith('.') &&
            (name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith(".sql.gz", StringComparison.OrdinalIgnoreCase));

        public Task<BackupResult> CreateBackupAsync(BackupKind kind, CancellationToken ct = default)
            => RunExclusiveAsync(() => CreateBackupCoreAsync(kind, ct));

        /// <summary>Validates an uploaded backup and stores it alongside the others. Does not restore it.</summary>
        public async Task<BackupResult> SaveUploadAsync(Stream upload, CancellationToken ct = default)
        {
            Directory.CreateDirectory(BackupDirectory);
            var name = NewFileName(BackupKind.Upload);
            var path = Path.Combine(BackupDirectory, name);
            var partial = path + ".partial";
            try
            {
                var magic = new byte[2];
                var read = await upload.ReadAtLeastAsync(magic, magic.Length, throwOnEndOfStream: false, ct);
                var gzipped = read == magic.Length && IsGzipMagic(magic);

                // Plain .sql (e.g. straight from pg_dump) is compressed on the way in.
                await using (Stream file = File.Create(partial))
                await using (var target = gzipped ? file : new GZipStream(file, CompressionLevel.Optimal))
                {
                    await target.WriteAsync(magic.AsMemory(0, read), ct);
                    await upload.CopyToAsync(target, ct);
                }

                var inspection = await InspectAsync(partial, ct);
                if (!inspection.IsValid) return BackupResult.Fail(inspection.Error!);

                File.Move(partial, path);
                return new BackupResult(true, FileName: name);
            }
            finally
            {
                File.Delete(partial);
            }
        }

        public bool DeleteBackup(string name)
        {
            var path = GetBackupPath(name);
            if (path == null) return false;
            File.Delete(path);
            return true;
        }

        /// <summary>Deletes all but the newest <paramref name="keep"/> automatic backups.</summary>
        public int PruneAutomaticBackups(int keep)
        {
            var stale = ListBackups().Where(b => b.Kind == BackupKind.Auto).Skip(Math.Max(keep, 0)).ToList();
            foreach (var backup in stale)
                File.Delete(Path.Combine(BackupDirectory, backup.Name));
            return stale.Count;
        }

        /// <summary>
        /// Replaces the whole database with the contents of a backup. The drop and the
        /// restore run in one transaction, so a failed restore leaves the database untouched.
        /// Pending migrations are applied afterwards, so backups from older versions work.
        /// </summary>
        /// <param name="safetyBackup">Back up the current database first (as a "pre-restore" backup).</param>
        public Task<BackupResult> RestoreAsync(string name, bool safetyBackup, CancellationToken ct = default)
            => RunExclusiveAsync(async () =>
            {
                var path = GetBackupPath(name);
                if (path == null) return BackupResult.Fail("Backup not found.");

                var inspection = await InspectAsync(path, ct);
                if (!inspection.IsValid) return BackupResult.Fail(inspection.Error!);

                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var unknown = inspection.Migrations.Except(db.Database.GetMigrations()).ToList();
                    if (unknown.Count > 0)
                        return BackupResult.Fail(
                            $"This backup was made by a newer version of GRID (unknown migrations: {string.Join(", ", unknown)}). Upgrade GRID first.");
                }

                if (safetyBackup)
                {
                    var safety = await CreateBackupCoreAsync(BackupKind.PreRestore, ct);
                    if (!safety.Success)
                        return BackupResult.Fail($"Could not back up the current database before restoring: {safety.Error}");
                }

                _isRestoring = true;
                try
                {
                    var error = await RunPsqlRestoreAsync(path, ct);
                    if (error != null) return BackupResult.Fail(error);

                    // Pooled connections may hold cached type/statement info for the dropped tables.
                    NpgsqlConnection.ClearAllPools();
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        await db.Database.MigrateAsync(ct);
                    }
                    _permissionService.InvalidateCache();
                    _logger.LogWarning("Database restored from backup {Backup}.", name);
                    return new BackupResult(true, FileName: name);
                }
                finally
                {
                    _isRestoring = false;
                }
            });

        /// <summary>
        /// Checks that a file (plain or gzipped) is a complete plain-SQL pg_dump of a GRID
        /// database and returns the EF migrations it contains.
        /// </summary>
        internal static async Task<BackupInspection> InspectAsync(string path, CancellationToken ct = default)
        {
            try
            {
                await using var dump = OpenDump(path);
                using var reader = new StreamReader(dump, Encoding.UTF8);
                var inspection = await InspectAsync(reader, ct);
                // StreamReader follows a UTF-16 byte order mark; psql would not.
                if (reader.CurrentEncoding is not UTF8Encoding)
                    return Invalid("The file is saved as UTF-16, which psql can't read. PowerShell does this when " +
                        "pg_dump's output is redirected with >; use pg_dump's --file option instead.");
                return inspection;
            }
            catch (InvalidDataException)
            {
                return Invalid("The file is damaged: it looks gzip-compressed but can't be decompressed.");
            }
        }

        internal static async Task<BackupInspection> InspectAsync(TextReader reader, CancellationToken ct = default)
        {
            var sawHeader = false;
            var sawTrailer = false;
            var migrations = new List<string>();
            var inMigrations = false;
            var lineNumber = 0;

            // Reads to the end: only a complete dump has the trailer.
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                lineNumber++;
                if (!sawHeader)
                {
                    if (line.StartsWith(DumpHeader, StringComparison.Ordinal)) sawHeader = true;
                    else if (line.StartsWith("PGDMP", StringComparison.Ordinal))
                        return Invalid("This is a pg_dump custom-format archive. GRID restores plain SQL dumps: run pg_dump without --format (or -F).");
                    else if (line.StartsWith(ClusterDumpHeader, StringComparison.Ordinal))
                        return Invalid("This is a pg_dumpall backup of the whole server. Back up just the GRID database with pg_dump.");
                    else if (lineNumber > 5) break;
                    continue;
                }

                if (inMigrations)
                {
                    if (line == "\\.") inMigrations = false;
                    else if (line.Split('\t')[0] is { Length: > 0 } id) migrations.Add(id);
                }
                else if (line.StartsWith(MigrationsCopyLine, StringComparison.Ordinal))
                {
                    inMigrations = true;
                }
                else if (line.StartsWith(MigrationsInsertLine, StringComparison.Ordinal) &&
                         MigrationInsertPattern().Match(line) is { Success: true } insert)
                {
                    migrations.Add(insert.Groups[1].Value);
                }
                else if (line.StartsWith(DumpTrailer, StringComparison.Ordinal))
                {
                    sawTrailer = true;
                }
            }

            if (!sawHeader)
                return Invalid("The file is not a PostgreSQL dump.");
            if (!sawTrailer)
                return Invalid("The dump is incomplete: it is missing pg_dump's closing \"dump complete\" line, so the file was probably cut short.");
            if (migrations.Count == 0)
                return Invalid("The dump does not contain a GRID database.");
            return new BackupInspection(true, null, migrations);
        }

        private static BackupInspection Invalid(string error) => new(false, error, []);

        private static bool IsGzipMagic(ReadOnlySpan<byte> bytes) => bytes is [0x1f, 0x8b, ..];

        /// <summary>Opens a backup for reading, decompressing it if it is gzipped.</summary>
        private static Stream OpenDump(string path)
        {
            var file = File.OpenRead(path);
            Span<byte> magic = stackalloc byte[2];
            var gzipped = file.ReadAtLeast(magic, magic.Length, throwOnEndOfStream: false) == magic.Length && IsGzipMagic(magic);
            file.Position = 0;
            return gzipped ? new GZipStream(file, CompressionMode.Decompress) : file;
        }

        internal static (BackupKind Kind, DateTime CreatedUtc)? ParseFileName(string name)
        {
            var match = FileNamePattern().Match(name);
            if (!match.Success) return null;
            if (!DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var created))
                return null;

            var kind = match.Groups[2].Value switch
            {
                "auto" => BackupKind.Auto,
                "pre-restore" => BackupKind.PreRestore,
                "upload" => BackupKind.Upload,
                _ => BackupKind.Manual,
            };
            return (kind, created);
        }

        private string NewFileName(BackupKind kind)
        {
            var suffix = kind switch
            {
                BackupKind.Auto => "auto",
                BackupKind.PreRestore => "pre-restore",
                BackupKind.Upload => "upload",
                _ => "manual",
            };
            // Names have one-second resolution; step forward past any that are taken.
            for (var time = DateTime.UtcNow; ; time = time.AddSeconds(1))
            {
                var name = $"grid-{time.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{suffix}.sql.gz";
                var path = Path.Combine(BackupDirectory, name);
                if (!File.Exists(path) && !File.Exists(path + ".partial")) return name;
            }
        }

        private async Task<BackupResult> RunExclusiveAsync(Func<Task<BackupResult>> action)
        {
            if (!await _lock.WaitAsync(TimeSpan.Zero))
                return BackupResult.Fail("Another backup or restore is already running. Try again in a moment.");
            try
            {
                return await action();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Backup operation failed.");
                return BackupResult.Fail(ex.Message);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<BackupResult> CreateBackupCoreAsync(BackupKind kind, CancellationToken ct)
        {
            Directory.CreateDirectory(BackupDirectory);
            var name = NewFileName(kind);
            var path = Path.Combine(BackupDirectory, name);
            var partial = path + ".partial";

            using var process = StartTool("pg_dump", ["--no-owner", "--no-privileges", "--format=plain"],
                redirectStdin: false);
            try
            {
                var stderr = process.StandardError.ReadToEndAsync(ct);

                await using (var file = File.Create(partial))
                await using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
                    await process.StandardOutput.BaseStream.CopyToAsync(gzip, ct);

                await process.WaitForExitAsync(ct);
                if (process.ExitCode != 0)
                    return BackupResult.Fail($"pg_dump failed: {(await stderr).Trim()}");

                File.Move(partial, path);
                _logger.LogInformation("Created database backup {Backup}.", name);
                return new BackupResult(true, FileName: name);
            }
            finally
            {
                if (!process.HasExited) process.Kill();
                File.Delete(partial);
            }
        }

        /// <summary>Returns null on success, otherwise psql's error output.</summary>
        private async Task<string?> RunPsqlRestoreAsync(string path, CancellationToken ct)
        {
            // --single-transaction wraps the schema drop and the whole dump: all or nothing.
            using var process = StartTool("psql",
                ["--no-psqlrc", "--quiet", "--set=ON_ERROR_STOP=1", "--single-transaction", "--file=-"],
                redirectStdin: true);
            try
            {
                var stdout = process.StandardOutput.ReadToEndAsync(ct);
                var stderr = process.StandardError.ReadToEndAsync(ct);

                try
                {
                    await using var stdin = new BufferedStream(process.StandardInput.BaseStream, 1 << 16);
                    // client_min_messages keeps the NOTICE listing every dropped table out of error messages.
                    await stdin.WriteAsync(
                        "SET client_min_messages = warning;\nDROP SCHEMA public CASCADE;\nCREATE SCHEMA public;\n"u8.ToArray(), ct);
                    await using var dump = OpenDump(path);
                    await CopyForRestoreAsync(dump, stdin, ct);
                }
                catch (IOException)
                {
                    // psql exited early (ON_ERROR_STOP) and closed its stdin; its stderr says why.
                }

                await process.WaitForExitAsync(ct);
                await stdout;
                return process.ExitCode == 0 ? null : $"Restore failed, database left unchanged: {(await stderr).Trim()}";
            }
            finally
            {
                // Killing psql mid-restore aborts its transaction, so nothing is half-applied.
                if (!process.HasExited) process.Kill();
            }
        }

        /// <summary>
        /// Copies a dump line by line, leaving out a UTF-8 byte order mark and the statements
        /// matched by <see cref="ServerSpecificStatementPattern"/>. Everything else, COPY data
        /// included, passes through byte for byte.
        /// </summary>
        internal static async Task CopyForRestoreAsync(Stream dump, Stream output, CancellationToken ct = default)
        {
            var reader = PipeReader.Create(dump);
            var inCopyData = false;
            var firstLine = true;
            try
            {
                while (true)
                {
                    var read = await reader.ReadAsync(ct);
                    var buffer = read.Buffer;
                    while (buffer.PositionOf((byte)'\n') is { } newline)
                    {
                        var line = buffer.Slice(0, buffer.GetPosition(1, newline));
                        await WriteLineAsync(line);
                        buffer = buffer.Slice(line.End);
                    }
                    if (read.IsCompleted)
                    {
                        if (!buffer.IsEmpty) await WriteLineAsync(buffer);
                        break;
                    }
                    reader.AdvanceTo(buffer.Start, buffer.End);
                }
            }
            finally
            {
                await reader.CompleteAsync();
            }

            async ValueTask WriteLineAsync(ReadOnlySequence<byte> sequence)
            {
                var line = sequence.IsSingleSegment ? sequence.First : sequence.ToArray();
                if (firstLine)
                {
                    firstLine = false;
                    if (line.Span.StartsWith("﻿"u8)) line = line[3..];
                }
                if (KeepForRestore(line.Span, ref inCopyData))
                    await output.WriteAsync(line, ct);
            }
        }

        private static bool KeepForRestore(ReadOnlySpan<byte> line, ref bool inCopyData)
        {
            var text = line.TrimEnd("\r\n"u8);
            if (inCopyData)
            {
                if (text.SequenceEqual("\\."u8)) inCopyData = false;
                return true;
            }
            if (text.StartsWith("COPY "u8) && text.EndsWith(" FROM stdin;"u8))
            {
                inCopyData = true;
                return true;
            }

            // Only lines that could match are decoded for the regex.
            if (!(text.StartsWith("ALTER "u8) || text.StartsWith("GRANT "u8) || text.StartsWith("REVOKE "u8) ||
                  text.StartsWith("CREATE DATABASE "u8) || text.StartsWith("\\connect "u8)))
                return true;
            return !ServerSpecificStatementPattern().IsMatch(Encoding.UTF8.GetString(text));
        }

        private Process StartTool(string fileName, IEnumerable<string> args, bool redirectStdin)
        {
            var conn = new NpgsqlConnectionStringBuilder(_connectionString);
            var psi = new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = redirectStdin,
                UseShellExecute = false,
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            // Credentials go through the environment so they never appear in the process list.
            psi.Environment["PGHOST"] = conn.Host;
            psi.Environment["PGPORT"] = conn.Port.ToString(CultureInfo.InvariantCulture);
            psi.Environment["PGDATABASE"] = conn.Database;
            psi.Environment["PGUSER"] = conn.Username;
            psi.Environment["PGPASSWORD"] = conn.Password;

            try
            {
                return Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {fileName}.");
            }
            catch (Win32Exception)
            {
                throw new InvalidOperationException(
                    $"{fileName} is not installed. Backups need the PostgreSQL client tools that ship in the GRID Docker image.");
            }
        }
    }
}
