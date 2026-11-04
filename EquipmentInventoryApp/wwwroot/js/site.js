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

// Shows the Repair Note box only when "Under Repair" is selected, makes it required then,
// and counts the characters typed. The server checks the note again when saving.
document.querySelectorAll("[data-repair-note-for]").forEach(function (group) {
    var select = document.getElementById(group.dataset.repairNoteFor);
    var textarea = group.querySelector("textarea");
    var counter = group.querySelector("[data-char-count]");

    if (!select || !textarea) {
        return;
    }

    function updateVisibility() {
        var isUnderRepair = select.value === "Under Repair";

        group.classList.toggle("d-none", !isUnderRepair);
        textarea.required = isUnderRepair;
    }

    function updateCount() {
        if (counter) {
            counter.textContent = textarea.value.length + " / " + textarea.maxLength + " characters";
        }
    }

    select.addEventListener("change", updateVisibility);
    textarea.addEventListener("input", updateCount);

    updateVisibility();
    updateCount();
});
