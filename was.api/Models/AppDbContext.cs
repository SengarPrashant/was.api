using Microsoft.EntityFrameworkCore;
using was.api.Models.Dtos;
using was.api.Models.Dtos.Forms;

namespace was.api.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<DtoRoles> Roles { get; set; }
        public DbSet<DtoUser> Users { get; set; }
        public DbSet<DtoFormDef> FormDefinition { get; set; }
        public DbSet<DtoFormOptions> FormOptions { get; set; }
        public DbSet<DtoFormSections> FormSections { get; set; }
        public DbSet<DtoFormSubmissions> FormSubmissions { get; set; }
        public DbSet<DtoFormSubmissionResult> FormSubmissionResult { get; set; }
        public DbSet<DtoFormWorkFlow> FormWorkFlow { get; set; }
        public DbSet<DtoFormFields> FormFields { get; set; }
        public DbSet<DtoFormDocument> FormDocuments { get; set; }
        public DbSet<DtoFormValidation> FormValidations { get; set; }
        public DbSet<DtoSecurityMailConfig> SecurityMailConfigs { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DtoFormSubmissions>()
                .Property(d => d.FormData)
                .HasColumnType("jsonb"); // ✅ Important!
        }

    }
}
