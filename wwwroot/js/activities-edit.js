const activityEditForm = document.getElementById("activity-edit-form");

if (activityEditForm !== null && activityEditForm.dataset.canEdit === "true")
{
    const activityTypeSelect = activityEditForm.querySelector("[data-activity-type-select]");
    const activityMatchSection = activityEditForm.querySelector("[data-activity-match-section]");
    let formIsSubmitting = false;

    function createFormSnapshot()
    {
        return new URLSearchParams(new FormData(activityEditForm)).toString();
    }

    function updateMatchSectionVisibility()
    {
        if (activityTypeSelect === null || activityMatchSection === null)
        {
            return;
        }

        const selectedOption = activityTypeSelect.options[activityTypeSelect.selectedIndex];
        const selectedTypeCode = selectedOption?.dataset.typeCode ?? "";
        const matchSectionIsVisible = selectedTypeCode === "Pracc" || selectedTypeCode === "OfficialMatch";

        activityMatchSection.hidden = !matchSectionIsVisible;
    }

    function focusFirstInvalidField()
    {
        const firstInvalidField = activityEditForm.querySelector(".input-validation-error, [aria-invalid=\"true\"]");

        if (firstInvalidField instanceof HTMLElement)
        {
            firstInvalidField.focus();
            firstInvalidField.scrollIntoView({ block: "center" });
        }
    }

    function formPassesClientValidation()
    {
        if (!activityEditForm.checkValidity())
        {
            return false;
        }

        if (typeof window.jQuery === "undefined")
        {
            return true;
        }

        const jqueryForm = window.jQuery(activityEditForm);

        if (typeof jqueryForm.valid !== "function")
        {
            return true;
        }

        return jqueryForm.valid();
    }

    updateMatchSectionVisibility();
    focusFirstInvalidField();

    const initialFormSnapshot = createFormSnapshot();

    if (activityTypeSelect !== null)
    {
        activityTypeSelect.addEventListener("change", updateMatchSectionVisibility);
    }

    activityEditForm.addEventListener("submit", () =>
    {
        if (!formPassesClientValidation())
        {
            formIsSubmitting = false;
            window.setTimeout(focusFirstInvalidField, 0);

            return;
        }

        formIsSubmitting = true;
    });

    window.addEventListener("beforeunload", event =>
    {
        const formHasUnsavedChanges = createFormSnapshot() !== initialFormSnapshot;

        if (formIsSubmitting || !formHasUnsavedChanges)
        {
            return;
        }

        event.preventDefault();
        event.returnValue = "";
    });
}