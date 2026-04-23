(() => {
  const token = () => localStorage.getItem('accessToken');
  if (!token()) {
    location.href = '/login.html';
    return;
  }
  let me = null;
  let roleSet = new Set();
  let permissionSet = new Set();
  let rbacRoles = [];
  let rbacPermissions = [];
  /** Chỉ Admin / Giảng viên được tạo tài khoản (POST). Sinh viên chỉ xem danh sách. */
  let canCreateUsers = false;
  let canAdminCrud = false;
  let usersAllRows = [];
  let usersPage = 1;
  let usersPageSize = 10;
  let enrollAllRows = [];
  /** Danh sách đăng ký thô từ API (dùng để nhóm theo môn + modal chi tiết). */
  let enrollSourceRows = [];
  let enrollModalCourseId = 0;
  let enrollModalSemester = '';
  let enrollPage = 1;
  let enrollPageSize = 10;
  let gradesAllRows = [];
  let gradesPage = 1;
  let gradesPageSize = 10;
  let availableStudentCourses = [];
  let availableStudents = [];
  let studentScoreChart = null;
  const LAST_SECTION_KEY = 'lastActiveSection';
  const roleLabelMap = { Admin: 'Quản trị viên', Lecturer: 'Giảng viên', Student: 'Sinh viên' };
  const moduleLabelMap = { Attendance: 'Điểm danh', Courses: 'Môn học', Grades: 'Điểm', Reports: 'Báo cáo', Schedules: 'Lịch', Users: 'Người dùng' };
  const permissionLabelMap = {
    'attendance.manage': 'Quản lý điểm danh',
    'courses.manage': 'Quản lý môn học',
    'grades.input': 'Nhập điểm',
    'grades.publish': 'Công bố điểm',
    'reports.export': 'Xuất báo cáo',
    'schedules.manage': 'Quản lý lịch học/lịch thi',
    'users.manage': 'Quản lý người dùng',
  };
  const attendanceInfoTitle = document.getElementById('attendanceInfoTitle');
  const attendanceInfoBody = document.getElementById('attendanceInfoBody');
  const attendanceInfoModalEl = document.getElementById('attendanceInfoModal');
  const attendanceInfoModal = attendanceInfoModalEl && window.bootstrap ? new window.bootstrap.Modal(attendanceInfoModalEl) : null;
  const enrollGroupModalEl = document.getElementById('enrollGroupModal');
  const enrollGroupModal = enrollGroupModalEl && window.bootstrap ? new window.bootstrap.Modal(enrollGroupModalEl) : null;

  function toDisplayDate(value) {
    if (!value) return 'Không có';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return value;
    return d.toLocaleString('vi-VN');
  }

  function showInfoModal(title, rows) {
    if (!attendanceInfoBody || !attendanceInfoModal) return;
    if (attendanceInfoTitle) attendanceInfoTitle.textContent = title || 'Thông tin chi tiết';
    attendanceInfoBody.innerHTML = rows
      .map(([label, value]) => `<tr><th class="text-nowrap" style="width: 140px;">${label}</th><td class="text-break">${value || 'Không có'}</td></tr>`)
      .join('');
    attendanceInfoModal.show();
  }

  function showAttendanceInfo(session) {
    showInfoModal('Chi tiết buổi điểm danh', [
      ['Môn học', session.course || 'Không có'],
      ['Ngày học', toDisplayDate(session.sessionDate)],
      ['Hết hạn QR', toDisplayDate(session.tokenExpiry)],
      ['Mã điểm danh', session.qrToken || session.qRToken || 'Không có'],
    ]);
  }

  function showCourseInfo(course) {
    showInfoModal('Chi tiết môn học', [
      ['Mã môn', course.courseCode],
      ['Tên môn', course.courseName],
      ['Số tín chỉ', String(course.credits ?? '')],
      ['Khoa', course.department],
    ]);
  }

  function showEnrollmentInfo(enrollment) {
    const rows = [];
    if (seesEnrollmentStaffTable()) {
      rows.push(['Mã SV', enrollment.student?.studentCode]);
      rows.push(['Họ tên', enrollment.student?.fullName]);
    }
    rows.push(
      ['Môn học', enrollment.course?.courseName || enrollment.course?.courseCode],
      ['Học kỳ', enrollment.semester],
      ['Trạng thái', enrollment.status === 'Active' ? 'Đang học' : enrollment.status]
    );
    showInfoModal('Chi tiết đăng ký', rows);
  }

  function showUserInfo(user) {
    const isStudentViewOnly = hasRole('Student') && !hasRole('Admin') && !hasRole('Lecturer');
    const commonRows = [
      ['Họ tên', user.fullName],
      ['Vai trò', Array.isArray(user.roles) ? user.roles.join(', ') : user.roles],
      ['Trạng thái', user.isActive ? 'Hoạt động' : 'Khóa'],
    ];
    if (isStudentViewOnly) {
      showInfoModal('Chi tiết người dùng', commonRows);
      return;
    }
    showInfoModal('Chi tiết người dùng', [
      ['Tài khoản', user.username],
      ...commonRows,
      ['Email', user.email],
      ['Mã sinh viên', user.studentCode],
    ]);
  }
  async function api(path, opts = {}) {
    const headers = { ...(opts.headers || {}) };
    if (!(opts.body instanceof FormData) && !headers['Content-Type'] && opts.method && opts.method !== 'GET') headers['Content-Type'] = 'application/json';
    if (token()) headers['Authorization'] = 'Bearer ' + token();
    const res = await fetch(path, { ...opts, headers });
    if (res.status === 401) {
      localStorage.clear();
      location.href = '/login.html';
      throw new Error('Unauthorized');
    }
    if (res.status === 403) {
      const denied = await res.text();
      throw new Error(denied || 'Bạn không có quyền thực hiện thao tác này.');
    }
    const ct = res.headers.get('content-type') || '';
    if (ct.includes('application/json')) {
      const data = await res.json();
      if (!res.ok) throw new Error(typeof data === 'string' ? data : JSON.stringify(data));
      return data;
    }
    const text = await res.text();
    if (!res.ok) {
      if (res.status >= 500) {
        const hint = text && text.length > 0 && text.length < 500 ? text.replace(/\s+/g, ' ').trim() : '';
        throw new Error(hint || 'Lỗi máy chủ (500). Xem log API hoặc kiểm tra dữ liệu (CourseId, LecturerId, ngày).');
      }
      throw new Error((text && text.length < 300) ? text : (res.statusText || 'Yêu cầu không hợp lệ.'));
    }
    return text;
  }
  /** Cắt 2 chữ số thập phân (không làm tròn lên), ví dụ 0.756456544 → 0.75. */
  const truncScore2 = (v) => {
    if (v === null || v === undefined || v === '') return null;
    const n = Number(v);
    if (!Number.isFinite(n)) return null;
    return Math.trunc(n * 100) / 100;
  };
  const formatTruncScore2 = (v) => {
    const t = truncScore2(v);
    if (t === null) return '';
    return t.toFixed(2);
  };
  const hasRole = (r) => roleSet.has(String(r || '').trim().toLowerCase());
  const hasPermission = (p) => permissionSet.has(String(p || '').trim().toLowerCase());
  const isStudentOnly = () => hasRole('Student') && !hasRole('Admin') && !hasRole('Lecturer');
  /** Bảng đăng ký nhóm theo môn + học kỳ (xem danh sách SV theo lớp) — Admin & GV giống nhau. */
  const seesEnrollmentStaffTable = () => hasRole('Admin') || hasRole('Lecturer');
  const showSection = (id) => {
    document.querySelectorAll('[data-section]').forEach((el) => el.classList.toggle('d-none', el.getAttribute('data-section') !== id));
    document.querySelectorAll('[data-nav]').forEach((a) => a.classList.toggle('active', a.getAttribute('data-nav') === id));
  };
  const canOpenSection = (id) => {
    const nav = document.querySelector(`[data-nav="${id}"]`);
    if (!nav) return false;
    const item = nav.closest('.nav-item');
    return !item || !item.classList.contains('d-none');
  };
  const refreshSectionData = async (id) => {
    if (id === 'dashboard') await refreshDashboard();
    if (id === 'courses') await refreshCourses();
    if (id === 'enrollments') await refreshEnrollments();
    if (id === 'users') await refreshUsers();
    if (id === 'permissions') await refreshPermissions();
    if (id === 'attendance') await refreshAttendanceSessions();
  };
  const openSection = async (id, persist = true) => {
    const target = canOpenSection(id) ? id : 'dashboard';
    showSection(target);
    if (persist) localStorage.setItem(LAST_SECTION_KEY, target);
    await refreshSectionData(target);
  };
  const toggleCreatePanel = (base, showCreate) => {
    document.getElementById(`${base}ListPanel`)?.classList.toggle('d-none', showCreate);
    document.getElementById(`${base}CreatePanel`)?.classList.toggle('d-none', !showCreate);
  };
  const iconEye = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 16 16" fill="currentColor"><path d="M16 8s-3-5.5-8-5.5S0 8 0 8s3 5.5 8 5.5S16 8 16 8z"/><path d="M8 5a3 3 0 1 0 0 6 3 3 0 0 0 0-6z"/></svg>';
  const iconEdit = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 16 16" fill="currentColor"><path d="M12.146.146a.5.5 0 0 1 .708 0l3 3a.5.5 0 0 1 0 .708l-10 10L3 14l.146-2.854z"/><path fill-rule="evenodd" d="M1 13.5V16h2.5l7.373-7.373-2.5-2.5z"/></svg>';
  const iconDelete = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 16 16" fill="currentColor"><path d="M5.5 5.5A.5.5 0 0 1 6 6v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5"/><path d="M8.5 5.5A.5.5 0 0 1 9 6v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5"/><path d="M11.5 5.5A.5.5 0 0 1 12 6v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5"/><path fill-rule="evenodd" d="M14.5 3a1 1 0 0 1-1 1H13v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4h-.5a1 1 0 0 1 0-2H5V1a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v1h2.5a1 1 0 0 1 1 1"/></svg>';
  const actions = (id, canEdit, canDelete) =>
    `<div class="d-inline-flex gap-1 flex-nowrap"><button type="button" class="btn btn-sm btn-outline-info act-view" data-id="${id}" title="Xem">${iconEye}</button>${canEdit ? `<button type="button" class="btn btn-sm btn-outline-primary act-edit" data-id="${id}" title="Sửa">${iconEdit}</button>` : ''}${canDelete ? `<button type="button" class="btn btn-sm btn-outline-danger act-del" data-id="${id}" title="Xóa">${iconDelete}</button>` : ''}</div>`;
  const bindActions = (tableId, handlers) => {
    document.querySelectorAll(`#${tableId} .act-view`).forEach((x) => x.onclick = () => handlers.view(+x.dataset.id));
    document.querySelectorAll(`#${tableId} .act-edit`).forEach((x) => x.onclick = () => handlers.edit(+x.dataset.id));
    document.querySelectorAll(`#${tableId} .act-del`).forEach((x) => x.onclick = () => handlers.del(+x.dataset.id));
  };
  async function loadMe() {
    me = await api('/api/auth/me');
    roleSet = new Set((me.roles || []).map((x) => String(x || '').trim().toLowerCase()));
    permissionSet = new Set((me.permissions || []).map((x) => String(x || '').trim().toLowerCase()));
    canCreateUsers = hasRole('Admin') || hasRole('Lecturer');
    canAdminCrud = hasRole('Admin') || hasPermission('users.manage');
    const roleDisplay = (me.roles || []).map((r) => `${r}${roleLabelMap[r] ? ` (${roleLabelMap[r]})` : ''}`);
    document.getElementById('hdrUser').textContent = `Xin chào, ${me.fullName || me.username}`;
    document.getElementById('hdrRoles').textContent = `Vai trò: ${roleDisplay.join(', ')}`;
    const navEnrollText = document.getElementById('navEnrollText');
    if (navEnrollText) {
      navEnrollText.textContent = seesEnrollmentStaffTable() ? 'Quản lý đăng ký môn' : 'Đăng ký môn';
    }
    document.querySelectorAll('[data-roles]').forEach((el) => {
      const roles = (el.getAttribute('data-roles') || '').split(',').map((s) => s.trim()).filter(Boolean);
      const ok = !roles.length || roles.some((r) => hasRole(r));
      el.classList.toggle('d-none', !ok);
    });
  }
  async function refreshDashboard() {
    const box = document.getElementById('dashContent');
    if (hasRole('Admin') || hasRole('Lecturer')) {
      const d = await api('/api/statistics/dashboard');
      const userLabel = hasRole('Admin') ? 'Người dùng toàn hệ thống' : 'Sinh viên có trong hệ thống';
      box.innerHTML = `
        <div class="row g-3">
          <div class="col-md-4">
            <div class="card admin-kpi-card kpi-primary h-100">
              <div class="card-body d-flex justify-content-between align-items-center">
                <div>
                  <div class="admin-kpi-label">${userLabel}</div>
                  <div class="admin-kpi-value">${d.totalUsers}</div>
                </div>
                <i class="bi bi-people-fill admin-kpi-icon"></i>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card admin-kpi-card kpi-success h-100">
              <div class="card-body d-flex justify-content-between align-items-center">
                <div>
                  <div class="admin-kpi-label">Môn học đang quản lý</div>
                  <div class="admin-kpi-value">${d.totalCourses}</div>
                </div>
                <i class="bi bi-journal-bookmark-fill admin-kpi-icon"></i>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card admin-kpi-card kpi-warning h-100">
              <div class="card-body d-flex justify-content-between align-items-center">
                <div>
                  <div class="admin-kpi-label">Lượt đăng ký học phần</div>
                  <div class="admin-kpi-value">${d.totalEnrollments}</div>
                </div>
                <i class="bi bi-card-checklist admin-kpi-icon"></i>
              </div>
            </div>
          </div>
        </div>
        <div class="alert alert-light border mt-3 mb-0 small">
          Bảng tổng quan được cập nhật theo dữ liệu hiện tại của hệ thống.
        </div>
      `;
    } else {
      const stats = await api('/api/statistics/students/' + me.userId);
      const chartData = Array.isArray(stats.chartData) ? stats.chartData : [];
      const courseCount = chartData.length;
      const scores = chartData.map((x) => {
        const t = truncScore2(x.totalScore);
        return t === null ? 0 : t;
      });
      const avgScore = scores.length ? scores.reduce((a, b) => a + b, 0) / scores.length : 0;
      const maxScore = scores.length ? Math.max(...scores) : 0;
      const passedCount = scores.filter((x) => x >= 5).length;

      box.innerHTML = `
        <div class="row g-3 mb-3">
          <div class="col-md-3">
            <div class="card student-kpi shadow-sm" style="background: linear-gradient(135deg,#4e73df,#224abe);">
              <div class="card-body">
                <div class="kpi-label">GPA hiện tại</div>
                <div class="kpi-value">${formatTruncScore2(stats.gpa) || '0.00'}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card student-kpi shadow-sm" style="background: linear-gradient(135deg,#1cc88a,#13855c);">
              <div class="card-body">
                <div class="kpi-label">Môn đang học</div>
                <div class="kpi-value">${courseCount}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card student-kpi shadow-sm" style="background: linear-gradient(135deg,#f6c23e,#dda20a);">
              <div class="card-body">
                <div class="kpi-label">Điểm trung bình</div>
                <div class="kpi-value">${formatTruncScore2(avgScore) || '0.00'}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card student-kpi shadow-sm" style="background: linear-gradient(135deg,#e74a3b,#be2617);">
              <div class="card-body">
                <div class="kpi-label">Môn đạt (>= 5)</div>
                <div class="kpi-value">${passedCount}/${courseCount}</div>
              </div>
            </div>
          </div>
        </div>
        <div class="row g-3">
          <div class="col-lg-8">
            <div class="card border-0 shadow-sm">
              <div class="card-header bg-white">Biểu đồ điểm theo môn học</div>
              <div class="card-body">
                <canvas id="studentScoreChart" height="130"></canvas>
              </div>
            </div>
          </div>
          <div class="col-lg-4">
            <div class="card border-0 shadow-sm h-100">
              <div class="card-header bg-white">Tổng hợp nhanh</div>
              <div class="card-body">
                <p class="mb-2"><strong>Điểm cao nhất:</strong> ${formatTruncScore2(maxScore) || '0.00'}</p>
                <p class="mb-2"><strong>Điểm thấp nhất:</strong> ${formatTruncScore2(scores.length ? Math.min(...scores) : 0) || '0.00'}</p>
                <p class="mb-2"><strong>Xếp loại tạm tính:</strong> ${stats.gpa >= 8 ? 'Giỏi' : stats.gpa >= 6.5 ? 'Khá' : stats.gpa >= 5 ? 'Trung bình' : 'Cần cải thiện'}</p>
                <div class="mt-3 small text-muted">Dữ liệu dựa trên các môn bạn đã được nhập điểm trong hệ thống.</div>
              </div>
            </div>
          </div>
        </div>
      `;

      const canvas = document.getElementById('studentScoreChart');
      if (canvas && window.Chart) {
        if (studentScoreChart) {
          studentScoreChart.destroy();
        }

        studentScoreChart = new Chart(canvas, {
          type: 'bar',
          data: {
            labels: chartData.map((x) => x.courseName || x.courseCode || ''),
            datasets: [
              {
                label: 'Điểm tổng kết',
                data: scores,
                backgroundColor: 'rgba(54, 162, 235, 0.65)',
                borderColor: 'rgba(54, 162, 235, 1)',
                borderWidth: 1.5,
                borderRadius: 8,
              },
            ],
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
              x: {
                ticks: {
                  maxRotation: 40,
                  minRotation: 0,
                  autoSkip: false,
                },
              },
              y: {
                beginAtZero: true,
                suggestedMax: 10,
                ticks: { stepSize: 1 },
              },
            },
            plugins: {
              legend: { display: true },
              tooltip: {
                callbacks: {
                  title: (items) => {
                    const idx = items[0]?.dataIndex ?? 0;
                    const row = chartData[idx];
                    if (!row) return '';
                    const code = row.courseCode || '';
                    const name = row.courseName || '';
                    return name && code ? `${code} — ${name}` : name || code;
                  },
                  label: (ctx) => `Điểm tổng kết: ${formatTruncScore2(ctx.parsed?.y)}`,
                },
              },
            },
          },
        });
      }
    }
  }
  async function refreshCourses() {
    const rows = await api('/api/courses');
    const tb = document.querySelector('#tblCourses tbody');
    tb.innerHTML = rows.map((c) => `<tr><td>${c.courseCode}</td><td>${c.courseName}</td><td>${c.credits}</td><td>${c.department ?? ''}</td><td class="action-cell">${actions(c.courseId, canAdminCrud, canAdminCrud)}</td></tr>`).join('');
    bindActions('tblCourses', {
      view: async (id) => showCourseInfo(await api('/api/courses/' + id)),
      edit: async (id) => { if (!canAdminCrud) return; const c = await api('/api/courses/' + id); const courseName = prompt('Tên môn', c.courseName); if (!courseName) return; await api('/api/courses/' + id, { method: 'PUT', body: JSON.stringify({ ...c, courseName }) }); refreshCourses(); },
      del: async (id) => { if (!canAdminCrud || !confirm('Xóa môn?')) return; await api('/api/courses/' + id, { method: 'DELETE' }); refreshCourses(); },
    });
  }
  function buildEnrollCourseGroups(rows) {
    const map = new Map();
    for (const e of rows || []) {
      const cid = e?.course?.courseId ?? e?.courseId;
      if (cid == null) continue;
      const sem = String(e?.semester ?? '');
      const key = `${cid}::${sem}`;
      if (!map.has(key)) {
        map.set(key, {
          courseId: cid,
          semester: sem,
          courseCode: e?.course?.courseCode ?? '',
          courseName: e?.course?.courseName ?? '',
          credits: e?.course?.credits ?? '',
          department: e?.course?.department ?? '',
          enrollments: [],
        });
      }
      map.get(key).enrollments.push(e);
    }
    return Array.from(map.values()).sort((a, b) => {
      const c = String(a.courseCode || '').localeCompare(String(b.courseCode || ''));
      if (c !== 0) return c;
      return String(a.semester || '').localeCompare(String(b.semester || ''));
    });
  }

  function showEnrollmentGroupModal(courseId, semester) {
    if (!enrollGroupModal) return;
    enrollModalCourseId = courseId;
    enrollModalSemester = semester;
    const titleEl = document.getElementById('enrollGroupTitle');
    const tb = document.getElementById('enrollGroupDetailBody');
    if (!tb || !titleEl) return;
    const list = (enrollSourceRows || []).filter(
      (e) => (e?.course?.courseId ?? e?.courseId) === courseId && String(e?.semester ?? '') === String(semester ?? '')
    );
    const first = list[0];
    const cc = first?.course?.courseCode ?? '';
    const cn = first?.course?.courseName ?? '';
    titleEl.textContent = `Sinh viên đăng ký: ${cc} — ${cn} (${semester})`;
    const canEnrollCrud = hasRole('Admin');
    const sorted = [...list].sort((a, b) => String(a.student?.studentCode ?? '').localeCompare(String(b.student?.studentCode ?? '')));
    tb.innerHTML = sorted
      .map(
        (e) =>
          `<tr><td>${e.enrollmentId}</td><td>${e.student?.studentCode ?? ''}</td><td>${e.student?.fullName ?? ''}</td><td>${e.status === 'Active' ? 'Đang học' : e.status}</td><td class="action-cell">${actions(e.enrollmentId, canEnrollCrud, canEnrollCrud)}</td></tr>`
      )
      .join('');
    bindActions('tblEnrollGroupDetail', {
      view: async (id) => showEnrollmentInfo(await api('/api/enrollments/' + id)),
      edit: async (id) => {
        if (!canEnrollCrud) return;
        const e = await api('/api/enrollments/' + id);
        const semesterNew = prompt('Học kỳ', e.semester) || e.semester;
        const body = { studentId: e.studentId, courseId: e.courseId, semester: semesterNew, status: e.status || 'Active' };
        await api('/api/enrollments/' + id, { method: 'PUT', body: JSON.stringify(body) });
        await refreshEnrollments();
        showEnrollmentGroupModal(enrollModalCourseId, enrollModalSemester);
      },
      del: async (id) => {
        if (!canEnrollCrud || !confirm('Xóa đăng ký?')) return;
        await api('/api/enrollments/' + id, { method: 'DELETE' });
        await refreshEnrollments();
        const rest = (enrollSourceRows || []).filter(
          (x) => (x?.course?.courseId ?? x?.courseId) === enrollModalCourseId && String(x?.semester ?? '') === String(enrollModalSemester ?? '')
        );
        if (rest.length === 0) enrollGroupModal.hide();
        else showEnrollmentGroupModal(enrollModalCourseId, enrollModalSemester);
      },
    });
    enrollGroupModal.show();
  }

  async function refreshEnrollments() {
    const rows = await api('/api/enrollments');
    enrollSourceRows = rows || [];
    if (seesEnrollmentStaffTable()) {
      enrollAllRows = buildEnrollCourseGroups(enrollSourceRows);
    } else {
      const myStudentId = me?.userId;
      enrollAllRows = enrollSourceRows.filter((e) => extractStudentIdFromEnrollment(e) === myStudentId);
    }
    updateEnrollmentTableLabels();
    renderEnrollmentsPage();
  }

  function updateEnrollmentTableLabels() {
    const studentView = !seesEnrollmentStaffTable();
    const setText = (id, text) => {
      const el = document.getElementById(id);
      if (el) el.textContent = text;
    };
    setText('enrollListTitle', studentView ? 'Môn/lớp đã đăng ký' : 'Quản lý đăng ký');
    if (studentView) {
      setText('enrollThId', 'Mã ĐK');
      setText('enrollThStudentCode', 'Mã môn');
      setText('enrollThStudentName', 'Tên môn');
      setText('enrollThCourse', 'Tín chỉ');
      setText('enrollThSemester', 'Học kỳ');
      setText('enrollThStatus', 'Trạng thái');
    } else {
      setText('enrollThId', 'Mã môn');
      setText('enrollThStudentCode', 'Tên môn');
      setText('enrollThStudentName', 'TC');
      setText('enrollThCourse', 'Khoa');
      setText('enrollThSemester', 'Học kỳ');
      setText('enrollThStatus', 'Số SV');
    }
    setText('enrollThAction', 'Thao tác');
    const suf = document.getElementById('enrollPageSizeSuffix');
    if (suf) suf.textContent = studentView ? 'đăng ký' : 'môn (HK)';
  }

  function renderEnrollmentsPage() {
    const tb = document.querySelector('#tblEnroll tbody');
    const total = enrollAllRows.length;
    const totalPages = Math.max(1, Math.ceil(total / enrollPageSize));
    if (enrollPage > totalPages) enrollPage = totalPages;
    const start = (enrollPage - 1) * enrollPageSize;
    const pageRows = enrollAllRows.slice(start, start + enrollPageSize);

    if (!seesEnrollmentStaffTable()) {
      tb.innerHTML = pageRows
        .map((e) => `<tr><td>${e.enrollmentId}</td><td>${e.course?.courseCode ?? ''}</td><td>${e.course?.courseName ?? ''}</td><td>${e.course?.credits ?? ''}</td><td>${e.semester}</td><td>${e.status === 'Active' ? 'Đang học' : e.status}</td><td class="action-cell">${actions(e.enrollmentId, false, false)}</td></tr>`)
        .join('');
      bindActions('tblEnroll', {
        view: async (id) => showEnrollmentInfo(await api('/api/enrollments/' + id)),
        edit: async () => {},
        del: async () => {},
      });
    } else {
      tb.innerHTML = pageRows
        .map(
          (g) =>
            `<tr><td>${g.courseCode}</td><td>${g.courseName}</td><td>${g.credits ?? ''}</td><td>${g.department ?? ''}</td><td>${g.semester}</td><td>${g.enrollments.length}</td><td class="action-cell"><button type="button" class="btn btn-sm btn-outline-info act-enroll-grp" data-cid="${g.courseId}" data-sem="${encodeURIComponent(g.semester)}" title="Xem danh sách SV">${iconEye}</button></td></tr>`
        )
        .join('');
      document.querySelectorAll('#tblEnroll .act-enroll-grp').forEach((btn) => {
        btn.onclick = () => showEnrollmentGroupModal(+btn.dataset.cid, decodeURIComponent(btn.dataset.sem || ''));
      });
    }
    document.getElementById('enrollPageInfo').textContent = `Trang ${enrollPage}/${totalPages} - Tổng: ${total}`;
    document.getElementById('btnEnrollPrev').disabled = enrollPage <= 1;
    document.getElementById('btnEnrollNext').disabled = enrollPage >= totalPages;
  }

  function extractStudentIdFromEnrollment(enrollment) {
    return enrollment?.student?.userId ?? enrollment?.studentId ?? null;
  }

  function applyEnrollStaffMode(isOpenClass) {
    const st = document.getElementById('enStuSelectWrap');
    const sem = document.getElementById('enSemRow');
    const cs = document.getElementById('enCourseSelectWrap');
    const createBtn = document.getElementById('btnCreateEnr');
    const openBtn = document.getElementById('btnOpenClass');
    const lec = document.getElementById('enLecturerSelectWrap');
    if (!createBtn || !openBtn) return;
    if (isOpenClass) {
      st?.classList.add('d-none');
      sem?.classList.add('d-none');
      cs?.classList.remove('d-none');
      createBtn.classList.add('d-none');
      openBtn.classList.remove('d-none');
      if (hasRole('Admin')) lec?.classList.remove('d-none');
      else lec?.classList.add('d-none');
    } else {
      st?.classList.remove('d-none');
      sem?.classList.remove('d-none');
      cs?.classList.remove('d-none');
      createBtn.classList.remove('d-none');
      openBtn.classList.add('d-none');
      lec?.classList.add('d-none');
    }
  }

  async function setupEnrollmentCreateUi() {
    const title = document.getElementById('enrollCreateTitle');
    const note = document.getElementById('enrollPermNote');
    const studentWrap = document.getElementById('enStuWrap');
    const studentSelectWrap = document.getElementById('enStuSelectWrap');
    const studentSelect = document.getElementById('enStuSelect');
    const studentInput = document.getElementById('enStu');
    const courseInputWrap = document.getElementById('enCourseInputWrap');
    const courseInput = document.getElementById('enCourse');
    const courseSelectWrap = document.getElementById('enCourseSelectWrap');
    const courseSelect = document.getElementById('enCourseSelect');
    const noCourseNotice = document.getElementById('enrollNoCourseNotice');
    const submitBtn = document.getElementById('btnCreateEnr');
    const semesterInput = document.getElementById('enSem');

    if (!title || !note || !studentWrap || !studentSelectWrap || !studentSelect || !studentInput || !courseInputWrap || !courseInput || !courseSelectWrap || !courseSelect || !noCourseNotice || !submitBtn || !semesterInput) return;

    if (seesEnrollmentStaffTable()) {
      title.textContent = 'Tạo đăng ký / Mở lớp (Admin/GV)';
      note.classList.add('d-none');
      studentWrap.classList.add('d-none');
      studentSelectWrap.classList.remove('d-none');
      courseInputWrap.classList.add('d-none');
      courseSelectWrap.classList.remove('d-none');
      noCourseNotice.classList.add('d-none');
      const semRowAdm = document.getElementById('enSemRow');
      const btnRowAdm = document.getElementById('enCreateBtnRow');
      if (semRowAdm) semRowAdm.classList.remove('d-none');
      if (btnRowAdm) btnRowAdm.classList.remove('d-none');
      submitBtn.disabled = false;
      submitBtn.textContent = 'Tạo đăng ký';
      document.getElementById('enStaffModeRow')?.classList.remove('d-none');
      document.getElementById('btnOpenClass')?.classList.add('d-none');
      const modeEnroll = document.getElementById('enModeEnroll');
      if (modeEnroll) modeEnroll.checked = true;
      const students = await api('/api/users/students');
      availableStudents = students || [];
      studentSelect.innerHTML = availableStudents
        .map((s) => `<option value="${s.userId}">${s.studentCode || 'N/A'} - ${s.fullName}</option>`)
        .join('');
      const staffCourses = await api('/api/courses');
      courseSelect.innerHTML = (staffCourses || [])
        .map((c) => `<option value="${c.courseId}">${c.courseCode} - ${c.courseName}</option>`)
        .join('');
      if (hasRole('Admin')) {
        const lecs = await api('/api/users/lecturers');
        const lecSel = document.getElementById('enOpenLecturerSelect');
        if (lecSel) {
          lecSel.innerHTML = (lecs || [])
            .map((l) => `<option value="${l.userId}">${l.lecturerCode || 'GV'} - ${l.fullName}</option>`)
            .join('');
        }
      }
      document.getElementById('enLecturerSelectWrap')?.classList.add('d-none');
      applyEnrollStaffMode(false);
      return;
    }

    title.textContent = 'Đăng ký môn học (Sinh viên)';
    document.getElementById('enStaffModeRow')?.classList.add('d-none');
    document.getElementById('enLecturerSelectWrap')?.classList.add('d-none');
    document.getElementById('btnOpenClass')?.classList.add('d-none');
    document.getElementById('btnCreateEnr')?.classList.remove('d-none');
    const enrollRadio = document.getElementById('enModeEnroll');
    if (enrollRadio) enrollRadio.checked = true;
    note.classList.remove('d-none');
    note.textContent =
      'Danh sách môn đã được Admin/GV mở lớp. Chọn môn, nhập học kỳ rồi bấm «Đăng ký môn» để tham gia lớp.';
    studentWrap.classList.add('d-none');
    studentSelectWrap.classList.add('d-none');
    courseInputWrap.classList.add('d-none');
    courseSelectWrap.classList.remove('d-none');
    submitBtn.textContent = 'Đăng ký môn';
    studentInput.value = String(me?.userId || '');
    studentInput.readOnly = true;

    const courses = await api('/api/enrollments/open-courses-for-student');
    const myStudentId = me?.userId;
    const enrolledCourseIds = new Set(
      enrollAllRows
        .filter((e) => extractStudentIdFromEnrollment(e) === myStudentId)
        .map((e) => e?.course?.courseId ?? e?.courseId)
        .filter(Boolean)
    );
    availableStudentCourses = (courses || []).filter((c) => !enrolledCourseIds.has(c.courseId));

    const semRow = document.getElementById('enSemRow');
    const btnRow = document.getElementById('enCreateBtnRow');
    if (!availableStudentCourses.length) {
      courseSelect.innerHTML = '';
      noCourseNotice.classList.remove('d-none');
      courseSelectWrap.classList.add('d-none');
      if (semRow) semRow.classList.add('d-none');
      if (btnRow) btnRow.classList.add('d-none');
      submitBtn.disabled = true;
      return;
    }

    noCourseNotice.classList.add('d-none');
    courseSelectWrap.classList.remove('d-none');
    if (semRow) semRow.classList.remove('d-none');
    if (btnRow) btnRow.classList.remove('d-none');
    submitBtn.disabled = false;
    courseSelect.innerHTML = availableStudentCourses
      .map((c) => `<option value="${c.courseId}">${c.courseCode} - ${c.courseName}</option>`)
      .join('');
  }
  async function refreshUsers() {
    usersAllRows = await api('/api/users');
    renderUsersPage();
  }

  function renderUsersPage() {
    const tb = document.querySelector('#tblUsers tbody');
    const total = usersAllRows.length;
    const totalPages = Math.max(1, Math.ceil(total / usersPageSize));
    if (usersPage > totalPages) usersPage = totalPages;
    const start = (usersPage - 1) * usersPageSize;
    const pageRows = usersAllRows.slice(start, start + usersPageSize);

    const canStudentSelfEdit = isStudentOnly();
    const canLecturerEditUsers = hasRole('Lecturer') && !hasRole('Admin');
    tb.innerHTML = pageRows.map((u) => `<tr><td>${u.userId}</td><td>${u.username}</td><td>${u.fullName}</td><td>${u.email}</td><td>${u.studentCode ?? ''}</td><td>${u.isActive ? 'Hoạt động' : 'Khóa'}</td><td class="action-cell">${actions(u.userId, canAdminCrud || canStudentSelfEdit || canLecturerEditUsers, canAdminCrud)}</td></tr>`).join('');
    document.getElementById('usersPageInfo').textContent = `Trang ${usersPage}/${totalPages} - Tổng: ${total}`;
    document.getElementById('btnUsersPrev').disabled = usersPage <= 1;
    document.getElementById('btnUsersNext').disabled = usersPage >= totalPages;

    bindActions('tblUsers', {
      view: async (id) => showUserInfo(await api('/api/users/' + id)),
      edit: async (id) => {
        const u = await api('/api/users/' + id);
        const fullName = prompt('Họ tên', u.fullName) || u.fullName;
        const email = prompt('Email', u.email) || u.email;
        if (isStudentOnly()) {
          const password = prompt('Mật khẩu mới (để trống nếu giữ nguyên)', '') ?? '';
          await api('/api/users/me', { method: 'PUT', body: JSON.stringify({ fullName, email, password }) });
        } else {
          if (!canAdminCrud && !canLecturerEditUsers) return;
          await api('/api/users/' + id, { method: 'PUT', body: JSON.stringify({ ...u, fullName, email, password: '123', roles: u.roles ?? ['Student'] }) });
        }
        refreshUsers();
      },
      del: async (id) => { if (!canAdminCrud || !confirm('Xóa user?')) return; await api('/api/users/' + id, { method: 'DELETE' }); refreshUsers(); },
    });
  }
  async function refreshPermissions() {
    rbacPermissions = await api('/api/permissions');
    rbacRoles = await api('/api/permissions/roles');
    const users = await api('/api/permissions/users');

    document.getElementById('permList').textContent = rbacPermissions
      .map((p) => {
        const moduleLabel = moduleLabelMap[p.moduleName] ? `${p.moduleName} (${moduleLabelMap[p.moduleName]})` : p.moduleName;
        const permissionCodeLabel = permissionLabelMap[p.permissionCode] ? `${p.permissionCode} (${permissionLabelMap[p.permissionCode]})` : p.permissionCode;
        return `[${moduleLabel}] ${permissionCodeLabel} — ${p.permissionName}`;
      })
      .join('\n');

    const roleSel = document.getElementById('rbacRoleSelect');
    roleSel.innerHTML = rbacRoles.map((r) => `<option value="${r.roleId}">${r.roleName}${roleLabelMap[r.roleName] ? ` (${roleLabelMap[r.roleName]})` : ''}</option>`).join('');

    const userSel = document.getElementById('rbacUserSelect');
    userSel.innerHTML = users.map((u) => `<option value="${u.userId}">${u.username} - ${u.fullName}</option>`).join('');

    await loadRolePermissionCheckboxes();
    await loadUserRoleCheckboxes();
  }

  async function loadRolePermissionCheckboxes() {
    const roleId = document.getElementById('rbacRoleSelect').value;
    if (!roleId) return;
    const role = await api('/api/permissions/roles/' + roleId);
    const assigned = new Set((role.permissions || []).map((x) => x.permissionId));
    const host = document.getElementById('rolePermCheckboxes');
    host.innerHTML = rbacPermissions
      .map(
        (p) =>
          `<div class="form-check"><input class="form-check-input rp-check" type="checkbox" value="${p.permissionId}" id="rp-${p.permissionId}" ${assigned.has(p.permissionId) ? 'checked' : ''}><label class="form-check-label small" for="rp-${p.permissionId}">[${p.moduleName}${moduleLabelMap[p.moduleName] ? ` (${moduleLabelMap[p.moduleName]})` : ''}] ${p.permissionCode}${permissionLabelMap[p.permissionCode] ? ` (${permissionLabelMap[p.permissionCode]})` : ''}</label></div>`
      )
      .join('');
  }

  async function loadUserRoleCheckboxes() {
    const userId = document.getElementById('rbacUserSelect').value;
    if (!userId) return;
    const user = await api('/api/permissions/users/' + userId + '/roles');
    const assigned = new Set((user.roles || []).map((x) => x.roleId));
    const host = document.getElementById('userRoleCheckboxes');
    host.innerHTML = rbacRoles
      .map(
        (r) =>
          `<div class="form-check"><input class="form-check-input ur-check" type="checkbox" value="${r.roleId}" id="ur-${r.roleId}" ${assigned.has(r.roleId) ? 'checked' : ''}><label class="form-check-label small" for="ur-${r.roleId}">${r.roleName}${roleLabelMap[r.roleName] ? ` (${roleLabelMap[r.roleName]})` : ''}</label></div>`
      )
      .join('');
  }

  async function loadGradesCourse() {
    const courseId = document.getElementById('gradeCourseId').value.trim();
    if (!courseId) return;
    gradesAllRows = await api('/api/grades/course/' + courseId);
    gradesPage = 1;
    renderGradesPage();
  }

  function renderGradesPage() {
    const tb = document.querySelector('#tblGradesCourse tbody');
    const total = gradesAllRows.length;
    const totalPages = Math.max(1, Math.ceil(total / gradesPageSize));
    if (gradesPage > totalPages) gradesPage = totalPages;
    const start = (gradesPage - 1) * gradesPageSize;
    const pageRows = gradesAllRows.slice(start, start + gradesPageSize);

    tb.innerHTML = pageRows
      .map((r) => {
        const g = r.grade;
        return `<tr><td>${r.enrollmentId}</td><td>${r.studentCode}</td><td>${r.fullName}</td><td>${formatTruncScore2(g?.midtermScore)}</td><td>${formatTruncScore2(g?.finalScore)}</td><td>${formatTruncScore2(g?.attendanceScore)}</td><td>${formatTruncScore2(g?.totalScore)}</td><td>${g?.isPublished ? 'Yes' : 'No'}</td>
          <td><button type="button" class="btn btn-sm btn-outline-primary btn-pub" data-eid="${r.enrollmentId}">Công bố</button></td></tr>`;
      })
      .join('');

    document.getElementById('gradesPageInfo').textContent = `Trang ${gradesPage}/${totalPages} - Tổng: ${total}`;
    document.getElementById('btnGradesPrev').disabled = gradesPage <= 1;
    document.getElementById('btnGradesNext').disabled = gradesPage >= totalPages;

    tb.querySelectorAll('.btn-pub').forEach((btn) => {
      btn.addEventListener('click', async () => {
        const eid = btn.getAttribute('data-eid');
        await api('/api/grades/' + eid + '/publish', { method: 'POST' });
        await loadGradesCourse();
      });
    });
  }

  async function refreshAttendanceSessions() {
    const rows = await api('/api/attendance/sessions');
    const tb = document.querySelector('#tblSess tbody');
    tb.innerHTML = rows.map((s) => `<tr><td>${s.sessionId}</td><td>${s.course}</td><td>${s.sessionDate}</td><td><code class="small">${s.qrToken}</code></td><td>${s.tokenExpiry}</td><td class="action-cell">${actions(s.sessionId, hasRole('Admin') || hasRole('Lecturer'), hasRole('Admin'))}</td></tr>`).join('');
    bindActions('tblSess', {
      view: async (id) => showAttendanceInfo(await api('/api/attendance/sessions/' + id)),
      edit: async (id) => {
        const s = await api('/api/attendance/sessions/' + id);
        const sessionDate = prompt('Ngày (yyyy-MM-dd)', s.sessionDate) || s.sessionDate;
        await api('/api/attendance/sessions/' + id, {
          method: 'PUT',
          body: JSON.stringify({
            courseId: s.courseId,
            lecturerId: s.lecturerId,
            sessionDate,
            startTime: s.startTime ?? null,
          }),
        });
        refreshAttendanceSessions();
      },
      del: async (id) => { if (!confirm('Xóa phiên?')) return; await api('/api/attendance/sessions/' + id, { method: 'DELETE' }); refreshAttendanceSessions(); },
    });
  }

  function showQrForToken(t) {
    const el = document.getElementById('qrHost');
    el.innerHTML = '';
    const url = location.origin + '/attendance-scan.html?t=' + encodeURIComponent(t);
    // eslint-disable-next-line no-undef
    new QRCode(el, { text: url, width: 200, height: 200 });
    document.getElementById('qrHint').textContent = url;
  }

  document.getElementById('btnLogout').addEventListener('click', () => {
    localStorage.clear();
    location.href = '/login.html';
  });

  document.querySelectorAll('[data-nav]').forEach((a) => {
    a.addEventListener('click', (e) => {
      e.preventDefault();
      const id = a.getAttribute('data-nav');
      openSection(id, true);
    });
  });

  document.getElementById('btnCreateSession').addEventListener('click', async () => {
    const courseId = +document.getElementById('sessCourseId').value;
    const lecturerId = +document.getElementById('sessLecturerId').value;
    const sessionDate = document.getElementById('sessDate').value;
    if (!courseId || !lecturerId || !sessionDate) return alert('Nhập đủ khóa học, giảng viên, ngày.');
    const body = { courseId, lecturerId, sessionDate, startTime: null };
    const s = await api('/api/attendance/sessions', { method: 'POST', body: JSON.stringify(body) });
    const tok = s.qrToken || s.qRToken;
    document.getElementById('lastToken').value = tok;
    showQrForToken(tok);
    await refreshAttendanceSessions();
  });

  document.getElementById('btnLoadGrades').addEventListener('click', loadGradesCourse);
  document.getElementById('gradesPageSize')?.addEventListener('change', (e) => {
    gradesPageSize = +(e.target.value || 10);
    gradesPage = 1;
    renderGradesPage();
  });
  document.getElementById('btnGradesPrev')?.addEventListener('click', () => {
    if (gradesPage > 1) {
      gradesPage--;
      renderGradesPage();
    }
  });
  document.getElementById('btnGradesNext')?.addEventListener('click', () => {
    const totalPages = Math.max(1, Math.ceil(gradesAllRows.length / gradesPageSize));
    if (gradesPage < totalPages) {
      gradesPage++;
      renderGradesPage();
    }
  });
  document.getElementById('btnGoCreateUser')?.addEventListener('click', () => {
    if (!canCreateUsers) return;
    toggleCreatePanel('users', true);
  });
  document.getElementById('btnBackUsersList')?.addEventListener('click', () => toggleCreatePanel('users', false));
  document.getElementById('userPageSize')?.addEventListener('change', (e) => {
    usersPageSize = +(e.target.value || 10);
    usersPage = 1;
    renderUsersPage();
  });
  document.getElementById('btnUsersPrev')?.addEventListener('click', () => {
    if (usersPage > 1) {
      usersPage--;
      renderUsersPage();
    }
  });
  document.getElementById('btnUsersNext')?.addEventListener('click', () => {
    const totalPages = Math.max(1, Math.ceil(usersAllRows.length / usersPageSize));
    if (usersPage < totalPages) {
      usersPage++;
      renderUsersPage();
    }
  });
  document.getElementById('btnGoCreateCourse')?.addEventListener('click', () => toggleCreatePanel('courses', true));
  document.getElementById('btnBackCoursesList')?.addEventListener('click', () => toggleCreatePanel('courses', false));
  document.getElementById('btnGoCreateEnroll')?.addEventListener('click', async () => {
    await setupEnrollmentCreateUi();
    toggleCreatePanel('enroll', true);
  });
  document.querySelectorAll('input[name="enStaffMode"]').forEach((el) => {
    el.addEventListener('change', () => {
      applyEnrollStaffMode(document.getElementById('enModeOpen')?.checked === true);
    });
  });
  document.getElementById('btnOpenClass')?.addEventListener('click', async () => {
    if (!seesEnrollmentStaffTable()) return;
    const courseId = +document.getElementById('enCourseSelect').value;
    if (!courseId) {
      alert('Chọn môn học.');
      return;
    }
    const body = { courseId };
    if (hasRole('Admin')) {
      const lid = +document.getElementById('enOpenLecturerSelect')?.value;
      if (!lid) {
        alert('Chọn giảng viên phụ trách lớp.');
        return;
      }
      body.lecturerId = lid;
    }
    await api('/api/schedules/classes/quick-open', { method: 'POST', body: JSON.stringify(body) });
    alert('Đã mở lớp. Sinh viên vào Đăng ký môn, chọn học kỳ và bấm «Đăng ký môn» để tham gia.');
    toggleCreatePanel('enroll', false);
    await refreshEnrollments();
  });
  document.getElementById('btnBackEnrollList')?.addEventListener('click', () => toggleCreatePanel('enroll', false));
  document.getElementById('enrollPageSize')?.addEventListener('change', (e) => {
    enrollPageSize = +(e.target.value || 10);
    enrollPage = 1;
    renderEnrollmentsPage();
  });
  document.getElementById('btnEnrollPrev')?.addEventListener('click', () => {
    if (enrollPage > 1) {
      enrollPage--;
      renderEnrollmentsPage();
    }
  });
  document.getElementById('btnEnrollNext')?.addEventListener('click', () => {
    const totalPages = Math.max(1, Math.ceil(enrollAllRows.length / enrollPageSize));
    if (enrollPage < totalPages) {
      enrollPage++;
      renderEnrollmentsPage();
    }
  });
  document.getElementById('btnGoCreateAttendance')?.addEventListener('click', () => toggleCreatePanel('attendance', true));
  document.getElementById('btnBackAttendanceList')?.addEventListener('click', () => toggleCreatePanel('attendance', false));

  document.getElementById('btnUpsertGrade').addEventListener('click', async () => {
    const body = {
      enrollmentId: +document.getElementById('gEnr').value,
      midtermScore: document.getElementById('gMid').value ? +document.getElementById('gMid').value : null,
      finalScore: document.getElementById('gFin').value ? +document.getElementById('gFin').value : null,
      attendanceScore: document.getElementById('gAtt').value ? +document.getElementById('gAtt').value : null,
    };
    await api('/api/grades', { method: 'POST', body: JSON.stringify(body) });
    if (document.getElementById('gradeCourseId').value.trim()) {
      await loadGradesCourse();
    }
    alert('Đã lưu điểm.');
  });

  document.getElementById('btnCreateUser').addEventListener('click', async () => {
    if (!canCreateUsers) {
      alert('Bạn không có quyền tạo tài khoản.');
      return;
    }
    const body = {
      username: document.getElementById('cuUser').value.trim(),
      password: document.getElementById('cuPass').value,
      fullName: document.getElementById('cuName').value.trim(),
      email: document.getElementById('cuEmail').value.trim(),
      studentCode: document.getElementById('cuStu').value.trim() || null,
      lecturerCode: document.getElementById('cuLec').value.trim() || null,
      roles: document.getElementById('cuRoles').value.split(',').map((s) => s.trim()).filter(Boolean),
    };
    await api('/api/users', { method: 'POST', body: JSON.stringify(body) });
    alert('Đã tạo user.');
    toggleCreatePanel('users', false);
    await refreshUsers();
  });

  document.getElementById('btnCreateCourse').addEventListener('click', async () => {
    const body = {
      courseCode: document.getElementById('ccCode').value.trim(),
      courseName: document.getElementById('ccName').value.trim(),
      credits: +document.getElementById('ccCred').value,
      department: document.getElementById('ccDep').value.trim() || null,
    };
    await api('/api/courses', { method: 'POST', body: JSON.stringify(body) });
    alert('Đã tạo môn.');
    toggleCreatePanel('courses', false);
    await refreshCourses();
  });

  document.getElementById('btnCreateEnr').addEventListener('click', async () => {
    const semester = document.getElementById('enSem').value.trim();
    if (!semester) {
      alert('Vui lòng nhập học kỳ.');
      return;
    }
    let studentId = +document.getElementById('enStu').value;
    let courseId = +document.getElementById('enCourse').value;
    if (isStudentOnly()) {
      if (!availableStudentCourses.length) {
        alert('Không có lớp mở sẵn để đăng ký.');
        return;
      }
      studentId = +(me?.userId || 0);
      courseId = +document.getElementById('enCourseSelect').value;
    } else {
      studentId = +document.getElementById('enStuSelect').value;
      courseId = +document.getElementById('enCourseSelect').value;
    }
    if (!studentId || !courseId) {
      alert('Thiếu thông tin sinh viên hoặc môn học.');
      return;
    }
    const body = { studentId, courseId, semester, status: 'Active' };
    await api('/api/enrollments', { method: 'POST', body: JSON.stringify(body) });
    alert(isStudentOnly() ? 'Đăng ký môn thành công.' : 'Đã tạo đăng ký.');
    toggleCreatePanel('enroll', false);
    await refreshEnrollments();
  });

  document.getElementById('rbacRoleSelect')?.addEventListener('change', loadRolePermissionCheckboxes);
  document.getElementById('rbacUserSelect')?.addEventListener('change', loadUserRoleCheckboxes);

  document.getElementById('btnSaveRolePerms')?.addEventListener('click', async () => {
    const roleId = +document.getElementById('rbacRoleSelect').value;
    const permissionIds = Array.from(document.querySelectorAll('.rp-check:checked')).map((x) => +x.value);
    await api('/api/permissions/roles/' + roleId, { method: 'POST', body: JSON.stringify({ permissionIds }) });
    alert('Đã cập nhật RolePermissions.');
  });

  document.getElementById('btnSaveUserRoles')?.addEventListener('click', async () => {
    const userId = +document.getElementById('rbacUserSelect').value;
    const roleIds = Array.from(document.querySelectorAll('.ur-check:checked')).map((x) => +x.value);
    await api('/api/permissions/users/' + userId + '/roles', { method: 'POST', body: JSON.stringify({ roleIds }) });
    alert('Đã cập nhật UserRoles.');
  });

  (async () => {
    await loadMe();
    const userNote = document.getElementById('userPermNote');
    const roleInput = document.getElementById('cuRoles');
    const createBtn = document.getElementById('btnCreateUser');
    const userInputs = ['cuUser', 'cuPass', 'cuName', 'cuEmail', 'cuStu', 'cuLec', 'cuRoles']
      .map((id) => document.getElementById(id))
      .filter(Boolean);

    const btnGoCreate = document.getElementById('btnGoCreateUser');
    if (!canCreateUsers) {
      userNote.classList.remove('d-none');
      userNote.textContent = hasRole('Student')
        ? 'Bạn chỉ được xem và sửa thông tin của chính bạn.'
        : 'Bạn không có quyền tạo tài khoản.';
      userInputs.forEach((el) => {
        el.disabled = true;
      });
      createBtn.disabled = true;
      btnGoCreate?.classList.add('d-none');
      btnGoCreate?.setAttribute('disabled', 'disabled');
    } else {
      btnGoCreate?.classList.remove('d-none');
      btnGoCreate?.removeAttribute('disabled');
      if (hasRole('Lecturer') && !hasRole('Admin')) {
        userNote.classList.remove('d-none');
        userNote.textContent = 'Giảng viên chỉ được tạo tài khoản sinh viên.';
        roleInput.value = 'Student';
        roleInput.readOnly = true;
      } else {
        userNote.classList.add('d-none');
        roleInput.readOnly = false;
      }
    }

    if (hasRole('Lecturer') && !hasRole('Admin')) {
      document.getElementById('sessLecturerId').value = me.userId;
    }
    if (hasRole('Student')) {
      const goEnrollBtn = document.getElementById('btnGoCreateEnroll');
      if (goEnrollBtn) goEnrollBtn.textContent = 'Đăng ký môn';
    }
    toggleCreatePanel('users', false);
    toggleCreatePanel('courses', false);
    toggleCreatePanel('enroll', false);
    toggleCreatePanel('attendance', false);
    const savedSection = localStorage.getItem(LAST_SECTION_KEY) || 'dashboard';
    await openSection(savedSection, true);
  })();
})();
