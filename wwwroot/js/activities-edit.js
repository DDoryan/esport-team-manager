const activityEditForm = document.getElementById("activity-edit-form");

if (activityEditForm !== null && activityEditForm.dataset.canEdit === "true")
{
    const activityTypeSelect = activityEditForm.querySelector("[data-activity-type-select]");
    const activityMatchSection = activityEditForm.querySelector("[data-activity-match-section]");
    const activityMatchInputs = activityEditForm.querySelectorAll("[data-activity-match-input]");
    const activityOpponentInput = activityEditForm.querySelector("[data-activity-opponent-input]");
    const activityScoreInputs = activityEditForm.querySelectorAll("[data-activity-score-input]");
    const activityResultLabel = activityEditForm.querySelector("[data-activity-result-label]");
    const activityStatusInputs = activityEditForm.querySelectorAll("[data-activity-status-input]");
    const activityStatusConfirmationInput = activityEditForm.querySelector("[data-activity-status-confirmation]");
    const activityCancellationInput = activityEditForm.querySelector("[data-activity-cancellation-input]");
    const activityAttendanceControls = activityEditForm.querySelectorAll("[data-activity-attendance-control]");
    const originalActivityStatus = activityEditForm.dataset.originalActivityStatus ?? "";
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

    function getSelectedActivityStatus()
    {
        for (const activityStatusInput of activityStatusInputs)
        {
            if (activityStatusInput instanceof HTMLInputElement && activityStatusInput.checked)
            {
                return activityStatusInput.value;
            }
        }

        return originalActivityStatus;
    }

    function getActivityStatusLabel(activityStatus)
    {
        switch (activityStatus)
        {
            case "Planned":
                return "planifiée";

            case "Completed":
                return "terminée";

            case "Cancelled":
                return "annulée";

            default:
                return activityStatus;
        }
    }

    function statusChangeRequiresConfirmation()
    {
        const selectedActivityStatus = getSelectedActivityStatus();
        const originalStatusIsFinal = originalActivityStatus === "Completed" || originalActivityStatus === "Cancelled";

        return originalStatusIsFinal && selectedActivityStatus !== originalActivityStatus;
    }

    function updateActivityStatusFields()
    {
        const selectedActivityStatus = getSelectedActivityStatus();
        const activityIsCompleted = selectedActivityStatus === "Completed";
        const activityIsCancelled = selectedActivityStatus === "Cancelled";

        if (activityCancellationInput instanceof HTMLInputElement)
        {
            activityCancellationInput.disabled = !activityIsCancelled;
        }

        if (activityCancellationInput instanceof HTMLTextAreaElement)
        {
            activityCancellationInput.disabled = !activityIsCancelled;
        }

        for (const activityAttendanceControl of activityAttendanceControls)
        {
            if (activityAttendanceControl instanceof HTMLElement)
            {
                activityAttendanceControl.hidden = !activityIsCompleted;
            }
        }

        if (activityAttendanceSelectAll instanceof HTMLInputElement)
        {
            activityAttendanceSelectAll.disabled = !activityIsCompleted;
        }

        for (const participantCheckbox of activityParticipantCheckboxes)
        {
            updateActivityAttendanceAvailability(participantCheckbox);
        }

        updateMatchSectionVisibility();
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

        for (const matchInput of activityMatchInputs)
        {
            if (matchInput instanceof HTMLInputElement)
            {
                matchInput.disabled = !matchSectionIsVisible;
            }
        }

        if (activityOpponentInput instanceof HTMLInputElement)
        {
            activityOpponentInput.required = matchSectionIsVisible;
        }

        const oneScoreIsProvided = Array.from(activityScoreInputs).some(scoreInput =>
            scoreInput instanceof HTMLInputElement && scoreInput.value !== "");

        for (const scoreInput of activityScoreInputs)
        {
            if (scoreInput instanceof HTMLInputElement)
            {
                const selectedActivityIsCompleted = getSelectedActivityStatus() === "Completed";

                scoreInput.required = matchSectionIsVisible && (selectedActivityIsCompleted || oneScoreIsProvided);
            }
        }

        if (activityResultLabel !== null)
        {
            const scoreValues = Array.from(activityScoreInputs)
                .filter(scoreInput => scoreInput instanceof HTMLInputElement)
                .map(scoreInput => scoreInput.value);

            if (!matchSectionIsVisible || scoreValues.length !== 2 || scoreValues.some(scoreValue => scoreValue === ""))
            {
                activityResultLabel.textContent = "Non disponible";

                return;
            }

            const teamScore = Number(scoreValues[0]);
            const opponentScore = Number(scoreValues[1]);

            if (!Number.isInteger(teamScore) || !Number.isInteger(opponentScore) || teamScore < 0 || opponentScore < 0)
            {
                activityResultLabel.textContent = "Non disponible";

                return;
            }

            activityResultLabel.textContent = teamScore > opponentScore
                ? "Victoire"
                : teamScore < opponentScore
                    ? "Défaite"
                    : "Égalité";
        }
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

        const selectedActivityIsCompleted = getSelectedActivityStatus() === "Completed";

        attendanceCheckbox.disabled = !selectedActivityIsCompleted || !participantCheckbox.checked;
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

    updateActivityStatusFields();
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

    for (const scoreInput of activityScoreInputs)
    {
        scoreInput.addEventListener("input", updateMatchSectionVisibility);
    }

    const initialFormSnapshot = createFormSnapshot();

    for (const activityStatusInput of activityStatusInputs)
    {
        if (!(activityStatusInput instanceof HTMLInputElement))
        {
            continue;
        }

        activityStatusInput.addEventListener("change", () =>
        {
            if (activityStatusConfirmationInput instanceof HTMLInputElement)
            {
                activityStatusConfirmationInput.value = "false";
            }

            updateActivityStatusFields();
        });
    }

    if (activityTypeSelect !== null)
    {
        activityTypeSelect.addEventListener("change", updateMatchSectionVisibility);
    }

    activityEditForm.addEventListener("submit", event =>
    {
        updateActivityStatusFields();

        if (!formPassesClientValidation())
        {
            event.preventDefault();
            formIsSubmitting = false;
            window.setTimeout(focusFirstInvalidField, 0);

            return;
        }

        if (statusChangeRequiresConfirmation())
        {
            const selectedActivityStatus = getSelectedActivityStatus();
            const confirmationMessage = `Cette activité est actuellement ${getActivityStatusLabel(originalActivityStatus)}. Confirmer son passage vers l’état ${getActivityStatusLabel(selectedActivityStatus)} ?`;

            if (!window.confirm(confirmationMessage))
            {
                event.preventDefault();
                formIsSubmitting = false;

                return;
            }

            if (activityStatusConfirmationInput instanceof HTMLInputElement)
            {
                activityStatusConfirmationInput.value = "true";
            }
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

    const deleteActivityFormElement = document.querySelector("[data-activity-delete-form]");

    if (deleteActivityFormElement)
    {
        deleteActivityFormElement.addEventListener("submit", event =>
        {
            const confirmationMessage = deleteActivityFormElement.getAttribute("data-confirmation-message");

            if (confirmationMessage && !window.confirm(confirmationMessage))
            {
                event.preventDefault();
            }
        });
    }
}