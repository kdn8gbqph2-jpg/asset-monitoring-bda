
    // ── Cached user list for drawer dropdowns ─────────────────────────────────
    let _drawerUsers = null;

    async function loadPumpUsers(currentOperator, currentJe) {
        const operatorSel = document.getElementById('editOperatorMobile');
        const jeSel       = document.getElementById('editJeMobile');
        if (!operatorSel || !jeSel) return; // dropdowns not rendered (operator role)

        if (!_drawerUsers) {
            try {
                const res  = await fetch('?handler=ActiveUsers');
                _drawerUsers = res.ok ? await res.json() : [];
            } catch { _drawerUsers = []; }
        }

        const populate = (sel, type, current) => {
            sel.innerHTML = '<option value="">— None —</option>';
            _drawerUsers
                .filter(u => u.userType === type)
                .forEach(u => {
                    const opt = new Option(`${u.name} (${u.mobile})`, u.mobile);
                    if (u.mobile === current) opt.selected = true;
                    sel.appendChild(opt);
                });
        };

        populate(operatorSel, 'OPERATOR', currentOperator);
        populate(jeSel,       'JE',       currentJe);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    function getToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]').value;
    }

    function getPumpDrawer() {
        const el = document.getElementById("editPumpDrawer");
        return bootstrap.Offcanvas.getInstance(el) ?? new bootstrap.Offcanvas(el);
    }

    // ── Edit existing pump ────────────────────────────────────────────────────
    function openEditPumpDrawer(id, vendor, location, status, latitude, longitude, isActive, operatorMobile, jeMobile) {
        document.getElementById("editPumpId").value       = id;
        document.getElementById("editVendorName").value   = vendor    ?? "";
        document.getElementById("editLocationName").value = location  ?? "";
        document.getElementById("editStatus").value       = status    ?? "OFF";
        document.getElementById("editLatitude").value     = latitude  ?? "";
        document.getElementById("editLongitude").value    = longitude ?? "";
        document.getElementById("editRemarks").value      = "";

        // Restore title + save button for edit mode
        document.getElementById("pumpDrawerTitle").innerHTML =
            '<i class="bi bi-pencil-square"></i> Edit Pump';
        const btn = document.getElementById("pumpDrawerSaveBtn");
        btn.textContent = "Save Changes";
        btn.onclick = savePump;

        // Populate operator / JE dropdowns asynchronously
        loadPumpUsers(operatorMobile ?? '', jeMobile ?? '');

        editDrawer = getPumpDrawer();
        editDrawer.show();
    }

    let editDrawer = null;

    function savePump() {
        const latRaw = parseFloat(document.getElementById("editLatitude").value);
        const lngRaw = parseFloat(document.getElementById("editLongitude").value);

        const opSel = document.getElementById("editOperatorMobile");
        const jeSel = document.getElementById("editJeMobile");

        fetch('?handler=UpdatePump', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            body: JSON.stringify({
                PumpId:         parseInt(document.getElementById("editPumpId").value),
                VendorName:     document.getElementById("editVendorName").value,
                LocationName:   document.getElementById("editLocationName").value,
                Status:         document.getElementById("editStatus").value,
                Latitude:       isNaN(latRaw) ? null : latRaw,
                Longitude:      isNaN(lngRaw) ? null : lngRaw,
                IsActive:       true,
                Remarks:        document.getElementById("editRemarks").value || null,
                OperatorMobile: opSel ? opSel.value : null,
                JeMobile:       jeSel ? jeSel.value : null
            })
        })
        .then(r => r.json())
        .then(res => {
            if (res.success) { editDrawer.hide(); location.reload(); }
            else alert("Update failed: " + (res.message ?? "Unknown error"));
        })
        .catch(err => { console.error(err); alert("Error saving pump"); });
    }

    // ── Add new pump ──────────────────────────────────────────────────────────
    function openAddPumpDrawer() {
        document.getElementById("editPumpId").value       = "0";
        document.getElementById("editVendorName").value   = "";
        document.getElementById("editLocationName").value = "";
        document.getElementById("editStatus").value       = "OFF";
        document.getElementById("editLatitude").value     = "";
        document.getElementById("editLongitude").value    = "";
        document.getElementById("editRemarks").value      = "";

        document.getElementById("pumpDrawerTitle").innerHTML =
            '<i class="bi bi-plus-circle"></i> Add Pump';
        const btn = document.getElementById("pumpDrawerSaveBtn");
        btn.textContent = "Add Pump";
        btn.onclick = saveNewPump;

        // Populate dropdowns with no pre-selection
        loadPumpUsers('', '');

        editDrawer = getPumpDrawer();
        editDrawer.show();
    }

    function saveNewPump() {
        const latRaw = parseFloat(document.getElementById("editLatitude").value);
        const lngRaw = parseFloat(document.getElementById("editLongitude").value);

        const opSel = document.getElementById("editOperatorMobile");
        const jeSel = document.getElementById("editJeMobile");

        fetch('?handler=AddPump', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            body: JSON.stringify({
                VendorName:     document.getElementById("editVendorName").value,
                LocationName:   document.getElementById("editLocationName").value,
                Status:         document.getElementById("editStatus").value,
                Latitude:       isNaN(latRaw) ? null : latRaw,
                Longitude:      isNaN(lngRaw) ? null : lngRaw,
                Remarks:        document.getElementById("editRemarks").value || null,
                OperatorMobile: opSel ? opSel.value : null,
                JeMobile:       jeSel ? jeSel.value : null
            })
        })
        .then(r => r.json())
        .then(res => {
            if (res.success) { editDrawer.hide(); location.reload(); }
            else alert("Failed to add pump: " + (res.message ?? "Unknown error"));
        })
        .catch(err => { console.error(err); alert("Error adding pump"); });
    }


    // -- Delegated click handler for .edit-pump-btn
    document.addEventListener('DOMContentLoaded', function () {
        document.addEventListener('click', function (e) {
            const btn = e.target.closest('.edit-pump-btn');
            if (!btn) return;

            const lat = btn.dataset.lat;
            const lng = btn.dataset.lng;

            openEditPumpDrawer(
                btn.dataset.pumpId,
                btn.dataset.vendor        || '',
                btn.dataset.location      || '',
                btn.dataset.status        || 'OFF',
                lat && lat !== '' ? parseFloat(lat) : null,
                lng && lng !== '' ? parseFloat(lng) : null,
                true,
                btn.dataset.operatorMobile || '',
                btn.dataset.jeMobile       || ''
            );
        });
    });
