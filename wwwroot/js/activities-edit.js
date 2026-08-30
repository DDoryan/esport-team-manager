const activityEditForm = document.getElementById("activity-edit-form");

if (activityEditForm !== null && activityEditForm.dataset.canEdit === "true")
{
    const activityTypeSelect = activityEditForm.querySelector("[data-activity-type-select]");
    const activityMatchSection = activityEditForm.querySelector("[data-activity-match-section]");
    const activityAddLinkButton = activityEditForm.querySelector("[data-add-activity-link]");
    const activityLinkList = activityEditForm.querySelector("[data-activity-link-list]");
    const activityLinkTemplate = document.getElementById("activity-edit-link-template");
    const activityParticipantSelectAll = activityEditForm.querySelector("[data-activity-participant-select-all]");
    const activityParticipantCheckboxes = activityEditForm.querySelectorAll("[data-activity-participant-checkbox]");
    const activityAttendanceSelectAll = activityEditForm.querySelector("[data-activity-attendance-select-all]");
    const activityAttendanceCheckboxes = activityEditForm.querySelectorAll("[data-activity-attendance-checkbox]");
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

    function updateActivityAttendanceAvailability(participantCheckbox)
    {
        if (!(participantCheckbox instanceof HTMLInputElement))
        {
            return;
        }

        const participantRow = participantCheckbox.closest("[data-activity-participant-row]");

        if (participantRow === null)
        {
            return;
        }

        const attendanceCheckbox = participantRow.querySelector("[data-activity-attendance-checkbox]");

        if (!(attendanceCheckbox instanceof HTMLInputElement))
        {
            return;
        }

        attendanceCheckbox.disabled = !participantCheckbox.checked;
    }

    function configureActivityToggleAll(toggleAllCheckbox, individualCheckboxes, afterIndividualChange = null)
    {
        if (!(toggleAllCheckbox instanceof HTMLInputElement))
        {
            return;
        }

        toggleAllCheckbox.indeterminate = false;

        toggleAllCheckbox.addEventListener("change", () =>
        {
            for (const individualCheckbox of individualCheckboxes)
            {
                if (!(individualCheckbox instanceof HTMLInputElement) || individualCheckbox.disabled)
                {
                    continue;
                }

                individualCheckbox.checked = toggleAllCheckbox.checked;

                if (typeof afterIndividualChange === "function")
                {
                    afterIndividualChange(individualCheckbox);
                }
            }
        });
    }

    function configureActivityLinkRemoval(activityLinkRow)
    {
        const removeButton = activityLinkRow.querySelector("[data-remove-activity-link]");

        if (removeButton === null)
        {
            return;
        }

        removeButton.addEventListener("click", () =>
        {
            activityLinkRow.remove();
        });
    }

    function configureActivityLinkRow(activityLinkRow, linkIndex)
    {
        const indexValue = String(linkIndex);
        const indexField = activityLinkRow.querySelector("[data-link-index-field]");
        const idInput = activityLinkRow.querySelector("[data-link-id-input]");
        const nameLabel = activityLinkRow.querySelector("[data-link-name-label]");
        const nameInput = activityLinkRow.querySelector("[data-link-name-input]");
        const nameValidation = activityLinkRow.querySelector("[data-link-name-validation]");
        const urlLabel = activityLinkRow.querySelector("[data-link-url-label]");
        const urlInput = activityLinkRow.querySelector("[data-link-url-input]");
        const urlValidation = activityLinkRow.querySelector("[data-link-url-validation]");

        activityLinkRow.dataset.linkIndex = indexValue;

        if (indexField !== null)
        {
            indexField.value = indexValue;
        }

        if (idInput !== null)
        {
            idInput.id = `Links_${indexValue}__ActivityLinkId`;
            idInput.name = `Links[${indexValue}].ActivityLinkId`;
        }

        if (nameLabel !== null)
        {
            nameLabel.htmlFor = `Links_${indexValue}__Name`;
        }

        if (nameInput !== null)
        {
            nameInput.id = `Links_${indexValue}__Name`;
            nameInput.name = `Links[${indexValue}].Name`;
        }

        if (nameValidation !== null)
        {
            nameValidation.dataset.valmsgFor = `Links[${indexValue}].Name`;
        }

        if (urlLabel !== null)
        {
            urlLabel.htmlFor = `Links_${indexValue}__Url`;
        }

        if (urlInput !== null)
        {
            urlInput.id = `Links_${indexValue}__Url`;
            urlInput.name = `Links[${indexValue}].Url`;
        }

        if (urlValidation !== null)
        {
            urlValidation.dataset.valmsgFor = `Links[${indexValue}].Url`;
        }

        configureActivityLinkRemoval(activityLinkRow);
    }

    function initializeActivityLinkRows()
    {
        if (activityLinkList === null)
        {
            return 0;
        }

        let nextIndex = 0;
        const existingRows = activityLinkList.querySelectorAll("[data-activity-link-row]");

        for (const existingRow of existingRows)
        {
            const existingIndex = Number(existingRow.dataset.linkIndex);

            if (Number.isInteger(existingIndex) && existingIndex >= nextIndex)
            {
                nextIndex = existingIndex + 1;
            }

            configureActivityLinkRemoval(existingRow);
        }

        return nextIndex;
    }

    for (const participantCheckbox of activityParticipantCheckboxes)
    {
        updateActivityAttendanceAvailability(participantCheckbox);

        participantCheckbox.addEventListener("change", () =>
        {
            updateActivityAttendanceAvailability(participantCheckbox);
        });
    }

    configureActivityToggleAll(activityParticipantSelectAll, activityParticipantCheckboxes, updateActivityAttendanceAvailability);
    configureActivityToggleAll(activityAttendanceSelectAll, activityAttendanceCheckboxes);

    updateMatchSectionVisibility();
    focusFirstInvalidField();

    let nextActivityLinkIndex = initializeActivityLinkRows();

    if (activityAddLinkButton !== null && activityLinkList !== null && activityLinkTemplate instanceof HTMLTemplateElement)
    {
        activityAddLinkButton.addEventListener("click", () =>
        {
            const activityLinkFragment = activityLinkTemplate.content.cloneNode(true);
            const activityLinkRow = activityLinkFragment.querySelector("[data-activity-link-row]");

            if (activityLinkRow === null)
            {
                return;
            }

            configureActivityLinkRow(activityLinkRow, nextActivityLinkIndex);
            nextActivityLinkIndex += 1;

            activityLinkList.append(activityLinkFragment);

            if (window.jQuery?.validator?.unobtrusive !== undefined)
            {
                window.jQuery.validator.unobtrusive.parse(activityLinkRow);
            }

            const nameInput = activityLinkRow.querySelector("[data-link-name-input]");

            if (nameInput instanceof HTMLElement)
            {
                nameInput.focus();
            }
        });
    }

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