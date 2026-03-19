
    let editDrawer = null;

    // ── Helpers ──────────────────────────────────────────────────────────────
    function getToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]').value;
    }

    function getPumpDrawer() {
        const el = document.getElementById("editPumpDrawer");
        return bootstrap.Offcanvas.getInstance(el) ?? new bootstrap.Offcanvas(el);
    }

    // ── Edit existing pump ────────────────────────────────────────────────────
    function openEditPumpDrawer(id, vendor, location, status, latitude, longitude, isActive) {
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

        editDrawer = getPumpDrawer();
        editDrawer.show();
    }

    function savePump() {
        const latRaw = parseFloat(document.getElementById("editLatitude").value);
        const lngRaw = parseFloat(document.getElementById("editLongitude").value);

        fetch('?handler=UpdatePump', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            body: JSON.stringify({
                PumpId:       parseInt(document.getElementById("editPumpId").value),
                VendorName:   document.getElementById("editVendorName").value,
                LocationName: document.getElementById("editLocationName").value,
                Status:       document.getElementById("editStatus").value,
                Latitude:     isNaN(latRaw) ? null : latRaw,
                Longitude:    isNaN(lngRaw) ? null : lngRaw,
                IsActive:     true,
                Remarks:      document.getElementById("editRemarks").value || null
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

        editDrawer = getPumpDrawer();
        editDrawer.show();
    }

    function saveNewPump() {
        const latRaw = parseFloat(document.getElementById("editLatitude").value);
        const lngRaw = parseFloat(document.getElementById("editLongitude").value);

        fetch('?handler=AddPump', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            body: JSON.stringify({
                VendorName:   document.getElementById("editVendorName").value,
                LocationName: document.getElementById("editLocationName").value,
                Status:       document.getElementById("editStatus").value,
                Latitude:     isNaN(latRaw) ? null : latRaw,
                Longitude:    isNaN(lngRaw) ? null : lngRaw,
                Remarks:      document.getElementById("editRemarks").value || null
            })
        })
        .then(r => r.json())
        .then(res => {
            if (res.success) { editDrawer.hide(); location.reload(); }
            else alert("Failed to add pump: " + (res.message ?? "Unknown error"));
        })
        .catch(err => { console.error(err); alert("Error adding pump"); });
    }

