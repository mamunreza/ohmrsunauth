using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OmsAuthApi.Data;

public class DataContext(DbContextOptions<DataContext> options) : IdentityDbContext(options)
{
}
