
    function openEditPumpDrawer(id, vendor, location, status, latitude, longitude, isActive) {
    document.getElementById("editPumpId").value = id;
    document.getElementById("editVendorName").value = vendor ?? "";
    document.getElementById("editLocationName").value = location ?? "";
    document.getElementById("editStatus").value = status ?? "OFF";
    document.getElementById("editLatitude").value = latitude ?? "";
    document.getElementById("editLongitude").value = longitude ?? "";
    /*document.getElementById("editIsActive").checked = isActive;*/

    const drawerEl = document.getElementById("editPumpDrawer");
    editDrawer = bootstrap.Offcanvas.getInstance(drawerEl)
    ?? new bootstrap.Offcanvas(drawerEl);

    editDrawer.show();
    }

    function savePump() {
        const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
    ).value;

    fetch('?handler=UpdatePump', {
        method: 'POST',
    headers: {
        'Content-Type': 'application/json',
    'RequestVerificationToken': token
            },
    body: JSON.stringify({
        PumpId: parseInt(document.getElementById("editPumpId").value),
    VendorName: document.getElementById("editVendorName").value,
    LocationName: document.getElementById("editLocationName").value,
    Latitude: parseFloat(document.getElementById("editLatitude").value),
    Longitude: parseFloat(document.getElementById("editLongitude").value),
    Status: document.getElementById("editStatus").value,
    /*IsActive: document.getElementById("editIsActive").checked*/
            })
        })
        .then(res => res.json())
        .then(res => {
            if (res.success) {
        editDrawer.hide();
    location.reload();
            } else {
        alert("Update failed");
            }
        })
        .catch(err => {
        console.error(err);
    alert("Error while saving pump");
        });
    }

