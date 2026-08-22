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

    const buttonRectangle = buttonElement.getBoundingClientRect();

    datePickerElement.style.top = `${buttonRectangle.top}px`;
    datePickerElement.style.left = `${buttonRectangle.left}px`;
    datePickerElement.style.width = `${buttonRectangle.width}px`;
    datePickerElement.style.height = `${buttonRectangle.height}px`;
}

document.addEventListener("DOMContentLoaded", () =>
{
    const calendarElement = document.getElementById("team-calendar");
    const datePickerElement = document.getElementById("calendar-date-picker");
    const loadErrorElement = document.getElementById("calendar-load-error");

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

    if (!eventsUrl || !teamTimeZone || datePickerElement === null)
    {
        if (loadErrorElement !== null)
        {
            loadErrorElement.hidden = false;
        }

        return;
    }

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
        dayMaxEvents: true,
        displayEventEnd: false,
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
        events:
        {
            url: eventsUrl,
            method: "GET"
        },
        loading: isLoading =>
        {
            calendarElement.setAttribute("aria-busy", isLoading.toString());
        },
        datesSet: () =>
        {
            updateSelectedDateButton(calendarElement, datePickerElement, calendar.getDate(), teamTimeZone);
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
            }
        }
    });

    datePickerElement.addEventListener("change", () =>
    {
        const selectedDate = datePickerElement.value;

        if (!selectedDate)
        {
            return;
        }

        calendar.gotoDate(selectedDate);
    });

    window.addEventListener("resize", () =>
    {
        updateCalendarAxisWidth(calendarElement);
        alignDatePickerWithButton(calendarElement, datePickerElement);
    });

    calendar.render();
    updateSelectedDateButton(calendarElement, datePickerElement, calendar.getDate(), teamTimeZone);
    updateViewButtons(calendarElement, calendar.view.type);
    updateCalendarAxisWidth(calendarElement);
    alignDatePickerWithButton(calendarElement, datePickerElement);
});