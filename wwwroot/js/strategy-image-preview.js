(() => {
    "use strict";

    const imageInput = document.querySelector("[data-strategy-image-input]");
    const previewContent = document.querySelector("[data-strategy-image-preview-content]");
    const previewPlaceholder = document.querySelector("[data-strategy-image-preview-placeholder]");

    if (!imageInput || !previewContent || !previewPlaceholder) {
        return;
    }

    let currentObjectUrl;

    imageInput.addEventListener("change", () => {
        if (currentObjectUrl) {
            URL.revokeObjectURL(currentObjectUrl);
            currentObjectUrl = undefined;
        }

        const selectedFile = imageInput.files?.[0];

        if (!selectedFile || !selectedFile.type.startsWith("image/")) {
            previewContent.removeAttribute("src");
            previewContent.hidden = true;
            previewPlaceholder.hidden = false;

            return;
        }

        currentObjectUrl = URL.createObjectURL(selectedFile);
        previewContent.src = currentObjectUrl;
        previewContent.hidden = false;
        previewPlaceholder.hidden = true;
    });

    window.addEventListener("pagehide", () => {
        if (currentObjectUrl) {
            URL.revokeObjectURL(currentObjectUrl);
        }
    });
})();