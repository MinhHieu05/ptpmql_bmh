(() => {
    const page = document.querySelector("#student-page");

    if (!page) {
        return;
    }

    const listUrl = page.dataset.listUrl;
    const createUrl = page.dataset.createUrl;
    const baseUrl = page.dataset.studentUrl;
    const tableBody = document.querySelector("#student-table-body");
    const searchInput = document.querySelector("#student-search");
    const majorFilter = document.querySelector("#student-major-filter");
    const yearFilter = document.querySelector("#student-year-filter");
    const systemFilter = document.querySelector("#student-system-filter");
    const approvalFilter = document.querySelector("#student-approval-filter");
    const columnCount = yearFilter ? 9 : 8;
    const pageSummary = document.querySelector("#student-page-summary");
    const previousPageButton = document.querySelector("#student-previous-page");
    const nextPageButton = document.querySelector("#student-next-page");
    const notice = document.querySelector("#student-notice");
    const createForm = document.querySelector("#create-student-form");
    const editForm = document.querySelector("#edit-student-form");
    const deleteForm = document.querySelector("#delete-student-form");
    const createModal = bootstrap.Modal.getOrCreateInstance(
        document.querySelector("#create-student-modal"));
    const editModal = bootstrap.Modal.getOrCreateInstance(
        document.querySelector("#edit-student-modal"));
    const detailsModal = bootstrap.Modal.getOrCreateInstance(
        document.querySelector("#details-student-modal"));
    const deleteModal = bootstrap.Modal.getOrCreateInstance(
        document.querySelector("#delete-student-modal"));

    const state = {
        page: 1,
        totalPages: 1,
        searchTimer: null,
        deleteStudentCode: null,
        sortBy: "studentCode",
        descending: false
    };
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    function studentUrl(code) {
        return `${baseUrl}/${encodeURIComponent(code)}`;
    }

    async function readResponse(response) {
        const text = await response.text();

        if (!text) {
            return null;
        }

        try {
            return JSON.parse(text);
        } catch {
            throw new Error("Máy chủ trả về dữ liệu không hợp lệ.");
        }
    }

    async function send(url, options = {}) {
        const headers = new Headers(options.headers || {});
        headers.set("Accept", "application/json");

        if (options.body) {
            headers.set("Content-Type", "application/json");
        }

        if (options.method && options.method !== "GET") {
            headers.set("RequestVerificationToken", token || "");
        }

        const response = await fetch(url, { ...options, headers });
        const data = await readResponse(response);

        if (!response.ok) {
            const error = new Error(
                data?.detail || data?.title || `Yêu cầu thất bại (HTTP ${response.status}).`);
            error.status = response.status;
            error.data = data;
            throw error;
        }

        return data;
    }

    function showNotice(message, type = "success") {
        notice.textContent = message;
        notice.className = `alert alert-${type}`;
    }

    function clearNotice() {
        notice.textContent = "";
        notice.className = "alert d-none";
    }

    function setBusy(form, busy, label) {
        const button = form.querySelector('button[type="submit"]');
        button.disabled = busy;
        button.querySelector(".button-label").textContent = busy ? "Đang xử lý..." : label;
        button.querySelector(".spinner-border").classList.toggle("d-none", !busy);
    }

    function clearFormErrors(form) {
        form.querySelectorAll(".field-error").forEach(element => {
            element.textContent = "";
        });
        const summary = form.querySelector(".student-form-errors");
        summary.textContent = "";
        summary.classList.add("d-none");
        form.querySelectorAll(".is-invalid").forEach(element => {
            element.classList.remove("is-invalid");
        });
    }

    function showFormErrors(form, error) {
        clearFormErrors(form);
        const data = error.data;
        const summary = form.querySelector(".student-form-errors");
        const unassignedMessages = [];

        if (data?.errors) {
            for (const [field, messages] of Object.entries(data.errors)) {
                const message = messages.join(" ");
                const fieldError = [...form.querySelectorAll(".field-error")]
                    .find(element => element.dataset.valmsgFor.toLowerCase() === field.toLowerCase());
                const input = form.elements.namedItem(field);

                if (fieldError) {
                    fieldError.textContent = message;
                    input?.classList.add("is-invalid");
                } else {
                    unassignedMessages.push(message);
                }
            }
        }

        const generalMessage = [
            data?.detail,
            data?.title,
            ...unassignedMessages
        ].filter(Boolean).join(" ");

        if (generalMessage) {
            summary.textContent = generalMessage;
            summary.classList.remove("d-none");
        } else if (error.message) {
            summary.textContent = error.message;
            summary.classList.remove("d-none");
        }
    }

    function describeTrainingSystem(value) {
        if (value === "21") {
            return "Đại học chính quy";
        }

        if (value === "20") {
            return "Đại học không chính quy";
        }

        return value || "—";
    }

    function addCell(row, value, className = "") {
        const cell = document.createElement("td");
        cell.textContent = value || "—";
        if (className) {
            cell.className = className;
        }
        row.append(cell);
        return cell;
    }

    function addActionButton(container, action, label, style, code) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = `btn btn-sm ${style}`;
        button.dataset.action = action;
        button.dataset.studentCode = code;
        button.textContent = label;
        container.append(button);
    }

    function renderStudents(data) {
        tableBody.replaceChildren();

        if (data.items.length === 0) {
            const row = document.createElement("tr");
            const cell = document.createElement("td");
            cell.colSpan = columnCount;
            cell.className = "student-table-message";
            cell.textContent = "Không tìm thấy sinh viên phù hợp.";
            row.append(cell);
            tableBody.append(row);
        } else {
            for (const student of data.items) {
                const row = document.createElement("tr");
                addCell(row, student.studentCode, "student-code");
                addCell(row, student.name);
                addCell(row, student.enrollmentYear);
                addCell(row, describeTrainingSystem(student.trainingSystem));
                addCell(row, student.majorName || student.majorCode);
                addCell(row, student.email);

                if (yearFilter) {
                    addCell(row, student.creatorEmail || "Chưa liên kết");
                }

                const approvalCell = addCell(row, "");
                const approvalBadge = document.createElement("span");
                approvalBadge.className = student.isApproved
                    ? "badge text-bg-success"
                    : "badge text-bg-warning";
                approvalBadge.textContent = student.isApproved ? "Đã duyệt" : "Chờ duyệt";
                approvalCell.append(approvalBadge);

                const actions = document.createElement("td");
                actions.className = "text-end text-nowrap";
                addActionButton(actions, "details", "Chi tiết", "btn-outline-secondary", student.studentCode);
                if (!student.isApproved) {
                    addActionButton(actions, "approve", "Duyệt", "btn-outline-success", student.studentCode);
                }
                addActionButton(actions, "edit", "Sửa", "btn-outline-primary", student.studentCode);
                addActionButton(actions, "delete", "Xóa", "btn-outline-danger", student.studentCode);
                row.append(actions);
                tableBody.append(row);
            }
        }

        state.page = data.page;
        state.totalPages = data.totalPages;
        pageSummary.textContent =
            `${data.totalItems} sinh viên · Trang ${data.page}/${data.totalPages}`;
        previousPageButton.disabled = data.page <= 1;
        nextPageButton.disabled = data.page >= data.totalPages;
    }

    async function loadStudents() {
        const query = new URLSearchParams({
            search: searchInput.value.trim(),
            majorCode: majorFilter.value,
            enrollmentYear: yearFilter?.value || "",
            trainingSystem: systemFilter?.value || "",
            approvalStatus: approvalFilter?.value || "",
            sortBy: state.sortBy,
            descending: String(state.descending),
            page: String(state.page)
        });

        tableBody.innerHTML =
            `<tr><td colspan="${columnCount}" class="student-table-message">Đang tải danh sách...</td></tr>`;

        try {
            renderStudents(await send(`${listUrl}?${query}`));
        } catch (error) {
            const row = document.createElement("tr");
            const cell = document.createElement("td");
            cell.colSpan = columnCount;
            cell.className = "student-table-message text-danger";
            cell.textContent = error instanceof TypeError
                ? "Không thể kết nối máy chủ. Vui lòng thử lại."
                : error.message;
            row.append(cell);
            tableBody.replaceChildren(row);
            pageSummary.textContent = "";
        }
    }

    function formDataToObject(form) {
        const values = Object.fromEntries(new FormData(form).entries());
        delete values.__RequestVerificationToken;
        return values;
    }

    function setFormBusyAndError(form, busy, error) {
        if (error instanceof TypeError) {
            const summary = form.querySelector(".student-form-errors");
            summary.textContent = "Không thể kết nối máy chủ. Vui lòng kiểm tra kết nối và thử lại.";
            summary.classList.remove("d-none");
        } else if (error) {
            showFormErrors(form, error);
        }
        const label = form === createForm
            ? "Thêm sinh viên"
            : form === editForm
                ? "Lưu thay đổi"
                : "Xóa sinh viên";
        setBusy(form, busy, label);
    }

    function displayDetails(student) {
        const content = document.querySelector("#student-details-content");
        content.replaceChildren();

        const rows = [
            ["Mã sinh viên", student.studentCode],
            ["Họ và tên", student.name],
            ["Năm nhập học", student.enrollmentYear],
            ["Hệ đào tạo", describeTrainingSystem(student.trainingSystem)],
            ["Ngành học", student.majorName || student.majorCode],
            ["Trạng thái hồ sơ", student.isApproved ? "Đã duyệt" : "Chờ duyệt"],
            ["Ngày sinh", student.dateOfBirth?.substring(0, 10) || "—"],
            ["Địa chỉ", student.address || "—"],
            ["Email", student.email]
        ];
        if (student.userId !== undefined) {
            rows.push(["Tài khoản tạo hồ sơ", student.creatorEmail || "Chưa liên kết"]);
        }
        const list = document.createElement("dl");
        list.className = "row student-details";

        for (const [label, value] of rows) {
            const term = document.createElement("dt");
            term.className = "col-sm-4";
            term.textContent = label;
            const description = document.createElement("dd");
            description.className = "col-sm-8";
            description.textContent = value;
            list.append(term, description);
        }

        content.append(list);
        detailsModal.show();
    }

    async function openStudentModal(action, code) {
        clearNotice();

        try {
            const student = await send(`${studentUrl(code)}/details`);

            if (action === "details") {
                displayDetails(student);
            } else if (action === "edit") {
                clearFormErrors(editForm);
                document.querySelector("#edit-code").value = student.studentCode;
                document.querySelector("#edit-major").value = student.majorName || student.majorCode;
                document.querySelector("#edit-year").value = student.enrollmentYear;
                document.querySelector("#edit-system").value = describeTrainingSystem(student.trainingSystem);
                editForm.elements.namedItem("Name").value = student.name;
                editForm.elements.namedItem("Email").value = student.email;
                editForm.elements.namedItem("DateOfBirth").value =
                    student.dateOfBirth?.substring(0, 10) || "";
                editForm.elements.namedItem("Address").value = student.address || "";
                editModal.show();
            } else {
                clearFormErrors(deleteForm);
                state.deleteStudentCode = student.studentCode;
                document.querySelector("#delete-student-name").textContent =
                    `${student.name} (${student.studentCode})`;
                deleteModal.show();
            }
        } catch (error) {
            showNotice(error instanceof TypeError
                ? "Không thể kết nối máy chủ. Vui lòng thử lại."
                : error.message, "danger");
        }
    }

    async function approveStudent(code) {
        clearNotice();

        try {
            await send(`${studentUrl(code)}/approve`, { method: "POST" });
            showNotice(`Đã duyệt hồ sơ ${code}.`);
            await loadStudents();
        } catch (error) {
            showNotice(error instanceof TypeError
                ? "Không thể kết nối máy chủ. Vui lòng thử lại."
                : error.message, "danger");
        }
    }

    createForm.addEventListener("submit", async event => {
        event.preventDefault();
        clearFormErrors(createForm);

        if (!createForm.reportValidity()) {
            return;
        }

        setFormBusyAndError(createForm, true);

        try {
            const payload = formDataToObject(createForm);
            payload.EnrollmentYear = Number(payload.EnrollmentYear);
            const createdStudent = await send(
                createUrl,
                { method: "POST", body: JSON.stringify(payload) });
            createModal.hide();
            showNotice(`Đã thêm sinh viên thành công. Mã sinh viên: ${createdStudent.studentCode}.`);
            state.page = 1;
            await loadStudents();
        } catch (error) {
            setFormBusyAndError(createForm, false, error);
        } finally {
            setFormBusy(createForm, false, "Thêm sinh viên");
        }
    });

    editForm.addEventListener("submit", async event => {
        event.preventDefault();
        clearFormErrors(editForm);

        if (!editForm.reportValidity()) {
            return;
        }

        setFormBusyAndError(editForm, true);

        try {
            const payload = formDataToObject(editForm);
            const code = document.querySelector("#edit-code").value;
            await send(studentUrl(code), { method: "PUT", body: JSON.stringify(payload) });
            editModal.hide();
            showNotice("Đã cập nhật thông tin sinh viên.");
            await loadStudents();
        } catch (error) {
            setFormBusyAndError(editForm, false, error);
        } finally {
            setBusy(editForm, false, "Lưu thay đổi");
        }
    });

    deleteForm.addEventListener("submit", async event => {
        event.preventDefault();
        clearFormErrors(deleteForm);
        setFormBusyAndError(deleteForm, true);

        try {
            await send(studentUrl(state.deleteStudentCode), { method: "DELETE" });
            deleteModal.hide();
            showNotice("Đã xóa sinh viên.");
            await loadStudents();
        } catch (error) {
            setFormBusyAndError(deleteForm, false, error);
        } finally {
            setBusy(deleteForm, false, "Xóa sinh viên");
        }
    });

    tableBody.addEventListener("click", event => {
        const button = event.target.closest("button[data-action]");

        if (button) {
            if (button.dataset.action === "approve") {
                approveStudent(button.dataset.studentCode);
            } else {
                openStudentModal(button.dataset.action, button.dataset.studentCode);
            }
        }
    });

    document.querySelectorAll("button[data-sort]").forEach(button => {
        button.addEventListener("click", () => {
            const sortBy = button.dataset.sort;
            state.descending = state.sortBy === sortBy ? !state.descending : false;
            state.sortBy = sortBy;
            state.page = 1;
            loadStudents();
        });
    });

    searchInput.addEventListener("input", () => {
        window.clearTimeout(state.searchTimer);
        state.searchTimer = window.setTimeout(() => {
            state.page = 1;
            loadStudents();
        }, 250);
    });

    majorFilter.addEventListener("change", () => {
        state.page = 1;
        loadStudents();
    });

    yearFilter?.addEventListener("change", () => {
        state.page = 1;
        loadStudents();
    });

    systemFilter?.addEventListener("change", () => {
        state.page = 1;
        loadStudents();
    });

    approvalFilter?.addEventListener("change", () => {
        state.page = 1;
        loadStudents();
    });

    previousPageButton.addEventListener("click", () => {
        if (state.page > 1) {
            state.page -= 1;
            loadStudents();
        }
    });

    nextPageButton.addEventListener("click", () => {
        if (state.page < state.totalPages) {
            state.page += 1;
            loadStudents();
        }
    });

    document.querySelector("#create-student-modal").addEventListener("show.bs.modal", () => {
        createForm.reset();
        clearFormErrors(createForm);
    });

    loadStudents();
})();
