const activitySelectAll = document.getElementById("activity-select-all");
const activityParticipantCheckboxes = document.querySelectorAll(".activity-participant-checkbox");
const activityPlannedStartInput = document.getElementById("PlannedStartLocal");
const activityTimeZoneElement = document.getElementById("activity-time-zone");
const activityTimeZoneOffsetElement = document.getElementById("activity-time-zone-offset");
const activityTypeSelect = document.getElementById("ActivityTypeId");
const activityOpponentField = document.getElementById("activity-opponent-field");
const activityOpponentNameInput = document.getElementById("activity-opponent-name");
const activityAddLinkButton = document.getElementById("activity-add-link");
const activityLinkList = document.getElementById("activity-link-list");
const activityLinkTemplate = document.getElementById("activity-link-template");

if (activitySelectAll !== null)
{
    activitySelectAll.addEventListener("change", () =>
    {
        for (const participantCheckbox of activityParticipantCheckboxes)
        {
            participantCheckbox.checked = activitySelectAll.checked;
        }
    });
}

function getTimeZoneOffsetMinutes(date, timeZoneId)
{
    const formatter = new Intl.DateTimeFormat("en-CA",
    {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
        hourCycle: "h23",
        timeZone: timeZoneId
    });

    const parts = formatter.formatToParts(date);
    const values = Object.fromEntries(parts.map(part => [part.type, part.value]));
    const representedAsUtc = Date.UTC(
        Number(values.year),
        Number(values.month) - 1,
        Number(values.day),
        Number(values.hour),
        Number(values.minute),
        Number(values.second));

    return Math.round((representedAsUtc - date.getTime()) / 60000);
}

function getOffsetForLocalDate(localDateValue, timeZoneId)
{
    const [datePart, timePart] = localDateValue.split("T");
    const [year, month, day] = datePart.split("-").map(Number);
    const [hour, minute] = timePart.split(":").map(Number);
    const requestedLocalTime = Date.UTC(year, month - 1, day, hour, minute);
    let candidateTime = requestedLocalTime;

    for (let iteration = 0; iteration < 3; iteration += 1)
    {
        const offsetMinutes = getTimeZoneOffsetMinutes(new Date(candidateTime), timeZoneId);

        candidateTime = requestedLocalTime - offsetMinutes * 60000;
    }

    return getTimeZoneOffsetMinutes(new Date(candidateTime), timeZoneId);
}

function formatUtcOffset(offsetMinutes)
{
    const sign = offsetMinutes >= 0 ? "+" : "-";
    const absoluteMinutes = Math.abs(offsetMinutes);
    const hours = Math.floor(absoluteMinutes / 60);
    const minutes = absoluteMinutes % 60;

    if (minutes === 0)
    {
        return `UTC${sign}${hours}`;
    }

    return `UTC${sign}${hours}:${String(minutes).padStart(2, "0")}`;
}

function updateActivityTimeZoneOffset()
{
    if (activityPlannedStartInput === null || activityTimeZoneElement === null || activityTimeZoneOffsetElement === null)
    {
        return;
    }

    const localDateValue = activityPlannedStartInput.value;
    const timeZoneId = activityTimeZoneElement.dataset.timeZone;

    if (!localDateValue || !timeZoneId)
    {
        activityTimeZoneOffsetElement.textContent = "";

        return;
    }

    try
    {
        const offsetMinutes = getOffsetForLocalDate(localDateValue, timeZoneId);

        activityTimeZoneOffsetElement.textContent = ` · ${formatUtcOffset(offsetMinutes)} à cette date`;
    }
    catch
    {
        activityTimeZoneOffsetElement.textContent = "";
    }
}

if (activityPlannedStartInput !== null)
{
    activityPlannedStartInput.addEventListener("change", updateActivityTimeZoneOffset);
    activityPlannedStartInput.addEventListener("input", updateActivityTimeZoneOffset);

    updateActivityTimeZoneOffset();
}

function updateActivityOpponentField()
{
    if (activityTypeSelect === null || activityOpponentField === null || activityOpponentNameInput === null)
    {
        return;
    }

    const selectedOption = activityTypeSelect.selectedOptions[0];
    const activityTypeCode = selectedOption?.dataset.activityCode ?? "";
    const opponentFieldMustBeVisible = activityTypeCode === "Pracc" || activityTypeCode === "OfficialMatch";

    activityOpponentField.hidden = !opponentFieldMustBeVisible;
    activityOpponentNameInput.disabled = !opponentFieldMustBeVisible;

    if (!opponentFieldMustBeVisible)
    {
        activityOpponentNameInput.value = "";
    }
}

function configureActivityLinkRow(activityLinkRow, linkIndex)
{
    const indexValue = String(linkIndex);
    const indexField = activityLinkRow.querySelector("[data-link-index-field]");
    const nameLabel = activityLinkRow.querySelector("[data-link-name-label]");
    const nameInput = activityLinkRow.querySelector("[data-link-name-input]");
    const nameValidation = activityLinkRow.querySelector("[data-link-name-validation]");
    const urlLabel = activityLinkRow.querySelector("[data-link-url-label]");
    const urlInput = activityLinkRow.querySelector("[data-link-url-input]");
    const urlValidation = activityLinkRow.querySelector("[data-link-url-validation]");
    const removeButton = activityLinkRow.querySelector("[data-remove-activity-link]");

    activityLinkRow.dataset.linkIndex = indexValue;

    if (indexField !== null)
    {
        indexField.value = indexValue;
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

    if (removeButton !== null)
    {
        removeButton.addEventListener("click", () =>
        {
            activityLinkRow.remove();
        });
    }
}

function findNextActivityLinkIndex()
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

        const removeButton = existingRow.querySelector("[data-remove-activity-link]");

        if (removeButton !== null)
        {
            removeButton.addEventListener("click", () =>
            {
                existingRow.remove();
            });
        }
    }

    return nextIndex;
}

let nextActivityLinkIndex = findNextActivityLinkIndex();

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
    });
}

if (activityTypeSelect !== null)
{
    activityTypeSelect.addEventListener("change", updateActivityOpponentField);

    updateActivityOpponentField();
}