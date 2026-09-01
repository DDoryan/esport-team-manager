(() => {
    "use strict";

    const imageInput = document.querySelector("[data-strategy-image-input]");
    const previewContent = document.querySelector("[data-strategy-image-preview-content]");
    const previewPlaceholder = document.querySelector("[data-strategy-image-preview-placeholder]");
    const previewButton = document.querySelector("[data-strategy-image-preview-link]");
    const imageDialog = document.querySelector("[data-strategy-image-dialog]");
    const dialogContent = document.querySelector("[data-strategy-image-dialog-content]");
    const dialogCloseButton = document.querySelector("[data-strategy-image-dialog-close]");

    if (!previewContent || !previewPlaceholder || !previewButton || !imageDialog || !dialogContent || !dialogCloseButton) {
        return;
    }

    const originalSource = previewContent.getAttribute("src");
    const contentWasInitiallyHidden = previewContent.hidden;
    const buttonWasInitiallyHidden = previewButton.hidden;
    let currentObjectUrl;

    const restoreInitialPreview = () => {
        if (originalSource) {
            previewContent.src = originalSource;
        } else {
            previewContent.removeAttribute("src");
        }

        previewContent.hidden = contentWasInitiallyHidden;
        previewButton.hidden = buttonWasInitiallyHidden;
        previewPlaceholder.hidden = !contentWasInitiallyHidden;
    };

    if (imageInput) {
        imageInput.addEventListener("change", () => {
            if (currentObjectUrl) {
                URL.revokeObjectURL(currentObjectUrl);
                currentObjectUrl = undefined;
            }

            const selectedFile = imageInput.files?.[0];

            if (!selectedFile || !selectedFile.type.startsWith("image/")) {
                restoreInitialPreview();

                return;
            }

            currentObjectUrl = URL.createObjectURL(selectedFile);
            previewContent.src = currentObjectUrl;
            previewContent.hidden = false;
            previewButton.hidden = false;
            previewPlaceholder.hidden = true;
        });
    }

    previewButton.addEventListener("click", () => {
        if (!previewContent.src) {
            return;
        }

        dialogContent.src = previewContent.src;
        imageDialog.showModal();
    });

    dialogCloseButton.addEventListener("click", () => {
        imageDialog.close();
    });

    imageDialog.addEventListener("click", event => {
        if (event.target === imageDialog) {
            imageDialog.close();
        }
    });

    imageDialog.addEventListener("close", () => {
        dialogContent.removeAttribute("src");
    });

    window.addEventListener("pagehide", () => {
        if (currentObjectUrl) {
            URL.revokeObjectURL(currentObjectUrl);
        }
    });
})();