using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Models;

namespace NguyenChauPhu_2121110104.Data
{
    public class AppDbContext : DbContext
    {
        // Constructor bắt buộc phải có
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Grade> Grades { get; set; }
        public DbSet<AttendanceSession> AttendanceSessions { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<ClassSchedule> ClassSchedules { get; set; }
        public DbSet<ExamSchedule> ExamSchedules { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<Permission>().ToTable("Permissions");
            modelBuilder.Entity<UserRole>().ToTable("UserRoles");
            modelBuilder.Entity<RolePermission>().ToTable("RolePermissions");
            modelBuilder.Entity<Course>().ToTable("Courses");
            modelBuilder.Entity<Enrollment>().ToTable("Enrollment");
            modelBuilder.Entity<Grade>().ToTable("Grades");
            modelBuilder.Entity<AttendanceSession>().ToTable("AttendanceSession");
            modelBuilder.Entity<AttendanceRecord>().ToTable("AttendanceRecord");
            modelBuilder.Entity<ClassSchedule>().ToTable("ClassSchedules");
            modelBuilder.Entity<ExamSchedule>().ToTable("ExamSchedules");
            modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");

            modelBuilder.Entity<AttendanceSession>()
                .HasKey(x => x.SessionId);
            modelBuilder.Entity<AttendanceRecord>()
                .HasKey(x => x.RecordId);
            modelBuilder.Entity<ClassSchedule>()
                .HasKey(x => x.ClassScheduleId);
            modelBuilder.Entity<ExamSchedule>()
                .HasKey(x => x.ExamScheduleId);
            modelBuilder.Entity<AuditLog>()
                .HasKey(x => x.AuditLogId);

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Username).IsUnique();
            modelBuilder.Entity<User>()
                .HasIndex(x => x.Email).IsUnique();

            modelBuilder.Entity<Role>()
                .HasIndex(x => x.RoleName).IsUnique();

            modelBuilder.Entity<Permission>()
                .HasIndex(x => x.PermissionCode).IsUnique();

            modelBuilder.Entity<Course>()
                .HasIndex(x => x.CourseCode).IsUnique();

            modelBuilder.Entity<AttendanceSession>()
                .HasIndex(x => x.QRToken).IsUnique();

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Student)
                .WithMany(u => u.Enrollments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId);

            modelBuilder.Entity<Grade>()
                .HasOne(g => g.Enrollment)
                .WithOne(e => e.Grade)
                .HasForeignKey<Grade>(g => g.EnrollmentId);

            modelBuilder.Entity<AttendanceSession>()
                .HasOne(a => a.Course)
                .WithMany(c => c.AttendanceSessions)
                .HasForeignKey(a => a.CourseId);

            modelBuilder.Entity<AttendanceSession>()
                .HasOne(a => a.Lecturer)
                .WithMany(u => u.AttendanceSessionsCreated)
                .HasForeignKey(a => a.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(a => a.Session)
                .WithMany(s => s.Records)
                .HasForeignKey(a => a.SessionId);

            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(a => a.Student)
                .WithMany(u => u.AttendanceRecords)
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClassSchedule>()
                .HasOne(s => s.Course)
                .WithMany(c => c.ClassSchedules)
                .HasForeignKey(s => s.CourseId);

            modelBuilder.Entity<ClassSchedule>()
                .HasOne(s => s.Lecturer)
                .WithMany(u => u.ClassSchedules)
                .HasForeignKey(s => s.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExamSchedule>()
                .HasOne(s => s.Course)
                .WithMany(c => c.ExamSchedules)
                .HasForeignKey(s => s.CourseId);

            modelBuilder.Entity<ExamSchedule>()
                .HasOne(s => s.Lecturer)
                .WithMany(u => u.ExamSchedules)
                .HasForeignKey(s => s.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}