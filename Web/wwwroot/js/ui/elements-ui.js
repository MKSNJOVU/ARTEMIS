// Get the Div Elements by ID
// 1. The Converter: Turns "file-info" into "fileInfo"
export const toCamel = (s) => {
  return s.replace(/-./g, (x) => x[1].toUpperCase());
};

// 2. The Mapper: Grabs elements and renames them
export const getElements = (...ids) => {
  const elements = {};
  ids.forEach((id) => {
    // Convert the HTML ID (kebab) to a JS Variable (camel)
    const camelName = toCamel(id);

    // Find the element
    const el = document.getElementById(id);

    // Store it under the new clean name
    if (el) {
      elements[camelName] = el;
    } else {
      console.warn(`Element with ID '${id}' not found!`);
    }
  });
  return elements;
};
// Use to calculate the file size in MB
export const fileSizeConverter = 1024 * 1024;
