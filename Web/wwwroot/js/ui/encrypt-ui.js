//imports
import { encrypt } from "/js/crypto/crypto-core.js";

import {
  readFile,
  downloadBlob,
  getEncryptedFilename,
} from "/js/crypto/file-handler.js";

import { getElements, fileSizeConverter } from "./elements-ui.js";

//
const form = document.getElementById("encrypt-form");

const formElements = getElements(
  "file-info",
  "password-error",
  "progress-container",
  "progress-fill",
  "progress-text",
  "error-container",
  "error-message",
);

let selectedFile = null;

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
