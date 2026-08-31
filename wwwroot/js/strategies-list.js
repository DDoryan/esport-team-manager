(() => {
    "use strict";

    const filterForm = document.querySelector("[data-strategy-filter-form]");
    const resultsContainer = document.querySelector("[data-strategy-results]");

    if (!filterForm || !resultsContainer) {
        return;
    }

    const automaticFilters = filterForm.querySelectorAll("[data-strategy-filter-auto-submit]");
    const searchInput = filterForm.querySelector("[data-strategy-search]");
    const resetButton = filterForm.querySelector("[data-strategy-filter-reset]");
    const filterStatus = document.querySelector("[data-strategy-filter-status]");
    let searchTimeoutId;
    let activeRequest;

    const createRequestUrl = () => {
        const requestUrl = new URL(filterForm.action || window.location.href, window.location.href);
        const formData = new FormData(filterForm);

        requestUrl.search = "";

        formData.forEach((value, name) => {
            if (typeof value === "string") {
                requestUrl.searchParams.append(name, value);
            }
        });

        return requestUrl;
    };

    const setStatus = message => {
        if (filterStatus) {
            filterStatus.textContent = message;
        }
    };

    const refreshResults = async () => {
        activeRequest?.abort();

        const requestController = new AbortController();
        const requestUrl = createRequestUrl();

        activeRequest = requestController;
        resultsContainer.setAttribute("aria-busy", "true");
        setStatus("Mise à jour de la liste des stratégies.");

        try {
            const response = await fetch(requestUrl, {
                method: "GET",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                signal: requestController.signal
            });

            if (!response.ok) {
                throw new Error(`La requête a échoué avec le statut ${response.status}.`);
            }

            const responseHtml = await response.text();
            const responseDocument = new DOMParser().parseFromString(responseHtml, "text/html");
            const updatedResults = responseDocument.querySelector("[data-strategy-results]");

            if (!updatedResults) {
                throw new Error("La réponse ne contient pas la liste des stratégies.");
            }

            resultsContainer.innerHTML = updatedResults.innerHTML;
            window.history.replaceState({}, "", requestUrl);
            setStatus("La liste des stratégies a été mise à jour.");
        }
        catch (error) {
            if (error.name !== "AbortError") {
                setStatus("Impossible de mettre à jour la liste des stratégies.");
            }
        }
        finally {
            if (activeRequest === requestController) {
                resultsContainer.setAttribute("aria-busy", "false");
                activeRequest = undefined;
            }
        }
    };

    automaticFilters.forEach(filter => {
        filter.addEventListener("change", () => {
            window.clearTimeout(searchTimeoutId);
            refreshResults();
        });
    });

    if (searchInput) {
        searchInput.addEventListener("input", () => {
            window.clearTimeout(searchTimeoutId);
            activeRequest?.abort();

            searchTimeoutId = window.setTimeout(() => {
                refreshResults();
            }, 250);
        });
    }

    if (resetButton) {
        resetButton.addEventListener("click", event => {
            event.preventDefault();
            window.clearTimeout(searchTimeoutId);
            activeRequest?.abort();

            if (searchInput) {
                searchInput.value = "";
            }

            const mapFilter = filterForm.querySelector("[name='mapId']");

            if (mapFilter) {
                mapFilter.value = "";
            }

            filterForm.querySelectorAll("[name='sides']").forEach(sideFilter => {
                sideFilter.checked = true;
            });

            const activeFilter = filterForm.querySelector("[name='includeActive']");
            const inactiveFilter = filterForm.querySelector("[name='includeInactive']");

            if (activeFilter) {
                activeFilter.checked = true;
            }

            if (inactiveFilter) {
                inactiveFilter.checked = false;
            }

            refreshResults();
        });
    }
})();