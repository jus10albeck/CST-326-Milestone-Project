// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Stops a form marked with data-submit-once from being sent twice,
// for example when its button is double-clicked.
document.addEventListener("submit", function (event) {
    var form = event.target;

    if (!form.matches || !form.matches("form[data-submit-once]")) {
        return;
    }

    if (form.dataset.submitted === "true") {
        event.preventDefault();
        return;
    }

    // Leaves the form alone if validation stopped it from being sent
    if (event.defaultPrevented) {
        return;
    }

    form.dataset.submitted = "true";

    // Disables the buttons just after the form is sent
    setTimeout(function () {
        form.querySelectorAll("button[type=submit]").forEach(function (button) {
            button.disabled = true;
        });
    }, 0);
});

// Turns the buttons back on if the user comes back to the page with the Back button
window.addEventListener("pageshow", function (event) {
    if (!event.persisted) {
        return;
    }

    document.querySelectorAll("form[data-submit-once]").forEach(function (form) {
        form.dataset.submitted = "";

        form.querySelectorAll("button[type=submit]").forEach(function (button) {
            button.disabled = false;
        });
    });
});
