(() => {
    "use strict";

    const deletionForm = document.querySelector("[data-strategy-delete-form]");

    if (!deletionForm) {
        return;
    }

    deletionForm.addEventListener("submit", event => {
        const strategyName = deletionForm.dataset.strategyName ?? "cette stratégie";
        const parsedAssociationCount = Number.parseInt(
            deletionForm.dataset.strategyAssociationCount ?? "0",
            10);

        const associationCount = Number.isNaN(parsedAssociationCount)
            ? 0
            : parsedAssociationCount;

        const associationMessage = associationCount === 0
            ? "Aucune association ne sera supprimée."
            : associationCount === 1
                ? "1 association sera supprimée."
                : `${associationCount} associations seront supprimées.`;

        const confirmed = window.confirm(
            `Supprimer définitivement la stratégie « ${strategyName} » ?\n\n`
            + `${associationMessage}\n`
            + "Les activités resteront disponibles.\n"
            + "L’image et sa miniature éventuelles seront supprimées.");

        if (!confirmed) {
            event.preventDefault();
        }
    });
})();