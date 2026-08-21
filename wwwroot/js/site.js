const teamCreatePanel = document.getElementById("create-team");
const teamCreateShortcut = document.querySelector(".team-create-shortcut");

if (teamCreatePanel !== null && teamCreateShortcut !== null) {
    const teamCreatePanelObserver = new IntersectionObserver(entries => {
        const teamCreatePanelIsVisible = entries[0].isIntersecting;

        teamCreateShortcut.hidden = teamCreatePanelIsVisible;
    }, { threshold: 0.15 });

    teamCreatePanelObserver.observe(teamCreatePanel);
}