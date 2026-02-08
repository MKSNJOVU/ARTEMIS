export function readFile(file, onProgress = null) {
  // Create the FileReader
  const reader = new FileReader();

  return new Promise((resolve, reject) => {
    reader.addEventListener(
      "load",
      function (event) {
        resolve(reader.result); // Resolve the loaded event
      },
      { once: true },
    );
    // Perform a progress function to show that the file is being read
    if (onProgress) {
      reader.addEventListener("progress", (event) => {
        if (event.lengthComputable) {
          const percent = (event.loaded / event.total) * 100;
          onProgress(percent, "Reading the file....");
        }
      });
    }
    // Provide an error if FileReader fails
    reader.addEventListener("error", function () {
      reject(new Error("Failed to read the file."));
    });
    // Read the file as an ArrayBuffer
    reader.readAsArrayBuffer(file);
  });
}

export function downloadBlob(
  data,
  fileName,
  mimeType = "application/octet-stream",
) {
  // Create the blob
  const blob = new Blob([data], { type: mimeType });

  //Creating the objectURL
  const objectURL = window.URL.createObjectURL(blob);

  //Creating the HTML element and document
  const aElement = document.createElement("a");

  aElement.href = objectURL;
  aElement.download = fileName;

  // append the link to the document body
  document.body.appendChild(aElement);

  // listening for the programmatic click
  const timeout = 300;

  aElement.addEventListener(
    "click",
    () => {
      setTimeout(() => window.URL.revokeObjectURL(objectURL), timeout);
    },
    { once: true },
  );
  // clikcing the link
  aElement.click();

  // Cleaning up the URL and document
  document.body.removeChild(aElement);
}
