window.AdminDrawer = (function () {

    let drawerInstance = null;

    function open(drawerId) {
        const el = document.getElementById(drawerId);

        drawerInstance =
            bootstrap.Offcanvas.getInstance(el) ||
            new bootstrap.Offcanvas(el);

        drawerInstance.show();
    }

    function close() {
        if (drawerInstance) {
            drawerInstance.hide();
        }
    }

    async function submit(url, payload, onSuccess) {

        const token = document.querySelector(
            'input[name="__RequestVerificationToken"]'
        ).value;

        const res = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token
            },
            body: JSON.stringify(payload)
        });

        const data = await res.json();

        if (data.success) {
            onSuccess?.(data);
            close();
        } else {
            alert("Operation failed");
        }
    }

    return {
        open,
        close,
        submit
    };
})();
