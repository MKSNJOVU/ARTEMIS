//#region Imports
import { decrypt } from "/js/crypto/crypto-core.js";
import {
  readFile,
  downloadBlob,
  getDecryptedFilename,
} from "/js/crypto/file-handler.js";

import { getElements, fileSizeConverter } from "./elements-ui.js";
//#endregion

//#region Constants
const form = document.getElementById("decrypt-form");

const formElements = getElements(
  "encrypted-file",
  "file-info",
  "password",
  "password-error",
  "progress-container",
  "progress-fill",
  "progress-text",
  "decrypt-btn",
  "error-container",
  "error-message",
);

let selectedFile = null;
//#endregion

//#region Event Handlers
formElements.encryptedFile.addEventListener("change", (e) => {
  selectedFile = e.target.files[0];
  if (!selectedFile) {
    formElements.fileInfo.textContent = "";
    return;
  }

  const name = selectedFile.name;
  const size = selectedFile.size / fileSizeConverter;

  formElements.fileInfo.textContent = `Selected: ${name} (${size.toFixed(2)} MB)`;
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  hideError();

  if (!selectedFile) {
    showError("Please select a file.");
    return;
  }

  if (!formElements.password.value) {
    showError("Please enter your password.");
    return;
  }
  formElements.decryptBtn.disabled = true;
  formElements.progressContainer.hidden = false;

  try {
    updateProgress(5, "Reading file...");
    const encrypted = await readFile(selectedFile);
    const plaintext = await decrypt(
      encrypted,
      formElements.password.value,
      updateProgress,
    );

    const filename = getDecryptedFilename(selectedFile.name);

    downloadBlob(plaintext, filename);

    updateProgress(100, "Complete!");
  } catch (error) {
    showError(error.message);
  } finally {
    formElements.decryptBtn.disabled = false;
  }
});
//#endregion

//#region Helper Functions
function showError(message) {
  formElements.errorContainer.hidden = false;
  formElements.errorMessage.textContent = message;
}

function hideError() {
  formElements.errorContainer.hidden = true;
}

function updateProgress(percent, message) {
  formElements.progressFill.style.width = `${percent}%`;
  formElements.progressText.textContent = message;
}
//#endregion
