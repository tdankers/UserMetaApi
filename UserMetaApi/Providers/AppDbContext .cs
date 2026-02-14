using BBT.EntityFrameworkCore;
using BbtEntities.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Runtime.Intrinsics.X86;

//in master
//CREATE LOGIN xxxxx 
//WITH PASSWORD = 'StrongPasswordHere!123';

//USE YourDatabase;
//CREATE USER xxxxx 
//FOR LOGIN xxxxx;

//--3️⃣ Remove any role memberships that grant extra rights
//EXEC sp_droprolemember 'db_datareader', 'xxxxx';
//EXEC sp_droprolemember 'db_datawriter', 'xxxxx';
//EXEC sp_droprolemember 'db_owner', 'xxxxx';
//-- public role cannot be removed; we handle it via DENY
//GO

//-- 4️⃣ DENY access to all tables in the database
//DECLARE @sql NVARCHAR(MAX) = N'';
//SELECT @sql += 'DENY SELECT, INSERT, UPDATE, DELETE ON [' + s.name + '].[' + t.name + '] TO xxxxx;'+CHAR(13)
//FROM sys.tables t
//JOIN sys.schemas s ON t.schema_id = s.schema_id
//WHERE t.name <> 'WebValidations'; --skip the allowed table
//EXEC sp_executesql @sql;
//GO

//-- 5️⃣ Grant INSERT on the specific table
//GRANT INSERT ON dbo.WebValidations TO xxxxx;
//GO

//-- 6️⃣ Explicitly DENY SELECT, UPDATE, DELETE on the allowed table
//DENY SELECT, UPDATE, DELETE ON dbo.WebValidations TO xxxxx;
//GO



namespace UserMetaApi.Providers
{
    public class AppDbContext : DbContextBBT
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            SqlConnectionStringBuilder sqlBuilder = [];

            sqlBuilder.UserID = Environment.GetEnvironmentVariable("SQL_USERNAME") ?? "";
            sqlBuilder.Password = Environment.GetEnvironmentVariable("SQL_PASSWORD") ?? "";
            sqlBuilder.InitialCatalog = Environment.GetEnvironmentVariable("SQL_CATALOG") ?? "";
            sqlBuilder.DataSource = Environment.GetEnvironmentVariable("SQL_SOURCE") ?? "";
            sqlBuilder.TrustServerCertificate = true;

            if (int.TryParse(Environment.GetEnvironmentVariable("SQL_TIMEOUT"), out int timeout))
            {
                sqlBuilder.ConnectTimeout = timeout;
            }

            var connection = sqlBuilder.ConnectionString;

            optionsBuilder
                .UseSqlServer(connection);        }
    }
}
