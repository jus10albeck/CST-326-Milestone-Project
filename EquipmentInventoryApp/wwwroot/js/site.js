// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Asks "Are you sure?" before sending a form marked with data-confirm, such as Cancel.
// Runs before the double-submit guard below.
document.addEventListener("submit", function (event) {
    var form = event.target;

    if (!form.matches || !form.matches("form[data-confirm]")) {
        return;
    }

    if (!window.confirm(form.dataset.confirm)) {
        event.preventDefault();
    }
}, true);

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

// Counts the characters typed in boxes such as the damage report description
document.querySelectorAll("[data-char-count-group]").forEach(function (group) {
    var textarea = group.querySelector("textarea");
    var counter = group.querySelector("[data-char-count]");

    if (!textarea || !counter) {
        return;
    }

    function updateCount() {
        counter.textContent = textarea.value.length + " / " + textarea.maxLength + " characters";
    }

    textarea.addEventListener("input", updateCount);
    updateCount();
});

// Request Reservation form: the end date cannot be before the start date.
// The server checks the dates again and shows the same message.
document.querySelectorAll("form[data-date-range]").forEach(function (form) {
    var start = form.querySelector("[data-range-start]");
    var end = form.querySelector("[data-range-end]");
    var error = form.querySelector("[data-range-error]");

    if (!start || !end) {
        return;
    }

    function isValid() {
        if (!start.value || !end.value) {
            return false;
        }

        return end.value >= start.value && (!start.min || start.value >= start.min);
    }

    start.addEventListener("change", function () {
        end.min = start.value || start.min;

        if (end.value && start.value && end.value < start.value) {
            end.value = start.value;
        }
    });

    form.addEventListener("submit", function (event) {
        if (!isValid()) {
            event.preventDefault();

            if (error) {
                error.classList.remove("d-none");
            }

            return;
        }

        if (error) {
            error.classList.add("d-none");
        }
    }, true);
});

// Print button on My Equipment opens the browser's print dialog (FR-38)
document.querySelectorAll("[data-print]").forEach(function (button) {
    button.addEventListener("click", function () {
        window.print();
    });
});
