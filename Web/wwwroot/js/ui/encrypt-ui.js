//#region Imports
import { encrypt } from "/js/crypto/crypto-core.js";

import {
  readFile,
  downloadBlob,
  getEncryptedFilename,
} from "/js/crypto/file-handler.js";

import { getElements, fileSizeConverter } from "./elements-ui.js";
//#endregion

//#region Constants
const form = document.getElementById("encrypt-form");

const formElements = getElements(
  "file-info",
  "password",
  "password-error",
  "password-confirm",
  "progress-container",
  "progress-fill",
  "progress-text",
  "encrypt-btn",
  "error-container",
  "error-message",
);

let selectedFile = null;
//#endregion

//#region Event Handlers
form.filename.addEventListener("change", (e) => {
  selectedFile = e.target.files[0];
  if (!selectedFile) {
    formElements.fileInfo.textContent = "";
    return;
  }

  const name = selectedFile.name;
  const size = selectedFile.size / fileSizeConverter;

  formElements.fileInfo.textContent = `Selected: ${name} (${size.toFixed(2)} MB)`;
});

formElements.passwordConfirm.addEventListener("input", (event) => {
  const password = formElements.password.value;
  const passwordConfirm = formElements.passwordConfirm.value;

  if (password !== passwordConfirm) {
    formElements.passwordError.textContent = "Passwords do not match!";
    return;
  } else {
    formElements.passwordError.textContent = "";
    return;
  }
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  hideError();

  if (formElements.password.value !== formElements.passwordConfirm.value) {
    showError("Passwords do not match!");
    return;
  }
  if (!selectedFile) {
    showError("Please select a file.");
    return;
  }
  formElements.encryptBtn.disabled = true;
  formElements.progressContainer.hidden = false;

  try {
    updateProgress(5, "Reading file....");
    const plaintext = await readFile(selectedFile);
    const encrypted = await encrypt(
      plaintext,
      formElements.password.value,
      updateProgress,
    );

    const filename = getEncryptedFilename(selectedFile.name);

    downloadBlob(encrypted, filename);
    updateProgress(100, "Complete!");
  } catch (error) {
    showError("Encryption failed!");
    console.error(error);
  } finally {
    formElements.encryptBtn.disabled = false;
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
