# SphericalCom.AspNetCore.Identity.CosmosDB
![NuGet Version](https://img.shields.io/nuget/v/SphericalCom.AspNetCore.Identity.CosmosDB?link=https%3A%2F%2Fwww.nuget.org%2Fpackages%2FSphericalCom.AspNetCore.Identity.CosmosDB)

A no-frills, close-to-spec implementation of the ASP.NET Core Identity database for CosmosDB using EntityFrameworkCore, as non-intrusive as possible and designed for ASP.NET 8.

Inspired by MoonriseSoftware's great [AspNetCore.Identity.CosmosDB](https://github.com/MoonriseSoftwareCalifornia/AspNetCore.Identity.CosmosDb), I decided to create another implementation of the AspNetCore Identity provider using CosmosDB through EntityFrameworkCode, while seeking to avoid the following:
* Exiting the conventions set by the current .NET 8 templates or CosmosDB guidance
* Including outdated/proprietary code (such as a library for DuendeSoftware's IdentityServer)
* Writing any SQL code, potentially introducing undesired behavior

And after thorough research, the Identity project left all the pieces in place to do this efficiently, by overriding the IdentityDbContext's model creation. Taking this approach minimized the work necessary to implement the library, and thus limited the possibilities for introducing behavior unexpected from the standard Identity model.

## Installation
For this we're assuming you have an existing ASP.NET core Identity application working with the default database provider (SqlServer). If this is not the case, please refer to the [documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-8.0), as their tutorials are better than anything I can come up with here.

### Nuget package
A nuget package can be found at [SphericalCom.AspNetCore.Identity.CosmosDB]([https://github.com](https://www.nuget.org/packages/SphericalCom.AspNetCore.Identity.CosmosDB/)). Installing the latest stable will allow for usage of the project. Please update all of your project dependencies, as the nuget package uses the latest stable versions of the packages it requires:
* Microsoft.AspNetCore.Identity.EntityFrameworkCore
* Microsoft.EntityFrameworkCore.Cosmos
* Microsoft.Extensions.Identity.Core

### DbContext
The default applications provide a DbContext class for you to modify: `ApplicationDbContext`. This, by default, inherits from `IdentityDbContext`. This is exemplified as follows:
```c#
//Data/ApplicationDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace YourApplication.Data;
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
}
```

We will instead inherit from our new `CosmosIdentityDbContext` to override the model creation with one compatible with CosmosDB.

```c#
//Data/ApplicationDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
// Ensure to add a reference to our package.
using SphericalCom.AspNetCore.Identity.CosmosDB;

namespace YourApplication.Data;
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : CosmosIdentityDbContext<ApplicationUser>(options)
{
}
```

### Database connection
We will now proceed to instruct EFCore to use the CosmosDB connector. If in doubt, refer to the [documentation](https://learn.microsoft.com/en-us/ef/core/providers/cosmos/?tabs=dotnet-core-cli) relevant to the connector. For development, we used the [CosmosDB Emulator for Windows](https://learn.microsoft.com/en-us/azure/cosmos-db/emulator), but feel free to use a real CosmosDB account.

By default, a DbContext is registered as follows for SqlServer:
```c#
//Program.cs
...
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
```

We will instead ask it to use CosmosDb, feel free to configure the connection as necessary. **Ensure your database name is not used within your storage account or you may face issues.** The database should be used exclusively for this application, and tables should not be messed with. Existing alongside other databases is OK.

Remember to get a ConnectionString from somewhere (likely your configuration/secrets storage) or the connection will not succeed. The default database name is OK, and it doesn't have to be stored in CosmosDb:Database, so you do you.
```c#
//Program.cs
...
var connectionString = builder.Configuration.GetConnectionString("CosmosEmulator") ?? throw new InvalidOperationException("Connection string for CosmosDB not found.");
var databaseName = builder.Configuration.GetValue<string>("CosmosDb:Database") ?? "AspNetCoreIdentity";
builder.Services.AddDbContext<ApplicationDbContext>(dbContextOptions =>
    dbContextOptions.UseCosmos(connectionString, databaseName));
...
```

### Initializing the database

Now, assuming this is your first time setting up the database for Identity, you're going to need to ensure it exists before we start using it. As Cosmos does not support migrations, the easiest way to do this is to insert the following block of code before your app starts running (but after it has been built):
```c#
//Program.cs
...
using (var scope = app.Services.CreateScope())
using (var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
    context.Database.EnsureCreated();

app.Run();
...
```
This will, in a nutshell, create a new scope to allow creating an instance of your DbContext, and then ask it to ensure the database is ready. This is done before app.Run() to ensure anything that needs the database will not get ahead of us.
While not recommended, you can instead create the database and containers manually, see the source to identify the required names.


## Support
I created this mainly as a personal need, so I will try and keep it mostly up to date. Depending on any future changes to Identity on .NET 9 major changes may be necessary (although it seems highly unlikely), and I may or may not wish to make said changes.
