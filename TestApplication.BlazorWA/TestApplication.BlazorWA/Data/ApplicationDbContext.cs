using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using SphericalCom.AspNetCore.Identity.CosmosDB;

namespace TestApplication.BlazorWA.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : CosmosIdentityDbContext<ApplicationUser>(options)
{
}