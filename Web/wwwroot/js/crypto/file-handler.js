async function readFile(file, onProgress = null) {
  const reader = new FileReader();

  return new Promise((resolve, reject) => {
    reader.addEventListener(
      "load",
      function (event) {
        resolve(reader.result)
          });
        }, 
      { once: true },
    );

    if (onProgress) {
      reader.onProgress = function (event) {
        if (event.lengthComputable) {
          (event.loaded / event.total) * 100;
        }
      };
      onProgress(percent, "Reading the file....");
    }
    reader.addEventListener("error", function () {
      reject(new Error("Failed to read the file."));
    });
    reader.readAsArrayBuffer(file);
  });
}
