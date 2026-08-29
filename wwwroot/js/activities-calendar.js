function formatDateKey(date, timeZone)
{
    const formatter = new Intl.DateTimeFormat("fr-FR",
    {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        timeZone: timeZone
    });

    const parts = formatter.formatToParts(date);
    const year = parts.find(part => part.type === "year")?.value ?? "";
    const month = parts.find(part => part.type === "month")?.value ?? "";
    const day = parts.find(part => part.type === "day")?.value ?? "";

    return `${year}-${month}-${day}`;
}

function isSameCalendarDate(firstDate, secondDate, timeZone)
{
    return formatDateKey(firstDate, timeZone) === formatDateKey(secondDate, timeZone);
}

function getDayCellClass(info, selectedDate, timeZone)
{
    const classNames = ["calendar-day-cell"];

    if (info.isToday)
    {
        classNames.push("calendar-today-day");
    }

    if (isSameCalendarDate(info.date, selectedDate, timeZone))
    {
        classNames.push("calendar-selected-day");
    }

    return classNames.join(" ");
}

function getDayLaneClass(info, selectedDate, timeZone)
{
    const classNames = ["calendar-day-lane"];

    if (info.isToday)
    {
        classNames.push("calendar-today-day-lane");
    }

    if (isSameCalendarDate(info.date, selectedDate, timeZone))
    {
        classNames.push("calendar-selected-day-lane");
    }

    return classNames.join(" ");
}

function createDayCellTopContent(info, selectedDate, timeZone)
{
    const labelElement = document.createElement("span");
    const numberElement = document.createElement("span");

    labelElement.className = "calendar-day-label";
    numberElement.className = "calendar-day-number";
    numberElement.textContent = info.dayNumberText;

    if (info.isToday)
    {
        numberElement.classList.add("calendar-day-number-today");
    }

    if (isSameCalendarDate(info.date, selectedDate, timeZone))
    {
        numberElement.classList.add("calendar-day-number-selected");
    }

    labelElement.append(numberElement);

    if (info.isMajor && info.monthText)
    {
        const monthElement = document.createElement("span");

        monthElement.className = "calendar-day-month";
        monthElement.textContent = info.monthText;

        labelElement.append(monthElement);
    }

    return { domNodes: [labelElement] };
}

function createDayHeaderContent(info, selectedDate, timeZone)
{
    const labelElement = document.createElement("span");

    labelElement.className = "calendar-day-header-label";
    labelElement.textContent = info.text;

    if (info.isToday)
    {
        labelElement.classList.add("calendar-day-header-today");
    }

    if (isSameCalendarDate(info.date, selectedDate, timeZone))
    {
        labelElement.classList.add("calendar-day-header-selected");
    }

    return { domNodes: [labelElement] };
}

function updateSelectedDateButton(calendarElement, datePickerElement, selectedDate, timeZone)
{
    const buttonElement = calendarElement.querySelector(".calendar-selected-date-button");

    if (buttonElement === null)
    {
        return;
    }

    const selectedDateKey = formatDateKey(selectedDate, timeZone);
    const todayDateKey = formatDateKey(new Date(), timeZone);
    const isToday = selectedDateKey === todayDateKey;
    const formatter = new Intl.DateTimeFormat("fr-FR",
    {
        day: "numeric",
        month: "short",
        year: "numeric",
        timeZone: timeZone
    });

    const selectedDateLabel = isToday ? "Aujourd’hui" : formatter.format(selectedDate);

    buttonElement.textContent = selectedDateLabel;
    buttonElement.setAttribute("aria-label", isToday ? "Aujourd’hui" : `Date sélectionnée : ${selectedDateLabel}. Cliquer pour revenir à aujourd’hui.`);
    datePickerElement.value = selectedDateKey;
}

function updateMobileSelectedDateButton(buttonElement, selectedDate, timeZone)
{
    const selectedDateKey = formatDateKey(selectedDate, timeZone);
    const todayDateKey = formatDateKey(new Date(), timeZone);
    const isToday = selectedDateKey === todayDateKey;
    const formatter = new Intl.DateTimeFormat("fr-FR",
    {
        day: "numeric",
        month: "short",
        year: "numeric",
        timeZone: timeZone
    });
    const selectedDateLabel = isToday ? "Aujourd’hui" : formatter.format(selectedDate);

    buttonElement.textContent = selectedDateLabel;
    buttonElement.setAttribute("aria-label", isToday ? "Aujourd’hui" : `Date sélectionnée : ${selectedDateLabel}. Cliquer pour revenir à aujourd’hui.`);
}

function updateViewButtons(calendarElement, activeViewType)
{
    const fourWeekButtonElement = calendarElement.querySelector(".calendar-four-week-view-button");
    const weekButtonElement = calendarElement.querySelector(".calendar-week-view-button");

    if (fourWeekButtonElement !== null)
    {
        fourWeekButtonElement.classList.toggle("calendar-view-button-active", activeViewType === "dayGridFourWeek");
    }

    if (weekButtonElement !== null)
    {
        weekButtonElement.classList.toggle("calendar-view-button-active", activeViewType === "timeGridWeek");
    }
}

function updateCalendarAxisWidth(calendarElement)
{
    const axisHeaderElement = calendarElement.querySelector(".calendar-week-view .calendar-time-axis-header");

    if (axisHeaderElement === null)
    {
        return;
    }

    const axisColumnElement = axisHeaderElement.parentElement;

    if (axisColumnElement === null)
    {
        return;
    }

    const axisWidth = axisColumnElement.getBoundingClientRect().width;

    calendarElement.style.setProperty("--calendar-time-axis-width", `${axisWidth}px`);
}

function alignDatePickerWithButton(calendarElement, datePickerElement)
{
    const buttonElement = calendarElement.querySelector(".calendar-date-picker-button");

    if (buttonElement === null)
    {
        return;
    }

    alignDatePickerWithElement(datePickerElement, buttonElement);
}

function alignDatePickerWithElement(datePickerElement, buttonElement)
{
    const buttonRectangle = buttonElement.getBoundingClientRect();

    datePickerElement.style.top = `${buttonRectangle.top}px`;
    datePickerElement.style.left = `${buttonRectangle.left}px`;
    datePickerElement.style.width = `${buttonRectangle.width}px`;
    datePickerElement.style.height = `${buttonRectangle.height}px`;
}

function alignMobileDatePickerWithButton(datePickerElement, buttonElement)
{
    const buttonRectangle = buttonElement.getBoundingClientRect();
    const viewportMargin = 8;
    const pickerWidth = Math.min(220, window.innerWidth - (viewportMargin * 2));
    const pickerLeft = Math.max(viewportMargin, buttonRectangle.right - pickerWidth);

    datePickerElement.style.top = `${buttonRectangle.top}px`;
    datePickerElement.style.left = `${pickerLeft}px`;
    datePickerElement.style.width = `${pickerWidth}px`;
    datePickerElement.style.height = `${buttonRectangle.height}px`;
}

function getCalendarActivityTypeClass(typeCode)
{
    switch (typeCode)
    {
        case "Pracc":
            return "calendar-activity-event-pracc";

        case "OfficialMatch":
            return "calendar-activity-event-official-match";

        case "Meeting":
            return "calendar-activity-event-meeting";

        case "VodReview":
            return "calendar-activity-event-vod-review";

        default:
            return "calendar-activity-event-default";
    }
}

function getCalendarActivityStatusIconClass(status)
{
    switch (status)
    {
        case "Completed":
            return "bi-check-lg";

        case "Cancelled":
            return "bi-x-lg";

        default:
            return "bi-clock";
    }
}

function createCalendarActivityStatusIcon(status)
{
    const iconElement = document.createElement("i");

    iconElement.className = `bi ${getCalendarActivityStatusIconClass(status)}`;
    iconElement.setAttribute("aria-hidden", "true");

    return iconElement;
}

function getSelectedCalendarFilterValues(filterInputs, kind)
{
    return new Set(filterInputs
        .filter(input => input.dataset.calendarFilterKind === kind && input.checked)
        .map(input => input.value));
}

function isCalendarEventSelected(eventData, filterInputs)
{
    const selectedTypes = getSelectedCalendarFilterValues(filterInputs, "type");
    const selectedStatuses = getSelectedCalendarFilterValues(filterInputs, "status");
    const typeCode = typeof eventData.typeCode === "string" ? eventData.typeCode : "";
    const status = typeof eventData.status === "string" ? eventData.status : "Planned";

    return selectedTypes.has(typeCode) && selectedStatuses.has(status);
}

function synchronizeCalendarFilterInputs(filterInputs, changedInput)
{
    for (const input of filterInputs)
    {
        if (input !== changedInput && input.dataset.calendarFilterKind === changedInput.dataset.calendarFilterKind && input.value === changedInput.value)
        {
            input.checked = changedInput.checked;
        }
    }
}

function applyInitialCalendarFilterSelection(filterInputs, isMobile)
{
    const completedIsSelected = !isMobile;

    for (const input of filterInputs)
    {
        if (input.dataset.calendarFilterKind === "status" && input.value === "Completed")
        {
            input.checked = completedIsSelected;
        }
    }
}

function updateCalendarActiveFilterCount(filterInputs, countElements)
{
    const filters = new Map();

    for (const input of filterInputs)
    {
        const key = `${input.dataset.calendarFilterKind ?? ""}:${input.value}`;

        if (!filters.has(key))
        {
            filters.set(key, input);
        }
    }

    const activeFilterCount = [...filters.values()]
        .filter(input => input.checked)
        .length;

    for (const countElement of countElements)
    {
        countElement.textContent = activeFilterCount.toString();
    }
}

async function loadCalendarEvents(eventsUrl, fetchInfo, filterInputs)
{
    const selectedStatuses = getSelectedCalendarFilterValues(filterInputs, "status");
    const requestUrl = new URL(eventsUrl, window.location.origin);

    requestUrl.searchParams.set("start", fetchInfo.startStr);
    requestUrl.searchParams.set("end", fetchInfo.endStr);
    requestUrl.searchParams.set("includeCancelled", selectedStatuses.has("Cancelled").toString());

    const response = await fetch(requestUrl,
    {
        method: "GET",
        headers:
        {
            Accept: "application/json"
        }
    });

    if (!response.ok)
    {
        throw new Error(`Le chargement des activités a échoué avec le statut ${response.status}.`);
    }

    const events = await response.json();

    if (!Array.isArray(events))
    {
        throw new Error("La réponse du calendrier est invalide.");
    }

    return events.filter(event => isCalendarEventSelected(event, filterInputs));
}

function getCalendarActivityComplement(event)
{
    const opponentName = typeof event.extendedProps.opponentName === "string" ? event.extendedProps.opponentName.trim() : "";
    const subtitle = typeof event.extendedProps.subtitle === "string" ? event.extendedProps.subtitle.trim() : "";
    const details = [];

    if (subtitle.length > 0)
    {
        details.push(subtitle);
    }

    if (opponentName.length > 0)
    {
        details.push(opponentName);
    }

    return details.join(" · ");
}

function getCalendarActivityStatusLabel(status)
{
    switch (status)
    {
        case "Completed":
            return "Terminée";

        case "Cancelled":
            return "Annulée";

        default:
            return "Planifiée";
    }
}

function formatMobileActivitySchedule(event, timeZone)
{
    const startDate = event.start;

    if (!(startDate instanceof Date))
    {
        return "";
    }

    const endDate = event.end instanceof Date ? event.end : startDate;
    const dateFormatter = new Intl.DateTimeFormat("fr-FR",
    {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        timeZone: timeZone
    });
    const timeFormatter = new Intl.DateTimeFormat("fr-FR",
    {
        hour: "2-digit",
        minute: "2-digit",
        hour12: false,
        timeZone: timeZone
    });

    const startDateLabel = dateFormatter.format(startDate);
    const startTimeLabel = timeFormatter.format(startDate);
    const endTimeLabel = timeFormatter.format(endDate);

    if (isSameCalendarDate(startDate, endDate, timeZone))
    {
        return `${startDateLabel} · ${startTimeLabel}–${endTimeLabel}`;
    }

    const endDateLabel = dateFormatter.format(endDate);

    return `${startDateLabel} ${startTimeLabel} – ${endDateLabel} ${endTimeLabel}`;
}

function createMobileActivityListItem(event, timeZone)
{
    const listItemElement = document.createElement("li");
    const activityUrl = typeof event.url === "string" ? event.url : "";
    const contentElement = document.createElement(activityUrl.length > 0 ? "a" : "div");
    const titleElement = document.createElement("h3");
    const scheduleElement = document.createElement("p");
    const statusElement = document.createElement("span");
    const typeCode = typeof event.extendedProps.typeCode === "string" ? event.extendedProps.typeCode : "";
    const status = typeof event.extendedProps.status === "string" ? event.extendedProps.status : "";
    const complement = getCalendarActivityComplement(event);
    const schedule = formatMobileActivitySchedule(event, timeZone);
    const statusLabel = getCalendarActivityStatusLabel(status);
    const accessibleParts = [event.title, schedule];

    listItemElement.className = "team-activity-list-item";
    listItemElement.classList.add(getCalendarActivityTypeClass(typeCode));

    contentElement.className = "team-activity-list-item-link";

    if (activityUrl.length > 0)
    {
        contentElement.href = activityUrl;
    }

    if (status === "Completed")
    {
        listItemElement.classList.add("calendar-activity-event-completed");
    }

    if (status === "Cancelled")
    {
        listItemElement.classList.add("calendar-activity-event-cancelled");
    }

    titleElement.className = "team-activity-list-item-title";
    titleElement.textContent = event.title;

    scheduleElement.className = "team-activity-list-item-schedule";
    scheduleElement.textContent = schedule;

    contentElement.append(titleElement);
    contentElement.append(scheduleElement);

    if (complement.length > 0)
    {
        const detailsElement = document.createElement("p");

        detailsElement.className = "team-activity-list-item-details";
        detailsElement.textContent = complement;

        contentElement.append(detailsElement);
        accessibleParts.push(complement);
    }

    statusElement.className = "team-activity-list-item-status";
    statusElement.textContent = `${statusLabel} `;
    statusElement.append(createCalendarActivityStatusIcon(status));

    contentElement.append(statusElement);

    accessibleParts.push(statusLabel);
    contentElement.setAttribute("aria-label", accessibleParts.filter(part => part.length > 0).join(". "));
    listItemElement.append(contentElement);

    return listItemElement;
}

function doesCalendarEventOccurOnDate(event, selectedDate, timeZone)
{
    if (!(event.start instanceof Date))
    {
        return false;
    }

    const eventEnd = event.end instanceof Date && event.end.getTime() > event.start.getTime() ? new Date(event.end.getTime() - 1) : event.start;
    const selectedDateKey = formatDateKey(selectedDate, timeZone);
    const startDateKey = formatDateKey(event.start, timeZone);
    const endDateKey = formatDateKey(eventEnd, timeZone);

    return selectedDateKey >= startDateKey && selectedDateKey <= endDateKey;
}

function renderMobileActivityList(activityListElement, activityListItemsElement, loadingElement, emptyElement, events, selectedDate, timeZone)
{
    const chronologicalEvents = [...events]
        .filter(event => doesCalendarEventOccurOnDate(event, selectedDate, timeZone))
        .sort((firstEvent, secondEvent) =>
        {
            const startDifference = firstEvent.start.getTime() - secondEvent.start.getTime();

            if (startDifference !== 0)
            {
                return startDifference;
            }

            const firstEndTime = firstEvent.end instanceof Date ? firstEvent.end.getTime() : firstEvent.start.getTime();
            const secondEndTime = secondEvent.end instanceof Date ? secondEvent.end.getTime() : secondEvent.start.getTime();

            return firstEndTime - secondEndTime;
        });

    activityListItemsElement.replaceChildren();

    if (chronologicalEvents.length > 0)
    {
        const fragment = document.createDocumentFragment();

        for (const event of chronologicalEvents)
        {
            fragment.append(createMobileActivityListItem(event, timeZone));
        }

        activityListItemsElement.append(fragment);
    }

    loadingElement.hidden = true;
    emptyElement.hidden = chronologicalEvents.length > 0;
    activityListElement.setAttribute("aria-busy", "false");
}

function updateResponsiveCalendarView(calendar, mobileMediaQuery)
{
    if (!mobileMediaQuery.matches)
    {
        return;
    }

    if (calendar.view.type !== "dayGridFourWeek")
    {
        calendar.changeView("dayGridFourWeek");
    }
}

function createCalendarActivityEventContent(info)
{
    const contentElement = document.createElement("span");
    const labelElement = document.createElement("span");
    const typeCode = typeof info.event.extendedProps.typeCode === "string" ? info.event.extendedProps.typeCode : "";
    const complement = getCalendarActivityComplement(info.event);
    const status = typeof info.event.extendedProps.status === "string" ? info.event.extendedProps.status : "";
    const label = complement.length > 0 ? `${info.event.title} · ${complement}` : info.event.title;

    contentElement.className = "calendar-activity-event-content";
    contentElement.classList.add(getCalendarActivityTypeClass(typeCode));

    if (info.view.type === "dayGridFourWeek" && info.timeText.length > 0)
    {
        const timeElement = document.createElement("span");

        timeElement.className = "calendar-activity-event-time";
        timeElement.textContent = info.timeText;

        contentElement.append(timeElement);
    }

    labelElement.className = "calendar-activity-event-label";
    labelElement.textContent = label;

    contentElement.append(labelElement);

    const statusElement = document.createElement("span");

    statusElement.className = "calendar-activity-event-status";
    statusElement.append(createCalendarActivityStatusIcon(status));
    statusElement.setAttribute("aria-label", getCalendarActivityStatusLabel(status));

    contentElement.append(statusElement);

    return { domNodes: [contentElement] };
}

function configureCalendarActivityEvent(info)
{
    const typeCode = typeof info.event.extendedProps.typeCode === "string" ? info.event.extendedProps.typeCode : "";
    const complement = getCalendarActivityComplement(info.event);
    const status = typeof info.event.extendedProps.status === "string" ? info.event.extendedProps.status : "";
    const activityLabel = complement.length > 0 ? `${info.event.title} · ${complement}` : info.event.title;
    const accessibleParts = [];

    info.el.classList.add(getCalendarActivityTypeClass(typeCode));

    if (status === "Completed")
    {
        info.el.classList.add("calendar-activity-event-completed");
    }

    if (status === "Cancelled")
    {
        info.el.classList.add("calendar-activity-event-cancelled");
    }

    if (info.timeText.length > 0)
    {
        accessibleParts.push(info.timeText);
    }

    accessibleParts.push(activityLabel);
    accessibleParts.push(getCalendarActivityStatusLabel(status));

    const accessibleLabel = accessibleParts.join(" — ");

    info.el.title = accessibleLabel;
    info.el.setAttribute("aria-label", accessibleLabel);
}

document.addEventListener("DOMContentLoaded", () =>
{
    const calendarElement = document.getElementById("team-calendar");
    const datePickerElement = document.getElementById("calendar-date-picker");
    const loadErrorElement = document.getElementById("calendar-load-error");
    const activityListElement = document.getElementById("team-activity-list");
    const activityListItemsElement = activityListElement?.querySelector(".team-activity-list-items") ?? null;
    const activityListLoadingElement = activityListElement?.querySelector(".team-activity-list-loading") ?? null;
    const activityListEmptyElement = activityListElement?.querySelector(".team-activity-list-empty") ?? null;
    const calendarFilterInputs = [...document.querySelectorAll("[data-calendar-filter-kind]")];
    const calendarActiveFilterCountElements = [...document.querySelectorAll("[data-calendar-active-filter-count]")];
    const mobilePreviousDayButtonElement = document.querySelector("[data-mobile-calendar-action='previous']");
    const mobileTodayButtonElement = document.querySelector("[data-mobile-calendar-action='today']");
    const mobileNextDayButtonElement = document.querySelector("[data-mobile-calendar-action='next']");
    const mobileDatePickerButtonElement = document.querySelector("[data-mobile-calendar-action='date-picker']");

    if (calendarElement === null)
    {
        return;
    }

    if (typeof FullCalendar === "undefined")
    {
        if (loadErrorElement !== null)
        {
            loadErrorElement.hidden = false;
        }

        return;
    }

    const eventsUrl = calendarElement.dataset.eventsUrl;
    const teamTimeZone = calendarElement.dataset.teamTimeZone;

    if (!eventsUrl || !teamTimeZone || datePickerElement === null || activityListElement === null || activityListItemsElement === null || activityListLoadingElement === null || activityListEmptyElement === null || mobilePreviousDayButtonElement === null || mobileTodayButtonElement === null || mobileNextDayButtonElement === null || mobileDatePickerButtonElement === null)
    {
        if (loadErrorElement !== null)
        {
            loadErrorElement.hidden = false;
        }

        return;
    }

    const mobileMediaQuery = window.matchMedia("(max-width: 575.98px)");
    let mobileCalendarEvents = [];

    applyInitialCalendarFilterSelection(calendarFilterInputs, mobileMediaQuery.matches);

    const calendar = new FullCalendar.Calendar(calendarElement,
    {
        initialView: "dayGridFourWeek",
        locale: "fr",
        timeZone: teamTimeZone,
        firstDay: 1,
        weekNumbers: false,
        editable: false,
        selectable: false,
        navLinks: false,
        nowIndicator: false,
        height: "100%",
        expandRows: true,
        dayMaxEvents: 2,
        moreLinkContent: info => `+ ${info.num} autres`,
        displayEventEnd: false,
        eventDisplay: "block",
        eventClass: "calendar-activity-event",
        popoverClass: "calendar-event-popover",
        eventContent: createCalendarActivityEventContent,
        eventDidMount: configureCalendarActivityEvent,
        dayCellClass: info =>
        {
            return getDayCellClass(info, calendar.getDate(), teamTimeZone);
        },
        dayCellTopContent: info =>
        {
            return createDayCellTopContent(info, calendar.getDate(), teamTimeZone);
        },
        dayHeaderClass: info => {
            return isSameCalendarDate(info.date, calendar.getDate(), teamTimeZone) ? "calendar-day-header calendar-selected-day-header" : "calendar-day-header";
        },
        dayHeaderContent: info => {
            return createDayHeaderContent(info, calendar.getDate(), teamTimeZone);
        },
        dayHeaderRowClass: "calendar-day-header-row",
        dayHeaderDividerClass: "calendar-day-header-divider",
        tableHeaderClass: "calendar-table-header",
        tableBodyClass: "calendar-table-body",
        dayLaneClass: info => {
            return getDayLaneClass(info, calendar.getDate(), teamTimeZone);
        },
        slotHeaderClass: "calendar-time-axis-header",
        slotHeaderInnerClass: "calendar-time-axis-header-inner",
        slotHeaderRowClass: "calendar-time-axis-row",
        slotHeaderDividerClass: "calendar-time-axis-divider",
        slotLaneClass: "calendar-time-slot-lane",
        eventTimeFormat:
        {
            hour: "2-digit",
            minute: "2-digit",
            hour12: false
        },
        buttons:
        {
            prev:
            {
                hint: "Afficher la période précédente",
                className: "calendar-navigation-arrow-button"
            },
            next:
            {
                hint: "Afficher la période suivante",
                className: "calendar-navigation-arrow-button"
            },
            selecteddate:
            {
                text: "Aujourd’hui",
                hint: "Revenir à aujourd’hui",
                className: "calendar-selected-date-button",
                click: () => {
                    calendar.today();
                }
            },
            datepicker:
            {
                text: "Sélectionner une date",
                hint: "Sélectionner une date",
                iconClass: "bi bi-calendar3",
                display: "icon",
                className: "calendar-date-picker-button",
                click: () => {
                    if (typeof datePickerElement.showPicker === "function") {
                        datePickerElement.showPicker();

                        return;
                    }

                    datePickerElement.focus();
                    datePickerElement.click();
                }
            },
            dayGridFourWeek:
            {
                text: "4 semaines",
                className: "calendar-view-button calendar-four-week-view-button"
            },
            timeGridWeek:
            {
                text: "Semaine",
                className: "calendar-view-button calendar-week-view-button"
            }
        },
        headerToolbarClass: "calendar-toolbar",
        toolbarSectionClass: info => {
            return `calendar-toolbar-section calendar-toolbar-section-${info.name}`;
        },
        toolbarTitleClass: "calendar-toolbar-title",
        headerToolbar:
        {
            left: "prev selecteddate next datepicker",
            center: "title",
            right: "dayGridFourWeek timeGridWeek"
        },
        views:
        {
            dayGridFourWeek:
            {
                type: "dayGrid",
                className: "calendar-four-week-view",
                duration:
                {
                    weeks: 4
                },
                dateAlignment: "week"
            },
            timeGridWeek:
            {
                className: "calendar-week-view",
                slotEventOverlap: false,
                slotDuration: "01:00:00",
                slotHeaderInterval: "01:00:00",
                slotHeaderFormat:
                {
                    hour: "2-digit",
                    minute: "2-digit",
                    hour12: false
                },
                slotMinTime: "12:00:00",
                slotMaxTime: "25:00:00",
                scrollTime: "12:00:00",
                allDaySlot: false
            }
        },
        events: async (fetchInfo, successCallback, failureCallback) =>
        {
            try
            {
                const events = await loadCalendarEvents(eventsUrl, fetchInfo, calendarFilterInputs);

                successCallback(events);
            }
            catch (error)
            {
                failureCallback(error);
            }
        },
        eventsSet: events =>
        {
            mobileCalendarEvents = events;
            renderMobileActivityList(activityListElement, activityListItemsElement, activityListLoadingElement, activityListEmptyElement, mobileCalendarEvents, calendar.getDate(), teamTimeZone);
        },
        loading: isLoading =>
        {
            calendarElement.setAttribute("aria-busy", isLoading.toString());
        },
        datesSet: () =>
        {
            updateSelectedDateButton(calendarElement, datePickerElement, calendar.getDate(), teamTimeZone);
            updateMobileSelectedDateButton(mobileTodayButtonElement, calendar.getDate(), teamTimeZone);
            renderMobileActivityList(activityListElement, activityListItemsElement, activityListLoadingElement, activityListEmptyElement, mobileCalendarEvents, calendar.getDate(), teamTimeZone);
            updateViewButtons(calendarElement, calendar.view.type);
            updateCalendarAxisWidth(calendarElement);
            alignDatePickerWithButton(calendarElement, datePickerElement);
        },
        eventSourceSuccess: events =>
        {
            if (loadErrorElement !== null)
            {
                loadErrorElement.hidden = true;
            }

            return events;
        },
        eventSourceFailure: () =>
        {
            if (loadErrorElement !== null)
            {
                loadErrorElement.hidden = false;
                activityListElement.setAttribute("aria-busy", "false");
                activityListLoadingElement.hidden = true;
                activityListEmptyElement.hidden = true;
            }
        }
    });

    const refreshMobileSelectedDate = () =>
    {
        const selectedDate = calendar.getDate();

        updateSelectedDateButton(calendarElement, datePickerElement, selectedDate, teamTimeZone);
        updateMobileSelectedDateButton(mobileTodayButtonElement, selectedDate, teamTimeZone);
        renderMobileActivityList(activityListElement, activityListItemsElement, activityListLoadingElement, activityListEmptyElement, mobileCalendarEvents, selectedDate, teamTimeZone);
    };

    datePickerElement.addEventListener("change", () =>
    {
        const selectedDate = datePickerElement.value;

        if (!selectedDate)
        {
            return;
        }

        calendar.gotoDate(selectedDate);
        refreshMobileSelectedDate();
    });

    mobilePreviousDayButtonElement.addEventListener("click", () =>
    {
        calendar.incrementDate({ days: -1 });
        refreshMobileSelectedDate();
    });

    mobileTodayButtonElement.addEventListener("click", () =>
    {
        calendar.today();
        refreshMobileSelectedDate();
    });

    mobileNextDayButtonElement.addEventListener("click", () =>
    {
        calendar.incrementDate({ days: 1 });
        refreshMobileSelectedDate();
    });

    mobileDatePickerButtonElement.addEventListener("click", () =>
    {
        alignMobileDatePickerWithButton(datePickerElement, mobileDatePickerButtonElement);

        if (typeof datePickerElement.showPicker === "function")
        {
            datePickerElement.showPicker();

            return;
        }

        datePickerElement.focus();
        datePickerElement.click();
    });

    window.addEventListener("resize", () =>
    {
        updateCalendarAxisWidth(calendarElement);
        alignDatePickerWithButton(calendarElement, datePickerElement);
        updateResponsiveCalendarView(calendar, mobileMediaQuery);
    });

    mobileMediaQuery.addEventListener("change", event =>
    {
        applyInitialCalendarFilterSelection(calendarFilterInputs, event.matches);
        updateCalendarActiveFilterCount(calendarFilterInputs, calendarActiveFilterCountElements);
        calendar.refetchEvents();
        updateResponsiveCalendarView(calendar, mobileMediaQuery);
    });

    for (const filterInput of calendarFilterInputs)
    {
        filterInput.addEventListener("change", () =>
        {
            synchronizeCalendarFilterInputs(calendarFilterInputs, filterInput);
            updateCalendarActiveFilterCount(calendarFilterInputs, calendarActiveFilterCountElements);
            calendar.refetchEvents();
        });
    }

    calendar.render();
    updateCalendarActiveFilterCount(calendarFilterInputs, calendarActiveFilterCountElements);
    updateResponsiveCalendarView(calendar, mobileMediaQuery);
    updateSelectedDateButton(calendarElement, datePickerElement, calendar.getDate(), teamTimeZone);
    updateViewButtons(calendarElement, calendar.view.type);
    updateCalendarAxisWidth(calendarElement);
    alignDatePickerWithButton(calendarElement, datePickerElement);
});
