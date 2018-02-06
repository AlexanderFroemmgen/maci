using Backend.Data.Persistence.Model;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions options)
            : base(options)
        {
            Database.SetCommandTimeout(150000);
        }
        
        public DbSet<Experiment> Experiments { get; set; }
        public DbSet<ExperimentInstance> ExperimentInstances { get; set; }
        public DbSet<Parameter> Parameters { get; set; }
        public DbSet<ParameterValue> ParameterInstances { get; set; }
        public DbSet<ExperimentParameterAssignment> ExperimentParameterAssignments { get; set; }
        public DbSet<Record> Records { get; set; }
        public DbSet<LogMessage> LogMessages { get; set; }
        public DbSet<Worker> Workers { get; set; }
        public DbSet<ScalingGroup> ScalingGroups { get; set; }
        public DbSet<Configuration> Configuration { get; set; }
        public DbSet<GlobalEventLogMessage> GlobalEventLogMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Worker>()
                .HasOne(w => w.ActiveExperimentInstance)
                .WithOne(s => s.AssignedWorker)
                .IsRequired(false);

            modelBuilder.Entity<Worker>()
                .Property("CapabilitiesSerialized");

            modelBuilder.Entity<Experiment>()
                .Property("RequiredCapabilitiesSerialized");

            modelBuilder.Entity<Configuration>()
                .Property(c => c.MaxIdleTimeSec)
                .HasDefaultValue(60 * 60);
        }
    }
}