using FluentAssertions;
using GRID.Services;
using System.IO.Compression;
using System.Text;

namespace GRID.Tests.Services;

public class BackupServiceTests
{
    private const string ValidDump = """
        --
        -- PostgreSQL database dump
        --

        \restrict abc123

        SET statement_timeout = 0;

        COPY public."AspNetRoles" ("Id", "Name") FROM stdin;
        1	Admin
        \.

        COPY public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
        20251202175355_InitialCreate	10.0.1
        20251222194031_AddInvites	10.0.1
        \.

        -- PostgreSQL database dump complete
        """;

    // ── File names ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("grid-20261006-153012-manual.sql.gz", BackupKind.Manual)]
    [InlineData("grid-20261006-153012-auto.sql.gz", BackupKind.Auto)]
    [InlineData("grid-20261006-153012-pre-restore.sql.gz", BackupKind.PreRestore)]
    [InlineData("grid-20261006-153012-upload.sql.gz", BackupKind.Upload)]
    public void ParseFileName_AcceptsGeneratedNames(string name, BackupKind kind)
    {
        var parsed = BackupService.ParseFileName(name);

        parsed.Should().NotBeNull();
        parsed!.Value.Kind.Should().Be(kind);
        parsed.Value.CreatedUtc.Should().Be(new DateTime(2026, 10, 6, 15, 30, 12, DateTimeKind.Utc));
        parsed.Value.CreatedUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("../grid-20261006-153012-manual.sql.gz")]
    [InlineData("grid-20261006-153012-manual.sql.gz/../../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("grid-20261006-153012-other.sql.gz")]
    [InlineData("grid-20261306-153012-manual.sql.gz")]
    [InlineData("grid-20261006-153012-manual.sql.gz.partial")]
    [InlineData("")]
    public void ParseFileName_RejectsAnythingElse(string name)
    {
        BackupService.ParseFileName(name).Should().BeNull();
    }

    [Theory]
    [InlineData("grid-20261006-153012-manual.sql.gz")]
    [InlineData("prod.sql")]
    [InlineData("old install 2026.SQL.GZ")]
    public void IsBackupFileName_AcceptsDumpsInTheDirectory(string name)
    {
        BackupService.IsBackupFileName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("../prod.sql")]
    [InlineData("sub/prod.sql")]
    [InlineData("/etc/prod.sql")]
    [InlineData(".hidden.sql")]
    [InlineData("grid-20261006-153012-manual.sql.gz.partial")]
    [InlineData("notes.txt")]
    [InlineData("")]
    public void IsBackupFileName_RejectsPathsAndOtherFiles(string name)
    {
        BackupService.IsBackupFileName(name).Should().BeFalse();
    }

    // ── Inspection ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Inspect_ValidDump_ReturnsMigrations()
    {
        var result = await BackupService.InspectAsync(new StringReader(ValidDump));

        result.IsValid.Should().BeTrue();
        result.Migrations.Should().Equal("20251202175355_InitialCreate", "20251222194031_AddInvites");
    }

    [Fact]
    public async Task Inspect_NotADump_IsRejected()
    {
        var result = await BackupService.InspectAsync(new StringReader("hello\nworld\n"));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("not a PostgreSQL dump");
    }

    [Fact]
    public async Task Inspect_DumpOfAnotherDatabase_IsRejected()
    {
        var dump = "--\n-- PostgreSQL database dump\n--\nCREATE TABLE public.foo (id int);\n-- PostgreSQL database dump complete\n";

        var result = await BackupService.InspectAsync(new StringReader(dump));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("GRID");
    }

    [Fact]
    public async Task Inspect_TruncatedDump_IsRejected()
    {
        var truncated = ValidDump[..ValidDump.IndexOf("-- PostgreSQL database dump complete", StringComparison.Ordinal)];

        var result = await BackupService.InspectAsync(new StringReader(truncated));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("incomplete");
    }

    [Fact]
    public async Task Inspect_InsertStyleDump_ReturnsMigrations()
    {
        var dump = """
            --
            -- PostgreSQL database dump
            --
            INSERT INTO public."__EFMigrationsHistory" VALUES ('20251202175355_InitialCreate', '10.0.1');
            INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20251222194031_AddInvites', '10.0.1');
            -- PostgreSQL database dump complete
            """;

        var result = await BackupService.InspectAsync(new StringReader(dump));

        result.IsValid.Should().BeTrue();
        result.Migrations.Should().Equal("20251202175355_InitialCreate", "20251222194031_AddInvites");
    }

    [Theory]
    [InlineData("PGDMP\u0001\u000e\u0000", "custom-format")]
    [InlineData("--\n-- PostgreSQL database cluster dump\n--\n", "pg_dumpall")]
    public async Task Inspect_OtherDumpFormats_ExplainTheProblem(string start, string expected)
    {
        var result = await BackupService.InspectAsync(new StringReader(start + "\nmore\n"));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain(expected);
    }

    [Fact]
    public async Task Inspect_GzippedFile_IsRead()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using (var file = File.Create(path))
            await using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
                await gzip.WriteAsync(Encoding.UTF8.GetBytes(ValidDump));

            var result = await BackupService.InspectAsync(path);

            result.IsValid.Should().BeTrue();
            result.Migrations.Should().HaveCount(2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Inspect_UncompressedFile_IsRead()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, ValidDump);

            var result = await BackupService.InspectAsync(path);

            result.IsValid.Should().BeTrue();
            result.Migrations.Should().HaveCount(2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Inspect_Utf16File_IsRejected()
    {
        var path = Path.GetTempFileName();
        try
        {
            // What PowerShell 5's `pg_dump ... > file.sql` produces
            await File.WriteAllTextAsync(path, ValidDump, Encoding.Unicode);

            var result = await BackupService.InspectAsync(path);

            result.IsValid.Should().BeFalse();
            result.Error.Should().Contain("UTF-16");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Restore stream ────────────────────────────────────────────────────────

    [Fact]
    public async Task CopyForRestore_DropsServerSpecificStatements_KeepsEverythingElse()
    {
        var dump = "\uFEFF--\r\n" +
                   "CREATE DATABASE grid WITH TEMPLATE = template0 ENCODING = 'UTF8';\n" +
                   "ALTER DATABASE grid OWNER TO produser;\n" +
                   "\\connect grid\n" +
                   "CREATE TABLE public.\"Docs\" (\"Id\" integer NOT NULL, \"Body\" text);\n" +
                   "ALTER TABLE public.\"Docs\" OWNER TO produser;\n" +
                   "ALTER TABLE ONLY public.\"Docs\" ADD CONSTRAINT \"PK_Docs\" PRIMARY KEY (\"Id\");\n" +
                   "ALTER TABLE public.\"Docs\" ALTER COLUMN \"Id\" ADD GENERATED BY DEFAULT AS IDENTITY (\n" +
                   "    SEQUENCE NAME public.\"Docs_Id_seq\"\n" +
                   ");\n" +
                   "COPY public.\"Docs\" (\"Id\", \"Body\") FROM stdin;\r\n" +
                   "1\tGRANT ALL ON x TO y;\r\n" +
                   "\\.\r\n" +
                   "REVOKE ALL ON SCHEMA public FROM PUBLIC;\n" +
                   "GRANT ALL ON SCHEMA public TO PUBLIC;\n" +
                   "ALTER DEFAULT PRIVILEGES FOR ROLE produser IN SCHEMA public GRANT ALL ON TABLES TO reader;\n" +
                   "-- PostgreSQL database dump complete";
        var expected = "--\r\n" +
                       "CREATE TABLE public.\"Docs\" (\"Id\" integer NOT NULL, \"Body\" text);\n" +
                       "ALTER TABLE ONLY public.\"Docs\" ADD CONSTRAINT \"PK_Docs\" PRIMARY KEY (\"Id\");\n" +
                       "ALTER TABLE public.\"Docs\" ALTER COLUMN \"Id\" ADD GENERATED BY DEFAULT AS IDENTITY (\n" +
                       "    SEQUENCE NAME public.\"Docs_Id_seq\"\n" +
                       ");\n" +
                       "COPY public.\"Docs\" (\"Id\", \"Body\") FROM stdin;\r\n" +
                       "1\tGRANT ALL ON x TO y;\r\n" +
                       "\\.\r\n" +
                       "-- PostgreSQL database dump complete";

        using var output = new MemoryStream();
        await BackupService.CopyForRestoreAsync(new MemoryStream(Encoding.UTF8.GetBytes(dump)), output);

        Encoding.UTF8.GetString(output.ToArray()).Should().Be(expected);
    }

    [Fact]
    public async Task CopyForRestore_PassesLongLinesAcrossBufferBoundaries()
    {
        var longRow = "1\t" + new string('x', 200_000) + "é\n";
        var dump = "COPY public.\"Docs\" (\"Id\", \"Body\") FROM stdin;\n" + longRow + "\\.\n";

        using var output = new MemoryStream();
        await BackupService.CopyForRestoreAsync(new MemoryStream(Encoding.UTF8.GetBytes(dump)), output);

        Encoding.UTF8.GetString(output.ToArray()).Should().Be(dump);
    }

    // ── Setup code ────────────────────────────────────────────────────────────

    [Fact]
    public void SetupCode_AcceptsDisplayFormAndLowercase()
    {
        var setup = new SetupService();

        setup.IsValidCode(setup.DisplayCode).Should().BeTrue();
        setup.IsValidCode(setup.SetupCode.ToLowerInvariant()).Should().BeTrue();
        setup.IsValidCode(" " + setup.DisplayCode + " ").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AAAA-AAAA-AAAA")]
    public void SetupCode_RejectsWrongCodes(string? code)
    {
        new SetupService().IsValidCode(code).Should().BeFalse();
    }
}
