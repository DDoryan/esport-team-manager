const teamCreatePanel = document.getElementById("create-team");
const teamCreateShortcut = document.querySelector(".team-create-shortcut");

if (teamCreatePanel !== null && teamCreateShortcut !== null) {
    const teamCreatePanelObserver = new IntersectionObserver(entries => {
        const teamCreatePanelIsVisible = entries[0].isIntersecting;

        teamCreateShortcut.hidden = teamCreatePanelIsVisible;
    }, { threshold: 0.15 });

    teamCreatePanelObserver.observe(teamCreatePanel);
}

document.addEventListener("shown.bs.dropdown", async event => {
    if (!(event.target instanceof Element)) {
        return;
    }

    const notificationToggle = event.target.closest("[data-notification-toggle]");

    if (notificationToggle === null) {
        return;
    }

    const notificationNavigation = notificationToggle.closest(".app-notification-navigation");
    const readForm = notificationNavigation?.querySelector("[data-notification-read-form]");

    if (!(readForm instanceof HTMLFormElement)) {
        return;
    }

    try {
        const response = await fetch(readForm.action, {
            method: "POST",
            body: new FormData(readForm),
            credentials: "same-origin",
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        });

        if (!response.ok) {
            return;
        }

        notificationNavigation.querySelector(".app-notification-count")?.remove();
        notificationNavigation.querySelector(".app-notification-mobile-count")?.remove();
        readForm.remove();

        notificationToggle.setAttribute("aria-label", "Notifications. Aucune notification non lue");
        notificationToggle.setAttribute("title", "Aucune notification non lue");
    } catch (error) {
        console.error("La consultation des notifications n’a pas pu être enregistrée.", error);
    }
});