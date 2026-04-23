using Microsoft.EntityFrameworkCore;
using NguyenChauPhu_2121110104.Models;

namespace NguyenChauPhu_2121110104.Services
{
    public static class SeedData
    {
        public static async Task InitializeAsync(Data.AppDbContext db, bool resetUsersOnStartup = false)
        {
            if (!await db.Roles.AnyAsync())
            {
                db.Roles.AddRange(
                    new Role { RoleName = "Admin", Description = "Quan tri he thong", Priority = 1 },
                    new Role { RoleName = "Lecturer", Description = "Giang vien", Priority = 2 },
                    new Role { RoleName = "Student", Description = "Sinh vien", Priority = 3 });
                await db.SaveChangesAsync();
            }

            if (!await db.Permissions.AnyAsync())
            {
                db.Permissions.AddRange(
                    new Permission { PermissionCode = "users.manage", PermissionName = "Manage users", ModuleName = "Users" },
                    new Permission { PermissionCode = "courses.manage", PermissionName = "Manage courses", ModuleName = "Courses" },
                    new Permission { PermissionCode = "grades.input", PermissionName = "Input grades", ModuleName = "Grades" },
                    new Permission { PermissionCode = "grades.publish", PermissionName = "Publish grades", ModuleName = "Grades" },
                    new Permission { PermissionCode = "attendance.manage", PermissionName = "Manage attendance", ModuleName = "Attendance" },
                    new Permission { PermissionCode = "reports.export", PermissionName = "Export reports", ModuleName = "Reports" },
                    new Permission { PermissionCode = "schedules.manage", PermissionName = "Manage schedules", ModuleName = "Schedules" }
                );
                await db.SaveChangesAsync();
            }

            if (resetUsersOnStartup)
            {
                await ResetAndSeedUsersAsync(db);
            }

            var adminRole = await db.Roles.Include(r => r.RolePermissions).FirstAsync(r => r.RoleName == "Admin");
            var lecturerRole = await db.Roles.Include(r => r.RolePermissions).FirstAsync(r => r.RoleName == "Lecturer");
            if (!adminRole.RolePermissions.Any())
            {
                var permissionIds = await db.Permissions.Select(p => p.PermissionId).ToListAsync();
                db.RolePermissions.AddRange(permissionIds.Select(id => new RolePermission
                {
                    RoleId = adminRole.RoleId,
                    PermissionId = id,
                    GrantedAt = DateTime.UtcNow
                }));
                await db.SaveChangesAsync();
            }

            if (!lecturerRole.RolePermissions.Any())
            {
                var lecturerPermissionIds = await db.Permissions
                    .Where(p => p.PermissionCode == "grades.input" || p.PermissionCode == "grades.publish" || p.PermissionCode == "attendance.manage" || p.PermissionCode == "reports.export" || p.PermissionCode == "schedules.manage")
                    .Select(p => p.PermissionId)
                    .ToListAsync();
                db.RolePermissions.AddRange(lecturerPermissionIds.Select(id => new RolePermission
                {
                    RoleId = lecturerRole.RoleId,
                    PermissionId = id,
                    GrantedAt = DateTime.UtcNow
                }));
                await db.SaveChangesAsync();
            }

            await EnsureCoursesAsync(db);

            if (!await db.Enrollments.AnyAsync())
            {
                var studentIds = await db.Users.Where(x => x.StudentCode != null).OrderBy(x => x.StudentCode).Select(x => x.UserId).ToListAsync();
                var courseIds = await db.Courses.OrderBy(x => x.CourseCode).Select(x => x.CourseId).ToListAsync();
                foreach (var studentId in studentIds)
                {
                    foreach (var courseId in courseIds.Take(2))
                    {
                        db.Enrollments.Add(new Enrollment
                        {
                            StudentId = studentId,
                            CourseId = courseId,
                            Semester = "HK1-2026",
                            EnrollmentDate = DateTime.UtcNow,
                            Status = "Active"
                        });
                    }
                }
                await db.SaveChangesAsync();
            }

            if (!await db.ClassSchedules.AnyAsync())
            {
                var lecturerId = await db.Users.Where(x => x.LecturerCode == "GV001").Select(x => x.UserId).FirstAsync();
                var courses = await db.Courses.OrderBy(x => x.CourseCode).ToListAsync();
                db.ClassSchedules.AddRange(
                    new ClassSchedule { CourseId = courses[0].CourseId, LecturerId = lecturerId, Room = "A101", DayOfWeek = "Monday", StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(9, 0), StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(3)) },
                    new ClassSchedule { CourseId = courses[1].CourseId, LecturerId = lecturerId, Room = "B202", DayOfWeek = "Wednesday", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 0), StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(3)) });
                db.ExamSchedules.AddRange(
                    new ExamSchedule { CourseId = courses[0].CourseId, LecturerId = lecturerId, ExamDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(4)), StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(9, 0), Room = "Hall 1", ExamType = "Midterm" },
                    new ExamSchedule { CourseId = courses[1].CourseId, LecturerId = lecturerId, ExamDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(4).AddDays(2)), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 0), Room = "Hall 2", ExamType = "Final" });
                await db.SaveChangesAsync();
            }

            await SeedGradesForEnrollmentsAsync(db);
        }

        private static async Task EnsureCoursesAsync(Data.AppDbContext db)
        {
            var seedCourses = new[]
            {
                new Course { CourseCode = "IT001", CourseName = "Lập trình C#", Credits = 3, Department = "CNTT" },
                new Course { CourseCode = "IT002", CourseName = "Cơ sở dữ liệu", Credits = 3, Department = "CNTT" },
                new Course { CourseCode = "IT003", CourseName = "Phát triển Web", Credits = 4, Department = "CNTT" },
                new Course { CourseCode = "IT004", CourseName = "Kiến trúc máy tính", Credits = 3, Department = "CNTT" },
                new Course { CourseCode = "IT005", CourseName = "Hệ điều hành", Credits = 3, Department = "CNTT" },
                new Course { CourseCode = "IT006", CourseName = "Mạng máy tính", Credits = 3, Department = "CNTT" },
                new Course { CourseCode = "IT007", CourseName = "Phân tích thiết kế hệ thống", Credits = 3, Department = "CNTT" },
                new Course { CourseCode = "IT008", CourseName = "Trí tuệ nhân tạo cơ bản", Credits = 3, Department = "CNTT" }
            };

            var existingCourses = await db.Courses
                .ToListAsync();
            var existingMap = existingCourses.ToDictionary(x => x.CourseCode, StringComparer.OrdinalIgnoreCase);
            var hasChanges = false;

            foreach (var seed in seedCourses)
            {
                if (existingMap.TryGetValue(seed.CourseCode, out var existing))
                {
                    if (existing.CourseName != seed.CourseName || existing.Credits != seed.Credits || existing.Department != seed.Department)
                    {
                        existing.CourseName = seed.CourseName;
                        existing.Credits = seed.Credits;
                        existing.Department = seed.Department;
                        hasChanges = true;
                    }
                }
                else
                {
                    db.Courses.Add(seed);
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await db.SaveChangesAsync();
            }
        }

        private static async Task SeedGradesForEnrollmentsAsync(Data.AppDbContext db)
        {
            var enrollmentsWithoutGrades = await db.Enrollments
                .Include(x => x.Course)
                .Where(x => x.Grade == null)
                .ToListAsync();
            if (enrollmentsWithoutGrades.Count == 0)
            {
                return;
            }

            foreach (var enrollment in enrollmentsWithoutGrades)
            {
                var midterm = CreateScore(enrollment.EnrollmentId, 0);
                var final = CreateScore(enrollment.EnrollmentId, 1);
                var attendance = CreateScore(enrollment.EnrollmentId, 2);
                var total = midterm * 0.3 + final * 0.5 + attendance * 0.2;

                db.Grades.Add(new Grade
                {
                    EnrollmentId = enrollment.EnrollmentId,
                    MidtermScore = midterm,
                    FinalScore = final,
                    AttendanceScore = attendance,
                    TotalScore = total,
                    GpaContribution = total * enrollment.Course.Credits,
                    IsPublished = true
                });
            }

            await db.SaveChangesAsync();
        }

        private static double CreateScore(int enrollmentId, int salt)
        {
            var raw = 6.0 + ((enrollmentId * 17 + salt * 13) % 40) / 10.0;
            return Math.Round(raw, 1);
        }

        private static async Task ResetAndSeedUsersAsync(Data.AppDbContext db)
        {
            db.Grades.RemoveRange(db.Grades);
            db.AttendanceRecords.RemoveRange(db.AttendanceRecords);
            db.AttendanceSessions.RemoveRange(db.AttendanceSessions);
            db.Enrollments.RemoveRange(db.Enrollments);
            db.ClassSchedules.RemoveRange(db.ClassSchedules);
            db.ExamSchedules.RemoveRange(db.ExamSchedules);
            db.UserRoles.RemoveRange(db.UserRoles);
            db.Users.RemoveRange(db.Users);
            await db.SaveChangesAsync();

            var passwordHash = BCrypt.Net.BCrypt.HashPassword("123");
            var admins = Enumerable.Range(1, 3).Select(i => new User
            {
                Username = $"admin{i:000}",
                PasswordHash = passwordHash,
                FullName = $"Admin {i:00}",
                Email = $"admin{i:000}@local.dev",
                IsActive = true
            }).ToList();

            var lecturers = Enumerable.Range(1, 10).Select(i => new User
            {
                Username = $"gv{i:000}",
                PasswordHash = passwordHash,
                FullName = $"Giang vien {i:00}",
                Email = $"gv{i:000}@local.dev",
                LecturerCode = $"GV{i:000}",
                IsActive = true
            }).ToList();

            var students = Enumerable.Range(1, 50).Select(i => new User
            {
                Username = $"sv{i:000}",
                PasswordHash = passwordHash,
                FullName = $"Sinh vien {i:00}",
                Email = $"sv{i:000}@local.dev",
                StudentCode = $"SV{i:000}",
                IsActive = true
            }).ToList();

            var users = admins.Concat(lecturers).Concat(students).ToList();
            db.Users.AddRange(users);
            await db.SaveChangesAsync();

            var roleMap = await db.Roles.ToDictionaryAsync(r => r.RoleName, r => r.RoleId);

            db.UserRoles.AddRange(admins.Select(a => new UserRole
            {
                UserId = a.UserId,
                RoleId = roleMap["Admin"],
                AssignedAt = DateTime.UtcNow
            }));
            db.UserRoles.AddRange(lecturers.Select(gv => new UserRole
            {
                UserId = gv.UserId,
                RoleId = roleMap["Lecturer"],
                AssignedAt = DateTime.UtcNow
            }));
            db.UserRoles.AddRange(students.Select(sv => new UserRole
            {
                UserId = sv.UserId,
                RoleId = roleMap["Student"],
                AssignedAt = DateTime.UtcNow
            }));
            await db.SaveChangesAsync();
        }
    }
}
